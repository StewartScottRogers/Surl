namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// Which transport carries a session (EIO "Transports", "Upgrade"; ADR-0120, decision 2).
/// </summary>
internal enum SocketIoTransportState
{
    /// <summary>HTTP long-polling, with no WebSocket probing.</summary>
    Polling,

    /// <summary>HTTP long-polling, while a WebSocket was upgraded and has not yet sent <c>2probe</c>.</summary>
    Probing,

    /// <summary>HTTP long-polling after <c>2probe</c> was answered: every poll is answered <c>6</c> (noop).</summary>
    Probed,

    /// <summary>The WebSocket, connected directly or moved to with <c>5</c>; polling requests are code 3.</summary>
    WebSocket,
}
