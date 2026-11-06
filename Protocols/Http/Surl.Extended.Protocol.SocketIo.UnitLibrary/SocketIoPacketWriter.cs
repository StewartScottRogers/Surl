using System.Buffers;
using System.Globalization;
using System.Text;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// Writes a Socket.IO v5 packet in the default parser's encoding, the text of one Engine.IO
/// <c>message</c> packet (SIO "Packet encoding"; ADR-0120, decisions 5 and 6).
/// </summary>
public static class SocketIoPacketWriter
{
    /// <summary>Writes a packet.</summary>
    /// <param name="packet">The packet.</param>
    /// <returns>
    /// The type digit, the attachment count and <c>-</c> for a binary packet, the namespace and
    /// <c>,</c> when it is not <c>/</c>, the ack id, then the payload byte for byte.
    /// </returns>
    public static byte[] Write(SocketIoPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        var encoded = new ArrayBufferWriter<byte>();
        encoded.Write([(byte)('0' + (int)packet.Type)]);
        if (SocketIoPacket.CarriesAttachments(packet.Type))
        {
            WriteNumber(encoded, packet.AttachmentCount);
            encoded.Write("-"u8);
        }

        if (packet.Namespace != SocketIoPacket.MainNamespace)
        {
            encoded.Write(Encoding.UTF8.GetBytes(packet.Namespace));
            encoded.Write(","u8);
        }

        if (packet.AckId is { } ackId)
        {
            WriteNumber(encoded, ackId);
        }

        encoded.Write(packet.Payload.Span);
        return encoded.WrittenSpan.ToArray();
    }

    private static void WriteNumber(ArrayBufferWriter<byte> encoded, long number) =>
        encoded.Write(Encoding.ASCII.GetBytes(number.ToString(CultureInfo.InvariantCulture)));
}
