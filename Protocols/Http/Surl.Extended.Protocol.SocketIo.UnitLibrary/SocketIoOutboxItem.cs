namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// One item of a session's <see cref="SocketIoOutbox"/>, written whole: Engine.IO packets that go
/// together (a Socket.IO packet and its attachments), or the WebSocket Pong that answers a client's
/// WebSocket Ping, which only a WebSocket carries (ADR-0120, decision 2 and amendment 1).
/// </summary>
/// <param name="Packets">The Engine.IO packets, in order; empty for a WebSocket Pong.</param>
/// <param name="WebSocketPong">The Pong's payload, or <see langword="null"/> for packets.</param>
internal sealed record SocketIoOutboxItem(IReadOnlyList<EngineIoPacket> Packets, byte[]? WebSocketPong = null)
{
    /// <summary>
    /// The bytes the item counts against the unsent limit: each packet's type character and payload,
    /// or the Pong's payload.
    /// </summary>
    public long Size => WebSocketPong?.Length ?? Packets.Sum(packet => 1L + packet.Data.Length);

    /// <summary>
    /// The item carrying <paramref name="packets"/>.
    /// </summary>
    public static SocketIoOutboxItem Of(params EngineIoPacket[] packets) => new(packets);

    /// <summary>
    /// The item carrying a WebSocket Pong with <paramref name="payload"/>.
    /// </summary>
    public static SocketIoOutboxItem Pong(byte[] payload) => new([], payload);
}
