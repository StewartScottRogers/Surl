using System.Globalization;
using System.Security.Cryptography;
using Surl.Content;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Reads a PutObject's or UploadPart's body into an upload session (ADR-0108 decisions 3 and 4.5):
/// the bytes as sent, or an <c>aws-chunked</c> body decoded by <see cref="S3AwsChunkedDecoder"/>,
/// hashing the object's bytes with MD5, SHA-256 and any additional checksum the request asks for.
/// </summary>
internal static class S3ObjectBodyReader
{
    private const string DecodedLengthFieldName = "x-amz-decoded-content-length";

    /// <summary>
    /// How the body is framed, by its <c>x-amz-content-sha256</c>.
    /// </summary>
    /// <param name="fields">The request's fields.</param>
    /// <returns>The form.</returns>
    public static S3PayloadForm FormOf(IReadOnlyList<KeyValuePair<string, string>> fields) =>
        S3RequestFields.Value(fields, "x-amz-content-sha256") switch
        {
            "STREAMING-AWS4-HMAC-SHA256-PAYLOAD" => S3PayloadForm.SignedChunks,
            "STREAMING-UNSIGNED-PAYLOAD-TRAILER" => S3PayloadForm.UnsignedChunksWithTrailer,
            { } value when value.StartsWith("STREAMING-", StringComparison.Ordinal) => S3PayloadForm.Unserved,
            _ => S3PayloadForm.Plain,
        };

    /// <summary>
    /// The <c>x-amz-decoded-content-length</c>, when it is a decimal number.
    /// </summary>
    /// <param name="fields">The request's fields.</param>
    /// <returns>The object's length, or <see langword="null"/>.</returns>
    public static long? DecodedLengthOf(IReadOnlyList<KeyValuePair<string, string>> fields) =>
        long.TryParse(S3RequestFields.Value(fields, DecodedLengthFieldName), NumberStyles.None, CultureInfo.InvariantCulture, out var length)
            ? length
            : null;

    /// <summary>
    /// The object's length as the head announces it: the decoded length of an <c>aws-chunked</c>
    /// body, else the body's announced length.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The length, or <see langword="null"/> when the head does not say.</returns>
    public static long? AnnouncedObjectLength(HttpRequest request) =>
        FormOf(request.Fields) == S3PayloadForm.Plain ? request.Body.AnnouncedLength : DecodedLengthOf(request.Fields);

    /// <summary>
    /// What the head says of the body before it is read: an <c>aws-chunked</c> form the server does
    /// not serve <c>501 NotImplemented</c>; no decoded length <c>411 MissingContentLength</c>; signed
    /// chunks with no chain to check them <c>403 SignatureDoesNotMatch</c>; a body that is chunked
    /// but not <c>aws-chunked</c>, or cannot be read, <c>400 InvalidRequest</c>; a plain body without
    /// <c>Content-Length</c> <c>411 MissingContentLength</c>; then the checksum fields
    /// (<see cref="S3ChecksumRequest.Read"/>).
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="chunkSignatures">The chain the login gave, or <see langword="null"/>.</param>
    /// <returns>The error, or <see langword="null"/>.</returns>
    public static S3Error? RefusalBeforeTheBody(HttpRequest request, IHttpChunkSignatureChain? chunkSignatures)
    {
        var form = FormOf(request.Fields);
        var framingRefusal = form == S3PayloadForm.Plain ? PlainFramingRefusal(request) : ChunkedFramingRefusal(request, form, chunkSignatures);
        if (framingRefusal is not null)
        {
            return framingRefusal;
        }

        S3ChecksumRequest.Read(request.Fields, form, out var checksumRefusal);

        return checksumRefusal;
    }

    /// <summary>
    /// Copies the body into <paramref name="session"/>, decoding it when it is <c>aws-chunked</c>.
    /// The head must have passed <see cref="RefusalBeforeTheBody"/>.
    /// </summary>
    /// <param name="request">The request, whose body is read.</param>
    /// <param name="session">The upload session the object's bytes are appended to.</param>
    /// <param name="chunkSignatures">The chain each signed chunk is checked against, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>What the body came to.</returns>
    public static async Task<S3ReceivedBody> ReadAsync(HttpRequest request, ContentUploadSession session, IHttpChunkSignatureChain? chunkSignatures, CancellationToken cancellationToken)
    {
        var form = FormOf(request.Fields);
        var checksumRequest = S3ChecksumRequest.Read(request.Fields, form, out _);
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var checksum = checksumRequest is null ? null : new S3Checksum(checksumRequest.Algorithm);
        var sink = new S3UploadStream(session, md5, sha256, checksum);
        using var decoder = form == S3PayloadForm.Plain ? null : new S3AwsChunkedDecoder(sink, chunkSignatures, DecodedLengthOf(request.Fields)!.Value);
        var outcome = await CopyAsync(request.Body, decoder ?? (Stream)sink, cancellationToken);
        var refusal = S3PutObject.BodyRefusal(outcome, sink.IsTooLarge) ?? decoder?.Finish();
        var received = refusal is null ? Received(checksumRequest, checksum, decoder) : null;
        refusal ??= checksumRequest is not null && received is null ? S3Error.InvalidChecksum : null;

        return new S3ReceivedBody(refusal, sink.IsTooLarge, md5.GetHashAndReset(), sha256.GetHashAndReset(), received);
    }

    private static Task<HttpRequestBodyDiscardOutcome> CopyAsync(IHttpRequestBody body, Stream destination, CancellationToken cancellationToken) =>
        body.IsAnnounced ? body.CopyToAsync(destination, cancellationToken) : Task.FromResult(HttpRequestBodyDiscardOutcome.Discarded);

    // The digest the field sent, or the one the trailer carried; null when none was asked for or
    // the trailer has none.
    private static S3ReceivedChecksum? Received(S3ChecksumRequest? checksumRequest, S3Checksum? checksum, S3AwsChunkedDecoder? decoder)
    {
        if (checksumRequest is null)
        {
            return null;
        }

        var algorithm = checksumRequest.Algorithm;
        var expected = checksumRequest.SentDigest
            ?? (decoder!.Trailer.TryGetValue(algorithm.FieldName, out var trailerValue) ? S3ChecksumRequest.ReadDigest(trailerValue, algorithm) : null);

        return expected is null ? null : new S3ReceivedChecksum(algorithm, expected, checksum!.Digest());
    }

    // A body must be readable and announced by Content-Length; a chunked one that is not
    // aws-chunked gives no object length (ADR-0108 Amendment 2 decision 5).
    internal static S3Error? PlainFramingRefusal(HttpRequest request)
    {
        var body = request.Body;
        if (!body.IsReadable || body.IsAnnounced && body.AnnouncedLength is null)
        {
            return S3Error.InvalidRequest;
        }

        return body.IsAnnounced || S3RequestFields.Value(request.Fields, "Content-Length") is not null ? null : S3Error.MissingContentLength;
    }

    private static S3Error? ChunkedFramingRefusal(HttpRequest request, S3PayloadForm form, IHttpChunkSignatureChain? chunkSignatures)
    {
        if (form == S3PayloadForm.Unserved)
        {
            return S3Error.NotImplemented;
        }

        if (!request.Body.IsReadable)
        {
            return S3Error.InvalidRequest;
        }

        if (DecodedLengthOf(request.Fields) is null)
        {
            return S3Error.MissingContentLength;
        }

        return form == S3PayloadForm.SignedChunks && chunkSignatures is null ? S3Error.SignatureDoesNotMatch : null;
    }
}
