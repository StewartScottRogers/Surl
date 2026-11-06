using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// ADR-0089 decision 4's rule for the frames a WebSocket client sent: after the last upgrade request
/// on a connection, every frame's payload is unmasked and its masking key zeroed (RFC 6455 section
/// 5.3), because a client picks a fresh random key for every frame. What is left is the frame as it
/// would be with the key <c>00 00 00 00</c>, so two clients sending the same frames compare equal.
/// </summary>
internal static class WebSocketFrameUnmasking
{
    private const int MaskBit = 0x80;
    private const int MaskKeyLength = 4;
    private const int SixteenBitLength = 126;
    private const int SixtyFourBitLength = 127;

    // An HTTP request header block that asks for a WebSocket upgrade (RFC 6455 section 4.1), up to its blank line.
    private static readonly Regex UpgradeRequest = new(
        @"^Upgrade:[ \t]*websocket[ \t]*\r\n(?:[^\r\n]*\r\n)*?\r\n",
        RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    /// <summary>
    /// Returns a copy of <paramref name="requestBytes"/> with every client frame after the last
    /// WebSocket upgrade request unmasked and its masking key zeroed; bytes with no upgrade request
    /// are copied unchanged. A frame cut short is unmasked as far as it goes.
    /// </summary>
    public static byte[] Unmask(byte[] requestBytes)
    {
        var bytes = (byte[])requestBytes.Clone();
        var upgrades = UpgradeRequest.Matches(Encoding.Latin1.GetString(bytes));
        if (upgrades.Count == 0)
        {
            return bytes;
        }

        var last = upgrades[^1];
        for (long at = last.Index + last.Length; at < bytes.Length;)
        {
            at = UnmaskFrame(bytes, (int)at);
        }

        return bytes;
    }

    /// <summary>Unmasks the frame at <paramref name="at"/> and returns where the next frame starts.</summary>
    private static long UnmaskFrame(byte[] bytes, int at)
    {
        var (headerLength, payloadLength) = ReadLengths(bytes, at);
        var payloadStart = (long)at + headerLength;
        if (headerLength == 0 || (bytes[at + 1] & MaskBit) == 0)
        {
            return headerLength == 0 ? bytes.Length : payloadStart + payloadLength;
        }

        var keyStart = (int)payloadStart;
        payloadStart += MaskKeyLength;
        if (payloadStart > bytes.Length)
        {
            bytes.AsSpan(keyStart).Clear();
            return bytes.Length;
        }

        var key = bytes.AsSpan(keyStart, MaskKeyLength).ToArray();
        bytes.AsSpan(keyStart, MaskKeyLength).Clear();
        var end = Math.Min(bytes.Length, payloadStart + payloadLength);
        for (var index = payloadStart; index < end; index++)
        {
            bytes[index] ^= key[(index - payloadStart) % MaskKeyLength];
        }

        return payloadStart + payloadLength;
    }

    /// <summary>
    /// The frame header's length before the masking key, and the payload length it states; a header
    /// length of 0 when the header is cut short.
    /// </summary>
    private static (int HeaderLength, long PayloadLength) ReadLengths(byte[] bytes, int at)
    {
        if (at + 2 > bytes.Length)
        {
            return (0, 0);
        }

        var length = bytes[at + 1] & ~MaskBit;
        var extended = length switch
        {
            SixteenBitLength => 2,
            SixtyFourBitLength => 8,
            _ => 0,
        };
        if (at + 2 + extended > bytes.Length)
        {
            return (0, 0);
        }

        var span = bytes.AsSpan(at + 2, extended);
        long payloadLength = extended switch
        {
            2 => BinaryPrimitives.ReadUInt16BigEndian(span),
            8 => (long)Math.Min(BinaryPrimitives.ReadUInt64BigEndian(span), long.MaxValue / 2),
            _ => length,
        };
        return (2 + extended, payloadLength);
    }
}
