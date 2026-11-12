namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// A connection a negotiate response promised and no WebSocket has taken up yet (TransportProtocols.md
/// "negotiate"; ADR-0119, decisions 2 and 5).
/// </summary>
/// <param name="ConnectionId">The <c>connectionId</c> sent: 16 random bytes, base64url without padding.</param>
/// <param name="ConnectionToken">
/// The <c>connectionToken</c> sent, independent of <paramref name="ConnectionId"/>; <see langword="null"/>
/// for a version 0 negotiate, whose client upgrades with the <c>connectionId</c>.
/// </param>
/// <param name="AccountName">
/// The account that negotiated it, the only one that may upgrade it; <see langword="null"/> for the
/// anonymous caller.
/// </param>
/// <param name="ExpiresAt">When it is forgotten if no upgrade has taken it up: <c>--head-timeout</c> after negotiate; once its WebSocket is lost, 30 s later (stateful reconnect).</param>
/// <param name="Reconnect">The numbering that survives a lost WebSocket, or <see langword="null"/> when the negotiate did not ask for stateful reconnect.</param>
public sealed record SignalRNegotiatedConnection(
    string ConnectionId,
    string? ConnectionToken,
    string? AccountName,
    DateTimeOffset ExpiresAt,
    SignalRReconnectState? Reconnect = null)
{
    /// <summary>
    /// The <c>id</c> a client's upgrade names this connection by: the token for version 1, the
    /// connection ID for version 0.
    /// </summary>
    public string UpgradeId => ConnectionToken ?? ConnectionId;

    /// <summary>
    /// What the request that follows a Server-Sent Events <c>GET</c> or a first Long Polling poll finds: the
    /// posted payloads and, for Long Polling, its transport. <see langword="null"/> for a WebSocket connection.
    /// </summary>
    internal SignalRHttpTransportState? HttpTransport { get; set; }
}
