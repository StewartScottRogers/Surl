using System.Buffers;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// Joins Engine.IO packets into the body of an HTTP long-polling response or request, and splits
/// one back into packets: the packets are separated by the record separator <c>0x1E</c> (EIO "Packet
/// encoding", "HTTP long-polling"; ADR-0120, decision 2).
/// </summary>
public static class EngineIoPollingPayload
{
    /// <summary>The byte between two packets of a polling payload.</summary>
    public const byte Separator = 0x1E;

    /// <summary>Joins packets into one polling payload.</summary>
    /// <param name="packets">The packets, in order.</param>
    /// <returns>Each packet's polling encoding, separated by <c>0x1E</c>.</returns>
    public static byte[] Join(IReadOnlyList<EngineIoPacket> packets)
    {
        ArgumentNullException.ThrowIfNull(packets);
        var payload = new ArrayBufferWriter<byte>();
        for (var index = 0; index < packets.Count; index++)
        {
            if (index > 0)
            {
                payload.Write([Separator]);
            }

            payload.Write(EngineIoPacketCodec.WritePollingPacket(packets[index]));
        }

        return payload.WrittenSpan.ToArray();
    }

    /// <summary>Splits a polling payload into its packets.</summary>
    /// <param name="payload">The payload.</param>
    /// <param name="packets">Where each packet is added, in order.</param>
    /// <returns><see langword="false"/> when any packet is malformed: a protocol error.</returns>
    public static bool TrySplit(ReadOnlyMemory<byte> payload, List<EngineIoPacket> packets)
    {
        ArgumentNullException.ThrowIfNull(packets);
        var rest = payload;
        while (true)
        {
            var end = rest.Span.IndexOf(Separator);
            var encoded = end < 0 ? rest : rest[..end];
            if (!EngineIoPacketCodec.TryReadPollingPacket(encoded, out var packet))
            {
                return false;
            }

            packets.Add(packet!);
            if (end < 0)
            {
                return true;
            }

            rest = rest[(end + 1)..];
        }
    }
}
