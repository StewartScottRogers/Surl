namespace Surl.LineProtocol;

/// <summary>
/// How an attempt to read one dot-stuffed message body ended (RFC 5321, section 4.5.2).
/// </summary>
public enum DotStuffedBodyReadOutcome
{
    /// <summary>
    /// The body ended at CRLF <c>.</c> CRLF and every byte of it was written.
    /// </summary>
    BodyRead,

    /// <summary>
    /// The peer closed the connection before the body ended.
    /// </summary>
    Closed,

    /// <summary>
    /// The body grew past <see cref="Surl.Protocol.Abstractions.ExchangeLimits.MaxUploadBytes"/>;
    /// nothing more was read, so the end of the body was not found.
    /// </summary>
    BodyTooLarge,
}
