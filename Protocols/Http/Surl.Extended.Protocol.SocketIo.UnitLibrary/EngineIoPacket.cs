using System.Text;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// One Engine.IO v4 packet: its type and its payload, which is text unless the packet is a binary
/// <c>message</c> (EIO "Protocol", "Packet encoding"; ADR-0120, decision 2).
/// </summary>
/// <param name="Type">The packet type.</param>
/// <param name="Data">The payload: the UTF-8 text after the type character, or the binary data.</param>
/// <param name="IsBinary">
/// <see langword="true"/> for a binary <c>message</c>, which has no type character on a WebSocket and is
/// <c>b</c> plus base64 in a polling payload.
/// </param>
public sealed record EngineIoPacket(EngineIoPacketType Type, ReadOnlyMemory<byte> Data, bool IsBinary)
{
    /// <summary>Makes a text packet of <paramref name="type"/> carrying <paramref name="text"/> as UTF-8.</summary>
    /// <param name="type">The packet type.</param>
    /// <param name="text">The payload text, empty for none.</param>
    /// <returns>The packet.</returns>
    public static EngineIoPacket Text(EngineIoPacketType type, string text) =>
        new(type, Encoding.UTF8.GetBytes(text), IsBinary: false);

    /// <summary>Makes a binary <c>message</c> packet carrying <paramref name="data"/>.</summary>
    /// <param name="data">The binary data.</param>
    /// <returns>The packet.</returns>
    public static EngineIoPacket Binary(ReadOnlyMemory<byte> data) =>
        new(EngineIoPacketType.Message, data, IsBinary: true);
}
