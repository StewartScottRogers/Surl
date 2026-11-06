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

        if (RefusalBeforeTheObjectBody(request, loginResult.ChunkSignatures) is { } refusal)
        {
            return reply.Error(refusal);
        }

        if (IsAnnouncedPastTheLimit(request))
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
            : await StoreAsync(reply, request, target, mapping, loginResult, cancellationToken);
    }

    /// <summary>
    /// What a committed upload's result is answered with.
    /// </summary>
    /// <param name="committed">What committing the upload session did.</param>
    /// <returns><see langword="null"/> when the file is written, else the error.</returns>
    internal static S3Error? CommitRefusal(ContentUploadResult committed) =>
        committed == ContentUploadResult.Written ? null : S3Error.AccessDenied;

    /// <summary>
    /// What an upload session the store would not open is answered with: a folder at the key,
    /// <c>409 InvalidArgument</c>; anything else the store refuses, <c>403 AccessDenied</c>.
    /// </summary>
    /// <param name="result">Why no session was opened.</param>
    /// <returns>The error.</returns>
    internal static S3Error OpeningRefusal(ContentUploadOpeningResult result) =>
        result == ContentUploadOpeningResult.IsADirectory ? S3Error.ConflictingKey : S3Error.AccessDenied;

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

    // The checks a CompleteMultipartUpload's head decides, before any byte of its body is read:
    // its body is a document, never aws-chunked, and its x-amz-checksum-* fields describe the
    // whole object, so they are not checked against the body.
    internal static S3Error? RefusalBeforeTheBody(HttpRequest request) =>
        HeadFieldRefusal(request.Fields)
        ?? (S3ObjectBodyReader.FormOf(request.Fields) == S3PayloadForm.Plain ? S3ObjectBodyReader.PlainFramingRefusal(request) : S3Error.NotImplemented);

    /// <summary>
    /// The checks a PutObject's or UploadPart's head decides, before any byte of its body is
    /// read: <c>Content-MD5</c>, the user metadata, then the body's framing and checksum fields
    /// (<see cref="S3ObjectBodyReader.RefusalBeforeTheBody"/>).
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="chunkSignatures">The chain the login gave for signed chunks, or <see langword="null"/>.</param>
    /// <returns>The error, or <see langword="null"/>.</returns>
    internal static S3Error? RefusalBeforeTheObjectBody(HttpRequest request, IHttpChunkSignatureChain? chunkSignatures) =>
        HeadFieldRefusal(request.Fields) ?? S3ObjectBodyReader.RefusalBeforeTheBody(request, chunkSignatures);

    private static S3Error? HeadFieldRefusal(IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        if (S3RequestFields.Value(fields, "Content-MD5") is { } contentMd5 && ExpectedMd5(contentMd5) is null)
        {
            return S3Error.InvalidDigest;
        }

        return HasTooMuchUserMetadata(fields) ? S3Error.MetadataTooLarge : null;
    }

    /// <summary>
    /// Whether the <c>x-amz-meta-*</c> names and values pass <see cref="MaxUserMetadataBytes"/>.
    /// </summary>
    /// <param name="fields">The request's fields.</param>
    /// <returns>Whether the user metadata is too large.</returns>
    internal static bool HasTooMuchUserMetadata(IReadOnlyList<KeyValuePair<string, string>> fields) =>
        UserMetadataBytes(UserMetadata(fields)) > MaxUserMetadataBytes;

    // The object's announced length (an aws-chunked body's decoded length) past --max-filesize.
    internal bool IsAnnouncedPastTheLimit(HttpRequest request) =>
        store.ExposureOptions.MaxUploadBytes > 0 && S3ObjectBodyReader.AnnouncedObjectLength(request) > store.ExposureOptions.MaxUploadBytes;

    // Each folder the key names, created when missing; a file in the way is a conflict.
    internal S3Error? CreateFolders(S3RequestTarget target, ContentCaller caller)
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

    private async Task<HttpAnswer> StoreAsync(S3Reply reply, HttpRequest request, S3RequestTarget target, ContentPathMapping mapping, S3LoginResult loginResult, CancellationToken cancellationToken)
    {
        if (target.RawKey.EndsWith('/'))
        {
            return request.Body.IsAnnounced
                ? reply.Error(S3Error.InvalidArgument, KeyArgument(target))
                : await StoreFolderObjectAsync(reply, request.Fields, target, cancellationToken);
        }

        var opening = await store.OpenUploadAsync(mapping, new ContentUploadOpening(StartsFromExistingBytes: false, CreatesMissingFile: true, RefusesExistingFile: false), cancellationToken);
        if (opening.Session is not { } session)
        {
            return reply.Error(OpeningRefusal(opening.Result));
        }

        await using (session)
        {
            return await WriteAsync(reply, request, target, mapping, session, loginResult, cancellationToken);
        }
    }

    // The body goes into the upload session, which is committed only once the body is the one
    // signed and matches any Content-MD5 and additional checksum; refused, the session is
    // disposed uncommitted and the old object stands.
    private async Task<HttpAnswer> WriteAsync(S3Reply reply, HttpRequest request, S3RequestTarget target, ContentPathMapping mapping, ContentUploadSession session, S3LoginResult loginResult, CancellationToken cancellationToken)
    {
        var received = await S3ObjectBodyReader.ReadAsync(request, session, loginResult.ChunkSignatures, cancellationToken);
        if (received.Refusal is { } bodyRefusal)
        {
            return reply.Error(bodyRefusal);
        }

        if (await BodyContentRefusalAsync(request, loginResult.BodyCheck, received, cancellationToken) is { } contentRefusal)
        {
            return reply.Error(contentRefusal);
        }

        if (CommitRefusal(await session.CommitAsync(cancellationToken)) is { } commitRefusal)
        {
            return reply.Error(commitRefusal);
        }

        return await StoreMetadataAsync(request.Fields, target, mapping, ETagOf(received.Md5), cancellationToken) is { } metadataRefusal
            ? reply.Error(metadataRefusal)
            : Stored(reply, ETagOf(received.Md5), received.Checksum);
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

    /// <summary>
    /// A PutObject's or UploadPart's body against what the request said of it: the signed
    /// <c>x-amz-content-sha256</c> first, then <c>Content-MD5</c>, then the additional checksum
    /// (<c>400 BadDigest</c> naming its algorithm).
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="bodyCheck">The rest of a signature that binds the body, or <see langword="null"/>.</param>
    /// <param name="received">What the body came to.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns><see langword="null"/> when the body is the one described, else the error.</returns>
    internal async Task<S3Error?> BodyContentRefusalAsync(HttpRequest request, IHttpRequestBodyCheck? bodyCheck, S3ReceivedBody received, CancellationToken cancellationToken) =>
        await ContentRefusalAsync(request, bodyCheck, received.Sha256, received.Md5, cancellationToken)
        ?? (received.Checksum is { IsMatch: false } checksum ? S3Error.ChecksumMismatch(checksum.Algorithm.Name) : null);

    /// <summary>
    /// Keeps the object's header fields, <c>x-amz-meta-*</c> and <paramref name="eTag"/> in the
    /// index for a file it keys; refused there, the object just written is removed.
    /// </summary>
    /// <param name="fields">The fields of the request that named the metadata.</param>
    /// <param name="target">The object's bucket and key.</param>
    /// <param name="mapping">Where the object was written.</param>
    /// <param name="eTag">The object's quoted ETag.</param>
    /// <param name="cancellationToken">Cancels writing the index.</param>
    /// <returns><see langword="null"/> when the object stands, else the error.</returns>
    internal async Task<S3Error?> StoreMetadataAsync(IReadOnlyList<KeyValuePair<string, string>> fields, S3RequestTarget target, ContentPathMapping mapping, string eTag, CancellationToken cancellationToken)
    {
        if (S3IndexKey.OfRaw(store, target.RawBucket, target.RawKey) is not { } indexKey)
        {
            return null;
        }

        var stored = await index.StoreAsync(indexKey, MetadataOf(fields, eTag), cancellationToken);
        if (stored.StorageFailure is { } failure)
        {
            log.Note($"The object metadata index could not be written: {failure}");
        }

        var refusal = MetadataRefusal(stored.Outcome);
        if (refusal is not null)
        {
            store.DeleteFile(mapping);
        }

        return refusal;
    }

    // A zero-byte PUT of a key ending in '/': the folder, already created, is recorded as an object
    // in the index so ListObjectsV2 lists it as the key x/ (ADR-0108, decision 6). A folder in a
    // share has no index key, so it stands as a folder only.
    private async Task<HttpAnswer> StoreFolderObjectAsync(S3Reply reply, IReadOnlyList<KeyValuePair<string, string>> fields, S3RequestTarget target, CancellationToken cancellationToken)
    {
        var digest = MD5.HashData(ReadOnlySpan<byte>.Empty);
        var indexKey = S3IndexKey.OfFolderObject(store, Uri.UnescapeDataString(target.RawBucket), Uri.UnescapeDataString(target.RawKey));
        if (indexKey is null)
        {
            return Stored(reply, ETagOf(digest));
        }

        var stored = await index.StoreFolderObjectAsync(indexKey, MetadataOf(fields, ETagOf(digest)), cancellationToken);
        if (stored.StorageFailure is { } failure)
        {
            log.Note($"The object metadata index could not be written: {failure}");
        }

        return MetadataRefusal(stored.Outcome) is { } refusal ? reply.Error(refusal) : Stored(reply, ETagOf(digest));
    }

    /// <summary>
    /// The <c>200</c> a stored object or part is answered with: its <c>ETag</c>, then the
    /// additional checksum echoed when one was checked.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="eTag">The quoted ETag.</param>
    /// <param name="checksum">The checksum checked, or <see langword="null"/>.</param>
    /// <returns>The answer.</returns>
    internal static HttpAnswer Stored(S3Reply reply, string eTag, S3ReceivedChecksum? checksum = null) =>
        reply.Answer(
            HttpStatus.Ok,
            [new("ETag", eTag), .. checksum is null ? [] : new[] { checksum.Field }, new("Content-Length", "0")],
            HttpAnswerBody.None);

    internal static string ETagOf(byte[] digest) => $"\"{Convert.ToHexStringLower(digest)}\"";

    private static ObjectMetadata MetadataOf(IReadOnlyList<KeyValuePair<string, string>> fields, string eTag) => new()
    {
        ContentType = S3RequestFields.Value(fields, "Content-Type"),
        ContentEncoding = StoredContentEncoding(S3RequestFields.Value(fields, "Content-Encoding")),
        ContentDisposition = S3RequestFields.Value(fields, "Content-Disposition"),
        CacheControl = S3RequestFields.Value(fields, "Cache-Control"),
        ContentLanguage = S3RequestFields.Value(fields, "Content-Language"),
        UserMetadata = UserMetadata(fields),
        ETag = eTag,
    };

    // The object's Content-Encoding without aws-chunked, which framed the upload and is not the
    // object's coding ("Transferring Payload in Multiple Chunks"); none left is none stored.
    internal static string? StoredContentEncoding(string? sent)
    {
        var codings = sent?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(coding => !coding.Equals("aws-chunked", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return codings is { Length: > 0 } ? string.Join(", ", codings) : null;
    }

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
    internal static byte[]? ExpectedMd5(string contentMd5)
    {
        var bytes = new byte[64];

        return Convert.TryFromBase64String(contentMd5.Trim(), bytes, out var written) && written == 16 ? bytes[..16] : null;
    }

    internal static KeyValuePair<string, string>[] KeyArgument(S3RequestTarget target) =>
        [new("ArgumentName", "key"), new("ArgumentValue", target.RawKey)];
}
