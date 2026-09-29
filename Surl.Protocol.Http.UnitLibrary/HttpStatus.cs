namespace Surl.Protocol.Http;

/// <summary>
/// A status code and the reason phrase sent with it (RFC 9110, section 15).
/// </summary>
/// <param name="Code">The three-digit status code.</param>
/// <param name="ReasonPhrase">The reason phrase RFC 9110 gives the code.</param>
internal sealed record HttpStatus(int Code, string ReasonPhrase)
{
    /// <summary>
    /// 200 OK: the file follows.
    /// </summary>
    public static HttpStatus Ok { get; } = new(200, "OK");

    /// <summary>
    /// 400 Bad Request: a malformed request head, or a missing or repeated <c>Host</c>.
    /// </summary>
    public static HttpStatus BadRequest { get; } = new(400, "Bad Request");

    /// <summary>
    /// 404 Not Found: nothing to serve at the path, or a path refused or hidden.
    /// </summary>
    public static HttpStatus NotFound { get; } = new(404, "Not Found");

    /// <summary>
    /// 405 Method Not Allowed: a method RFC 9110 defines that the content store refuses.
    /// </summary>
    public static HttpStatus MethodNotAllowed { get; } = new(405, "Method Not Allowed");

    /// <summary>
    /// 408 Request Timeout: the head timeout ran out part way through a head.
    /// </summary>
    public static HttpStatus RequestTimeout { get; } = new(408, "Request Timeout");

    /// <summary>
    /// 413 Content Too Large: a request body past the upload limit.
    /// </summary>
    public static HttpStatus ContentTooLarge { get; } = new(413, "Content Too Large");

    /// <summary>
    /// 431 Request Header Fields Too Large: the head grew past its limit.
    /// </summary>
    public static HttpStatus RequestHeaderFieldsTooLarge { get; } = new(431, "Request Header Fields Too Large");

    /// <summary>
    /// 501 Not Implemented: a method the server does not know.
    /// </summary>
    public static HttpStatus NotImplemented { get; } = new(501, "Not Implemented");

    /// <summary>
    /// 503 Service Unavailable: a connection past a connection limit (ADR-0006, section 5).
    /// </summary>
    public static HttpStatus ServiceUnavailable { get; } = new(503, "Service Unavailable");

    /// <summary>
    /// 505 HTTP Version Not Supported: a major version other than 1.
    /// </summary>
    public static HttpStatus HttpVersionNotSupported { get; } = new(505, "HTTP Version Not Supported");
}
