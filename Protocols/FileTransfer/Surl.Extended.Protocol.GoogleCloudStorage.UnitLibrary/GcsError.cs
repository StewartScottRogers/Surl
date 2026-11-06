using Surl.HttpMessage;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// One row of the codes the server sends ("HTTP status and error codes"; ADR-0110, decision 1.3):
/// the code, the status the page gives it, and Surl's fixed message (ADR-0006, section 3).
/// </summary>
/// <param name="Code">The <c>Code</c> element's text, such as <c>NoSuchKey</c>.</param>
/// <param name="Status">The status the page gives the code.</param>
/// <param name="Message">The <c>Message</c> element's fixed text.</param>
internal sealed record GcsError(string Code, HttpStatus Status, string Message)
{
    /// <summary>
    /// <c>403 AccessDenied</c>: a logged-in write while <c>--allow-uploads</c> is off, a write a
    /// share's access forbids, or a listing while <c>--list-directories</c> is off.
    /// </summary>
    public static GcsError AccessDenied { get; } = new("AccessDenied", HttpStatus.Forbidden, "Access denied.");

    /// <summary>
    /// <c>401 AuthenticationRequired</c>: no credential with anonymous off, or an anonymous write.
    /// </summary>
    public static GcsError AuthenticationRequired { get; } = new("AuthenticationRequired", HttpStatus.Unauthorized, "Authentication required.");

    /// <summary>
    /// <c>400 BadDigest</c>: a <c>Content-MD5</c>, <c>x-goog-hash</c> or signed
    /// <c>x-goog-content-sha256</c> that does not match the body.
    /// </summary>
    public static GcsError BadDigest { get; } = new("BadDigest", HttpStatus.BadRequest, "The Content-MD5 or x-goog-hash you specified did not match what was received.");

    /// <summary>
    /// <c>400 EntityTooLarge</c>: a body past <c>--max-filesize</c> (decision 7).
    /// </summary>
    public static GcsError EntityTooLarge { get; } = new("EntityTooLarge", HttpStatus.BadRequest, "Your proposed upload exceeds the maximum allowed object size.");

    /// <summary>
    /// <c>400 EntityTooSmall</c>: a multipart part but the last below 5 MiB, at completion (decision 9).
    /// </summary>
    public static GcsError EntityTooSmall { get; } = new("EntityTooSmall", HttpStatus.BadRequest, "Your proposed upload is smaller than the minimum allowed object size.");

    /// <summary>
    /// <c>400 IncompleteBody</c>: a body that ended before its <c>Content-Length</c>.
    /// </summary>
    public static GcsError IncompleteBody { get; } = new("IncompleteBody", HttpStatus.BadRequest, "You did not provide the number of bytes specified by the Content-Length HTTP header.");

    /// <summary>
    /// <c>403 InsufficientQuota</c>: the object metadata index is full.
    /// </summary>
    public static GcsError InsufficientQuota { get; } = new("InsufficientQuota", HttpStatus.Forbidden, "The object metadata index is full.");

    /// <summary>
    /// <c>403 InvalidAccessKeyId</c>: a <c>GOOG4-HMAC-SHA256</c> access ID that is no account (decision 3.3).
    /// </summary>
    public static GcsError InvalidAccessKeyId { get; } = new("InvalidAccessKeyId", HttpStatus.Forbidden, "The access key ID you provided does not exist in our records.");

    /// <summary>
    /// <c>400 InvalidArgument</c>: a bad query or condition value; a <c>GOOG4-HMAC-SHA256</c> field
    /// that does not parse, whose scope does not end <c>/storage/goog4_request</c>, or that leaves
    /// <c>host</c> unsigned (decision 3.4).
    /// </summary>
    public static GcsError InvalidArgument { get; } = new("InvalidArgument", HttpStatus.BadRequest, "Invalid argument.");

    /// <summary>
    /// <c>400 InvalidAuthentication</c>: an <c>Authorization</c> field of a scheme the server does not take.
    /// </summary>
    public static GcsError UnsupportedAuthentication { get; } = new("InvalidAuthentication", HttpStatus.BadRequest, "The provided authentication header is invalid.");

    /// <summary>
    /// <c>401 InvalidAuthentication</c>: a Bearer token that is no account, or one the session
    /// refuses over plain text.
    /// </summary>
    public static GcsError InvalidBearerToken { get; } = new("InvalidAuthentication", HttpStatus.Unauthorized, "The provided authentication header is invalid.");

    /// <summary>
    /// <c>400 InvalidDigest</c>: a <c>Content-MD5</c> that is not the base64 of 16 bytes.
    /// </summary>
    public static GcsError InvalidDigest { get; } = new("InvalidDigest", HttpStatus.BadRequest, "The Content-MD5 you specified is not valid.");

    /// <summary>
    /// <c>400 InvalidHash</c>: an <c>x-goog-hash</c> that does not parse.
    /// </summary>
    public static GcsError InvalidHash { get; } = new("InvalidHash", HttpStatus.BadRequest, "The x-goog-hash you specified is not valid.");

    /// <summary>
    /// <c>400 InvalidObjectName</c>: an object name the store cannot hold, on a write.
    /// </summary>
    public static GcsError InvalidObjectName { get; } = new("InvalidObjectName", HttpStatus.BadRequest, "The specified object name is not valid.");

    /// <summary>
    /// <c>409 InvalidObjectName</c>: an object name conflicting with a folder or file of the other kind.
    /// </summary>
    public static GcsError ConflictingObjectName { get; } = new("InvalidObjectName", new HttpStatus(409, "Conflict"), "The specified object name is not valid.");

    /// <summary>
    /// <c>400 InvalidPart</c>: a completion naming a part not uploaded, or with another ETag (decision 9).
    /// </summary>
    public static GcsError InvalidPart { get; } = new("InvalidPart", HttpStatus.BadRequest, "One or more of the specified parts could not be found.");

    /// <summary>
    /// <c>400 InvalidPartOrder</c>: a completion whose parts are not in ascending order (decision 9).
    /// </summary>
    public static GcsError InvalidPartOrder { get; } = new("InvalidPartOrder", HttpStatus.BadRequest, "The list of parts was not in ascending order.");

    /// <summary>
    /// <c>416 InvalidRange</c>: a single range that names no byte of the object.
    /// </summary>
    public static GcsError InvalidRange { get; } = new("InvalidRange", new HttpStatus(416, "Range Not Satisfiable"), "The requested range cannot be satisfied.");

    /// <summary>
    /// <c>400 KeyTooLong</c>: an object name over <see cref="GcsObjectName.MaxNameBytes"/> UTF-8 bytes.
    /// </summary>
    public static GcsError KeyTooLong { get; } = new("KeyTooLong", HttpStatus.BadRequest, "Your key is too long.");

    /// <summary>
    /// <c>400 MetadataTooLarge</c>: <c>x-goog-meta-*</c> names and values past 8 KiB.
    /// </summary>
    public static GcsError MetadataTooLarge { get; } = new("MetadataTooLarge", HttpStatus.BadRequest, "Your metadata headers exceed the maximum allowed metadata size.");

    /// <summary>
    /// <c>411 MissingContentLength</c>: a <c>PUT</c> without <c>Content-Length</c>, a chunked body included.
    /// </summary>
    public static GcsError MissingContentLength { get; } = new("MissingContentLength", new HttpStatus(411, "Length Required"), "You must provide the Content-Length HTTP header.");

    /// <summary>
    /// <c>400 MissingSecurityHeader</c>: a <c>GOOG4-HMAC-SHA256</c> request without
    /// <c>x-goog-date</c> and <c>Date</c>, or without <c>x-goog-content-sha256</c> (decision 3.4).
    /// </summary>
    public static GcsError MissingSecurityHeader { get; } = new("MissingSecurityHeader", HttpStatus.BadRequest, "Your request was missing a required header.");

    /// <summary>
    /// <c>404 NoSuchBucket</c>: no such top-level folder, or the caller may not read its share.
    /// </summary>
    public static GcsError NoSuchBucket { get; } = new("NoSuchBucket", HttpStatus.NotFound, "The specified bucket does not exist.");

    /// <summary>
    /// <c>404 NoSuchKey</c>: no file at the name, the name is a folder or cannot be held, or a
    /// <c>generation</c> that is not the current one.
    /// </summary>
    public static GcsError NoSuchKey { get; } = new("NoSuchKey", HttpStatus.NotFound, "The specified key does not exist.");

    /// <summary>
    /// <c>404 NoSuchUpload</c>: an <c>uploadId</c> or <c>upload_id</c> naming no open upload of the
    /// bucket and object - never made, completed, aborted, cancelled or expired (decision 9).
    /// </summary>
    public static GcsError NoSuchUpload { get; } = new("NoSuchUpload", HttpStatus.NotFound, "The specified upload does not exist.");

    /// <summary>
    /// <c>400 NotImplemented</c>: every request decision 1 does not list.
    /// </summary>
    public static GcsError NotImplemented { get; } = new("NotImplemented", HttpStatus.BadRequest, "A header or query you provided requested a function that is not implemented.");

    /// <summary>
    /// <c>412 PreconditionFailed</c>: a failing <c>If-Match</c>, <c>If-Unmodified-Since</c> or <c>x-goog-if-*</c>.
    /// </summary>
    public static GcsError PreconditionFailed { get; } = new("PreconditionFailed", new HttpStatus(412, "Precondition Failed"), "At least one of the pre-conditions you specified did not hold.");

    /// <summary>
    /// <c>403 RequestTimeTooSkewed</c>: a <c>GOOG4-HMAC-SHA256</c> request time more than 15 minutes
    /// from the server's clock, or not the scope's date (decision 3.4).
    /// </summary>
    public static GcsError RequestTimeTooSkewed { get; } = new("RequestTimeTooSkewed", HttpStatus.Forbidden, "The difference between the request time and the server's time is too large.");

    /// <summary>
    /// <c>403 SignatureDoesNotMatch</c>: a <c>GOOG4-HMAC-SHA256</c> signature that does not verify.
    /// </summary>
    public static GcsError SignatureDoesNotMatch { get; } = new("SignatureDoesNotMatch", HttpStatus.Forbidden, "The request signature we calculated does not match the signature you provided.");

    /// <summary>
    /// <c>503 SlowDown</c>: a connection past <c>--max-connections</c> or
    /// <c>--max-connections-per-address</c> (decision 7).
    /// </summary>
    public static GcsError SlowDown { get; } = new("SlowDown", HttpStatus.ServiceUnavailable, "Please reduce your request rate.");
}
