namespace Surl.Protocol.Socks;

/// <summary>
/// How reading a SOCKS4 request ended (ADR-0121 decisions 2 and 6).
/// </summary>
internal enum Socks4ReadOutcome
{
    /// <summary>
    /// The whole request was read.
    /// </summary>
    Read = 0,

    /// <summary>
    /// curl closed its end before the request's last byte: the connection is closed without a reply.
    /// </summary>
    Truncated,

    /// <summary>
    /// The user ID or the SOCKS4a host name ran past <see cref="Socks4RequestReader.MaxNameBytes"/>
    /// without its terminating zero: the request is answered <c>5B</c> without reading further.
    /// </summary>
    NameTooLong,
}
