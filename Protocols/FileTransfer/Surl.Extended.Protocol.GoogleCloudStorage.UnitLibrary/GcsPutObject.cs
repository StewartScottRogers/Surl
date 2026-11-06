using System.Globalization;
using System.Text;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Answers PUT Object ("PUT Object", "HTTP headers and common request headers"; ADR-0110,
/// decisions 1.2 and 7): stores the body as the file at the object name, creating its folders,
/// keeps its header fields, <c>x-goog-meta-*</c> and generation in the object metadata index, and
/// answers <c>200</c> with <c>ETag</c>, <c>x-goog-generation</c> and <c>x-goog-hash</c>.
/// </summary>
/// <remarks>
/// Uploads need <c>--allow-uploads</c>, which the store's exposure options carry: off, every PUT
/// Object is <c>403 AccessDenied</c> and nothing is read. A name ending in <c>/</c> with an empty
/// body creates the folder; a non-empty one is <c>400 InvalidObjectName</c>.
/// </remarks>
/// <param name="store">The content store the object is written to.</param>
/// <param name="index">The object metadata index of the store's served root.</param>
/// <param name="properties">Reads the existing object when a generation condition is sent.</param>
/// <param name="log">Where a failure to rewrite the index file is noted.</param>
internal sealed class GcsPutObject(ContentStore store, ObjectMetadataIndex index, GcsObjectPropertiesReader properties, IExchangeLog log)
{
    /// <summary>
    /// The most UTF-8 bytes of <c>x-goog-meta-*</c> names and values ("HTTP headers").
    /// </summary>
    public const int MaxUserMetadataBytes = 8192;

    private const string UserMetadataPrefix = "x-goog-meta-";

    /// <summary>
    /// Answers a PUT Object whose bucket exists.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose body is read.</param>
    /// <param name="target">The request's bucket and object name.</param>
    /// <param name="caller">Who writes, on <c>gs</c>.</param>
    /// <param name="cancellationToken">Cancels reading the body and writing the file.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.AllowUploads)
        {
            return reply.Error(GcsError.AccessDenied);
        }

        if (RefusalBeforeTheBody(request, out var sent) is { } refusal)
        {
            return reply.Error(refusal);
        }

        var mapping = store.MapBucketRequestPathForCaller(target.ObjectPath, caller);
        if (!mapping.IsMapped)
        {
            return reply.Error(GcsError.InvalidObjectName);
        }

        if (await GenerationRefusalAsync(request, target, mapping, cancellationToken) is { } generationRefusal)
        {
            return reply.Error(generationRefusal);
        }

        return CreateFolders(target, caller) is { } folderRefusal
            ? reply.Error(folderRefusal)
            : await StoreAsync(reply, request, target, mapping, sent!, cancellationToken);
    }

    /// <summary>
    /// What reading a body into the upload is answered with when it fails: past the upload limit,
    /// <c>EntityTooLarge</c>; cut short, <c>IncompleteBody</c>.
    /// </summary>
    /// <param name="outcome">How reading the body ended.</param>
    /// <param name="isTooLarge">Whether the upload session refused the bytes as too many.</param>
    /// <returns><see langword="null"/> when the whole body is in the upload, else the error.</returns>
    internal static GcsError? BodyRefusal(HttpRequestBodyDiscardOutcome outcome, bool isTooLarge)
    {
        if (outcome == HttpRequestBodyDiscardOutcome.TooLarge || isTooLarge)
        {
            return GcsError.EntityTooLarge;
        }

        return outcome == HttpRequestBodyDiscardOutcome.Incomplete ? GcsError.IncompleteBody : null;
    }

    /// <summary>
    /// What storing an upload's metadata is answered with when it is refused.
    /// </summary>
    /// <param name="outcome">What the index did.</param>
    /// <returns><see langword="null"/> when the object stands, else the error.</returns>
    internal static GcsError? MetadataRefusal(ObjectMetadataStoreOutcome outcome) => outcome switch
    {
        ObjectMetadataStoreOutcome.MetadataTooLarge => GcsError.MetadataTooLarge,
        ObjectMetadataStoreOutcome.MetadataIndexFull => GcsError.InsufficientQuota,
        _ => null,
    };

    // The checks a request's head decides, before any byte of its body is read.
    private GcsError? RefusalBeforeTheBody(HttpRequest request, out GcsSentDigests? sent)
    {
        sent = null;
        if (GcsRequestFields.Value(request.Fields, "Content-Length") is null)
        {
            return GcsError.MissingContentLength;
        }

        if ((sent = GcsSentDigests.Read(request.Fields, out var digestRefusal)) is null)
        {
            return digestRefusal;
        }

        if (UserMetadataBytes(UserMetadata(request.Fields)) > MaxUserMetadataBytes)
        {
            return GcsError.MetadataTooLarge;
        }

        return store.ExposureOptions.MaxUploadBytes > 0 && request.Body.AnnouncedLength > store.ExposureOptions.MaxUploadBytes
            ? GcsError.EntityTooLarge
            : null;
    }

    // x-goog-if-generation-match and x-goog-if-metageneration-match against the object there now.
    private async Task<GcsError?> GenerationRefusalAsync(HttpRequest request, GcsRequestTarget target, ContentPathMapping mapping, CancellationToken cancellationToken)
    {
        if (!GcsGenerationConditions.AreGiven(request.Fields))
        {
            return null;
        }

        var current = await properties.ReadAsync(mapping, GcsIndexKey.OfRaw(store, target.RawBucket, target.RawName), cancellationToken);

        return GcsGenerationConditions.Check(request.Fields, current);
    }

    // Each folder the name holds, created when missing; a file in the way is a conflict.
    private GcsError? CreateFolders(GcsRequestTarget target, ContentCaller caller)
    {
        var segments = target.RawName.Split('/');
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

    private GcsError? CreateFolder(ContentPathMapping folder) => folder.EntryKind switch
    {
        ContentEntryKind.Directory => null,
        ContentEntryKind.File => GcsError.ConflictingObjectName,
        _ => store.CreateDirectory(folder) == ContentChangeResult.Done ? null : GcsError.AccessDenied,
    };

    private async Task<HttpAnswer> StoreAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, ContentPathMapping mapping, GcsSentDigests sent, CancellationToken cancellationToken)
    {
        if (target.RawName.EndsWith('/'))
        {
            using var empty = new GcsObjectHashes();
            return request.Body.IsAnnounced
                ? reply.Error(GcsError.InvalidObjectName)
                : Stored(reply, empty.Finish(), GcsGeneration.Of(reply.Now));
        }

        var opening = await store.OpenUploadAsync(mapping, new ContentUploadOpening(StartsFromExistingBytes: false, CreatesMissingFile: true, RefusesExistingFile: false), cancellationToken);
        if (opening.Session is not { } session)
        {
            return reply.Error(opening.Result == ContentUploadOpeningResult.IsADirectory ? GcsError.ConflictingObjectName : GcsError.AccessDenied);
        }

        await using (session)
        {
            return await WriteAsync(reply, request, target, mapping, session, sent, cancellationToken);
        }
    }

    // The body goes into the upload session, which is committed only once the body matches every
    // digest sent; refused, the session is disposed uncommitted and the old object stands.
    private async Task<HttpAnswer> WriteAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, ContentPathMapping mapping, ContentUploadSession session, GcsSentDigests sent, CancellationToken cancellationToken)
    {
        using var hashes = new GcsObjectHashes();
        var sink = new GcsHashingStream(hashes, session);
        var outcome = request.Body.IsAnnounced ? await request.Body.CopyToAsync(sink, cancellationToken) : HttpRequestBodyDiscardOutcome.Discarded;
        if (BodyRefusal(outcome, sink.IsTooLarge) is { } bodyRefusal)
        {
            return reply.Error(bodyRefusal);
        }

        var digests = hashes.Finish();
        if (!sent.Match(digests))
        {
            return reply.Error(GcsError.BadDigest);
        }

        if (await session.CommitAsync(cancellationToken) != ContentUploadResult.Written)
        {
            return reply.Error(GcsError.AccessDenied);
        }

        return await StoreMetadataAsync(reply, request, target, mapping, digests, cancellationToken);
    }

    // The metadata goes into the index for a file it keys; refused there, the object is removed.
    private async Task<HttpAnswer> StoreMetadataAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, ContentPathMapping mapping, GcsObjectDigests digests, CancellationToken cancellationToken)
    {
        if (GcsIndexKey.OfRaw(store, target.RawBucket, target.RawName) is not { } indexKey)
        {
            return Stored(reply, digests, GcsGeneration.Of(store.GetFileStatus(mapping)!.LastModifiedUtc));
        }

        var generation = GcsGeneration.Of(reply.Now);
        var stored = await index.StoreAsync(indexKey, MetadataOf(request.Fields, digests.ETag, generation), cancellationToken);
        if (stored.StorageFailure is { } failure)
        {
            log.Note($"The object metadata index could not be written: {failure}");
        }

        if (MetadataRefusal(stored.Outcome) is { } refusal)
        {
            store.DeleteFile(mapping);
            return reply.Error(refusal);
        }

        return Stored(reply, digests, generation);
    }

    private static HttpAnswer Stored(GcsReply reply, GcsObjectDigests digests, long generation) =>
        reply.Answer(
            HttpStatus.Ok,
            [
                new("ETag", digests.ETag),
                new("x-goog-generation", generation.ToString(CultureInfo.InvariantCulture)),
                new("x-goog-metageneration", GcsGeneration.Metageneration.ToString(CultureInfo.InvariantCulture)),
                new("x-goog-hash", digests.HashField),
                new("Content-Length", "0"),
            ],
            HttpAnswerBody.None);

    private static ObjectMetadata MetadataOf(IReadOnlyList<KeyValuePair<string, string>> fields, string eTag, long generation) => new()
    {
        ContentType = GcsRequestFields.Value(fields, "Content-Type"),
        ContentEncoding = GcsRequestFields.Value(fields, "Content-Encoding"),
        ContentDisposition = GcsRequestFields.Value(fields, "Content-Disposition"),
        CacheControl = GcsRequestFields.Value(fields, "Cache-Control"),
        ContentLanguage = GcsRequestFields.Value(fields, "Content-Language"),
        UserMetadata = UserMetadata(fields),
        ETag = eTag,
        Generation = generation,
    };

    // Each x-goog-meta-* field by its name, lower-cased and without the prefix; a repeated name keeps its last value.
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
}
