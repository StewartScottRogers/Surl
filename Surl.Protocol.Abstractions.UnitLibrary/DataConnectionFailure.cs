namespace Surl.Protocol.Abstractions;

/// <summary>
/// Why an FTP data connection could not be opened (ADR-0052, decision 9). The FTP server answers
/// every one of them <c>425 Cannot open data connection</c>.
/// </summary>
public enum DataConnectionFailure
{
    /// <summary>
    /// The exchange has no data connections to give: the <see cref="RefusingDataConnectionOpener"/> default.
    /// </summary>
    Unavailable = 0,

    /// <summary>
    /// The request named an address that is not the control connection's peer, or a port below 1024.
    /// </summary>
    Refused,

    /// <summary>
    /// The active target could not be reached, or no listener could be bound.
    /// </summary>
    Unreachable,

    /// <summary>
    /// No connection opened or arrived within the timeout.
    /// </summary>
    TimedOut,
}
