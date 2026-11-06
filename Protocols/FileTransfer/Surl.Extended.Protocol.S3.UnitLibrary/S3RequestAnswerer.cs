using System.Security.Cryptography;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Decides the answer to each S3 request on one connection (ADR-0108): ListBuckets, HeadBucket,
/// ListObjectsV2, GetObject, HeadObject, PutObject, DeleteObject and the four multipart upload operations, path style or virtual-hosted style, made as the caller its
/// Signature Version 4 login names, and <c>501 NotImplemented</c> for every other request.
/// </summary>
/// <remarks>
/// The order is ADR-0108 decision 4.1's: the addressing, the SigV4 check of the head
/// (<see cref="S3Login"/>), a fault rule's chosen error (<see cref="S3ChosenErrorFault"/>), the operation - with a body the operation does not use read first
/// when the signature binds it - then the bucket (<c>404 NoSuchBucket</c>), then the
/// operation's own checks: for a read, the key's length (<c>400 KeyTooLongError</c>) and the
/// key (<c>404 NoSuchKey</c>).
/// </remarks>
internal sealed class S3RequestAnswerer : IHttpRequestHandler, IHttpHeadNotReadRefuser
{
    private readonly ContentStore store;
    private readonly ExchangeContext context;
    private readonly S3Login login;
    private readonly S3ObjectPropertiesReader properties;
    private readonly S3PutObject putObject;
    private readonly S3ListObjectsV2 listObjects;
    private readonly S3DeleteObject deleteObject;
    private readonly S3ListBuckets listBuckets;
    private readonly S3CreateMultipartUpload createMultipartUpload;
    private readonly S3UploadPart uploadPart;
    private readonly S3CompleteMultipartUpload completeMultipartUpload;
    private readonly S3AbortMultipartUpload abortMultipartUpload;

    /// <summary>
    /// Creates the answerer for one exchange.
    /// </summary>
    /// <param name="store">The content store whose top-level folders are the buckets.</param>
    /// <param name="index">The object metadata index of the store's served root.</param>
    /// <param name="authentication">The connection's authentication session.</param>
    /// <param name="context">The exchange: its listen URL, clock and log.</param>
    /// <param name="uploads">The server's open multipart uploads.</param>
    /// <param name="isOverTls">Whether the connection is TLS.</param>
    public S3RequestAnswerer(ContentStore store, ObjectMetadataIndex index, IHttpAuthenticationSession authentication, ExchangeContext context, S3MultipartUploads uploads, bool isOverTls)
    {
        this.store = store;
        this.context = context;
        login = new S3Login(authentication, context.Log);
        properties = new S3ObjectPropertiesReader(store, index, context.Log);
        putObject = new S3PutObject(store, index, login, context.Log);
        listObjects = new S3ListObjectsV2(store, properties);
        deleteObject = new S3DeleteObject(store);
        listBuckets = new S3ListBuckets(store);
        createMultipartUpload = new S3CreateMultipartUpload(store, putObject, uploads);
        uploadPart = new S3UploadPart(store, putObject, uploads);
        completeMultipartUpload = new S3CompleteMultipartUpload(store, putObject, uploads, isOverTls, context.ListenUrl.Host);
        abortMultipartUpload = new S3AbortMultipartUpload(store, uploads);
    }

    /// <inheritdoc/>
    public async Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var target = S3RequestTarget.Parse(request.Target, S3VirtualHost.BucketOf(request.Authority, context.ListenUrl.Host));
        var reply = new S3Reply(request, target.Path, RandomNumberGenerator.GetHexString(16), context.TimeProvider.GetUtcNow());
        var loginResult = await login.JudgeHeadAsync(request, cancellationToken);
        if (loginResult.Refusal is { } loginRefusal)
        {
            return reply.Error(loginRefusal);
        }

        if (await S3ChosenErrorFault.FindAsync(context, request.Method, target, loginResult.Caller!.AccountName) is { } chosenError)
        {
            return reply.Error(chosenError);
        }

        var operation = S3OperationRecognition.Of(request, target);
        if (await UnusedBodyRefusalAsync(request, operation, loginResult, cancellationToken) is { } bodyRefusal)
        {
            return reply.Error(bodyRefusal);
        }

        if (operation == S3Operation.NotImplemented)
        {
            context.Log.Note($"{request.Method} is no operation the S3 server serves; answered 501 NotImplemented.");

            return reply.Error(S3Error.NotImplemented);
        }

        var caller = loginResult.Caller!;
        if (operation == S3Operation.ListBuckets)
        {
            return listBuckets.Answer(reply, caller, cancellationToken);
        }

        if (!IsBucket(store.MapBucketRequestPathForCaller($"/{target.RawBucket}/", caller)))
        {
            return reply.Error(S3Error.NoSuchBucket, new KeyValuePair<string, string>("BucketName", target.RawBucket));
        }

        return await AnswerInBucketAsync(reply, request, target, operation, loginResult, cancellationToken);
    }

    // The operations on a bucket that exists and the caller may read.
    private async Task<HttpAnswer> AnswerInBucketAsync(S3Reply reply, HttpRequest request, S3RequestTarget target, S3Operation operation, S3LoginResult loginResult, CancellationToken cancellationToken)
    {
        var caller = loginResult.Caller!;

        return operation switch
        {
            S3Operation.HeadBucket => reply.Answer(HttpStatus.Ok, [], HttpAnswerBody.None),
            S3Operation.ListObjectsV2 => await listObjects.AnswerAsync(reply, target, S3QueryParameters.Parse(target.Query!), caller, cancellationToken),
            S3Operation.PutObject => await PutObjectAsync(reply, request, target, loginResult, cancellationToken),
            S3Operation.DeleteObject => deleteObject.Answer(reply, target, caller),
            S3Operation.ReadObject => await ReadObjectAsync(reply, request, target, caller, cancellationToken),
            _ => await MultipartAsync(reply, request, target, operation, loginResult, cancellationToken),
        };
    }

    // The multipart requests: the key's length, then the operation.
    private async Task<HttpAnswer> MultipartAsync(S3Reply reply, HttpRequest request, S3RequestTarget target, S3Operation operation, S3LoginResult loginResult, CancellationToken cancellationToken)
    {
        if (S3ObjectKey.IsTooLong(target.RawKey))
        {
            return reply.Error(S3Error.KeyTooLong);
        }

        return operation switch
        {
            S3Operation.CreateMultipartUpload => await createMultipartUpload.AnswerAsync(reply, request, target, loginResult.Caller!, cancellationToken),
            S3Operation.UploadPart => await uploadPart.AnswerAsync(reply, request, target, loginResult, cancellationToken),
            S3Operation.CompleteMultipartUpload => await completeMultipartUpload.AnswerAsync(reply, request, target, loginResult, cancellationToken),
            _ => await abortMultipartUpload.AnswerAsync(reply, target, cancellationToken),
        };
    }

    /// <summary>
    /// Words a head past <c>--max-request-head</c> <c>400 RequestHeaderSectionTooLarge</c> with
    /// the <c>Error</c> body (ADR-0108, decision 7); every other head that could not be read
    /// keeps <paramref name="standardRefusal"/>.
    /// </summary>
    /// <param name="outcome">Why no head was read.</param>
    /// <param name="standardRefusal">The refusal <see cref="Http1ConnectionServer"/> writes otherwise.</param>
    /// <returns>The refusal to write.</returns>
    public HttpAnswer RefuseHeadNotRead(HttpRequestHeadReadOutcome outcome, HttpAnswer standardRefusal) =>
        outcome == HttpRequestHeadReadOutcome.HeadTooLarge
            ? S3Reply.UnreadRequestError(S3Error.RequestHeaderSectionTooLarge, RandomNumberGenerator.GetHexString(16), context.TimeProvider.GetUtcNow())
            : standardRefusal;

    // A signature binding a body that only PutObject, UploadPart and CompleteMultipartUpload read:
    // any other operation reads and discards it first, so its hash is checked before anything is answered.
    private async Task<S3Error?> UnusedBodyRefusalAsync(HttpRequest request, S3Operation operation, S3LoginResult loginResult, CancellationToken cancellationToken) =>
        operation is S3Operation.PutObject or S3Operation.UploadPart or S3Operation.CompleteMultipartUpload || loginResult.BodyCheck is not { } bodyCheck
            ? null
            : await login.JudgeDiscardedBodyAsync(request, bodyCheck, cancellationToken);

    private async Task<HttpAnswer> PutObjectAsync(S3Reply reply, HttpRequest request, S3RequestTarget target, S3LoginResult loginResult, CancellationToken cancellationToken) =>
        S3ObjectKey.IsTooLong(target.RawKey)
            ? reply.Error(S3Error.KeyTooLong)
            : await putObject.AnswerAsync(reply, request, target, loginResult, cancellationToken);

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
