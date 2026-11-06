using System.Globalization;
using System.Security.Cryptography;
using Surl.Content;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Answers UploadPart ("UploadPart"; ADR-0108, decisions 1 and 7): appends the body to the
/// upload's staging session, records it under its part number - replacing a part uploaded before
/// with the number - and answers <c>200</c> with the part's MD5 as its <c>ETag</c>.
/// </summary>
/// <remarks>
/// Uploads need <c>--allow-uploads</c> (off: <c>403 AccessDenied</c>). A <c>partNumber</c> that is
/// not 1 to 10000 is <c>400 InvalidArgument</c>; PutObject's body checks apply (<c>Content-MD5</c>,
/// <c>Content-Length</c>, the signed body hash, <c>--max-filesize</c> for the announced length); an
/// <c>uploadId</c> naming no open upload of the bucket and key is <c>404 NoSuchUpload</c>. The
/// staged bytes of one upload together are held to <c>--max-filesize</c>, because the completed
/// object is: past it the upload is aborted and the answer is <c>400 EntityTooLarge</c>.
/// </remarks>
/// <param name="store">The content store's exposure options, read for <c>--allow-uploads</c>.</param>
/// <param name="putObject">PutObject, whose body checks the part shares.</param>
/// <param name="uploads">The server's open uploads.</param>
internal sealed class S3UploadPart(ContentStore store, S3PutObject putObject, S3MultipartUploads uploads)
{
    /// <summary>
    /// The highest part number ("UploadPart").
    /// </summary>
    public const int MaxPartNumber = 10000;

    /// <summary>
    /// Answers an UploadPart whose bucket exists and whose key is not too long.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="request">The request, whose body is read.</param>
    /// <param name="target">The request's bucket, key and query.</param>
    /// <param name="loginResult">The rest of a signature that binds the body.</param>
    /// <param name="cancellationToken">Cancels reading the body.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(S3Reply reply, HttpRequest request, S3RequestTarget target, S3LoginResult loginResult, CancellationToken cancellationToken)
    {
        var parameters = S3QueryParameters.Parse(target.Query!);
        var sentPartNumber = S3QueryParameters.Value(parameters, "partNumber")!;
        if (RefusalBeforeTheBody(request, sentPartNumber, loginResult.ChunkSignatures, out var partNumber) is { } refusal)
        {
            return refusal == S3Error.InvalidArgument
                ? reply.Error(refusal, [new("ArgumentName", "partNumber"), new("ArgumentValue", sentPartNumber)])
                : reply.Error(refusal);
        }

        var uploadId = S3QueryParameters.Value(parameters, "uploadId")!;
        await uploads.EnterAsync(cancellationToken);
        try
        {
            return uploads.Find(uploadId, target) is { } upload
                ? await WriteAsync(reply, request, upload, partNumber, loginResult, cancellationToken)
                : reply.Error(S3Error.NoSuchUpload, new KeyValuePair<string, string>("UploadId", uploadId));
        }
        finally
        {
            uploads.Exit();
        }
    }

    // Uploads, the part number, then PutObject's head checks and the announced length.
    private S3Error? RefusalBeforeTheBody(HttpRequest request, string sentPartNumber, IHttpChunkSignatureChain? chunkSignatures, out int partNumber)
    {
        if (!store.ExposureOptions.AllowUploads)
        {
            partNumber = 0;
            return S3Error.AccessDenied;
        }

        partNumber = PartNumberOf(sentPartNumber);
        if (partNumber == 0)
        {
            return S3Error.InvalidArgument;
        }

        return S3PutObject.RefusalBeforeTheObjectBody(request, chunkSignatures) ?? (putObject.IsAnnouncedPastTheLimit(request) ? S3Error.EntityTooLarge : null);
    }

    // The part number when it is a decimal 1 to 10000, else 0.
    private static int PartNumberOf(string sent) =>
        int.TryParse(sent, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number <= MaxPartNumber ? number : 0;

    // The body goes after the bytes already staged; past the upload limit the upload is lost.
    private async Task<HttpAnswer> WriteAsync(S3Reply reply, HttpRequest request, S3MultipartUpload upload, int partNumber, S3LoginResult loginResult, CancellationToken cancellationToken)
    {
        var session = upload.Session;
        var offset = session.Length;
        var received = await S3ObjectBodyReader.ReadAsync(request, session, loginResult.ChunkSignatures, cancellationToken);
        if (received.Refusal is { } bodyRefusal)
        {
            if (received.IsTooLarge)
            {
                await uploads.RemoveAsync(upload);
            }

            return reply.Error(bodyRefusal);
        }

        if (await putObject.BodyContentRefusalAsync(request, loginResult.BodyCheck, received, cancellationToken) is { } contentRefusal)
        {
            return reply.Error(contentRefusal);
        }

        var part = new S3UploadedPart(offset, session.Length - offset, received.Md5);
        upload.Record(partNumber, part, uploads.TimeProvider.GetUtcNow());

        return S3PutObject.Stored(reply, part.ETag, received.Checksum);
    }
}
