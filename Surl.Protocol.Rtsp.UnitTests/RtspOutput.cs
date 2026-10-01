using System.Buffers.Binary;
using System.Text;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// What an RTSP server wrote, split into response heads and interleaved frames (RFC 2326
/// section 10.12). Bodies are not expected: no test that reads output this way sends a
/// <c>DESCRIBE</c>.
/// </summary>
internal static class RtspOutput
{
    public static List<object> Parse(byte[] written)
    {
        var items = new List<object>();
        var offset = 0;
        while (offset < written.Length)
        {
            if (written[offset] == '$')
            {
                var length = BinaryPrimitives.ReadUInt16BigEndian(written.AsSpan(offset + 2));
                items.Add(new Frame(written[offset + 1], written.AsSpan(offset + 4, length).ToArray()));
                offset += 4 + length;
            }
            else
            {
                var end = Encoding.Latin1.GetString(written, offset, written.Length - offset).IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4;
                items.Add(Encoding.Latin1.GetString(written, offset, end));
                offset += end;
            }
        }

        return items;
    }

    public static IReadOnlyList<string> Heads(byte[] written) => Parse(written).OfType<string>().ToList();

    public static int FramesOn(byte[] written, byte channel) => Parse(written).OfType<Frame>().Count(frame => frame.Channel == channel);

    /// <summary>
    /// One interleaved frame: its channel and the packet it carries.
    /// </summary>
    public sealed record Frame(byte Channel, byte[] Packet)
    {
        public bool Marker => (Packet[1] & 0x80) != 0;

        public int PayloadType => Packet[1] & 0x7F;

        public ushort SequenceNumber => BinaryPrimitives.ReadUInt16BigEndian(Packet.AsSpan(2));

        public uint Timestamp => BinaryPrimitives.ReadUInt32BigEndian(Packet.AsSpan(4));

        public uint Ssrc => BinaryPrimitives.ReadUInt32BigEndian(Packet.AsSpan(8));

        public byte[] Payload => Packet[12..];

        public uint Word(int index) => BinaryPrimitives.ReadUInt32BigEndian(Packet.AsSpan(index * 4));
    }
}
