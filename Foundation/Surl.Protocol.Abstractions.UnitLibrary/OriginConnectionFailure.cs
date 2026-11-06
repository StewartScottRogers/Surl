namespace Surl.Protocol.Abstractions;

/// <summary>
/// Why a proxy's connection to an origin could not be opened (ADR-0088, decision 2). The HTTP
/// proxy answers <see cref="NotAllowed"/> <c>403</c>, <see cref="TimedOut"/> <c>504</c> and every
/// other one <c>502</c>.
/// </summary>
public enum OriginConnectionFailure
{
    /// <summary>
    /// The host name did not resolve to any address.
    /// </summary>
    HostNotFound = 0,

    /// <summary>
    /// The allowed-origin rule admits none of the addresses the host named, or the target is the
    /// proxy's own end point.
    /// </summary>
    NotAllowed,

    /// <summary>
    /// The origin actively refused the connection: nothing listens on its port.
    /// </summary>
    Refused,

    /// <summary>
    /// The origin could not be reached for any other reason.
    /// </summary>
    Unreachable,

    /// <summary>
    /// The name did not resolve, or the origin did not accept, within the timeout.
    /// </summary>
    TimedOut,
}
