using System.Buffers.Binary;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// ADR-0089 decision 4's normalisation for the libcurl drivers' reports (decision 1 (c)), as amended for
/// them: <c>surl</c>'s port as <c>&lt;port&gt;</c>, and what <c>surl</c> draws at random for each RTSP
/// session (ADR-0074 decision 5) - a 16-hex-digit session ID as <c>&lt;session&gt;</c>, and in each
/// interleaved frame the RTP sequence number, timestamp and SSRC, and the RTCP SSRCs, NTP time and RTP
/// timestamp, zeroed. Everything else - every result, status, CSeq, body byte, payload byte, length and
/// count - stays compared.
/// </summary>
public static class CurlPortLibcurlReportNormaliser
{
    /// <summary>What a session ID <c>surl</c> drew is replaced with.</summary>
    public const string SessionPlaceholder = "<session>";

    private const string InterleavedLabel = ", interleaved ";
    private const int InterleavedHeaderLength = 4;
    private const int RtpFixedHeaderLength = 12;
    private const int RtcpHeaderLength = 4;
    private const byte FirstRtcpPacketType = 200;
    private const byte LastRtcpPacketType = 204;
    private const byte SenderReport = 200;
    private const byte Bye = 203;

    // An SR's sender SSRC, NTP timestamp and RTP timestamp: bytes 4 to 20 of the packet (RFC 3550 6.4.1).
    private const int SenderReportRandomEnd = 20;

    /// <summary>
    /// Normalises one report line of a run whose <c>surl</c> listened on <paramref name="surlPort"/>.
    /// </summary>
    /// <param name="line">The line as the driver printed it.</param>
    /// <param name="surlPort">The port that run's <c>surl</c> listened on.</param>
    /// <returns>The normalised line.</returns>
    public static string NormaliseLine(string line, int surlPort)
    {
        ArgumentNullException.ThrowIfNull(line);

        var text = Regex.Replace(line, $"(?<=:){surlPort}(?![0-9])", CurlPortResultNormaliser.PortPlaceholder, RegexOptions.CultureInvariant);
        text = SendCallCount.Replace(text, string.Empty);
        text = SessionId.Replace(text, $"session \"{SessionPlaceholder}\"");
        var interleaved = text.IndexOf(InterleavedLabel, StringComparison.Ordinal);
        return interleaved < 0
            ? text
            : text[..interleaved] + ShownRun.Replace(text[interleaved..], run => LibcurlBytes.Describe(MaskInterleavedFrame(LibcurlBytes.Unescape(run.Groups["shown"].Value))));
    }

    /// <summary>
    /// Zeroes what <c>surl</c> draws at random in one interleaved frame (<c>$</c>, channel, length, packet):
    /// an RTP packet's sequence number, timestamp and SSRC, or in each packet of an RTCP compound its SSRC,
    /// a sender report's NTP and RTP timestamps and a <c>BYE</c>'s SSRCs. A frame that is not whole is
    /// zeroed as far as it goes; bytes that are not an interleaved frame are returned unchanged.
    /// </summary>
    /// <param name="frame">The bytes the interleave callback received.</param>
    /// <returns>A copy with the random fields zeroed.</returns>
    public static byte[] MaskInterleavedFrame(byte[] frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var masked = (byte[])frame.Clone();
        if (masked.Length <= InterleavedHeaderLength + 1 || masked[0] != (byte)'$')
        {
            return masked;
        }

        var packet = masked.AsSpan(InterleavedHeaderLength);
        if (packet[1] is >= FirstRtcpPacketType and <= LastRtcpPacketType)
        {
            MaskRtcpCompound(packet);
        }
        else
        {
            Zero(packet, 2, RtpFixedHeaderLength);
        }

        return masked;
    }

    private static void MaskRtcpCompound(Span<byte> compound)
    {
        var offset = 0;
        while (offset + RtcpHeaderLength <= compound.Length)
        {
            var packet = compound[offset..];
            var end = packet[1] switch
            {
                SenderReport => SenderReportRandomEnd,
                Bye => RtcpHeaderLength + (4 * (packet[0] & 0x1F)),
                _ => RtcpHeaderLength + 4,
            };
            Zero(packet, RtcpHeaderLength, end);
            offset += (BinaryPrimitives.ReadUInt16BigEndian(packet[2..]) + 1) * 4;
        }
    }

    private static void Zero(Span<byte> bytes, int start, int end) =>
        bytes[start..Math.Min(end, bytes.Length)].Clear();

    // A send step's " in <n> calls": how often libcurl took the payload in part, which the socket's
    // buffer space and surl's reading pace decide, not the protocol (ADR-0089 amendment 8).
    private static readonly Regex SendCallCount = new(@"(?<=^send .*, sent [0-9]+) in [0-9]+ calls$", RegexOptions.CultureInvariant);

    private static readonly Regex SessionId = new("""session "[0-9A-F]{16}"(?=,)""", RegexOptions.CultureInvariant);

    private static readonly Regex ShownRun = new(@"\d+ bytes ""(?<shown>(?:[^""\\]|\\.)*)""", RegexOptions.CultureInvariant);
}
