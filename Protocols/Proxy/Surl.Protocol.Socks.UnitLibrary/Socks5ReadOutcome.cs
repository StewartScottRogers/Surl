namespace Surl.Protocol.Socks;

/// <summary>
/// How reading a SOCKS5 request ended (ADR-0121 decision 3).
/// </summary>
internal enum Socks5ReadOutcome
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
    /// The request's <c>VER</c> is not <c>05</c>: the connection is closed without a reply.
    /// </summary>
    WrongVersion,

    /// <summary>
    /// <c>ATYP</c> is not <c>01</c>, <c>03</c> or <c>04</c>: the request is answered <c>08</c>
    /// without reading further.
    /// </summary>
    AddressTypeNotSupported,
}
