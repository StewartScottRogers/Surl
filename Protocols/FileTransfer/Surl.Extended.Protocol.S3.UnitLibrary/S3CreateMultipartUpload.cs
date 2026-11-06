using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Answers CreateMultipartUpload ("CreateMultipartUpload"; ADR-0108, decisions 1, 3 and 7): opens
/// an upload of the key, its parts staged in an upload session beside the object that is never
/// committed, keeps the request's header fields and <c>x-amz-meta-*</c> for the completed object,
/// and answers <c>200</c> with <c>InitiateMultipartUploadResult</c>.
/// </summary>
/// <remarks>
/// Uploads need <c>--allow-uploads</c> (off: <c>403 AccessDenied</c>). <c>x-amz-meta-*</c> past
/// 2 KiB is <c>400 MetadataTooLarge</c>; a key the store cannot hold, or one ending in <c>/</c>,
/// <c>400 InvalidArgument</c>; the 1001st open upload <c>503 SlowDown</c>. The key's folders are
/// created here, because the staging file sits in them; a file in the way, or a folder at the key,
/// is <c>409 InvalidArgument</c>.
/// </remarks>
/// <param name="store">The content store the object is written to.</param>
/// <param name="putObject">PutObject, whose checks and folder creation the upload shares.</param>
/// <param name="uploads">The server's open uploads.</param>
internal sealed class S3CreateMultipartUpload(ContentStore store, S3PutObject putObject, S3MultipartUploads uploads)
{
    private const string Namespace = "http://s3.amazonaws.com/doc/2006-03-01/";

    /// <summary>
    /// Answers a CreateMultipartUpload whose bucket exists and whose key is not too long.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request.</param>
    /// <param name="target">The request's bucket and key.</param>
    /// <param name="caller">Who uploads, on <c>s3</c>.</param>
    /// <param name="cancellationToken">Cancels waiting for the uploads.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(S3Reply reply, HttpRequest request, S3RequestTarget target, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.AllowUploads)
        {
            return reply.Error(S3Error.AccessDenied);
        }

        if (S3PutObject.HasTooMuchUserMetadata(request.Fields))
        {
            return reply.Error(S3Error.MetadataTooLarge);
        }

        var mapping = store.MapBucketRequestPathForCaller(target.ObjectPath, caller);
        if (!mapping.IsMapped || target.RawKey.EndsWith('/'))
        {
            return reply.Error(S3Error.InvalidArgument, S3PutObject.KeyArgument(target));
        }

        await uploads.EnterAsync(cancellationToken);
        try
        {
            return await OpenAsync(reply, request, target, mapping, caller, cancellationToken);
        }
        finally
        {
            uploads.Exit();
        }
    }

    // Room for one more upload, the key's folders, then the staging session.
    private async Task<HttpAnswer> OpenAsync(S3Reply reply, HttpRequest request, S3RequestTarget target, ContentPathMapping mapping, ContentCaller caller, CancellationToken cancellationToken)
    {
        if (!uploads.HasRoom)
        {
            return reply.Error(S3Error.SlowDown);
        }

        if (putObject.CreateFolders(target, caller) is { } folderRefusal)
        {
            return reply.Error(folderRefusal);
        }

        var opening = await store.OpenUploadAsync(mapping, new ContentUploadOpening(StartsFromExistingBytes: false, CreatesMissingFile: true, RefusesExistingFile: false), cancellationToken);
        if (opening.Session is not { } session)
        {
            return reply.Error(S3PutObject.OpeningRefusal(opening.Result));
        }

        var upload = new S3MultipartUpload(RandomNumberGenerator.GetHexString(32, lowercase: true), target, request.Fields, session, uploads.TimeProvider.GetUtcNow());
        uploads.Add(upload);
        var body = Encoding.UTF8.GetBytes(Render(target, upload.UploadId));

        return reply.Answer(
            HttpStatus.Ok,
            [new("Content-Type", "application/xml"), new("Content-Length", body.Length.ToString(CultureInfo.InvariantCulture))],
            new HttpAnswerBody.Bytes(body));
    }

    // InitiateMultipartUploadResult in the order of the API Reference's example.
    private static string Render(S3RequestTarget target, string uploadId)
    {
        var xml = new StringBuilder($"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<InitiateMultipartUploadResult xmlns=\"{Namespace}\">");
        S3XmlElement.Append(xml, "Bucket", Uri.UnescapeDataString(target.RawBucket));
        S3XmlElement.Append(xml, "Key", Uri.UnescapeDataString(target.RawKey));
        S3XmlElement.Append(xml, "UploadId", uploadId);

        return xml.Append("</InitiateMultipartUploadResult>").ToString();
    }
}
