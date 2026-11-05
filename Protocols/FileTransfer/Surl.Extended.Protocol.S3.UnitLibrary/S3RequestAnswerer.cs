using System.Security.Cryptography;
using Surl.Content;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Decides the answer to each S3 request on one connection (ADR-0108): GetObject and HeadObject
/// on a path-style <c>/&lt;bucket&gt;/&lt;key&gt;</c> with no query, read from the content store
/// as an anonymous caller, and <c>501 NotImplemented</c> for every other request.
/// </summary>
/// <remarks>
/// The order is ADR-0108 decision 4.1's, without the authentication BL-420 adds: the operation,
/// then the bucket (<c>404 NoSuchBucket</c>), then the key's length (<c>400 KeyTooLongError</c>),
/// then the key (<c>404 NoSuchKey</c>). An object's properties are the object metadata index's for
/// a file under the served root, and the index's defaults, computed here, for a file in a share
/// or behind a bucket name, which the index does not key.
/// </remarks>
internal sealed class S3RequestAnswerer : IHttpRequestHandler
{
    private readonly ContentStore store;
    private readonly ObjectMetadataIndex index;
    private readonly ExchangeContext context;

    /// <summary>
    /// Creates the answerer for one exchange.
    /// </summary>
    /// <param name="store">The content store whose top-level folders are the buckets.</param>
    /// <param name="index">The object metadata index of the store's served root.</param>
    /// <param name="context">The exchange: its clock and log.</param>
    public S3RequestAnswerer(ContentStore store, ObjectMetadataIndex index, ExchangeContext context)
    {
        this.store = store;
        this.index = index;
        this.context = context;
    }

    /// <inheritdoc/>
    public async Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var target = S3RequestTarget.Parse(request.Target);
        var reply = new S3Reply(request, target.Path, RandomNumberGenerator.GetHexString(16), context.TimeProvider.GetUtcNow());
        if (request.Method is not ("GET" or "HEAD") || !target.NamesAnObject || target.Query is not null)
        {
            context.Log.Note($"{request.Method} is no operation the S3 server serves; answered 501 NotImplemented.");

            return reply.Error(S3Error.NotImplemented);
        }

        return await ReadObjectAsync(reply, request, target, cancellationToken);
    }

    // GetObject and HeadObject: the bucket, then the key's length, then the key.
    private async Task<HttpAnswer> ReadObjectAsync(S3Reply reply, HttpRequest request, S3RequestTarget target, CancellationToken cancellationToken)
    {
        var caller = ContentCaller.Anonymous(S3ProtocolServer.Scheme);
        if (!IsBucket(store.MapBucketRequestPathForCaller($"/{target.RawBucket}/", caller)))
        {
            return reply.Error(S3Error.NoSuchBucket, new KeyValuePair<string, string>("BucketName", target.RawBucket));
        }

        if (S3ObjectKey.IsTooLong(target.RawKey))
        {
            return reply.Error(S3Error.KeyTooLong);
        }

        var mapping = store.MapBucketRequestPathForCaller(target.Path, caller);
        var properties = mapping.IsMapped ? await PropertiesAsync(mapping, target.Path, cancellationToken) : null;

        return properties is null
            ? reply.Error(S3Error.NoSuchKey, new KeyValuePair<string, string>("Key", target.RawKey))
            : S3ObjectAnswer.Answer(reply, request.Fields, properties, new HttpAnswerBody.ContentFile(store, mapping, ContentByteRange.WholeFile(properties.Length)));
    }

    private bool IsBucket(ContentPathMapping bucket) =>
        bucket.IsMapped && store.GetEntryKind(bucket) == ContentEntryKind.Directory;

    // The index keys a file by its path relative to the served root; a file it does not find
    // there - one in a share or behind a bucket name - answers the index's defaults.
    private async Task<ObjectProperties?> PropertiesAsync(ContentPathMapping mapping, string path, CancellationToken cancellationToken)
    {
        if (store.GetFileStatus(mapping) is not { } status)
        {
            return null;
        }

        var reading = await index.ReadAsync(Uri.UnescapeDataString(path[1..]), cancellationToken);
        if (reading.StorageFailure is { } failure)
        {
            context.Log.Note($"The object metadata index could not be written: {failure}");
        }

        return reading.Properties ?? await DefaultPropertiesAsync(mapping, status, cancellationToken);
    }

    private async Task<ObjectProperties> DefaultPropertiesAsync(ContentPathMapping mapping, ContentFileStatus status, CancellationToken cancellationToken)
    {
        using var md5 = MD5.Create();
        await using (var hashing = new CryptoStream(Stream.Null, md5, CryptoStreamMode.Write))
        {
            await store.CopyFileBytesAsync(mapping, ContentByteRange.WholeFile(status.Length), hashing, cancellationToken);
        }

        return new ObjectProperties(
            ObjectMetadataIndex.DefaultContentType, null, null, null, null, [], $"\"{Convert.ToHexStringLower(md5.Hash!)}\"", 0, status.Length, status.LastModifiedUtc);
    }
}
