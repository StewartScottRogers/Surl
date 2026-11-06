using System.Buffers;
using System.Buffers.Text;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// Reads and writes one Engine.IO v4 packet as a WebSocket message and as one packet of an HTTP
/// long-polling payload (EIO "Packet encoding"; ADR-0120, decision 2).
/// </summary>
/// <remarks>
/// A text packet is its type character <c>0</c> to <c>6</c> then its payload in both transports. A
/// binary <c>message</c> is the bare bytes of a binary WebSocket message, and <c>b</c> then the bytes in
/// base64 inside a polling payload.
/// </remarks>
public static class EngineIoPacketCodec
{
    /// <summary>The character that starts a base64 binary packet in a polling payload.</summary>
    public const byte BinaryMarker = (byte)'b';

    /// <summary>Reads a text WebSocket message, or one text packet of a polling payload.</summary>
    /// <param name="text">The message's bytes.</param>
    /// <param name="packet">The packet read, or <see langword="null"/> when the message is malformed.</param>
    /// <returns>
    /// <see langword="false"/> when the message is empty or its first byte is not a type character
    /// <c>0</c> to <c>6</c>: a protocol error.
    /// </returns>
    public static bool TryReadText(ReadOnlyMemory<byte> text, out EngineIoPacket? packet)
    {
        packet = null;
        if (text.IsEmpty || !IsTypeCharacter(text.Span[0]))
        {
            return false;
        }

        packet = new EngineIoPacket((EngineIoPacketType)(text.Span[0] - '0'), text[1..], IsBinary: false);
        return true;
    }

    /// <summary>Reads a binary WebSocket message, which is always a binary <c>message</c> packet.</summary>
    /// <param name="data">The message's bytes.</param>
    /// <returns>The packet.</returns>
    public static EngineIoPacket ReadBinary(ReadOnlyMemory<byte> data) => EngineIoPacket.Binary(data);

    /// <summary>Writes a packet as the bytes of one WebSocket message.</summary>
    /// <param name="packet">The packet.</param>
    /// <returns>
    /// The type character then the payload for a text packet, to send as a text message; the bare
    /// data for a binary packet, to send as a binary message.
    /// </returns>
    public static byte[] WriteWebSocketMessage(EngineIoPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        return packet.IsBinary ? packet.Data.ToArray() : WriteText(packet);
    }

    /// <summary>Reads one packet of a polling payload, text or <c>b</c>-prefixed base64.</summary>
    /// <param name="encoded">The packet's bytes, without the <c>0x1E</c> separators.</param>
    /// <param name="packet">The packet read, or <see langword="null"/> when it is malformed.</param>
    /// <returns><see langword="false"/> when the packet is malformed: a protocol error.</returns>
    public static bool TryReadPollingPacket(ReadOnlyMemory<byte> encoded, out EngineIoPacket? packet)
    {
        if (encoded.IsEmpty || encoded.Span[0] != BinaryMarker)
        {
            return TryReadText(encoded, out packet);
        }

        packet = null;
        var base64 = encoded.Span[1..];
        var decoded = new byte[Base64.GetMaxDecodedFromUtf8Length(base64.Length)];
        if (Base64.DecodeFromUtf8(base64, decoded, out _, out var written) != OperationStatus.Done)
        {
            return false;
        }

        packet = EngineIoPacket.Binary(decoded.AsMemory(0, written));
        return true;
    }

    /// <summary>Writes one packet of a polling payload.</summary>
    /// <param name="packet">The packet.</param>
    /// <returns>The type character then the payload, or <c>b</c> then the binary data in base64.</returns>
    public static byte[] WritePollingPacket(EngineIoPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (!packet.IsBinary)
        {
            return WriteText(packet);
        }

        var encoded = new byte[1 + Base64.GetMaxEncodedToUtf8Length(packet.Data.Length)];
        encoded[0] = BinaryMarker;
        Base64.EncodeToUtf8(packet.Data.Span, encoded.AsSpan(1), out _, out _);
        return encoded;
    }

    private static byte[] WriteText(EngineIoPacket packet)
    {
        var encoded = new byte[1 + packet.Data.Length];
        encoded[0] = (byte)('0' + (int)packet.Type);
        packet.Data.Span.CopyTo(encoded.AsSpan(1));
        return encoded;
    }

    private static bool IsTypeCharacter(byte value) => value is >= (byte)'0' and <= (byte)'6';
}
