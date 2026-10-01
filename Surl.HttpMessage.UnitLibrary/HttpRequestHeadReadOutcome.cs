namespace Surl.HttpMessage;

/// <summary>
/// How reading one request head from a connection ended.
/// </summary>
public enum HttpRequestHeadReadOutcome
{
    /// <summary>
    /// A complete, well-formed HTTP/1.x request head was read.
    /// </summary>
    HeadRead,

    /// <summary>
    /// The client closed the connection before sending any byte of a head, or after only
    /// empty lines (which RFC 9112, section 2.2, says to ignore): the normal end of a
    /// persistent connection.
    /// </summary>
    ConnectionClosed,

    /// <summary>
    /// The client closed the connection part way through a head.
    /// </summary>
    ConnectionClosedBeforeHeadEnded,

    /// <summary>
    /// The request line is not <c>method SP request-target SP version</c>, with a version
    /// naming the protocol the reader was given
    /// (RFC 9112, section 3; RFC 2326, section 6.1).
    /// </summary>
    MalformedRequestLine,

    /// <summary>
    /// The request line names a well-formed version whose major version is not 1, such
    /// as <c>HTTP/2.0</c>.
    /// </summary>
    UnsupportedVersion,

    /// <summary>
    /// A header field line is not <c>field-name ":" OWS field-value OWS</c>, or is an
    /// obsolete line folding (RFC 9112, sections 5 and 5.2).
    /// </summary>
    MalformedHeaderField,

    /// <summary>
    /// Whitespace sits between a field name and its colon, which RFC 9112, section 5.1,
    /// requires a server to reject.
    /// </summary>
    WhitespaceBeforeColon,

    /// <summary>
    /// The head grew past the request-head limit
    /// (<see cref="Surl.Protocol.Abstractions.ExchangeLimits.MaxRequestHeadBytes"/>) without
    /// ending.
    /// </summary>
    HeadTooLarge,

    /// <summary>
    /// The head timeout (<see cref="Surl.Protocol.Abstractions.ExchangeLimits.HeadTimeout"/>)
    /// ran out after some bytes of the head had arrived, but before its end.
    /// </summary>
    HeadTimedOut,

    /// <summary>
    /// The head timeout ran out before any byte of a head arrived: a connection opened and
    /// never used.
    /// </summary>
    HeadTimedOutBeforeAnyByte,
}
