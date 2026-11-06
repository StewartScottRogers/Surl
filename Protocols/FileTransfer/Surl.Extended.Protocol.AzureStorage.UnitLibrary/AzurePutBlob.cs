using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Answers Put Blob ("Put Blob"; ADR-0109, decisions 2, 5.6 and 7): stores the body as the file at
/// the blob name, overwriting one there and creating the name's folders, keeps its
/// <c>x-ms-blob-*</c> fields and <c>x-ms-meta-*</c> in the object metadata index, and answers
/// <c>201</c> with the body's MD5 as its <c>ETag</c> and <c>Content-MD5</c>.
/// </summary>
/// <remarks>
/// Uploads need <c>--allow-uploads</c>, which the store's exposure options carry: off, every Put
/// Blob is <c>403 AuthorizationPermissionMismatch</c> and nothing is read. A name ending in
/// <c>/</c> with an empty body creates the folder. The conditional fields are checked against the
/// blob at the name before the body is read (<see cref="AzureConditions.WriteRefusal"/>).
/// </remarks>
/// <param name="store">The content store the blob is written to.</param>
/// <param name="index">The object metadata index of the store's served root.</param>
/// <param name="log">Where a failure to rewrite the index file is noted.</param>
internal sealed class AzurePutBlob(ContentStore store, ObjectMetadataIndex index, IExchangeLog log)
{
    /// <summary>
    /// The most UTF-8 bytes of <c>x-ms-meta-*</c> names and values (ADR-0109, decision 7).
    /// </summary>
    public const int MaxUserMetadataBytes = 8192;

    private const string UserMetadataPrefix = "x-ms-meta-";

    private readonly AzureBlobPropertiesReader properties = new(store, index, log);

    /// <summary>
    /// Answers a Put Blob whose container exists and whose name is not too long.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose body is read.</param>
    /// <param name="target">The request's container and blob name.</param>
    /// <param name="caller">Who writes, and on which scheme.</param>
    /// <param name="cancellationToken">Cancels reading the body and writing the file.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(AzureReply reply, HttpRequest request, AzureBlobTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.AllowUploads)
        {
            return reply.Error(AzureRefusal.Of(AzureError.AuthorizationPermissionMismatch));
        }

        if (RefusalBeforeTheBody(request) is { } refusal)
        {
            return reply.Error(AzureRefusal.Of(refusal));
        }

        if (IsAnnouncedPastTheLimit(request.Body))
        {
            return reply.Error(AzureRefusal.Of(AzureError.RequestBodyTooLarge));
        }

        var mapping = store.MapBucketRequestPathForCaller(target.Path, caller);
        if (!mapping.IsMapped)
        {
            return reply.Error(AzureRefusal.Of(AzureError.InvalidResourceName));
        }

        if (await ConditionRefusalAsync(request, target, mapping, cancellationToken) is { } conditionRefusal)
        {
            return reply.Error(AzureRefusal.Of(conditionRefusal));
        }

        return CreateFolders(target, caller) is { } folderRefusal
            ? reply.Error(AzureRefusal.Of(folderRefusal))
            : await StoreAsync(reply, request, target, mapping, cancellationToken);
    }

    // The conditional fields, checked against the blob at the name (ADR-0109, decision 1).
    private async Task<AzureError?> ConditionRefusalAsync(HttpRequest request, AzureBlobTarget target, ContentPathMapping mapping, CancellationToken cancellationToken)
    {
        var existing = await properties.ReadAsync(mapping, AzureIndexKey.OfRaw(store, target.RawContainer, target.RawBlob), cancellationToken);

        return AzureConditions.WriteRefusal(request.Fields, existing);
    }

    /// <summary>
    /// What reading a body into the upload is answered with when it fails: past the upload limit,
    /// <c>RequestBodyTooLarge</c>; cut short or malformed, <c>InvalidInput</c>.
    /// </summary>
    /// <param name="outcome">How reading the body ended.</param>
    /// <param name="isTooLarge">Whether the upload session refused the bytes as too many.</param>
    /// <returns><see langword="null"/> when the whole body is in the upload, else the error.</returns>
    internal static AzureError? BodyRefusal(HttpRequestBodyDiscardOutcome outcome, bool isTooLarge)
    {
        if (outcome == HttpRequestBodyDiscardOutcome.TooLarge || isTooLarge)
        {
            return AzureError.RequestBodyTooLarge;
        }

        return outcome == HttpRequestBodyDiscardOutcome.Incomplete ? AzureError.InvalidInput : null;
    }

    /// <summary>
    /// What storing an upload's metadata is answered with when it is refused.
    /// </summary>
    /// <param name="outcome">What the index did.</param>
    /// <returns><see langword="null"/> when the blob stands, else the error.</returns>
    internal static AzureError? MetadataRefusal(ObjectMetadataStoreOutcome outcome) => outcome switch
    {
        ObjectMetadataStoreOutcome.MetadataTooLarge => AzureError.MetadataTooLarge,
        ObjectMetadataStoreOutcome.MetadataIndexFull => AzureError.ServerBusy,
        _ => null,
    };

    /// <summary>
    /// What committing the upload session is answered with when it does not write the file.
    /// </summary>
    /// <param name="committed">What committing did.</param>
    /// <returns><see langword="null"/> when the file is written, else the error.</returns>
    internal static AzureError? CommitRefusal(ContentUploadResult committed) =>
        committed == ContentUploadResult.Written ? null : AzureError.AuthorizationPermissionMismatch;

    // The checks a request's head decides, before any byte of its body is read.
    internal static AzureError? RefusalBeforeTheBody(HttpRequest request)
    {
        if (AzureRequestFields.Value(request.Fields, "Content-MD5") is { } contentMd5 && ExpectedMd5(contentMd5) is null)
        {
            return AzureError.InvalidMd5;
        }

        var metadata = UserMetadata(request.Fields);
        if (!metadata.Keys.All(IsMetadataName))
        {
            return AzureError.InvalidMetadata;
        }

        return UserMetadataBytes(metadata) > MaxUserMetadataBytes ? AzureError.MetadataTooLarge : BodyFramingRefusal(request);
    }

    // A body must be readable, and announced by Content-Length or a chunked framing.
    internal static AzureError? BodyFramingRefusal(HttpRequest request)
    {
        if (!request.Body.IsReadable)
        {
            return AzureError.InvalidInput;
        }

        return request.Body.IsAnnounced || AzureRequestFields.Value(request.Fields, "Content-Length") is not null
            ? null
            : AzureError.MissingContentLengthHeader;
    }

    // A metadata name must be a C# identifier ("Naming and Referencing Containers, Blobs, and Metadata").
    internal static bool IsMetadataName(string name) =>
        name.Length > 0 && (char.IsAsciiLetter(name[0]) || name[0] == '_') && name.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');

    internal bool IsAnnouncedPastTheLimit(IHttpRequestBody body) =>
        store.ExposureOptions.MaxUploadBytes > 0 && body.AnnouncedLength > store.ExposureOptions.MaxUploadBytes;

    // Each folder the name holds, created when missing; a file in the way is a conflict.
    internal AzureError? CreateFolders(AzureBlobTarget target, ContentCaller caller)
    {
        var segments = target.RawBlob.Split('/');
        for (var count = 1; count < segments.Length; count++)
        {
            var folder = store.MapBucketRequestPathForCaller($"/{target.RawContainer}/{string.Join('/', segments[..count])}", caller);
            if (CreateFolder(folder) is { } refusal)
            {
                return refusal;
            }
        }

        return null;
    }

    private AzureError? CreateFolder(ContentPathMapping folder) => folder.EntryKind switch
    {
        ContentEntryKind.Directory => null,
        ContentEntryKind.File => AzureError.ResourceNameConflict,
        _ => store.CreateDirectory(folder) == ContentChangeResult.Done ? null : AzureError.AuthorizationPermissionMismatch,
    };

    private async Task<HttpAnswer> StoreAsync(AzureReply reply, HttpRequest request, AzureBlobTarget target, ContentPathMapping mapping, CancellationToken cancellationToken)
    {
        if (target.RawBlob.EndsWith('/'))
        {
            return request.Body.IsAnnounced
                ? reply.Error(AzureRefusal.Of(AzureError.InvalidResourceName))
                : Stored(reply, MD5.HashData(ReadOnlySpan<byte>.Empty), reply.Now);
        }

        var opening = await store.OpenUploadAsync(mapping, new ContentUploadOpening(StartsFromExistingBytes: false, CreatesMissingFile: true, RefusesExistingFile: false), cancellationToken);
        if (opening.Session is not { } session)
        {
            return reply.Error(AzureRefusal.Of(OpeningRefusal(opening.Result)));
        }

        await using (session)
        {
            return await WriteAsync(reply, request, target, mapping, session, cancellationToken);
        }
    }

    private async Task<HttpAnswer> WriteAsync(AzureReply reply, HttpRequest request, AzureBlobTarget target, ContentPathMapping mapping, ContentUploadSession session, CancellationToken cancellationToken)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        var sink = new AzureUploadStream(session, md5);
        var outcome = request.Body.IsAnnounced ? await request.Body.CopyToAsync(sink, cancellationToken) : HttpRequestBodyDiscardOutcome.Discarded;
        if (BodyRefusal(outcome, sink.IsTooLarge) is { } bodyRefusal)
        {
            return reply.Error(AzureRefusal.Of(bodyRefusal));
        }

        var digest = md5.GetHashAndReset();
        if (AzureRequestFields.Value(request.Fields, "Content-MD5") is { } contentMd5 && !digest.AsSpan().SequenceEqual(ExpectedMd5(contentMd5)))
        {
            return reply.Error(AzureRefusal.Of(AzureError.Md5Mismatch));
        }

        if (CommitRefusal(await session.CommitAsync(cancellationToken)) is { } commitRefusal)
        {
            return reply.Error(AzureRefusal.Of(commitRefusal));
        }

        var lastModified = store.GetFileStatus(mapping)!.LastModifiedUtc;

        return await StoreMetadataAsync(request.Fields, target, mapping, digest, cancellationToken) is { } metadataRefusal
            ? reply.Error(AzureRefusal.Of(metadataRefusal))
            : Stored(reply, digest, lastModified);
    }

    /// <summary>
    /// Keeps a just-written blob's <c>x-ms-blob-*</c> fields, <c>x-ms-meta-*</c> and ETag in the
    /// index, for a blob the index keys; refused there, the blob is removed.
    /// </summary>
    /// <param name="fields">The request's fields.</param>
    /// <param name="target">The blob's container and name.</param>
    /// <param name="mapping">Where the blob was written.</param>
    /// <param name="digest">The blob's MD5, its ETag.</param>
    /// <param name="cancellationToken">Cancels rewriting the index.</param>
    /// <returns><see langword="null"/> when the blob stands, else the error.</returns>
    internal async Task<AzureError?> StoreMetadataAsync(IReadOnlyList<KeyValuePair<string, string>> fields, AzureBlobTarget target, ContentPathMapping mapping, byte[] digest, CancellationToken cancellationToken)
    {
        if (AzureIndexKey.OfRaw(store, target.RawContainer, target.RawBlob) is not { } indexKey)
        {
            return null;
        }

        var stored = await index.StoreAsync(indexKey, MetadataOf(fields, ETagOf(digest)), cancellationToken);
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

    /// <summary>
    /// Creates the blob name's folders and opens an upload session that writes the blob afresh,
    /// as Put Block List writes it and Put Block stages beside it.
    /// </summary>
    /// <param name="target">The blob's container and name.</param>
    /// <param name="mapping">Where the blob is written.</param>
    /// <param name="caller">Who writes, and on which scheme.</param>
    /// <param name="cancellationToken">Cancels opening the session.</param>
    /// <returns>The session, which the caller disposes, or the error when it does not open.</returns>
    internal async Task<(AzureError? Refusal, ContentUploadSession? Session)> OpenBlobUploadAsync(AzureBlobTarget target, ContentPathMapping mapping, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (CreateFolders(target, caller) is { } folderRefusal)
        {
            return (folderRefusal, null);
        }

        var opening = await store.OpenUploadAsync(mapping, new ContentUploadOpening(StartsFromExistingBytes: false, CreatesMissingFile: true, RefusesExistingFile: false), cancellationToken);

        return opening.Session is { } session ? (null, session) : (OpeningRefusal(opening.Result), null);
    }

    /// <summary>
    /// What opening an upload session is answered with when it does not open: a folder at the
    /// name <c>409 InvalidResourceName</c>, anything else <c>403 AuthorizationPermissionMismatch</c>.
    /// </summary>
    /// <param name="result">Why the session did not open.</param>
    /// <returns>The error.</returns>
    internal static AzureError OpeningRefusal(ContentUploadOpeningResult result) =>
        result == ContentUploadOpeningResult.IsADirectory ? AzureError.ResourceNameConflict : AzureError.AuthorizationPermissionMismatch;

    // 201 with the tag, the date, the body's MD5 and no body ("Put Blob", "Response headers").
    private static HttpAnswer Stored(AzureReply reply, byte[] digest, DateTimeOffset lastModified) =>
        reply.Answer(
            AzureBlobRequestAnswerer.Created,
            [
                new("ETag", ETagOf(digest)),
                new("Last-Modified", lastModified.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture)),
                new("Content-MD5", Convert.ToBase64String(digest)),
                new("x-ms-request-server-encrypted", "false"),
                new("Content-Length", "0"),
            ],
            HttpAnswerBody.None);

    internal static string ETagOf(byte[] digest) => $"\"{Convert.ToHexStringLower(digest)}\"";

    // x-ms-blob-content-type, else Content-Type; the other x-ms-blob-* fields; each x-ms-meta-*.
    private static ObjectMetadata MetadataOf(IReadOnlyList<KeyValuePair<string, string>> fields, string eTag) => new()
    {
        ContentType = AzureRequestFields.Value(fields, "x-ms-blob-content-type") ?? AzureRequestFields.Value(fields, "Content-Type"),
        ContentEncoding = AzureRequestFields.Value(fields, "x-ms-blob-content-encoding"),
        ContentDisposition = AzureRequestFields.Value(fields, "x-ms-blob-content-disposition"),
        CacheControl = AzureRequestFields.Value(fields, "x-ms-blob-cache-control"),
        ContentLanguage = AzureRequestFields.Value(fields, "x-ms-blob-content-language"),
        UserMetadata = UserMetadata(fields),
        ETag = eTag,
    };

    // Each x-ms-meta-* field by its name, lower-cased and without the prefix; a repeated name keeps its last value.
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
}
