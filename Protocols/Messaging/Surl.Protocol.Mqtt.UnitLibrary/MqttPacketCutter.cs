namespace Surl.Protocol.Mqtt;

/// <summary>
/// Gathers the bytes of one direction of an MQTT connection and cuts whole packets from them, by
/// the fixed header's remaining length (MQTT 3.1.1 section 2.2.3). Once a remaining length runs
/// past its fourth byte, the direction is malformed and nothing more is cut.
/// </summary>
internal sealed class MqttPacketCutter
{
    private const int MaxRemainingLengthBytes = 4;

    private readonly List<byte> pending = [];
    private bool isMalformed;

    /// <summary>
    /// Adds <paramref name="bytes"/> and cuts every packet now whole.
    /// </summary>
    /// <param name="bytes">The bytes that crossed next.</param>
    /// <returns>Each whole packet, fixed header included, with its fixed header's length.</returns>
    public List<(byte[] Packet, int HeaderLength)> Cut(ReadOnlySpan<byte> bytes)
    {
        var packets = new List<(byte[], int)>();
        if (isMalformed)
        {
            return packets;
        }

        pending.AddRange(bytes);
        while (TryMeasure(out var headerLength, out var packetLength) && pending.Count >= packetLength)
        {
            packets.Add((pending.GetRange(0, packetLength).ToArray(), headerLength));
            pending.RemoveRange(0, packetLength);
        }

        return packets;
    }

    private bool TryMeasure(out int headerLength, out int packetLength)
    {
        (headerLength, packetLength) = (0, 0);
        var remaining = 0;
        for (var index = 1; index < pending.Count && !isMalformed; index++)
        {
            remaining |= (pending[index] & 0x7F) << (7 * (index - 1));
            if ((pending[index] & 0x80) == 0)
            {
                (headerLength, packetLength) = (index + 1, index + 1 + remaining);
                return true;
            }

            isMalformed = index == MaxRemainingLengthBytes;
        }

        return false;
    }
}
