using System.Globalization;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Answers <c>storage.objects.insert</c> with <c>uploadType=resumable</c> (ADR-0148, decision 7; "Perform resumable uploads", JSON
/// API tab): a <c>POST</c> opens the upload and answers <c>200</c> with the session URI in <c>Location</c>; each <c>PUT</c> to the
/// session URI appends a chunk named by <c>Content-Range</c>, answered <c>308</c> with <c>Range</c> until the total is in, then
/// <c>200</c> with the <c>storage#object</c>; a <c>PUT</c> of <c>bytes */&lt;total&gt;</c> is a status check; a <c>DELETE</c> cancels
/// it with <c>499</c>.
/// </summary>
/// <remarks>
/// The bytes are staged in the upload's session, shared with the XML API's uploads (the same 1000 open and 24-hour idle bounds), and
/// stay invisible until the last one is in. A session belongs to the JSON grammar (<see cref="GcsUploadKind.JsonResumable"/>), and
/// the session URI names the bucket only, so an ID of another bucket, of an XML upload, of a cancelled or an expired one is
/// <c>404 notFound</c>. The conditions and the metadata (the body's, with <c>X-Upload-Content-Type</c> as the type it lacks) are
/// judged when the upload is opened.
/// </remarks>
/// <param name="store">The content store the object is written to.</param>
/// <param name="properties">Reads the object there now, and the one just stored.</param>
/// <param name="staging">What the uploads share.</param>
internal sealed class GcsJsonResumableUpload(ContentStore store, GcsObjectPropertiesReader properties, GcsUploadStaging staging)
{
    private const string DefaultContentType = "application/octet-stream";

    private static readonly HttpStatus ResumeIncomplete = new(308, "Resume Incomplete");
    private static readonly HttpStatus ClientClosedRequest = new(499, "Client Closed Request");

    /// <summary>
    /// Answers the opening <c>POST</c>.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose body is the optional JSON metadata.</param>
    /// <param name="target">Its target, whose query is read.</param>
    /// <param name="route">Its route, whose bucket is the destination.</param>
    /// <param name="caller">Who uploads, on <c>gs</c>.</param>
    /// <param name="cancellationToken">Cancels reading the body and waiting for the uploads.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> OpenAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, GcsJsonRoute route, ContentCaller caller, CancellationToken cancellationToken)
    {
        var (refusal, opening) = await ReadOpeningAsync(request, target, cancellationToken);
        if (refusal is not null)
        {
            return reply.JsonError(refusal);
        }

        var objectTarget = target with { RawBucket = route.RawBucket, RawName = opening!.RawName };
        var current = await properties.ReadAsync(store.MapBucketRequestPathForCaller(objectTarget.ObjectPath, caller), GcsIndexKey.OfRaw(store, objectTarget.RawBucket, objectTarget.RawName), cancellationToken);
        if (!opening.Conditions.AreMetByWrite(request.Fields, current))
        {
            return reply.JsonError(GcsJsonError.ConditionNotMet);
        }

        var (error, upload) = await staging.OpenAsync(request, objectTarget, GcsUploadKind.JsonResumable, caller, cancellationToken);
        if (error is not null)
        {
            return reply.JsonError(OpeningRefusal(error));
        }

        upload!.Metadata = opening.Metadata;

        return reply.Answer(HttpStatus.Ok, [new("Location", staging.UriOf(request, target, $"?uploadType=resumable&upload_id={upload.UploadId}")), new("Content-Length", "0")], HttpAnswerBody.None);
    }

    /// <summary>
    /// Answers a <c>PUT</c> to the session URI: a chunk, or a status check with no bytes.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose body is read.</param>
    /// <param name="target">Its target, whose <c>upload_id</c> is read.</param>
    /// <param name="route">Its route, whose bucket the session must belong to.</param>
    /// <param name="caller">Who uploads, on <c>gs</c>.</param>
    /// <param name="resources">Writes the answer.</param>
    /// <param name="cancellationToken">Cancels reading the body and writing the object.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> ResumeAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, GcsJsonRoute route, ContentCaller caller, GcsJsonResources resources, CancellationToken cancellationToken)
    {
        if (RefusalBeforeTheChunk(request, out var range) is { } refusal)
        {
            return reply.JsonError(refusal);
        }

        var uploadId = GcsQueryParameters.Value(target.Parameters, "upload_id") ?? string.Empty;

        return await staging.Uploads.RunAsync(
            async () => staging.Uploads.FindJson(uploadId, route.RawBucket) is { } upload
                ? await AppendAsync(reply, request, caller, resources, upload, range!, cancellationToken)
                : reply.JsonError(GcsJsonError.NotFound),
            cancellationToken);
    }

    /// <summary>
    /// Answers a <c>DELETE</c> of the session URI: ends the upload, discarding its bytes, with <c>499</c>.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="target">Its target, whose <c>upload_id</c> is read.</param>
    /// <param name="route">Its route, whose bucket the session must belong to.</param>
    /// <param name="cancellationToken">Cancels waiting for the uploads.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> CancelAsync(GcsReply reply, GcsRequestTarget target, GcsJsonRoute route, CancellationToken cancellationToken)
    {
        if (!staging.AllowsUploads)
        {
            return reply.JsonError(GcsJsonError.Forbidden);
        }

        var uploadId = GcsQueryParameters.Value(target.Parameters, "upload_id") ?? string.Empty;

        return await staging.Uploads.RunAsync(
            async () =>
            {
                if (staging.Uploads.FindJson(uploadId, route.RawBucket) is not { } upload)
                {
                    return reply.JsonError(GcsJsonError.NotFound);
                }

                await staging.Uploads.RemoveAsync(upload);

                return reply.Answer(ClientClosedRequest, [new("Content-Length", "0")], HttpAnswerBody.None);
            },
            cancellationToken);
    }

    // What the opening request decides: uploads on, the conditions, the length, the declared size, the metadata and the name.
    private async Task<(GcsJsonError? Refusal, Opening? Opening)> ReadOpeningAsync(HttpRequest request, GcsRequestTarget target, CancellationToken cancellationToken)
    {
        if (!staging.AllowsUploads)
        {
            return (GcsJsonError.Forbidden, null);
        }

        if (GcsJsonConditions.Read(target.Parameters) is not { } conditions)
        {
            return (GcsJsonError.InvalidParameter, null);
        }

        return HeadRefusal(request) is { } headRefusal
            ? (headRefusal, null)
            : await ReadMetadataAsync(request, target, conditions, cancellationToken);
    }

    // The length announced, and the size declared within --max-filesize.
    private GcsJsonError? HeadRefusal(HttpRequest request)
    {
        if (GcsRequestFields.Value(request.Fields, "Content-Length") is null)
        {
            return GcsJsonError.LengthRequired;
        }

        if (!long.TryParse(GcsRequestFields.Value(request.Fields, "X-Upload-Content-Length") ?? "0", NumberStyles.None, CultureInfo.InvariantCulture, out var declared))
        {
            return GcsJsonError.Invalid;
        }

        return staging.IsPastTheUploadLimit(declared) ? GcsJsonError.UploadTooLarge : null;
    }

    // The metadata body, which may be empty, and the name.
    private async Task<(GcsJsonError? Refusal, Opening? Opening)> ReadMetadataAsync(HttpRequest request, GcsRequestTarget target, GcsJsonConditions conditions, CancellationToken cancellationToken)
    {
        using var body = new GcsBoundedBodyStream(GcsJsonObjectUpdate.MaxBodyBytes);
        var outcome = request.Body.IsAnnounced ? await request.Body.CopyToAsync(body, cancellationToken) : HttpRequestBodyDiscardOutcome.Discarded;
        var fields = GcsJsonObjectFields.Read(body.Length == 0 ? "{}"u8.ToArray() : body.ToArray());
        if (body.IsTooLarge || outcome != HttpRequestBodyDiscardOutcome.Discarded || fields is null)
        {
            return (GcsJsonError.Invalid, null);
        }

        return NameAndMetadata(request, target, conditions, fields);
    }

    // The name from the metadata or the query, which the store can hold; the metadata with the type X-Upload-Content-Type gives.
    private static (GcsJsonError? Refusal, Opening? Opening) NameAndMetadata(HttpRequest request, GcsRequestTarget target, GcsJsonConditions conditions, GcsJsonObjectFields fields)
    {
        var name = fields.Name ?? GcsQueryParameters.Value(target.Parameters, "name");
        if (string.IsNullOrEmpty(name))
        {
            return (GcsJsonError.Required, null);
        }

        var rawName = Uri.EscapeDataString(name).Replace("%2F", "/", StringComparison.Ordinal);
        var metadata = fields.Compose(null);
        metadata = metadata with
        {
            ContentType = metadata.ContentType ?? GcsRequestFields.Value(request.Fields, "X-Upload-Content-Type") ?? DefaultContentType,
            ContentEncoding = metadata.ContentEncoding ?? GcsQueryParameters.Value(target.Parameters, "contentEncoding"),
        };

        return name.EndsWith('/') || GcsObjectName.IsTooLong(rawName) || GcsPutObject.UserMetadataBytes(metadata.UserMetadata) > GcsPutObject.MaxUserMetadataBytes
            ? (GcsJsonError.Invalid, null)
            : (null, new Opening(rawName, conditions, metadata));
    }

    // The row of what opening an upload refuses: no room, a name in conflict, x-goog-meta-* fields past 8 KiB, or the store's refusal.
    private static GcsJsonError OpeningRefusal(GcsError error) =>
        error == GcsError.SlowDown ? GcsJsonError.BackendError
        : error == GcsError.ConflictingObjectName ? GcsJsonError.Conflict
        : error == GcsError.MetadataTooLarge ? GcsJsonError.Invalid
        : GcsJsonError.Forbidden;

    // Uploads on, the chunk's length announced, and a Content-Range that parses and matches it.
    private GcsJsonError? RefusalBeforeTheChunk(HttpRequest request, out GcsContentRange? range)
    {
        range = null;
        if (!staging.AllowsUploads)
        {
            return GcsJsonError.Forbidden;
        }

        if (GcsRequestFields.Value(request.Fields, "Content-Length") is null)
        {
            return GcsJsonError.LengthRequired;
        }

        var length = request.Body.AnnouncedLength ?? 0;
        range = GcsContentRange.Of(GcsRequestFields.Value(request.Fields, "Content-Range"), length);

        return range is null || range.Count != length ? GcsJsonError.BadRequest : null;
    }

    // The chunk appended when it starts at the next byte expected, then the upload finished when every byte is in.
    private async Task<HttpAnswer> AppendAsync(GcsReply reply, HttpRequest request, ContentCaller caller, GcsJsonResources resources, GcsStagedUpload upload, GcsContentRange range, CancellationToken cancellationToken)
    {
        upload.Touch(reply.Now);
        if (staging.IsPastTheUploadLimit(range.Total))
        {
            await staging.Uploads.RemoveAsync(upload);
            return reply.JsonError(GcsJsonError.UploadTooLarge);
        }

        var session = upload.Session;
        if (range.First == session.Length
            && (await staging.AppendBodyAsync(request, upload, null, null, cancellationToken)).Refusal is { } chunkRefusal)
        {
            return reply.JsonError(chunkRefusal == GcsError.EntityTooLarge ? GcsJsonError.UploadTooLarge : GcsJsonError.BadRequest);
        }

        if (range.Total == session.Length)
        {
            return await FinishAsync(reply, caller, resources, upload, cancellationToken);
        }

        return range.Total < session.Length ? reply.JsonError(GcsJsonError.BadRequest) : Incomplete(reply, session.Length);
    }

    // The staged bytes hashed, the session committed over the name, and the object just stored answered.
    private async Task<HttpAnswer> FinishAsync(GcsReply reply, ContentCaller caller, GcsJsonResources resources, GcsStagedUpload upload, CancellationToken cancellationToken)
    {
        using var hashes = new GcsObjectHashes();
        await GcsUploadStaging.CopyAsync(upload.Session, 0, upload.Session.Length, new GcsHashingStream(hashes, null), cancellationToken);
        var digests = hashes.Finish();
        var (refusal, _) = await staging.CommitAsync(upload.Session, upload, upload.Target, caller, digests.ETag, digests.Crc32cValue, cancellationToken);
        await staging.Uploads.RemoveAsync(upload);
        if (refusal is not null)
        {
            return reply.JsonError(refusal == GcsError.AccessDenied ? GcsJsonError.Forbidden : GcsJsonError.QuotaExceeded);
        }

        var mapping = store.MapBucketRequestPathForCaller(upload.Target.ObjectPath, caller);
        var stored = await properties.ReadAsync(mapping, GcsIndexKey.OfRaw(store, upload.Target.RawBucket, upload.Target.RawName), cancellationToken);

        return reply.Json(resources.Object(Uri.UnescapeDataString(upload.Target.RawBucket), Uri.UnescapeDataString(upload.Target.RawName), stored!, digests));
    }

    // 308 with the bytes persisted so far; none persisted, no Range.
    private static HttpAnswer Incomplete(GcsReply reply, long persisted) =>
        reply.Answer(
            ResumeIncomplete,
            persisted == 0
                ? [new("Content-Length", "0")]
                : [new("Range", $"bytes=0-{(persisted - 1).ToString(CultureInfo.InvariantCulture)}"), new("Content-Length", "0")],
            HttpAnswerBody.None);

    private sealed record Opening(string RawName, GcsJsonConditions Conditions, ObjectMetadata Metadata);
}
