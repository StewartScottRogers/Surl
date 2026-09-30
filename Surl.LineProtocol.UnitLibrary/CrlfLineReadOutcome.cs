namespace Surl.LineProtocol;

/// <summary>
/// How an attempt to read one CRLF-ended line ended (ADR-0050, decision 8).
/// </summary>
public enum CrlfLineReadOutcome
{
    /// <summary>
    /// A whole line, ended by CRLF, was read.
    /// </summary>
    LineRead,

    /// <summary>
    /// The peer closed the connection before a whole line arrived; no partial line is returned.
    /// </summary>
    Closed,

    /// <summary>
    /// <see cref="Surl.Protocol.Abstractions.ExchangeLimits.MaxLineBytes"/> bytes are buffered
    /// and no CRLF is among them; no byte past the limit was read.
    /// </summary>
    LineTooLong,

    /// <summary>
    /// The head timeout ran out before the line was complete.
    /// </summary>
    HeadTimedOut,
}
