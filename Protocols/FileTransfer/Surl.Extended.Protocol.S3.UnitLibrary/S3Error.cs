using Surl.HttpMessage;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// One row of the S3 API Reference's "List of error codes" the server sends: the code, the
/// status the table gives it, and its fixed message (ADR-0108, decision 1.1; ADR-0006, section 3).
/// </summary>
/// <param name="Code">The <c>Code</c> element's text, such as <c>NoSuchKey</c>.</param>
/// <param name="Status">The status the table gives the code.</param>
/// <param name="Message">The <c>Message</c> element's fixed text.</param>
internal sealed record S3Error(string Code, HttpStatus Status, string Message)
{
    /// <summary>
    /// <c>404 NoSuchKey</c>: no file at the key, the key is a folder, or the store cannot hold it.
    /// </summary>
    public static S3Error NoSuchKey { get; } = new("NoSuchKey", HttpStatus.NotFound, "The specified key does not exist.");

    /// <summary>
    /// <c>404 NoSuchBucket</c>: the bucket's top-level folder does not exist.
    /// </summary>
    public static S3Error NoSuchBucket { get; } = new("NoSuchBucket", HttpStatus.NotFound, "The specified bucket does not exist.");

    /// <summary>
    /// <c>501 NotImplemented</c>: every request ADR-0108 decision 1 does not serve.
    /// </summary>
    public static S3Error NotImplemented { get; } = new("NotImplemented", HttpStatus.NotImplemented, "A header you provided implies functionality that is not implemented");

    /// <summary>
    /// <c>416 InvalidRange</c>: a single range that names no byte of the object.
    /// </summary>
    public static S3Error InvalidRange { get; } = new("InvalidRange", new HttpStatus(416, "Range Not Satisfiable"), "The requested range is not satisfiable");

    /// <summary>
    /// <c>412 PreconditionFailed</c>: a failed <c>If-Match</c> or <c>If-Unmodified-Since</c>.
    /// </summary>
    public static S3Error PreconditionFailed { get; } = new("PreconditionFailed", new HttpStatus(412, "Precondition Failed"), "At least one of the preconditions you specified did not hold.");

    /// <summary>
    /// <c>400 KeyTooLongError</c>: a key longer than <see cref="S3ObjectKey.MaxKeyBytes"/> UTF-8 bytes.
    /// </summary>
    public static S3Error KeyTooLong { get; } = new("KeyTooLongError", HttpStatus.BadRequest, "Your key is too long.");

    /// <summary>
    /// <c>403 AccessDenied</c>: a write while <c>--allow-uploads</c> is off, a write a share's
    /// access forbids, or a listing while <c>--list-directories</c> is off.
    /// </summary>
    public static S3Error AccessDenied { get; } = new("AccessDenied", new HttpStatus(403, "Forbidden"), "Access Denied");

    /// <summary>
    /// <c>400 BadDigest</c>: a <c>Content-MD5</c> that does not match the body.
    /// </summary>
    public static S3Error BadDigest { get; } = new("BadDigest", HttpStatus.BadRequest, "The Content-MD5 you specified did not match what we received.");

    /// <summary>
    /// <c>400 InvalidDigest</c>: a <c>Content-MD5</c> that is not the base64 of 16 bytes.
    /// </summary>
    public static S3Error InvalidDigest { get; } = new("InvalidDigest", HttpStatus.BadRequest, "The Content-MD5 you specified is not valid.");

    /// <summary>
    /// <c>400 EntityTooLarge</c>: a body past <c>--max-filesize</c>; the answer closes the
    /// connection without draining the body (ADR-0108, decision 7).
    /// </summary>
    public static S3Error EntityTooLarge { get; } = new("EntityTooLarge", HttpStatus.BadRequest, "Your proposed upload exceeds the maximum allowed object size.") { ClosesWithoutDrain = true };

    /// <summary>
    /// <c>400 IncompleteBody</c>: a body that ended before the length it announced, or malformed
    /// chunked coding; the answer closes the connection without draining the body (ADR-0108,
    /// decision 7).
    /// </summary>
    public static S3Error IncompleteBody { get; } = new("IncompleteBody", HttpStatus.BadRequest, "You did not provide the number of bytes specified by the Content-Length HTTP header.") { ClosesWithoutDrain = true };

    /// <summary>
    /// Whether an answer with this error is a refusal that closes the connection with
    /// <c>Connection: close</c> and leaves what the client still sends of the request body
    /// unread (<see cref="Surl.HttpServing.HttpAnswer.DrainsBeforeClose"/>; ADR-0108, decision 7).
    /// </summary>
    public bool ClosesWithoutDrain { get; init; }

    /// <summary>
    /// <c>400 InvalidArgument</c>: a key the store cannot hold on a write, or a bad query value.
    /// </summary>
    public static S3Error InvalidArgument { get; } = new("InvalidArgument", HttpStatus.BadRequest, "Invalid Argument");

    /// <summary>
    /// <c>409 InvalidArgument</c>: a key that conflicts with a folder or a file (ADR-0108, decision 3).
    /// </summary>
    public static S3Error ConflictingKey { get; } = new("InvalidArgument", new HttpStatus(409, "Conflict"), "Invalid Argument");

    /// <summary>
    /// <c>400 InvalidArgument</c> for a <c>continuation-token</c> that does not decode (ADR-0108, decision 6).
    /// </summary>
    public static S3Error InvalidContinuationToken { get; } = new("InvalidArgument", HttpStatus.BadRequest, "The continuation token provided is incorrect");

    /// <summary>
    /// <c>400 InvalidRequest</c>: a body the server cannot read as one, such as a chunked
    /// <c>PUT</c> with no <c>x-amz-decoded-content-length</c>.
    /// </summary>
    public static S3Error InvalidRequest { get; } = new("InvalidRequest", HttpStatus.BadRequest, "Invalid Request");

    /// <summary>
    /// <c>400 MetadataTooLarge</c>: user metadata past 2 KiB, or an object's metadata past the
    /// object metadata index's bound.
    /// </summary>
    public static S3Error MetadataTooLarge { get; } = new("MetadataTooLarge", HttpStatus.BadRequest, "Your metadata headers exceed the maximum allowed metadata size.");

    /// <summary>
    /// <c>411 MissingContentLength</c>: a <c>PUT</c> with neither <c>Content-Length</c> nor a chunked body.
    /// </summary>
    public static S3Error MissingContentLength { get; } = new("MissingContentLength", new HttpStatus(411, "Length Required"), "You must provide the Content-Length HTTP header.");

    /// <summary>
    /// <c>503 ServiceUnavailable</c>: the object metadata index is full.
    /// </summary>
    public static S3Error ServiceUnavailable { get; } = new("ServiceUnavailable", HttpStatus.ServiceUnavailable, "Service is unable to handle request.");

    /// <summary>
    /// <c>503 SlowDown</c>: a connection past <c>--max-connections</c> or
    /// <c>--max-connections-per-address</c>, or a CreateMultipartUpload past the open uploads
    /// (ADR-0108, decision 7).
    /// </summary>
    public static S3Error SlowDown { get; } = new("SlowDown", HttpStatus.ServiceUnavailable, "Please reduce your request rate.");

    /// <summary>
    /// <c>400 RequestHeaderSectionTooLarge</c>: a request head past <c>--max-request-head</c>
    /// (ADR-0108, decision 7).
    /// </summary>
    public static S3Error RequestHeaderSectionTooLarge { get; } = new("RequestHeaderSectionTooLarge", HttpStatus.BadRequest, "Your request header section exceeds the maximum allowed size.");

    /// <summary>
    /// <c>400 InvalidArgument</c>: an <c>Authorization</c> field of another scheme than
    /// Signature Version 4 (ADR-0108, decision 4.3).
    /// </summary>
    public static S3Error UnsupportedAuthorizationType { get; } = new("InvalidArgument", HttpStatus.BadRequest, "Unsupported Authorization Type");

    /// <summary>
    /// <c>400 AuthorizationHeaderMalformed</c>: an <c>AWS4-HMAC-SHA256</c> field that does not
    /// parse, or whose scope is not for <c>s3</c> (ADR-0108, decision 4.3).
    /// </summary>
    public static S3Error AuthorizationHeaderMalformed { get; } = new("AuthorizationHeaderMalformed", HttpStatus.BadRequest, "The authorization header that you provided is not valid.");

    /// <summary>
    /// <c>400 InvalidRequest</c>: a signed request without <c>x-amz-content-sha256</c> (ADR-0108,
    /// decision 4.3).
    /// </summary>
    public static S3Error MissingContentSha256 { get; } = new("InvalidRequest", HttpStatus.BadRequest, "Missing required header for this request: x-amz-content-sha256");

    /// <summary>
    /// <c>403 InvalidAccessKeyId</c>: a signature whose access key is no account (ADR-0108,
    /// decision 4.3).
    /// </summary>
    public static S3Error InvalidAccessKeyId { get; } = new("InvalidAccessKeyId", new HttpStatus(403, "Forbidden"), "The AWS access key Id you provided does not exist in our records.");

    /// <summary>
    /// <c>403 SignatureDoesNotMatch</c>: a signature that does not verify (ADR-0108, decision 4.3).
    /// </summary>
    public static S3Error SignatureDoesNotMatch { get; } = new("SignatureDoesNotMatch", new HttpStatus(403, "Forbidden"), "The request signature we calculated does not match the signature you provided. Check your key and signing method.");

    /// <summary>
    /// <c>403 RequestTimeTooSkewed</c>: <c>x-amz-date</c> more than 15 minutes from the server's
    /// clock (ADR-0108, decision 4.3).
    /// </summary>
    public static S3Error RequestTimeTooSkewed { get; } = new("RequestTimeTooSkewed", new HttpStatus(403, "Forbidden"), "The difference between the request time and the server's time is too large.");

    /// <summary>
    /// <c>404 NoSuchUpload</c>: an <c>uploadId</c> that names no open multipart upload of the
    /// bucket and key (ADR-0108, decision 1.1).
    /// </summary>
    public static S3Error NoSuchUpload { get; } = new("NoSuchUpload", HttpStatus.NotFound, "The specified upload does not exist. The upload ID may be invalid, or the upload may have been aborted or completed.");

    /// <summary>
    /// <c>400 InvalidPart</c>: a CompleteMultipartUpload part that was not uploaded, or whose
    /// <c>ETag</c> is not the part's.
    /// </summary>
    public static S3Error InvalidPart { get; } = new("InvalidPart", HttpStatus.BadRequest, "One or more of the specified parts could not be found. The part might not have been uploaded, or the specified entity tag might not have matched the part's entity tag.");

    /// <summary>
    /// <c>400 InvalidPartOrder</c>: CompleteMultipartUpload parts not in ascending part-number order.
    /// </summary>
    public static S3Error InvalidPartOrder { get; } = new("InvalidPartOrder", HttpStatus.BadRequest, "The list of parts was not in ascending order. The parts list must be specified in order by part number.");

    /// <summary>
    /// <c>400 EntityTooSmall</c>: a completed part other than the last below 5 MiB.
    /// </summary>
    public static S3Error EntityTooSmall { get; } = new("EntityTooSmall", HttpStatus.BadRequest, "Your proposed upload is smaller than the minimum allowed object size.");

    /// <summary>
    /// <c>400 MalformedXML</c>: a CompleteMultipartUpload body that does not parse, names no part,
    /// or passes 10000 parts or 1 MiB (ADR-0108, decision 7).
    /// </summary>
    public static S3Error MalformedXml { get; } = new("MalformedXML", HttpStatus.BadRequest, "The XML you provided was not well-formed or did not validate against our published schema.");

    /// <summary>
    /// <c>400 XAmzContentSHA256Mismatch</c>: a body whose SHA-256 is not
    /// <c>x-amz-content-sha256</c> (ADR-0108, decision 4.3).
    /// </summary>
    public static S3Error ContentSha256Mismatch { get; } = new("XAmzContentSHA256Mismatch", HttpStatus.BadRequest, "The provided 'x-amz-content-sha256' header does not match what was computed.");

    /// <summary>
    /// <c>403 AccessDenied</c>: a presigned URL past <c>X-Amz-Date</c> plus <c>X-Amz-Expires</c>
    /// (ADR-0108, decisions 4.3 and 4.5).
    /// </summary>
    public static S3Error RequestExpired { get; } = new("AccessDenied", new HttpStatus(403, "Forbidden"), "Request has expired");

    /// <summary>
    /// <c>400 InvalidArgument</c>: a presigned URL whose <c>X-Amz-Expires</c> is not 1 to 604800
    /// seconds (ADR-0108, decision 4.5).
    /// </summary>
    public static S3Error ExpiresOutOfRange { get; } = new("InvalidArgument", HttpStatus.BadRequest, "X-Amz-Expires must be a number of seconds from 1 to 604800");

    /// <summary>
    /// <c>400 InvalidArgument</c>: a presigned URL with an <c>Authorization</c> field too
    /// (ADR-0108, decision 4.5).
    /// </summary>
    public static S3Error ConflictingAuthentication { get; } = new("InvalidArgument", HttpStatus.BadRequest, "Only one auth mechanism allowed; only the X-Amz-Algorithm query parameter, Signature query string parameter or the Authorization header should be specified");

    /// <summary>
    /// <c>400 InvalidRequest</c>: an <c>x-amz-checksum-*</c> value that is not the base64 of
    /// its algorithm's digest, or an <c>x-amz-trailer</c> naming no checksum the request can
    /// carry, or a trailer that does not carry it (ADR-0108, decision 4.5).
    /// </summary>
    public static S3Error InvalidChecksum { get; } = new("InvalidRequest", HttpStatus.BadRequest, "Value for x-amz-checksum header is invalid.");

    /// <summary>
    /// <c>400 InvalidRequest</c>: more than one <c>x-amz-checksum-*</c> algorithm named
    /// (ADR-0108, decision 4.5).
    /// </summary>
    public static S3Error MultipleChecksums { get; } = new("InvalidRequest", HttpStatus.BadRequest, "Expecting a single x-amz-checksum- header. Multiple checksum Types are not allowed.");

    /// <summary>
    /// <c>400 BadDigest</c>: a body whose checksum is not the <c>x-amz-checksum-*</c> sent
    /// ("Checking object integrity"; ADR-0108, decision 4.5).
    /// </summary>
    /// <param name="algorithmName">The algorithm's name as the message spells it, such as <c>CRC32</c>.</param>
    /// <returns>The error.</returns>
    public static S3Error ChecksumMismatch(string algorithmName) =>
        new("BadDigest", HttpStatus.BadRequest, $"The {algorithmName} you specified did not match the calculated checksum.");
}
