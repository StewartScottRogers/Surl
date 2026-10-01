using System.Buffers.Binary;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// Builds the interleaved frames <c>PLAY</c> streams (ADR-0074 decision 5): <c>$</c>, the
/// channel byte, a two-byte big-endian length, then one RTP packet (RFC 3550 section 5.1) or
/// one RTCP compound packet (RFC 3550 sections 6.4.1 and 6.6), as RFC 2326 section 10.12 frames
/// them.
/// </summary>
internal static class RtspInterleavedFrame
{
    /// <summary>
    /// The RTP payload type every packet carries: dynamic type 96, which the session
    /// description names <c>octet-stream</c> (ADR-0074 decision 4).
    /// </summary>
    public const byte PayloadType = 96;

    /// <summary>
    /// The length of the RTP header surl writes: version 2, no padding, no extension, no CSRC.
    /// </summary>
    public const int RtpHeaderBytes = 12;

    /// <summary>
    /// The byte every interleaved frame starts with, which no request head does.
    /// </summary>
    public const byte Marker = (byte)'$';

    /// <summary>
    /// The length of a frame's header: the marker, the channel and the two-byte length.
    /// </summary>
    public const int FrameHeaderBytes = 4;

    private const int RtpVersionShift = 6;

    private const byte PaddingBit = 0x20;

    private const byte ExtensionBit = 0x10;

    private const byte CsrcCountMask = 0x0F;

    private const byte RtpVersion2 = 0x80;

    private const byte MarkerBit = 0x80;

    // Seconds from the NTP epoch, 1900-01-01, to the Unix epoch, 1970-01-01 (RFC 3550 section 4).
    private const long NtpEpochToUnixEpochSeconds = 2_208_988_800;

    /// <summary>
    /// A frame holding one RTP packet.
    /// </summary>
    /// <param name="channel">The interleaved RTP channel.</param>
    /// <param name="sequenceNumber">The packet's sequence number.</param>
    /// <param name="timestamp">The packet's RTP timestamp.</param>
    /// <param name="ssrc">The session's synchronization source.</param>
    /// <param name="isLast">Whether the packet is the presentation's last, which sets the marker bit.</param>
    /// <param name="payload">The packet's payload.</param>
    /// <returns>The frame's bytes.</returns>
    public static byte[] Rtp(byte channel, ushort sequenceNumber, uint timestamp, uint ssrc, bool isLast, ReadOnlySpan<byte> payload)
    {
        var frame = new byte[FrameHeaderBytes + RtpHeaderBytes + payload.Length];
        var packet = WriteFrameHeader(frame, channel);
        packet[0] = RtpVersion2;
        packet[1] = (byte)((isLast ? MarkerBit : 0) | PayloadType);
        BinaryPrimitives.WriteUInt16BigEndian(packet[2..], sequenceNumber);
        BinaryPrimitives.WriteUInt32BigEndian(packet[4..], timestamp);
        BinaryPrimitives.WriteUInt32BigEndian(packet[8..], ssrc);
        payload.CopyTo(packet[RtpHeaderBytes..]);

        return frame;
    }

    /// <summary>
    /// A frame holding the RTCP compound packet sent after a presentation's last RTP packet: a
    /// sender report, then a <c>BYE</c>.
    /// </summary>
    /// <param name="channel">The interleaved RTCP channel.</param>
    /// <param name="ssrc">The session's synchronization source.</param>
    /// <param name="now">The wall-clock time the report is for, written as an NTP timestamp.</param>
    /// <param name="timestamp">The last RTP timestamp sent.</param>
    /// <param name="packetCount">The RTP packets the session has sent.</param>
    /// <param name="octetCount">The payload octets the session has sent.</param>
    /// <returns>The frame's bytes.</returns>
    public static byte[] SenderReportAndBye(byte channel, uint ssrc, DateTimeOffset now, uint timestamp, uint packetCount, uint octetCount)
    {
        var frame = new byte[FrameHeaderBytes + 28 + 8];
        var packet = WriteFrameHeader(frame, channel);
        var sinceNtpEpoch = now.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks + (NtpEpochToUnixEpochSeconds * TimeSpan.TicksPerSecond);
        var seconds = sinceNtpEpoch / TimeSpan.TicksPerSecond;
        var fraction = (sinceNtpEpoch % TimeSpan.TicksPerSecond * (1L << 32)) / TimeSpan.TicksPerSecond;

        BinaryPrimitives.WriteUInt32BigEndian(packet, 0x80C8_0006);
        BinaryPrimitives.WriteUInt32BigEndian(packet[4..], ssrc);
        BinaryPrimitives.WriteUInt32BigEndian(packet[8..], unchecked((uint)seconds));
        BinaryPrimitives.WriteUInt32BigEndian(packet[12..], (uint)fraction);
        BinaryPrimitives.WriteUInt32BigEndian(packet[16..], timestamp);
        BinaryPrimitives.WriteUInt32BigEndian(packet[20..], packetCount);
        BinaryPrimitives.WriteUInt32BigEndian(packet[24..], octetCount);
        BinaryPrimitives.WriteUInt32BigEndian(packet[28..], 0x81CB_0001);
        BinaryPrimitives.WriteUInt32BigEndian(packet[32..], ssrc);

        return frame;
    }

    /// <summary>
    /// Where a recorded RTP packet's payload lies: after the 12-byte header, the CSRC list and
    /// any header extension, and before any padding (RFC 3550 section 5.1; ADR-0074 decision 6).
    /// </summary>
    /// <param name="packet">One RTP packet, the frame's header already taken off.</param>
    /// <returns>The payload's range, possibly empty; <see langword="null"/> when the packet is not
    /// RTP version 2 or is too short for its own header and padding.</returns>
    public static Range? RtpPayload(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < RtpHeaderBytes || packet[0] >> RtpVersionShift != 2)
        {
            return null;
        }

        var start = PayloadStart(packet);
        var end = packet.Length - ((packet[0] & PaddingBit) != 0 ? packet[^1] : 0);

        return start <= end ? start..end : null;
    }

    // After the header and its CSRCs, and after the extension when there is one; past the
    // packet's end when the extension's own header is cut off.
    private static int PayloadStart(ReadOnlySpan<byte> packet)
    {
        var start = RtpHeaderBytes + (4 * (packet[0] & CsrcCountMask));
        if ((packet[0] & ExtensionBit) == 0)
        {
            return start;
        }

        return packet.Length < start + 4
            ? packet.Length + 1
            : start + 4 + (4 * BinaryPrimitives.ReadUInt16BigEndian(packet[(start + 2)..]));
    }

    private static Span<byte> WriteFrameHeader(byte[] frame, byte channel)
    {
        frame[0] = Marker;
        frame[1] = channel;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), (ushort)(frame.Length - FrameHeaderBytes));

        return frame.AsSpan(FrameHeaderBytes);
    }
}
