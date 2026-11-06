namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// The Engine.IO v4 packet types, numbered as their type character numbers them on the wire
/// (EIO "Protocol"; ADR-0120, decision 2).
/// </summary>
public enum EngineIoPacketType
{
    /// <summary><c>0</c> open: the server's first packet of a session, carrying the handshake data.</summary>
    Open = 0,

    /// <summary><c>1</c> close: ends the session.</summary>
    Close = 1,

    /// <summary><c>2</c> ping: the heartbeat's question, or <c>2probe</c> during an upgrade.</summary>
    Ping = 2,

    /// <summary><c>3</c> pong: the heartbeat's answer, or <c>3probe</c> during an upgrade.</summary>
    Pong = 3,

    /// <summary><c>4</c> message: carries a Socket.IO packet, or binary data.</summary>
    Message = 4,

    /// <summary><c>5</c> upgrade: moves a session from HTTP long-polling to WebSocket.</summary>
    Upgrade = 5,

    /// <summary><c>6</c> noop: does nothing; answers a held poll during an upgrade.</summary>
    Noop = 6,
}
