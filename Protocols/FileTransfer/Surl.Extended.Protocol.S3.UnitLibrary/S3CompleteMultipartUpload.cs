using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Answers CompleteMultipartUpload ("CompleteMultipartUpload", "Checking object integrity";
/// ADR-0108, decisions 1, 3 and 7): writes the object as the parts the body names, in its order,
/// through a fresh upload session committed over the key, keeps the metadata CreateMultipartUpload
/// named with the ETag <c>"&lt;hex MD5 of the parts' MD5s&gt;-&lt;part count&gt;"</c>, ends the
/// upload, and answers <c>200</c> with <c>CompleteMultipartUploadResult</c>.
/// </summary>
/// <remarks>
/// Uploads need <c>--allow-uploads</c> (off: <c>403 AccessDenied</c>). PutObject's head checks
/// apply to the body; a body past 1 MiB, or one that does not parse, is <c>400 MalformedXML</c>; an
/// <c>uploadId</c> naming no open upload of the bucket and key <c>404 NoSuchUpload</c>; parts not
/// in ascending order <c>400 InvalidPartOrder</c>; a part not uploaded, or with another ETag,
/// <c>400 InvalidPart</c>; a part but the last below <see cref="MinPartBytes"/> <c>400
/// EntityTooSmall</c>. A refused completion leaves the upload open, as S3 does, so the client may
/// try again.
/// </remarks>
/// <param name="store">The content store the object is written to.</param>
/// <param name="putObject">PutObject, whose checks and metadata the completion shares.</param>
/// <param name="uploads">The server's open uploads.</param>
/// <param name="isOverTls">Whether the connection is TLS, for the <c>Location</c>'s scheme.</param>
/// <param name="listenHost">The host the server listens on, for a <c>Location</c> when the request named none.</param>
internal sealed class S3CompleteMultipartUpload(ContentStore store, S3PutObject putObject, S3MultipartUploads uploads, bool isOverTls, string listenHost)
{
    /// <summary>
    /// The fewest bytes every part but the last holds: 5 MiB ("UploadPart").
    /// </summary>
    public const long MinPartBytes = 5 * 1024 * 1024;

    private const string Namespace = "http://s3.amazonaws.com/doc/2006-03-01/";
    private const int CopyBufferBytes = 81920;

    /// <summary>
    /// Answers a CompleteMultipartUpload whose bucket exists and whose key is not too long.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose body is read.</param>
    /// <param name="target">The request's bucket, key and query.</param>
    /// <param name="loginResult">Who writes, and the rest of a signature that binds the body.</param>
    /// <param name="cancellationToken">Cancels reading the body and writing the object.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(S3Reply reply, HttpRequest request, S3RequestTarget target, S3LoginResult loginResult, CancellationToken cancellationToken)
    {
        if (RefusalBeforeTheBody(request) is { } refusal)
        {
            return reply.Error(refusal);
        }

        var (bodyRefusal, body) = await ReadBodyAsync(request, loginResult, cancellationToken);
        if (bodyRefusal is not null)
        {
            return reply.Error(bodyRefusal);
        }

        var uploadId = S3QueryParameters.Value(S3QueryParameters.Parse(target.Query!), "uploadId")!;
        await uploads.EnterAsync(cancellationToken);
        try
        {
            return uploads.Find(uploadId, target) is { } upload
                ? await CompleteAsync(reply, request, target, loginResult.Caller!, upload, body, cancellationToken)
                : reply.Error(S3Error.NoSuchUpload, new KeyValuePair<string, string>("UploadId", uploadId));
        }
        finally
        {
            uploads.Exit();
        }
    }

    private S3Error? RefusalBeforeTheBody(HttpRequest request)
    {
        if (!store.ExposureOptions.AllowUploads)
        {
            return S3Error.AccessDenied;
        }

        return S3PutObject.RefusalBeforeTheBody(request);
    }

    // The body, at most 1 MiB, then the signed hash and Content-MD5 checked against it.
    private async Task<(S3Error? Refusal, byte[] Body)> ReadBodyAsync(HttpRequest request, S3LoginResult loginResult, CancellationToken cancellationToken)
    {
        using var sink = new S3BoundedBodyStream(S3CompleteMultipartUploadDocument.MaxBodyBytes);
        var outcome = request.Body.IsAnnounced ? await request.Body.CopyToAsync(sink, cancellationToken) : HttpRequestBodyDiscardOutcome.Discarded;
        if (S3PutObject.BodyRefusal(outcome, isTooLarge: false) is { } bodyRefusal)
        {
            return (bodyRefusal, []);
        }

        var body = sink.ToArray();
        var contentRefusal = await putObject.BodyContentRefusalAsync(
            request, loginResult.BodyCheck, new S3ReceivedBody(null, false, MD5.HashData(body), SHA256.HashData(body), null), cancellationToken);

        return (contentRefusal ?? (sink.IsTooLarge ? S3Error.MalformedXml : null), body);
    }

    // The parts the body names checked against the upload, then the object written from them.
    private async Task<HttpAnswer> CompleteAsync(S3Reply reply, HttpRequest request, S3RequestTarget target, ContentCaller caller, S3MultipartUpload upload, byte[] body, CancellationToken cancellationToken)
    {
        upload.Touch(uploads.TimeProvider.GetUtcNow());
        var named = S3CompleteMultipartUploadDocument.Read(body);
        if (named is null)
        {
            return reply.Error(S3Error.MalformedXml);
        }

        var (partRefusal, parts) = Resolve(upload, named);
        if (partRefusal is not null)
        {
            return reply.Error(partRefusal);
        }

        var mapping = store.MapBucketRequestPathForCaller(target.ObjectPath, caller);
        var eTag = MultipartETagOf(parts);
        var writeRefusal = await WriteObjectAsync(upload, parts, mapping, cancellationToken)
            ?? await putObject.StoreMetadataAsync(upload.MetadataFields, target, mapping, eTag, cancellationToken);
        if (writeRefusal is not null)
        {
            return reply.Error(writeRefusal);
        }

        await uploads.RemoveAsync(upload);
        var result = Encoding.UTF8.GetBytes(Render(request, target, eTag));

        return reply.Answer(
            HttpStatus.Ok,
            [new("Content-Type", "application/xml"), new("Content-Length", result.Length.ToString(CultureInfo.InvariantCulture))],
            new HttpAnswerBody.Bytes(result));
    }

    // The order first, then each part found with its ETag, then every part but the last big enough.
    private static (S3Error? Refusal, List<S3UploadedPart> Parts) Resolve(S3MultipartUpload upload, List<S3CompletedPart> named)
    {
        if (named.Zip(named.Skip(1)).Any(pair => pair.Second.PartNumber <= pair.First.PartNumber))
        {
            return (S3Error.InvalidPartOrder, []);
        }

        if (!named.All(part => upload.Part(part.PartNumber) is { } uploaded && uploaded.HasETag(part.ETag)))
        {
            return (S3Error.InvalidPart, []);
        }

        var parts = named.Select(part => upload.Part(part.PartNumber)!).ToList();

        return parts.SkipLast(1).Any(part => part.Size < MinPartBytes)
            ? (S3Error.EntityTooSmall, [])
            : (null, parts);
    }

    // "<hex MD5 of the parts' MD5s>-<part count>", quoted ("Checking object integrity").
    private static string MultipartETagOf(List<S3UploadedPart> parts) =>
        $"\"{Convert.ToHexStringLower(MD5.HashData([.. parts.SelectMany(part => part.Md5)]))}-{parts.Count}\"";

    // The parts' bytes, one after another, into a fresh session committed over the key.
    private async Task<S3Error?> WriteObjectAsync(S3MultipartUpload upload, List<S3UploadedPart> parts, ContentPathMapping mapping, CancellationToken cancellationToken)
    {
        var opening = await store.OpenUploadAsync(mapping, new ContentUploadOpening(StartsFromExistingBytes: false, CreatesMissingFile: true, RefusesExistingFile: false), cancellationToken);
        if (opening.Session is not { } session)
        {
            return S3PutObject.OpeningRefusal(opening.Result);
        }

        await using (session)
        {
            foreach (var part in parts)
            {
                await CopyPartAsync(upload.Session, part, session, cancellationToken);
            }

            // Every part was staged within the upload limit, so the object is too; a commit the
            // store refuses (a fault, a share gone read-only) is AccessDenied, as for PutObject.
            return S3PutObject.CommitRefusal(await session.CommitAsync(cancellationToken));
        }
    }

    private static async Task CopyPartAsync(ContentUploadSession staged, S3UploadedPart part, ContentUploadSession destination, CancellationToken cancellationToken)
    {
        var buffer = new byte[CopyBufferBytes];
        var copied = 0L;
        while (copied < part.Size)
        {
            var read = await staged.ReadAtAsync(part.Offset + copied, buffer.AsMemory(0, (int)Math.Min(buffer.Length, part.Size - copied)), cancellationToken);
            _ = await destination.WriteAtAsync(destination.Length, buffer.AsMemory(0, read), cancellationToken);
            copied += read;
        }
    }

    // CompleteMultipartUploadResult in the order of the API Reference's example.
    private string Render(HttpRequest request, S3RequestTarget target, string eTag)
    {
        var xml = new StringBuilder($"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<CompleteMultipartUploadResult xmlns=\"{Namespace}\">");
        S3XmlElement.Append(xml, "Location", $"{(isOverTls ? "https" : "http")}://{request.Authority ?? listenHost}{target.Path}");
        S3XmlElement.Append(xml, "Bucket", Uri.UnescapeDataString(target.RawBucket));
        S3XmlElement.Append(xml, "Key", Uri.UnescapeDataString(target.RawKey));
        S3XmlElement.Append(xml, "ETag", eTag);

        return xml.Append("</CompleteMultipartUploadResult>").ToString();
    }
}
