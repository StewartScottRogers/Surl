using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Answers AbortMultipartUpload ("AbortMultipartUpload"; ADR-0108, decision 1): ends the upload,
/// discarding every part staged for it, and answers <c>204</c>.
/// </summary>
/// <remarks>
/// Uploads need <c>--allow-uploads</c> (off: <c>403 AccessDenied</c>); an <c>uploadId</c> naming no
/// open upload of the bucket and key is <c>404 NoSuchUpload</c>.
/// </remarks>
/// <param name="store">The content store's exposure options, read for <c>--allow-uploads</c>.</param>
/// <param name="uploads">The server's open uploads.</param>
internal sealed class S3AbortMultipartUpload(ContentStore store, S3MultipartUploads uploads)
{
    private static readonly HttpStatus NoContent = new(204, "No Content");

    /// <summary>
    /// Answers an AbortMultipartUpload whose bucket exists and whose key is not too long.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="target">The request's bucket, key and query.</param>
    /// <param name="cancellationToken">Cancels waiting for the uploads.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(S3Reply reply, S3RequestTarget target, CancellationToken cancellationToken)
    {
        if (!store.ExposureOptions.AllowUploads)
        {
            return reply.Error(S3Error.AccessDenied);
        }

        var uploadId = S3QueryParameters.Value(S3QueryParameters.Parse(target.Query!), "uploadId")!;
        await uploads.EnterAsync(cancellationToken);
        try
        {
            if (uploads.Find(uploadId, target) is not { } upload)
            {
                return reply.Error(S3Error.NoSuchUpload, new KeyValuePair<string, string>("UploadId", uploadId));
            }

            await uploads.RemoveAsync(upload);

            return reply.Answer(NoContent, [], HttpAnswerBody.None);
        }
        finally
        {
            uploads.Exit();
        }
    }
}
