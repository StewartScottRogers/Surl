using System.Security.Cryptography;
using System.Text;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Answers <c>POST /&lt;bucket&gt;/&lt;object&gt;?uploadId=&lt;id&gt;</c> ("POST Object complete
/// multipart"; ADR-0110, decision 9): writes the object as the parts the body names, in its order,
/// through a fresh upload session committed over the name, keeps the metadata the initiation named
/// with the ETag <c>"&lt;hex MD5 of the parts' MD5s&gt;-&lt;part count&gt;"</c>, ends the upload,
/// and answers <c>200</c> with <c>CompleteMultipartUploadResult</c>.
/// </summary>
/// <remarks>
/// Uploads need <c>--allow-uploads</c> (<c>403 AccessDenied</c>). A body past 1 MiB, or one that does
/// not parse, is <c>400 InvalidArgument</c>; an <c>uploadId</c> naming no open multipart upload of the
/// bucket and object <c>404 NoSuchUpload</c>; parts not in ascending order <c>400 InvalidPartOrder</c>;
/// a part not uploaded, or with another ETag, <c>400 InvalidPart</c>; a part but the last below
/// <see cref="MinPartBytes"/> <c>400 EntityTooSmall</c>. A refused completion leaves the upload open,
/// so the client may try again.
/// </remarks>
/// <param name="staging">What the uploads share.</param>
/// <param name="login">Finishes a <c>GOOG4-HMAC-SHA256</c> check that binds the body.</param>
internal sealed class GcsCompleteMultipartUpload(GcsUploadStaging staging, GcsLoginJudge login)
{
    /// <summary>
    /// The fewest bytes every part but the last holds: 5 MiB ("POST Object complete multipart").
    /// </summary>
    public const long MinPartBytes = 5 * 1024 * 1024;

    /// <summary>
    /// Answers a completion whose bucket exists and whose name is not too long.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose body is read.</param>
    /// <param name="target">The bucket, object and query.</param>
    /// <param name="loggedIn">Who writes, and the rest of a signature that binds the body.</param>
    /// <param name="cancellationToken">Cancels reading the body and writing the object.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, GcsLogin loggedIn, CancellationToken cancellationToken)
    {
        if (!staging.AllowsUploads)
        {
            return reply.Error(GcsError.AccessDenied);
        }

        var (bodyRefusal, named) = await ReadBodyAsync(request, loggedIn.BodyCheck, cancellationToken);
        if (bodyRefusal is not null)
        {
            return reply.Error(bodyRefusal);
        }

        var uploadId = GcsQueryParameters.Value(target.Parameters, "uploadId")!;

        return await staging.Uploads.RunAsync(
            async () => staging.Uploads.Find(uploadId, GcsUploadKind.Multipart, target) is { } upload
                ? await CompleteAsync(reply, request, target, loggedIn.Caller!, upload, named!, cancellationToken)
                : reply.Error(GcsError.NoSuchUpload),
            cancellationToken);
    }

    // The body, at most 1 MiB, the signature that binds it, then its parts.
    private async Task<(GcsError? Refusal, List<GcsCompletedPart>? Named)> ReadBodyAsync(HttpRequest request, IHttpRequestBodyCheck? bodyCheck, CancellationToken cancellationToken)
    {
        using var sink = new GcsBoundedBodyStream(GcsCompleteMultipartUploadDocument.MaxBodyBytes);
        var outcome = request.Body.IsAnnounced ? await request.Body.CopyToAsync(sink, cancellationToken) : HttpRequestBodyDiscardOutcome.Discarded;
        var body = sink.ToArray();
        var refusal = GcsPutObject.BodyRefusal(outcome, isTooLarge: false)
            ?? await login.JudgeBodyAsync(bodyCheck, SHA256.HashData(body), cancellationToken);
        if (refusal is not null)
        {
            return (refusal, null);
        }

        var named = sink.IsTooLarge ? null : GcsCompleteMultipartUploadDocument.Read(body);

        return named is null ? (GcsError.InvalidArgument, null) : (null, named);
    }

    // The parts the body names checked against the upload, then the object written from them.
    private async Task<HttpAnswer> CompleteAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, ContentCaller caller, GcsStagedUpload upload, List<GcsCompletedPart> named, CancellationToken cancellationToken)
    {
        upload.Touch(reply.Now);
        var (partRefusal, parts) = Resolve(upload, named);
        if (partRefusal is not null)
        {
            return reply.Error(partRefusal);
        }

        var eTag = MultipartETagOf(parts);
        if (await WriteObjectAsync(upload, parts, target, caller, eTag, cancellationToken) is { } writeRefusal)
        {
            return reply.Error(writeRefusal);
        }

        await staging.Uploads.RemoveAsync(upload);
        var xml = GcsMultipartUpload.Start("CompleteMultipartUploadResult", target, null, staging.UriOf(request, target, string.Empty));
        GcsXml.Append(xml, "ETag", eTag);

        return reply.Xml(Encoding.UTF8.GetBytes(xml.Append("</CompleteMultipartUploadResult>").ToString()));
    }

    // The order first, then each part found with its ETag, then every part but the last big enough.
    private static (GcsError? Refusal, List<GcsUploadedPart> Parts) Resolve(GcsStagedUpload upload, List<GcsCompletedPart> named)
    {
        if (named.Zip(named.Skip(1)).Any(pair => pair.Second.PartNumber <= pair.First.PartNumber))
        {
            return (GcsError.InvalidPartOrder, []);
        }

        if (!named.All(part => upload.Part(part.PartNumber) is { } uploaded && uploaded.HasETag(part.ETag)))
        {
            return (GcsError.InvalidPart, []);
        }

        var parts = named.Select(part => upload.Part(part.PartNumber)!).ToList();

        return parts.SkipLast(1).Any(part => part.Size < MinPartBytes)
            ? (GcsError.EntityTooSmall, [])
            : (null, parts);
    }

    // "<hex MD5 of the parts' MD5s>-<part count>", quoted, as the page's example ETag ends "-9".
    private static string MultipartETagOf(List<GcsUploadedPart> parts) =>
        $"\"{Convert.ToHexStringLower(MD5.HashData([.. parts.SelectMany(part => part.Md5)]))}-{parts.Count}\"";

    // The parts' bytes, one after another, into a fresh session committed over the name. Every
    // part was staged within the upload limit, so the object is too.
    private async Task<GcsError?> WriteObjectAsync(GcsStagedUpload upload, List<GcsUploadedPart> parts, GcsRequestTarget target, ContentCaller caller, string eTag, CancellationToken cancellationToken)
    {
        var (openingRefusal, session) = await staging.OpenObjectSessionAsync(target, caller, cancellationToken);
        if (openingRefusal is not null)
        {
            return openingRefusal;
        }

        await using (session)
        {
            using var hashes = new GcsObjectHashes();
            var sink = new GcsHashingStream(hashes, session);
            foreach (var part in parts)
            {
                await GcsUploadStaging.CopyAsync(upload.Session, part.Offset, part.Size, sink, cancellationToken);
            }

            return (await staging.CommitAsync(session!, upload, target, caller, eTag, cancellationToken)).Refusal;
        }
    }
}
