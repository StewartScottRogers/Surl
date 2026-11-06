using System.Globalization;
using System.Text;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Answers the XML API multipart requests but the completion ("XML API multipart uploads": "POST
/// Object multipart", "PUT Object multipart", "GET Object multipart" and "DELETE Object multipart";
/// ADR-0110, decision 9): initiating an upload, uploading a part, listing the parts and aborting.
/// </summary>
/// <remarks>
/// An <c>uploadId</c> naming no open multipart upload of the bucket and object is
/// <c>404 NoSuchUpload</c>. Parts are staged in the upload's session and stay invisible - to a
/// read and to a listing - until <see cref="GcsCompleteMultipartUpload"/> writes the object.
/// </remarks>
/// <param name="staging">What the uploads share.</param>
internal sealed class GcsMultipartUpload(GcsUploadStaging staging)
{
    /// <summary>
    /// The highest part number ("PUT Object multipart").
    /// </summary>
    public const int MaxPartNumber = 10000;

    /// <summary>
    /// The most parts one listing returns ("GET Object multipart").
    /// </summary>
    public const int MaxListedParts = 1000;

    /// <summary>
    /// The namespace the multipart results are written in, as the pages' examples write it.
    /// </summary>
    public const string Namespace = "http://s3.amazonaws.com/doc/2006-03-01/";

    private static readonly HttpStatus NoContent = new(204, "No Content");

    /// <summary>
    /// Answers <c>POST /&lt;bucket&gt;/&lt;object&gt;?uploads</c>: opens the upload and answers
    /// <c>200</c> with <c>InitiateMultipartUploadResult</c>.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose fields become the object's metadata.</param>
    /// <param name="target">The bucket and object.</param>
    /// <param name="caller">Who uploads.</param>
    /// <param name="cancellationToken">Cancels waiting for the uploads.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> InitiateAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        var (refusal, upload) = await staging.OpenAsync(request, target, GcsUploadKind.Multipart, caller, cancellationToken);
        if (refusal is not null)
        {
            return reply.Error(refusal);
        }

        var xml = Start("InitiateMultipartUploadResult", target, upload!.UploadId);

        return reply.Xml(Encoding.UTF8.GetBytes(xml.Append("</InitiateMultipartUploadResult>").ToString()));
    }

    /// <summary>
    /// Answers <c>PUT /&lt;bucket&gt;/&lt;object&gt;?partNumber=&lt;n&gt;&amp;uploadId=&lt;id&gt;</c>:
    /// stages the body as the part, replacing one uploaded before with the number, and answers
    /// <c>200</c> with the part's MD5 as its <c>ETag</c> and its <c>x-goog-hash</c>.
    /// </summary>
    /// <remarks>
    /// Uploads need <c>--allow-uploads</c> (<c>403 AccessDenied</c>); a <c>partNumber</c> that is not 1
    /// to 10000 is <c>400 InvalidArgument</c>; PUT Object's head checks apply (<c>411
    /// MissingContentLength</c>, <c>400 InvalidDigest</c>, <c>400 InvalidHash</c>, the announced
    /// length past <c>--max-filesize</c> <c>400 EntityTooLarge</c>), and its body checks to the part.
    /// The staged parts of one upload together are held to <c>--max-filesize</c>: past it the upload
    /// is discarded and the answer is <c>400 EntityTooLarge</c>.
    /// </remarks>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose body is read.</param>
    /// <param name="target">The bucket, object and query.</param>
    /// <param name="bodyCheck">The rest of a signature that binds the body, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels reading the body.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> UploadPartAsync(GcsReply reply, HttpRequest request, GcsRequestTarget target, IHttpRequestBodyCheck? bodyCheck, CancellationToken cancellationToken)
    {
        if (RefusalBeforeThePart(request, target, out var partNumber, out var sent) is { } refusal)
        {
            return reply.Error(refusal);
        }

        var uploadId = GcsQueryParameters.Value(target.Parameters, "uploadId")!;

        return await staging.Uploads.RunAsync(
            async () => staging.Uploads.Find(uploadId, GcsUploadKind.Multipart, target) is { } upload
                ? await StagePartAsync(reply, request, upload, partNumber, bodyCheck, sent!, cancellationToken)
                : reply.Error(GcsError.NoSuchUpload),
            cancellationToken);
    }

    /// <summary>
    /// Answers <c>GET /&lt;bucket&gt;/&lt;object&gt;?uploadId=&lt;id&gt;</c>: <c>200</c> with
    /// <c>ListPartsResult</c>, the parts after <c>part-number-marker</c> in number order, at most
    /// <c>max-parts</c> (default and cap 1000); a value that is not a decimal number is
    /// <c>400 InvalidArgument</c>.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="target">The bucket, object and query.</param>
    /// <param name="cancellationToken">Cancels waiting for the uploads.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> ListPartsAsync(GcsReply reply, GcsRequestTarget target, CancellationToken cancellationToken)
    {
        var parameters = target.Parameters;
        var maxParts = NumberOf(GcsQueryParameters.Value(parameters, "max-parts"), MaxListedParts);
        var marker = NumberOf(GcsQueryParameters.Value(parameters, "part-number-marker"), 0);
        if (maxParts < 0 || marker < 0)
        {
            return reply.Error(GcsError.InvalidArgument);
        }

        var uploadId = GcsQueryParameters.Value(parameters, "uploadId")!;

        return await staging.Uploads.RunAsync(
            () => Task.FromResult(staging.Uploads.Find(uploadId, GcsUploadKind.Multipart, target) is { } upload
                ? reply.Xml(Encoding.UTF8.GetBytes(RenderParts(target, upload, marker, Math.Min(maxParts, MaxListedParts))))
                : reply.Error(GcsError.NoSuchUpload)),
            cancellationToken);
    }

    /// <summary>
    /// Answers <c>DELETE /&lt;bucket&gt;/&lt;object&gt;?uploadId=&lt;id&gt;</c>: ends the upload,
    /// discarding its parts, and answers <c>204</c>; uploads off is <c>403 AccessDenied</c>.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="target">The bucket, object and query.</param>
    /// <param name="cancellationToken">Cancels waiting for the uploads.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AbortAsync(GcsReply reply, GcsRequestTarget target, CancellationToken cancellationToken)
    {
        if (!staging.AllowsUploads)
        {
            return reply.Error(GcsError.AccessDenied);
        }

        var uploadId = GcsQueryParameters.Value(target.Parameters, "uploadId")!;

        return await staging.Uploads.RunAsync(
            async () =>
            {
                if (staging.Uploads.Find(uploadId, GcsUploadKind.Multipart, target) is not { } upload)
                {
                    return reply.Error(GcsError.NoSuchUpload);
                }

                await staging.Uploads.RemoveAsync(upload);

                return reply.Answer(NoContent, [], HttpAnswerBody.None);
            },
            cancellationToken);
    }

    /// <summary>
    /// The result element, the declaration and namespace first, then <c>Bucket</c>, <c>Key</c> and
    /// <c>UploadId</c>, as each multipart page's example begins.
    /// </summary>
    /// <param name="element">The result element's name.</param>
    /// <param name="target">The bucket and object.</param>
    /// <param name="uploadId">The upload's ID, or <see langword="null"/> to leave it out.</param>
    /// <param name="location">The object's URI, written first as <c>Location</c>, or <see langword="null"/> to leave it out.</param>
    /// <returns>The body so far, its root element open.</returns>
    internal static StringBuilder Start(string element, GcsRequestTarget target, string? uploadId, string? location = null)
    {
        var xml = new StringBuilder($"{GcsErrorBody.Declaration}<{element} xmlns=\"{Namespace}\">");
        GcsXml.AppendWhenGiven(xml, "Location", location);
        GcsXml.Append(xml, "Bucket", Uri.UnescapeDataString(target.RawBucket));
        GcsXml.Append(xml, "Key", Uri.UnescapeDataString(target.RawName));
        GcsXml.AppendWhenGiven(xml, "UploadId", uploadId);

        return xml;
    }

    // Uploads, the part number, then PUT Object's head checks and the announced length.
    private GcsError? RefusalBeforeThePart(HttpRequest request, GcsRequestTarget target, out int partNumber, out GcsSentDigests? sent)
    {
        partNumber = NumberOf(GcsQueryParameters.Value(target.Parameters, "partNumber"), -1);
        sent = null;
        if (!staging.AllowsUploads)
        {
            return GcsError.AccessDenied;
        }

        if (partNumber is < 1 or > MaxPartNumber)
        {
            return GcsError.InvalidArgument;
        }

        if (GcsRequestFields.Value(request.Fields, "Content-Length") is null)
        {
            return GcsError.MissingContentLength;
        }

        if ((sent = GcsSentDigests.Read(request.Fields, out var digestRefusal)) is null)
        {
            return digestRefusal;
        }

        return staging.IsPastTheUploadLimit(request.Body.AnnouncedLength) ? GcsError.EntityTooLarge : null;
    }

    private async Task<HttpAnswer> StagePartAsync(GcsReply reply, HttpRequest request, GcsStagedUpload upload, int partNumber, IHttpRequestBodyCheck? bodyCheck, GcsSentDigests sent, CancellationToken cancellationToken)
    {
        var offset = upload.Session.Length;
        var (refusal, digests) = await staging.AppendBodyAsync(request, upload, bodyCheck, sent, cancellationToken);
        if (refusal is not null)
        {
            return reply.Error(refusal);
        }

        var part = new GcsUploadedPart(offset, upload.Session.Length - offset, digests!.Md5, staging.Uploads.TimeProvider.GetUtcNow());
        upload.Record(partNumber, part);

        return reply.Answer(HttpStatus.Ok, [new("ETag", part.ETag), new("x-goog-hash", digests.HashField), new("Content-Length", "0")], HttpAnswerBody.None);
    }

    // ListPartsResult in the order of "GET Object multipart"'s example.
    private static string RenderParts(GcsRequestTarget target, GcsStagedUpload upload, int marker, int maxParts)
    {
        var after = upload.Parts.Where(part => part.Key > marker).ToList();
        var page = after.Take(maxParts).ToList();
        var isTruncated = after.Count > page.Count;
        var xml = Start("ListPartsResult", target, upload.UploadId);
        GcsXml.Append(xml, "StorageClass", "STANDARD");
        GcsXml.Append(xml, "PartNumberMarker", marker.ToString(CultureInfo.InvariantCulture));
        GcsXml.AppendWhenGiven(xml, "NextPartNumberMarker", isTruncated ? page[^1].Key.ToString(CultureInfo.InvariantCulture) : null);
        GcsXml.Append(xml, "MaxParts", maxParts.ToString(CultureInfo.InvariantCulture));
        GcsXml.Append(xml, "IsTruncated", isTruncated ? "true" : "false");
        foreach (var (number, part) in page)
        {
            xml.Append("<Part>");
            GcsXml.Append(xml, "PartNumber", number.ToString(CultureInfo.InvariantCulture));
            GcsXml.Append(xml, "LastModified", GcsXml.Time(part.Uploaded));
            GcsXml.Append(xml, "ETag", part.ETag);
            GcsXml.Append(xml, "Size", part.Size.ToString(CultureInfo.InvariantCulture));
            xml.Append("</Part>");
        }

        return xml.Append("</ListPartsResult>").ToString();
    }

    // A decimal number, the fallback when absent, or -1 when it does not read.
    private static int NumberOf(string? text, int whenAbsent)
    {
        if (text is null)
        {
            return whenAbsent;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : -1;
    }
}
