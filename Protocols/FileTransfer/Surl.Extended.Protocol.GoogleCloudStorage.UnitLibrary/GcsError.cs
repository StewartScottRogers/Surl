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
    /// <c>400 BadDigest</c>: a <c>Content-MD5</c> or <c>x-goog-hash</c> that does not match the body.
    /// </summary>
    public static GcsError BadDigest { get; } = new("BadDigest", HttpStatus.BadRequest, "The Content-MD5 or x-goog-hash you specified did not match what was received.");

    /// <summary>
    /// <c>400 EntityTooLarge</c>: a body past <c>--max-filesize</c> (decision 7).
    /// </summary>
    public static GcsError EntityTooLarge { get; } = new("EntityTooLarge", HttpStatus.BadRequest, "Your proposed upload exceeds the maximum allowed object size.");

    /// <summary>
    /// <c>400 IncompleteBody</c>: a body that ended before its <c>Content-Length</c>.
    /// </summary>
    public static GcsError IncompleteBody { get; } = new("IncompleteBody", HttpStatus.BadRequest, "You did not provide the number of bytes specified by the Content-Length HTTP header.");

    /// <summary>
    /// <c>403 InsufficientQuota</c>: the object metadata index is full.
    /// </summary>
    public static GcsError InsufficientQuota { get; } = new("InsufficientQuota", HttpStatus.Forbidden, "The object metadata index is full.");

    /// <summary>
    /// <c>400 InvalidArgument</c>: a bad query or condition value.
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
    /// <c>404 NoSuchBucket</c>: no such top-level folder, or the caller may not read its share.
    /// </summary>
    public static GcsError NoSuchBucket { get; } = new("NoSuchBucket", HttpStatus.NotFound, "The specified bucket does not exist.");

    /// <summary>
    /// <c>404 NoSuchKey</c>: no file at the name, the name is a folder or cannot be held, or a
    /// <c>generation</c> that is not the current one.
    /// </summary>
    public static GcsError NoSuchKey { get; } = new("NoSuchKey", HttpStatus.NotFound, "The specified key does not exist.");

    /// <summary>
    /// <c>400 NotImplemented</c>: every request decision 1 does not list.
    /// </summary>
    public static GcsError NotImplemented { get; } = new("NotImplemented", HttpStatus.BadRequest, "A header or query you provided requested a function that is not implemented.");

    /// <summary>
    /// <c>412 PreconditionFailed</c>: a failing <c>If-Match</c>, <c>If-Unmodified-Since</c> or <c>x-goog-if-*</c>.
    /// </summary>
    public static GcsError PreconditionFailed { get; } = new("PreconditionFailed", new HttpStatus(412, "Precondition Failed"), "At least one of the pre-conditions you specified did not hold.");

    /// <summary>
    /// <c>503 SlowDown</c>: a connection past <c>--max-connections</c> or
    /// <c>--max-connections-per-address</c> (decision 7).
    /// </summary>
    public static GcsError SlowDown { get; } = new("SlowDown", HttpStatus.ServiceUnavailable, "Please reduce your request rate.");
}
