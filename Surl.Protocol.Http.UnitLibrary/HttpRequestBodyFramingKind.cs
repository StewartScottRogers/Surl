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
    /// A framing the server does not read: another transfer coding, a coding list, both
    /// fields, or a <c>Content-Length</c> that is not one number. The body is never read, so
    /// the connection closes after the response.
    /// </summary>
    Unreadable,
}
