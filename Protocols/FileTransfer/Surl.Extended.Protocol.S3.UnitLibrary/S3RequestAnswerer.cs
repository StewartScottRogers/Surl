using System.Security.Cryptography;
using Surl.Content;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Decides the answer to each S3 request on one connection (ADR-0108): GetObject, HeadObject,
/// PutObject and ListObjectsV2, path style or virtual-hosted style, made as an anonymous caller,
/// and <c>501 NotImplemented</c> for every other request.
/// </summary>
/// <remarks>
/// The order is ADR-0108 decision 4.1's, without the authentication BL-420 adds: the addressing,
/// the operation, then the bucket (<c>404 NoSuchBucket</c>), then the operation's own checks -
/// for a read, the key's length (<c>400 KeyTooLongError</c>) and the key (<c>404 NoSuchKey</c>).
/// </remarks>
internal sealed class S3RequestAnswerer : IHttpRequestHandler
{
    private readonly ContentStore store;
    private readonly ExchangeContext context;
    private readonly S3ObjectPropertiesReader properties;
    private readonly S3PutObject putObject;
    private readonly S3ListObjectsV2 listObjects;

    /// <summary>
    /// Creates the answerer for one exchange.
    /// </summary>
    /// <param name="store">The content store whose top-level folders are the buckets.</param>
    /// <param name="index">The object metadata index of the store's served root.</param>
    /// <param name="context">The exchange: its listen URL, clock and log.</param>
    public S3RequestAnswerer(ContentStore store, ObjectMetadataIndex index, ExchangeContext context)
    {
        this.store = store;
        this.context = context;
        properties = new S3ObjectPropertiesReader(store, index, context.Log);
        putObject = new S3PutObject(store, index, context.Log);
        listObjects = new S3ListObjectsV2(store, properties);
    }

    /// <inheritdoc/>
    public async Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var target = S3RequestTarget.Parse(request.Target, S3VirtualHost.BucketOf(request.Authority, context.ListenUrl.Host));
        var reply = new S3Reply(request, target.Path, RandomNumberGenerator.GetHexString(16), context.TimeProvider.GetUtcNow());
        var operation = S3OperationRecognition.Of(request, target);
        if (operation == S3Operation.NotImplemented)
        {
            context.Log.Note($"{request.Method} is no operation the S3 server serves; answered 501 NotImplemented.");

            return reply.Error(S3Error.NotImplemented);
        }

        var caller = ContentCaller.Anonymous(S3ProtocolServer.Scheme);
        if (!IsBucket(store.MapBucketRequestPathForCaller($"/{target.RawBucket}/", caller)))
        {
            return reply.Error(S3Error.NoSuchBucket, new KeyValuePair<string, string>("BucketName", target.RawBucket));
        }

        return operation switch
        {
            S3Operation.ListObjectsV2 => await listObjects.AnswerAsync(reply, target, S3QueryParameters.Parse(target.Query!), caller, cancellationToken),
            S3Operation.PutObject => await PutObjectAsync(reply, request, target, caller, cancellationToken),
            _ => await ReadObjectAsync(reply, request, target, caller, cancellationToken),
        };
    }

    private async Task<HttpAnswer> PutObjectAsync(S3Reply reply, HttpRequest request, S3RequestTarget target, ContentCaller caller, CancellationToken cancellationToken) =>
        S3ObjectKey.IsTooLong(target.RawKey)
            ? reply.Error(S3Error.KeyTooLong)
            : await putObject.AnswerAsync(reply, request, target, caller, cancellationToken);

    // GetObject and HeadObject: the key's length, then the key.
    private async Task<HttpAnswer> ReadObjectAsync(S3Reply reply, HttpRequest request, S3RequestTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (S3ObjectKey.IsTooLong(target.RawKey))
        {
            return reply.Error(S3Error.KeyTooLong);
        }

        var mapping = store.MapBucketRequestPathForCaller(target.ObjectPath, caller);
        var found = mapping.IsMapped
            ? await properties.ReadAsync(mapping, S3IndexKey.OfRaw(store, target.RawBucket, target.RawKey), cancellationToken)
            : null;

        return found is null
            ? reply.Error(S3Error.NoSuchKey, new KeyValuePair<string, string>("Key", target.RawKey))
            : S3ObjectAnswer.Answer(reply, request.Fields, found, new HttpAnswerBody.ContentFile(store, mapping, ContentByteRange.WholeFile(found.Length)));
    }

    private bool IsBucket(ContentPathMapping bucket) =>
        bucket.IsMapped && store.GetEntryKind(bucket) == ContentEntryKind.Directory;
}
