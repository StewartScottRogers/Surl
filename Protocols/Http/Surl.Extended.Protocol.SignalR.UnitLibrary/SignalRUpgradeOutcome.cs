namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// How the hub answered a WebSocket upgrade's <c>id</c> (TransportProtocols.md "WebSockets";
/// ADR-0119, decisions 2, 5 and 7).
/// </summary>
public enum SignalRUpgradeOutcome
{
    /// <summary>
    /// The connection is the upgrade's from now on: a negotiated one not yet taken up, or a new one
    /// for an upgrade that names no <c>id</c>.
    /// </summary>
    TakenUp,

    /// <summary>
    /// The <c>id</c> names no connection waiting for its upgrade: <c>404 Not Found</c>.
    /// </summary>
    NotFound,

    /// <summary>
    /// The <c>id</c> names a connection that already has its WebSocket: <c>409 Conflict</c>.
    /// </summary>
    AlreadyTakenUp,

    /// <summary>
    /// The connection was negotiated by another caller: <c>403 Forbidden</c>.
    /// </summary>
    NegotiatedByAnotherCaller,

    /// <summary>
    /// An upgrade without <c>id</c> found the hub full: <c>503 Service Unavailable</c>.
    /// </summary>
    HubFull,
}
