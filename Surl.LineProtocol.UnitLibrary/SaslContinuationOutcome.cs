namespace Surl.LineProtocol;

/// <summary>
/// How an attempt to read one SASL continuation line ended (RFC 4954, section 4; RFC 3501,
/// section 6.2.2; ADR-0049, section 6).
/// </summary>
public enum SaslContinuationOutcome
{
    /// <summary>
    /// The line was base64, or empty, and decoded to the response.
    /// </summary>
    ResponseRead,

    /// <summary>
    /// The line was <c>*</c> alone: the client cancelled the exchange.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The line was not base64; whitespace anywhere in it counts as not base64.
    /// </summary>
    NotBase64,

    /// <summary>
    /// The peer closed the connection before a whole line arrived.
    /// </summary>
    Closed,

    /// <summary>
    /// The line was longer than <see cref="Surl.Protocol.Abstractions.ExchangeLimits.MaxLineBytes"/>.
    /// </summary>
    LineTooLong,

    /// <summary>
    /// The head timeout ran out before the line was complete.
    /// </summary>
    HeadTimedOut,
}
