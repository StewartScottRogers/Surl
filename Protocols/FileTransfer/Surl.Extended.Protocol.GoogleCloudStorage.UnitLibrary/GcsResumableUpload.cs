using System.Globalization;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Answers resumable uploads ("Resumable uploads", "Perform resumable uploads"; ADR-0110, decision
/// 9): a <c>POST</c> with <c>x-goog-resumable: start</c> opens one and answers <c>201</c> with the
/// session URI in <c>Location</c> (the object's URI with <c>?upload_id=&lt;id&gt;</c>); each <c>PUT</c>
/// to the session URI appends a chunk named by <c>Content-Range</c>, answered <c>308 Resume
/// Incomplete</c> with <c>Range: bytes=0-&lt;last persisted byte&gt;</c> until the total is in, then
/// <c>200</c> with the object's ETag, generation and <c>x-goog-hash</c>; a <c>DELETE</c> cancels it
/// with <c>204</c>.
/// </summary>
/// <remarks>
/// The bytes are staged in the upload's session and stay invisible until the last one is in, when
/// that session is committed over the name. A chunk that does not start at the next byte expected
/// is not read, and the answer says where to resume. An <c>upload_id</c> naming no open resumable
/// upload of the bucket and object is <c>404 NoSuchUpload</c>.
/// </remarks>
/// <param name="staging">What the uploads share.</param>
internal sealed class GcsResumableUpload(GcsUploadStaging staging)
{
    private static readonly HttpStatus Created = new(201, "Created");
    private static readonly HttpStatus NoContent = new(204, "No Content");
    private static readonly HttpStatus ResumeIncomplete = new(308, "Resume Incomplete");

    /// <summary>
    /// Answers <c>POST /&lt;bucket&gt;/&lt;object&gt;</c> with <c>x-goog-resumable: start</c>.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose fields become the object's metadata.</param>
    /// <param name="target">The bucket and object.</param>
    /// <param name="caller">Who uploads.</param>
    /// <param name="cancellationToken">Cancels waiting for the uploads.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> StartAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        var (refusal, upload) = await staging.OpenAsync(request, target, GcsUploadKind.Resumable, caller, cancellationToken);
        if (refusal is not null)
        {
            return reply.Error(refusal);
        }

        return reply.Answer(Created, [new("Location", staging.UriOf(request, target, $"?upload_id={upload!.UploadId}")), new("Content-Length", "0")], HttpAnswerBody.None);
    }

    /// <summary>
    /// Answers <c>PUT /&lt;bucket&gt;/&lt;object&gt;?upload_id=&lt;id&gt;</c>: a chunk, or a status
    /// check with no bytes.
    /// </summary>
    /// <remarks>
    /// Uploads need <c>--allow-uploads</c> (<c>403 AccessDenied</c>) and <c>Content-Length</c> (<c>411
    /// MissingContentLength</c>). A <c>Content-Range</c> that does not parse, whose chunk is not the
    /// announced length, or whose total is below the bytes already in, is <c>400 InvalidArgument</c>;
    /// without one the body is the whole object. A total past <c>--max-filesize</c>, or bytes staged
    /// past it, end the upload with <c>400 EntityTooLarge</c>.
    /// </remarks>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose body is read.</param>
    /// <param name="target">The bucket, object and query.</param>
    /// <param name="loggedIn">Who uploads, and the rest of a signature that binds the body.</param>
    /// <param name="cancellationToken">Cancels reading the body and writing the object.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> PutAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, GcsLogin loggedIn, CancellationToken cancellationToken)
    {
        if (RefusalBeforeTheChunk(request, out var range) is { } refusal)
        {
            return reply.Error(refusal);
        }

        var uploadId = GcsQueryParameters.Value(target.Parameters, "upload_id")!;

        return await staging.Uploads.RunAsync(
            async () => staging.Uploads.Find(uploadId, GcsUploadKind.Resumable, target) is { } upload
                ? await ResumeAsync(reply, request, target, loggedIn, upload, range!, cancellationToken)
                : reply.Error(GcsError.NoSuchUpload),
            cancellationToken);
    }

    /// <summary>
    /// Answers <c>DELETE /&lt;bucket&gt;/&lt;object&gt;?upload_id=&lt;id&gt;</c>: ends the upload,
    /// discarding its bytes, and answers <c>204</c>; uploads off is <c>403 AccessDenied</c>.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="target">The bucket, object and query.</param>
    /// <param name="cancellationToken">Cancels waiting for the uploads.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> CancelAsync(GcsReply reply, GcsRequestTarget target, CancellationToken cancellationToken)
    {
        if (!staging.AllowsUploads)
        {
            return reply.Error(GcsError.AccessDenied);
        }

        var uploadId = GcsQueryParameters.Value(target.Parameters, "upload_id")!;

        return await staging.Uploads.RunAsync(
            async () =>
            {
                if (staging.Uploads.Find(uploadId, GcsUploadKind.Resumable, target) is not { } upload)
                {
                    return reply.Error(GcsError.NoSuchUpload);
                }

                await staging.Uploads.RemoveAsync(upload);

                return reply.Answer(NoContent, [], HttpAnswerBody.None);
            },
            cancellationToken);
    }

    private GcsError? RefusalBeforeTheChunk(HttpRequest request, out GcsContentRange? range)
    {
        range = null;
        if (!staging.AllowsUploads)
        {
            return GcsError.AccessDenied;
        }

        if (GcsRequestFields.Value(request.Fields, "Content-Length") is null)
        {
            return GcsError.MissingContentLength;
        }

        var length = request.Body.AnnouncedLength ?? 0;
        range = GcsContentRange.Of(GcsRequestFields.Value(request.Fields, "Content-Range"), length);

        return range is null || range.Count != length ? GcsError.InvalidArgument : null;
    }

    // The chunk appended when it starts at the next byte expected, then the upload finished when
    // every byte is in.
    private async Task<HttpAnswer> ResumeAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, GcsLogin loggedIn, GcsStagedUpload upload, GcsContentRange range, CancellationToken cancellationToken)
    {
        upload.Touch(reply.Now);
        if (staging.IsPastTheUploadLimit(range.Total))
        {
            await staging.Uploads.RemoveAsync(upload);
            return reply.Error(GcsError.EntityTooLarge);
        }

        var session = upload.Session;
        if (range.First == session.Length
            && (await staging.AppendBodyAsync(request, upload, loggedIn.BodyCheck, null, cancellationToken)).Refusal is { } chunkRefusal)
        {
            return reply.Error(chunkRefusal);
        }

        if (range.Total == session.Length)
        {
            return await FinishAsync(reply, target, loggedIn.Caller!, upload, cancellationToken);
        }

        return range.Total < session.Length ? reply.Error(GcsError.InvalidArgument) : Incomplete(reply, session.Length);
    }

    // The staged bytes hashed, then the session committed over the name.
    private async Task<HttpAnswer> FinishAsync(GcsReply reply, GcsRequestTarget target, ContentCaller caller, GcsStagedUpload upload, CancellationToken cancellationToken)
    {
        using var hashes = new GcsObjectHashes();
        await GcsUploadStaging.CopyAsync(upload.Session, 0, upload.Session.Length, new GcsHashingStream(hashes, null), cancellationToken);
        var digests = hashes.Finish();
        var (refusal, generation) = await staging.CommitAsync(upload.Session, upload, target, caller, digests.ETag, digests.Crc32cValue, cancellationToken);
        await staging.Uploads.RemoveAsync(upload);

        return refusal is null ? GcsPutObject.Stored(reply, digests, generation) : reply.Error(refusal);
    }

    // 308 with the bytes persisted so far; none persisted, no Range ("Resumable uploads").
    private static HttpAnswer Incomplete(GcsReply reply, long persisted) =>
        reply.Answer(
            ResumeIncomplete,
            persisted == 0
                ? [new("Content-Length", "0")]
                : [new("Range", $"bytes=0-{(persisted - 1).ToString(CultureInfo.InvariantCulture)}"), new("Content-Length", "0")],
            HttpAnswerBody.None);
}
