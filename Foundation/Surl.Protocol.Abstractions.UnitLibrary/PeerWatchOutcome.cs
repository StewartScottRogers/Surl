namespace Surl.Protocol.Abstractions;

/// <summary>
/// How <see cref="IConnection.WatchForPeerCloseAsync"/> ended (ADR-0141, amendment 2, decision 2).
/// </summary>
public enum PeerWatchOutcome
{
    /// <summary>The connection does not watch: the caller carries on as if the client stayed.</summary>
    NotWatched,

    /// <summary>The client half-closed (FIN) and every byte before it is kept for the next read.</summary>
    PeerClosed,

    /// <summary>The client reset the connection.</summary>
    PeerReset,

    /// <summary>The read-ahead buffer filled before the client left; the watch stopped.</summary>
    BufferFull,
}
