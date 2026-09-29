namespace Surl.Protocol.Http;

/// <summary>
/// Which body framing a request head declares (RFC 9112, section 6.3).
/// </summary>
internal enum HttpRequestBodyFramingKind
{
    /// <summary>
    /// No body: neither <c>Transfer-Encoding</c> nor a <c>Content-Length</c> other than 0.
    /// </summary>
    None,

    /// <summary>
    /// A body of the length one valid <c>Content-Length</c> field gives.
    /// </summary>
    ContentLength,

    /// <summary>
    /// A chunked body: <c>Transfer-Encoding: chunked</c> and nothing else.
    /// </summary>
    Chunked,

    /// <summary>
    /// A framing the server does not read: another transfer coding, a coding list, two
    /// <c>Transfer-Encoding</c> fields, <c>Transfer-Encoding</c> with <c>Content-Length</c>,
    /// or <c>Transfer-Encoding</c> on HTTP/1.0. The body is never read as a body, so the
    /// connection closes after the response.
    /// </summary>
    Unreadable,

    /// <summary>
    /// No <c>Transfer-Encoding</c>, and a <c>Content-Length</c> that is not one field of
    /// decimal digits fitting a 64-bit length: RFC 9112, section 6.3, item 5, makes it an
    /// unrecoverable framing error, answered <c>400 Bad Request</c> and closed.
    /// </summary>
    InvalidContentLength,
}
