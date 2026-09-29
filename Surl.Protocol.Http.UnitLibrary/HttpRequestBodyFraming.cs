using System.Globalization;

namespace Surl.Protocol.Http;

/// <summary>
/// How a request says its body is framed (RFC 9112, section 6.3), as far as the server reads
/// request bodies.
/// </summary>
/// <param name="Kind">Which framing the head declares.</param>
/// <param name="ContentLength">The declared length, when <paramref name="Kind"/> is <see cref="HttpRequestBodyFramingKind.ContentLength"/>; otherwise 0.</param>
internal readonly record struct HttpRequestBodyFraming(HttpRequestBodyFramingKind Kind, long ContentLength)
{
    /// <summary>
    /// Reads the framing <paramref name="head"/> declares.
    /// </summary>
    /// <remarks>
    /// <c>Transfer-Encoding</c> is <see cref="HttpRequestBodyFramingKind.Chunked"/> only when
    /// it is one field whose one coding is <c>chunked</c>, no <c>Content-Length</c> comes
    /// with it, and the request is HTTP/1.1: RFC 9112, section 6.1, makes an HTTP/1.0
    /// message with <c>Transfer-Encoding</c> badly framed. <c>Content-Length</c> is <see cref="HttpRequestBodyFramingKind.ContentLength"/>
    /// only when it is one field of decimal digits that fits a 64-bit length, and
    /// <c>Content-Length: 0</c> is no body. Every other combination is
    /// <see cref="HttpRequestBodyFramingKind.Unreadable"/>.
    /// </remarks>
    /// <param name="head">The request head.</param>
    /// <returns>The declared framing.</returns>
    public static HttpRequestBodyFraming Of(HttpRequestHead head)
    {
        var transferEncodings = head.GetFieldValues("Transfer-Encoding");
        var contentLengths = head.GetFieldValues("Content-Length");

        if (transferEncodings.Count > 0)
        {
            return head.Version.Minor >= 1 && IsChunkedOnly(transferEncodings, contentLengths) ? Declared(HttpRequestBodyFramingKind.Chunked) : Declared(HttpRequestBodyFramingKind.Unreadable);
        }

        return contentLengths.Count switch
        {
            0 => Declared(HttpRequestBodyFramingKind.None),
            1 => OfContentLength(contentLengths[0]),
            _ => Declared(HttpRequestBodyFramingKind.Unreadable),
        };
    }

    private static HttpRequestBodyFraming Declared(HttpRequestBodyFramingKind kind) => new(kind, 0);

    private static bool IsChunkedOnly(IReadOnlyList<string> transferEncodings, IReadOnlyList<string> contentLengths) =>
        transferEncodings.Count == 1
        && contentLengths.Count == 0
        && string.Equals(transferEncodings[0], "chunked", StringComparison.OrdinalIgnoreCase);

    // NumberStyles.None: digits only, no sign and no whitespace.
    private static HttpRequestBodyFraming OfContentLength(string value)
    {
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var length))
        {
            return Declared(HttpRequestBodyFramingKind.Unreadable);
        }

        return length == 0 ? Declared(HttpRequestBodyFramingKind.None) : new HttpRequestBodyFraming(HttpRequestBodyFramingKind.ContentLength, length);
    }
}
