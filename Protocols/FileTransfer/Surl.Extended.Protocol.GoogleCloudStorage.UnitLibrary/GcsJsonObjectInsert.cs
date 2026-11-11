using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Answers <c>storage.objects.insert</c> with <c>uploadType=media</c> and <c>uploadType=multipart</c> (ADR-0148, decision 7):
/// stores the bytes as PUT Object does, through an upload session committed only once every check passes, keeps the
/// metadata and the upload time as the generation in the object metadata index, and answers the <c>storage#object</c>.
/// </summary>
/// <remarks>
/// Needs <c>--allow-uploads</c> and a <c>Content-Length</c>. Media takes the name from <c>name</c> in the query and the type from
/// the request's <c>Content-Type</c>; multipart reads a <c>multipart/related</c> body of exactly two parts (<see cref="GcsMultipartRelatedSink"/>),
/// the metadata first, whose <c>name</c> wins over the query's. A hash sent (<c>md5Hash</c> and <c>crc32c</c> in the metadata,
/// <c>Content-MD5</c> and <c>x-goog-hash</c> in the head) that the bytes do not have is <c>400 invalid</c> and stores nothing.
/// </remarks>
/// <param name="store">The content store the object is written to.</param>
/// <param name="properties">Reads the object there now, and the one just stored.</param>
/// <param name="putObject">Stores the metadata, creates folders and measures the limits as PUT Object does.</param>
internal sealed class GcsJsonObjectInsert(ContentStore store, GcsObjectPropertiesReader properties, GcsPutObject putObject)
{
    private const string DefaultContentType = "application/octet-stream";

    /// <summary>
    /// Answers one insert.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose body is read.</param>
    /// <param name="target">Its target, whose query is read.</param>
    /// <param name="route">Its route, whose bucket is the destination.</param>
    /// <param name="caller">Who writes, on <c>gs</c>.</param>
    /// <param name="resources">Writes the answer.</param>
    /// <param name="cancellationToken">Cancels reading the body and writing the file.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, GcsJsonRoute route, ContentCaller caller, GcsJsonResources resources, CancellationToken cancellationToken)
    {
        var (refusal, parsed) = Parse(request, target);
        if (refusal is not null)
        {
            return reply.JsonError(refusal);
        }

        var insertion = new Insertion(reply, request, target, route, caller, resources, parsed!);

        return parsed!.Boundary is { } boundary
            ? await MultipartAsync(insertion, boundary, cancellationToken)
            : await MediaAsync(insertion, cancellationToken);
    }

    // What the head alone decides: uploads on, the type, the conditions, the length and the digests.
    private (GcsJsonError? Refusal, Parsed? Parsed) Parse(HttpRequest request, GcsRequestTarget target)
    {
        var parameters = target.Parameters;
        var uploadType = GcsQueryParameters.Value(parameters, "uploadType");
        if (!store.ExposureOptions.AllowUploads)
        {
            return (GcsJsonError.Forbidden, null);
        }

        if (uploadType is not ("media" or "multipart"))
        {
            return (GcsJsonError.InvalidParameter, null);
        }

        if (GcsJsonConditions.Read(parameters) is not { } conditions)
        {
            return (GcsJsonError.InvalidParameter, null);
        }

        if (LengthRefusal(request) is { } lengthRefusal)
        {
            return (lengthRefusal, null);
        }

        if (GcsSentDigests.Read(request.Fields, out _) is not { } digests)
        {
            return (GcsJsonError.Invalid, null);
        }

        var boundary = uploadType == "multipart" ? GcsMultipartRelatedSink.BoundaryOf(GcsRequestFields.Value(request.Fields, "Content-Type")) : null;

        return uploadType == "multipart" && boundary is null
            ? (GcsJsonError.BadRequest, null)
            : (null, new Parsed(boundary, conditions, digests));
    }

    // A body needs its length announced, and within --max-filesize.
    private GcsJsonError? LengthRefusal(HttpRequest request)
    {
        if (GcsRequestFields.Value(request.Fields, "Content-Length") is null)
        {
            return GcsJsonError.LengthRequired;
        }

        return putObject.IsPastTheUploadLimit(request.Body.AnnouncedLength) ? GcsJsonError.UploadTooLarge : null;
    }

    // Media: the name is in the query, the bytes are the body.
    private async Task<HttpAnswer> MediaAsync(Insertion insertion, CancellationToken cancellationToken)
    {
        var name = GcsQueryParameters.Value(insertion.Target.Parameters, "name");
        if (string.IsNullOrEmpty(name))
        {
            return insertion.Reply.JsonError(GcsJsonError.Required);
        }

        var (refusal, upload) = await BeginAsync(insertion, name, cancellationToken);
        if (refusal is not null)
        {
            return insertion.Reply.JsonError(refusal);
        }

        await using (upload)
        {
            var outcome = insertion.Request.Body.IsAnnounced ? await insertion.Request.Body.CopyToAsync(upload!.Sink, cancellationToken) : HttpRequestBodyDiscardOutcome.Discarded;
            if (BodyRefusal(outcome, upload!.Sink.IsTooLarge) is { } bodyRefusal)
            {
                return insertion.Reply.JsonError(bodyRefusal);
            }

            var metadata = new ObjectMetadata
            {
                ContentType = GcsRequestFields.Value(insertion.Request.Fields, "Content-Type") ?? DefaultContentType,
                ContentEncoding = GcsQueryParameters.Value(insertion.Target.Parameters, "contentEncoding"),
            };

            return await FinishAsync(insertion, upload, metadata, [insertion.Parsed.HeaderDigests], cancellationToken);
        }
    }

    // Multipart: the metadata part names the object, so the upload opens once that part is read.
    private async Task<HttpAnswer> MultipartAsync(Insertion insertion, string boundary, CancellationToken cancellationToken)
    {
        await using var state = new MultipartState();

        return await ReadMultipartAsync(insertion, boundary, state, cancellationToken);
    }

    // Copies the body through the reader, which opens the upload once the metadata part is in; then the refusals, then the finish.
    private async Task<HttpAnswer> ReadMultipartAsync(Insertion insertion, string boundary, MultipartState state, CancellationToken cancellationToken)
    {
        using var sink = new GcsMultipartRelatedSink(boundary, (metadataPart, token) => OpenMultipartContentAsync(insertion, state, metadataPart, token));
        var outcome = insertion.Request.Body.IsAnnounced ? await insertion.Request.Body.CopyToAsync(sink, cancellationToken) : HttpRequestBodyDiscardOutcome.Discarded;
        if ((state.Opening ?? MultipartRefusal(sink, outcome, state.Upload?.Sink.IsTooLarge ?? false)) is { } refusal)
        {
            return insertion.Reply.JsonError(refusal);
        }

        return await FinishMultipartAsync(insertion, state, sink.ContentPartType, cancellationToken);
    }

    // The metadata part is read: it names the object, and the upload opens, or is refused.
    private async Task<Stream?> OpenMultipartContentAsync(Insertion insertion, MultipartState state, byte[] metadataPart, CancellationToken cancellationToken)
    {
        state.Fields = GcsJsonObjectFields.Read(metadataPart);
        var name = state.Fields?.Name ?? GcsQueryParameters.Value(insertion.Target.Parameters, "name");
        if (state.Fields is null || string.IsNullOrEmpty(name))
        {
            state.Opening = state.Fields is null ? GcsJsonError.Invalid : GcsJsonError.Required;

            return null;
        }

        (state.Opening, state.Upload) = await BeginAsync(insertion, name, cancellationToken);

        return state.Upload?.Sink;
    }

    // The metadata's contentType wins over the second part's own; its digests are checked with the head's.
    private async Task<HttpAnswer> FinishMultipartAsync(Insertion insertion, MultipartState state, string? contentPartType, CancellationToken cancellationToken)
    {
        var metadata = state.Fields!.Compose(null);
        metadata = metadata with
        {
            ContentType = metadata.ContentType ?? contentPartType ?? DefaultContentType,
            ContentEncoding = metadata.ContentEncoding ?? GcsQueryParameters.Value(insertion.Target.Parameters, "contentEncoding"),
        };
        var sentInMetadata = state.Fields.SentDigests();

        return sentInMetadata is null
            ? insertion.Reply.JsonError(GcsJsonError.Invalid)
            : await FinishAsync(insertion, state.Upload!, metadata, [insertion.Parsed.HeaderDigests, sentInMetadata], cancellationToken);
    }

    /// <summary>
    /// What the reading of a multipart body made of the upload: its metadata past the limit, its size, or its end.
    /// </summary>
    /// <param name="sink">The reader of the body.</param>
    /// <param name="outcome">How copying the body ended.</param>
    /// <param name="isUploadTooLarge">Whether the upload session refused the bytes as too many.</param>
    /// <returns><see langword="null"/> when the body was whole, else the row.</returns>
    internal static GcsJsonError? MultipartRefusal(GcsMultipartRelatedSink sink, HttpRequestBodyDiscardOutcome outcome, bool isUploadTooLarge)
    {
        if (sink.Problem == GcsMultipartRelatedProblem.MetadataTooLarge)
        {
            return GcsJsonError.Invalid;
        }

        if (BodyRefusal(outcome, isUploadTooLarge) is { } bodyRefusal)
        {
            return bodyRefusal;
        }

        return sink.IsComplete ? null : GcsJsonError.BadRequest;
    }

    // The object's name, the store's room for it, the conditions, its folders and the staging session.
    private async Task<(GcsJsonError? Refusal, Upload? Upload)> BeginAsync(Insertion insertion, string name, CancellationToken cancellationToken)
    {
        var rawName = Uri.EscapeDataString(name).Replace("%2F", "/", StringComparison.Ordinal);
        var rawBucket = insertion.Route.RawBucket;
        if (name.EndsWith('/') || GcsObjectName.IsTooLong(rawName))
        {
            return (GcsJsonError.Invalid, null);
        }

        var mapping = store.MapBucketRequestPathForCaller($"/{rawBucket}/{rawName}", insertion.Caller);
        if (!mapping.IsMapped)
        {
            return (GcsJsonError.Invalid, null);
        }

        var current = await properties.ReadAsync(mapping, GcsIndexKey.OfRaw(store, rawBucket, rawName), cancellationToken);
        if (!insertion.Parsed.Conditions.AreMetByWrite(insertion.Request.Fields, current))
        {
            return (GcsJsonError.ConditionNotMet, null);
        }

        if (putObject.CreateFolders(rawBucket, rawName, insertion.Caller) is { } folderRefusal)
        {
            return (folderRefusal == GcsError.ConflictingObjectName ? GcsJsonError.Conflict : GcsJsonError.Forbidden, null);
        }

        var opening = await store.OpenUploadAsync(mapping, new ContentUploadOpening(StartsFromExistingBytes: false, CreatesMissingFile: true, RefusesExistingFile: false), cancellationToken);
        if (opening.Session is not { } session)
        {
            return (opening.Result == ContentUploadOpeningResult.IsADirectory ? GcsJsonError.Conflict : GcsJsonError.Forbidden, null);
        }

        return (null, new Upload(session, rawBucket, rawName, mapping));
    }

    // Checks the digests and the metadata, commits the bytes, keeps the metadata and answers the object just stored.
    private async Task<HttpAnswer> FinishAsync(Insertion insertion, Upload upload, ObjectMetadata metadata, GcsSentDigests[] sent, CancellationToken cancellationToken)
    {
        var digests = upload.Hashes.Finish();
        if (!sent.All(digest => digest.Match(digests)) || GcsPutObject.UserMetadataBytes(metadata.UserMetadata) > GcsPutObject.MaxUserMetadataBytes)
        {
            return insertion.Reply.JsonError(GcsJsonError.Invalid);
        }

        if (await upload.Session.CommitAsync(cancellationToken) != ContentUploadResult.Written)
        {
            return insertion.Reply.JsonError(GcsJsonError.Forbidden);
        }

        var (refusal, _) = await putObject.StoreMetadataAsync(upload.RawBucket, upload.RawName, upload.Mapping, metadata, digests.ETag, insertion.Reply.Now, cancellationToken);
        if (refusal is not null)
        {
            return insertion.Reply.JsonError(refusal == GcsError.MetadataTooLarge ? GcsJsonError.Invalid : GcsJsonError.QuotaExceeded);
        }

        var stored = await properties.ReadAsync(upload.Mapping, GcsIndexKey.OfRaw(store, upload.RawBucket, upload.RawName), cancellationToken);

        return insertion.Reply.Json(insertion.Resources.Object(Uri.UnescapeDataString(upload.RawBucket), Uri.UnescapeDataString(upload.RawName), stored!, digests));
    }

    /// <summary>
    /// What reading a body is answered with when it fails: past the limit, <c>413</c>; cut short, <c>400 badRequest</c>.
    /// </summary>
    /// <param name="outcome">How reading the body ended.</param>
    /// <param name="isTooLarge">Whether the upload session refused the bytes as too many.</param>
    /// <returns><see langword="null"/> when the whole body is in, else the row.</returns>
    internal static GcsJsonError? BodyRefusal(HttpRequestBodyDiscardOutcome outcome, bool isTooLarge)
    {
        if (outcome == HttpRequestBodyDiscardOutcome.TooLarge || isTooLarge)
        {
            return GcsJsonError.UploadTooLarge;
        }

        return outcome == HttpRequestBodyDiscardOutcome.Incomplete ? GcsJsonError.BadRequest : null;
    }

    private sealed class MultipartState : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            if (Upload is not null)
            {
                await Upload.DisposeAsync();
            }
        }

        public GcsJsonObjectFields? Fields { get; set; }

        public GcsJsonError? Opening { get; set; }

        public Upload? Upload { get; set; }
    }

    private sealed record Parsed(string? Boundary, GcsJsonConditions Conditions, GcsSentDigests HeaderDigests);

    private sealed record Insertion(GcsReply Reply, HttpRequest Request, GcsRequestTarget Target, GcsJsonRoute Route, ContentCaller Caller, GcsJsonResources Resources, Parsed Parsed);

    // The staging session an object's bytes go into, hashed on their way; disposed uncommitted unless committed.
    private sealed class Upload : IAsyncDisposable
    {
        public Upload(ContentUploadSession session, string rawBucket, string rawName, ContentPathMapping mapping)
        {
            Session = session;
            RawBucket = rawBucket;
            RawName = rawName;
            Mapping = mapping;
            Hashes = new GcsObjectHashes();
            Sink = new GcsHashingStream(Hashes, session);
        }

        public ContentUploadSession Session { get; }

        public GcsObjectHashes Hashes { get; }

        public GcsHashingStream Sink { get; }

        public string RawBucket { get; }

        public string RawName { get; }

        public ContentPathMapping Mapping { get; }

        public async ValueTask DisposeAsync()
        {
            await Sink.DisposeAsync();
            await Session.DisposeAsync();
            Hashes.Dispose();
        }
    }
}
