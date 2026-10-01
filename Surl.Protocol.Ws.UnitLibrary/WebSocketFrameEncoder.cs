using System.Buffers.Binary;
using System.Text;

namespace Surl.Protocol.Ws;

/// <summary>
/// Encodes the frames a server sends (RFC 6455 section 5.2): never masked (section 5.1), with the
/// payload length in its minimal encoding.
/// </summary>
internal static class WebSocketFrameEncoder
{
    /// <summary>The most bytes a control frame's payload may hold (section 5.5).</summary>
    public const int MaxControlPayloadBytes = 125;

    /// <summary>The most bytes a close frame's reason may hold: a control payload less its two-byte code.</summary>
    public const int MaxCloseReasonBytes = MaxControlPayloadBytes - 2;

    private const int MaxSevenBitLength = 125;
    private const byte SixteenBitLengthMarker = 126;
    private const byte SixtyFourBitLengthMarker = 127;

    /// <summary>
    /// Encodes one unmasked frame with no <c>RSV</c> bit set.
    /// </summary>
    /// <param name="fin">Whether the frame ends its message.</param>
    /// <param name="opcode">What the frame carries.</param>
    /// <param name="payload">The payload.</param>
    /// <returns>The frame's bytes.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="opcode"/> is a control opcode and <paramref name="fin"/> is clear or
    /// <paramref name="payload"/> is over <see cref="MaxControlPayloadBytes"/> bytes.
    /// </exception>
    public static byte[] EncodeFrame(bool fin, WebSocketOpcode opcode, ReadOnlySpan<byte> payload)
    {
        if (WebSocketOpcodes.IsControl(opcode) && (!fin || payload.Length > MaxControlPayloadBytes))
        {
            throw new ArgumentException(
                $"A control frame must have FIN set and a payload of at most {MaxControlPayloadBytes} bytes (RFC 6455 section 5.5).",
                nameof(payload));
        }

        var lengthBytes = ExtendedLengthBytes(payload.Length);
        var frame = new byte[2 + lengthBytes + payload.Length];
        frame[0] = (byte)((fin ? 0x80 : 0x00) | (byte)opcode);
        WriteLength(frame, payload.Length, lengthBytes);
        payload.CopyTo(frame.AsSpan(2 + lengthBytes));
        return frame;
    }

    /// <summary>
    /// Encodes a close frame carrying <paramref name="closeCode"/> and <paramref name="reason"/> (section 5.5.1).
    /// </summary>
    /// <param name="closeCode">The status code; one section 7.4 allows on the wire.</param>
    /// <param name="reason">The reason, sent as UTF-8; may be empty.</param>
    /// <returns>The close frame's bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="closeCode"/> is not allowed on the wire.</exception>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is over <see cref="MaxCloseReasonBytes"/> bytes as UTF-8.</exception>
    public static byte[] EncodeClose(ushort closeCode, string reason)
    {
        if (!WebSocketCloseCodes.IsAllowedOnTheWire(closeCode))
        {
            throw new ArgumentOutOfRangeException(nameof(closeCode), closeCode, "RFC 6455 section 7.4 does not allow this close code on the wire.");
        }

        var reasonBytes = Encoding.UTF8.GetBytes(reason);
        if (reasonBytes.Length > MaxCloseReasonBytes)
        {
            throw new ArgumentException($"A close reason may hold at most {MaxCloseReasonBytes} UTF-8 bytes.", nameof(reason));
        }

        var payload = new byte[2 + reasonBytes.Length];
        BinaryPrimitives.WriteUInt16BigEndian(payload, closeCode);
        reasonBytes.CopyTo(payload, 2);
        return EncodeFrame(fin: true, WebSocketOpcode.Close, payload);
    }

    private static int ExtendedLengthBytes(int payloadLength) => payloadLength switch
    {
        <= MaxSevenBitLength => 0,
        <= ushort.MaxValue => 2,
        _ => 8,
    };

    private static void WriteLength(byte[] frame, int payloadLength, int lengthBytes)
    {
        switch (lengthBytes)
        {
            case 0:
                frame[1] = (byte)payloadLength;
                break;
            case 2:
                frame[1] = SixteenBitLengthMarker;
                BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), (ushort)payloadLength);
                break;
            default:
                frame[1] = SixtyFourBitLengthMarker;
                BinaryPrimitives.WriteUInt64BigEndian(frame.AsSpan(2), (ulong)payloadLength);
                break;
        }
    }
}
