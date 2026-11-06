using System.Security.Cryptography;
using System.Text;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Answers PutObject (PutObject, "Checking object integrity", "Working with object metadata";
/// ADR-0108, decisions 1, 3 and 7): stores the body as the file at the key, creating the key's
/// folders, keeps its header fields and <c>x-amz-meta-*</c> in the object metadata index, and
/// answers <c>200</c> with the body's MD5 as its <c>ETag</c>.
/// </summary>
/// <remarks>
/// Uploads need <c>--allow-uploads</c>, which the store's exposure options carry: off, every
/// PutObject is <c>403 AccessDenied</c> and nothing is read. A key ending in <c>/</c> with an empty
/// body creates the folder.
/// </remarks>
/// <param name="store">The content store the object is written to.</param>
/// <param name="index">The object metadata index of the store's served root.</param>
/// <param name="login">Finishes a signature that binds the body, once the body is read.</param>
/// <param name="log">Where a failure to rewrite the index file is noted.</param>
internal sealed class S3PutObject(ContentStore store, ObjectMetadataIndex index, S3Login login, IExchangeLog log)
{
    /// <summary>
    /// The most UTF-8 bytes of <c>x-amz-meta-*</c> names and values ("Working with object metadata").
    /// </summary>
    public const int MaxUserMetadataBytes = 2048;

    private const string UserMetadataPrefix = "x-amz-meta-";

    /// <summary>
    /// Answers a PutObject whose bucket exists.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose body is read.</param>
    /// <param name="target">The request's bucket and key.</param>
    /// <param name="loginResult">Who writes, on <c>s3</c>, and the rest of a signature that binds the body.</param>
    /// <param name="cancellationToken">Cancels reading the body and writing the file.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(S3Reply reply, HttpRequest request, S3RequestTarget target, S3LoginResult loginResult, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.AllowUploads)
        {
            return reply.Error(S3Error.AccessDenied);
        }

        if (RefusalBeforeTheBody(request) is { } refusal)
        {
            return reply.Error(refusal);
        }

        if (IsAnnouncedPastTheLimit(request.Body))
        {
            return reply.Error(S3Error.EntityTooLarge);
        }

        var caller = loginResult.Caller!;
        var mapping = store.MapBucketRequestPathForCaller(target.ObjectPath, caller);
        if (!mapping.IsMapped)
        {
            return reply.Error(S3Error.InvalidArgument, KeyArgument(target));
        }

        return CreateFolders(target, caller) is { } folderRefusal
            ? reply.Error(folderRefusal)
            : await StoreAsync(reply, request, target, mapping, loginResult.BodyCheck, cancellationToken);
    }

    /// <summary>
    /// What a committed upload's result is answered with.
    /// </summary>
    /// <param name="committed">What committing the upload session did.</param>
    /// <returns><see langword="null"/> when the file is written, else the error.</returns>
    internal static S3Error? CommitRefusal(ContentUploadResult committed) =>
        committed == ContentUploadResult.Written ? null : S3Error.AccessDenied;

    /// <summary>
    /// What reading a body into the upload is answered with when it fails: past the upload limit,
    /// <c>EntityTooLarge</c>; cut short or malformed, <c>IncompleteBody</c>.
    /// </summary>
    /// <param name="outcome">How reading the body ended.</param>
    /// <param name="isTooLarge">Whether the upload session refused the bytes as too many.</param>
    /// <returns><see langword="null"/> when the whole body is in the upload, else the error.</returns>
    internal static S3Error? BodyRefusal(HttpRequestBodyDiscardOutcome outcome, bool isTooLarge)
    {
        if (outcome == HttpRequestBodyDiscardOutcome.TooLarge || isTooLarge)
        {
            return S3Error.EntityTooLarge;
        }

        return outcome == HttpRequestBodyDiscardOutcome.Incomplete ? S3Error.IncompleteBody : null;
    }

    /// <summary>
    /// What storing an upload's metadata is answered with when it is refused.
    /// </summary>
    /// <param name="outcome">What the index did.</param>
    /// <returns><see langword="null"/> when the object stands, else the error.</returns>
    internal static S3Error? MetadataRefusal(ObjectMetadataStoreOutcome outcome) => outcome switch
    {
        ObjectMetadataStoreOutcome.MetadataTooLarge => S3Error.MetadataTooLarge,
        ObjectMetadataStoreOutcome.MetadataIndexFull => S3Error.ServiceUnavailable,
        _ => null,
    };

    // The checks a request's head decides, before any byte of its body is read.
    private static S3Error? RefusalBeforeTheBody(HttpRequest request)
    {
        if (S3RequestFields.Value(request.Fields, "Content-MD5") is { } contentMd5 && ExpectedMd5(contentMd5) is null)
        {
            return S3Error.InvalidDigest;
        }

        return UserMetadataBytes(UserMetadata(request.Fields)) > MaxUserMetadataBytes
            ? S3Error.MetadataTooLarge
            : BodyFramingRefusal(request);
    }

    // A body must be readable and announced by Content-Length; a chunked one is aws-chunked
    // (BL-711) when it names its decoded length, and unreadable as an object when it does not.
    private static S3Error? BodyFramingRefusal(HttpRequest request)
    {
        var body = request.Body;
        if (!body.IsReadable)
        {
            return S3Error.InvalidRequest;
        }

        if (body.IsAnnounced && body.AnnouncedLength is null)
        {
            return S3RequestFields.Value(request.Fields, "x-amz-decoded-content-length") is null ? S3Error.InvalidRequest : S3Error.NotImplemented;
        }

        return body.IsAnnounced || S3RequestFields.Value(request.Fields, "Content-Length") is not null ? null : S3Error.MissingContentLength;
    }

    private bool IsAnnouncedPastTheLimit(IHttpRequestBody body) =>
        store.ExposureOptions.MaxUploadBytes > 0 && body.AnnouncedLength > store.ExposureOptions.MaxUploadBytes;

    // Each folder the key names, created when missing; a file in the way is a conflict.
    private S3Error? CreateFolders(S3RequestTarget target, ContentCaller caller)
    {
        var segments = target.RawKey.Split('/');
        for (var count = 1; count < segments.Length; count++)
        {
            var folder = store.MapBucketRequestPathForCaller($"/{target.RawBucket}/{string.Join('/', segments[..count])}", caller);
            if (CreateFolder(folder) is { } refusal)
            {
                return refusal;
            }
        }

        return null;
    }

    private S3Error? CreateFolder(ContentPathMapping folder) => folder.EntryKind switch
    {
        ContentEntryKind.Directory => null,
        ContentEntryKind.File => S3Error.ConflictingKey,
        _ => store.CreateDirectory(folder) == ContentChangeResult.Done ? null : S3Error.AccessDenied,
    };

    private async Task<HttpAnswer> StoreAsync(S3Reply reply, HttpRequest request, S3RequestTarget target, ContentPathMapping mapping, IHttpRequestBodyCheck? bodyCheck, CancellationToken cancellationToken)
    {
        if (target.RawKey.EndsWith('/'))
        {
            return request.Body.IsAnnounced
                ? reply.Error(S3Error.InvalidArgument, KeyArgument(target))
                : Stored(reply, MD5.HashData(ReadOnlySpan<byte>.Empty));
        }

        var opening = await store.OpenUploadAsync(mapping, new ContentUploadOpening(StartsFromExistingBytes: false, CreatesMissingFile: true, RefusesExistingFile: false), cancellationToken);
        if (opening.Session is not { } session)
        {
            return reply.Error(opening.Result == ContentUploadOpeningResult.IsADirectory ? S3Error.ConflictingKey : S3Error.AccessDenied);
        }

        await using (session)
        {
            return await WriteAsync(reply, request, target, mapping, session, bodyCheck, cancellationToken);
        }
    }

    // The body goes into the upload session, which is committed only once the body is the one
    // signed and matches any Content-MD5; refused, the session is disposed uncommitted and the
    // old object stands.
    private async Task<HttpAnswer> WriteAsync(S3Reply reply, HttpRequest request, S3RequestTarget target, ContentPathMapping mapping, ContentUploadSession session, IHttpRequestBodyCheck? bodyCheck, CancellationToken cancellationToken)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var sink = new S3UploadStream(session, md5, sha256);
        var outcome = request.Body.IsAnnounced ? await request.Body.CopyToAsync(sink, cancellationToken) : HttpRequestBodyDiscardOutcome.Discarded;
        if (BodyRefusal(outcome, sink.IsTooLarge) is { } bodyRefusal)
        {
            return reply.Error(bodyRefusal);
        }

        var digest = md5.GetHashAndReset();
        if (await ContentRefusalAsync(request, bodyCheck, sha256.GetHashAndReset(), digest, cancellationToken) is { } contentRefusal)
        {
            return reply.Error(contentRefusal);
        }

        if (CommitRefusal(await session.CommitAsync(cancellationToken)) is { } commitRefusal)
        {
            return reply.Error(commitRefusal);
        }

        return await StoreMetadataAsync(reply, request, target, mapping, digest, cancellationToken);
    }

    // The body against what the request said of it: the signed x-amz-content-sha256 first
    // (XAmzContentSHA256Mismatch), then Content-MD5 (BadDigest).
    private async Task<S3Error?> ContentRefusalAsync(HttpRequest request, IHttpRequestBodyCheck? bodyCheck, byte[] bodySha256, byte[] bodyMd5, CancellationToken cancellationToken)
    {
        if (bodyCheck is not null && await login.JudgeBodyAsync(bodyCheck, bodySha256, cancellationToken) is { } signatureRefusal)
        {
            return signatureRefusal;
        }

        return S3RequestFields.Value(request.Fields, "Content-MD5") is { } contentMd5 && !bodyMd5.AsSpan().SequenceEqual(ExpectedMd5(contentMd5))
            ? S3Error.BadDigest
            : null;
    }

    // The metadata goes into the index for a file it keys; refused there, the object is removed.
    private async Task<HttpAnswer> StoreMetadataAsync(S3Reply reply, HttpRequest request, S3RequestTarget target, ContentPathMapping mapping, byte[] digest, CancellationToken cancellationToken)
    {
        if (S3IndexKey.OfRaw(store, target.RawBucket, target.RawKey) is not { } indexKey)
        {
            return Stored(reply, digest);
        }

        var stored = await index.StoreAsync(indexKey, MetadataOf(request.Fields, ETagOf(digest)), cancellationToken);
        if (stored.StorageFailure is { } failure)
        {
            log.Note($"The object metadata index could not be written: {failure}");
        }

        if (MetadataRefusal(stored.Outcome) is { } refusal)
        {
            store.DeleteFile(mapping);
            return reply.Error(refusal);
        }

        return Stored(reply, digest);
    }

    private static HttpAnswer Stored(S3Reply reply, byte[] digest) =>
        reply.Answer(HttpStatus.Ok, [new("ETag", ETagOf(digest)), new("Content-Length", "0")], HttpAnswerBody.None);

    private static string ETagOf(byte[] digest) => $"\"{Convert.ToHexStringLower(digest)}\"";

    private static ObjectMetadata MetadataOf(IReadOnlyList<KeyValuePair<string, string>> fields, string eTag) => new()
    {
        ContentType = S3RequestFields.Value(fields, "Content-Type"),
        ContentEncoding = S3RequestFields.Value(fields, "Content-Encoding"),
        ContentDisposition = S3RequestFields.Value(fields, "Content-Disposition"),
        CacheControl = S3RequestFields.Value(fields, "Cache-Control"),
        ContentLanguage = S3RequestFields.Value(fields, "Content-Language"),
        UserMetadata = UserMetadata(fields),
        ETag = eTag,
    };

    // Each x-amz-meta-* field by its name, lower-cased and without the prefix; a repeated name keeps its last value.
    private static Dictionary<string, string> UserMetadata(IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in fields.Where(field => field.Key.StartsWith(UserMetadataPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            metadata[field.Key[UserMetadataPrefix.Length..].ToLowerInvariant()] = field.Value;
        }

        return metadata;
    }

    private static int UserMetadataBytes(Dictionary<string, string> metadata) =>
        metadata.Sum(entry => Encoding.UTF8.GetByteCount(entry.Key) + Encoding.UTF8.GetByteCount(entry.Value));

    // The 16 bytes a Content-MD5 field's base64 holds, or null when it holds anything else.
    private static byte[]? ExpectedMd5(string contentMd5)
    {
        var bytes = new byte[64];

        return Convert.TryFromBase64String(contentMd5.Trim(), bytes, out var written) && written == 16 ? bytes[..16] : null;
    }

    private static KeyValuePair<string, string>[] KeyArgument(S3RequestTarget target) =>
        [new("ArgumentName", "key"), new("ArgumentValue", target.RawKey)];
}
