using System.Buffers.Binary;
using System.Text.Unicode;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ws;

/// <summary>
/// Reads RFC 6455 frames from one connection, one after another (section 5.2), unmasking each
/// payload (section 5.3).
/// </summary>
/// <remarks>
/// The reader never reads a byte past the frame it is reading, so it can refuse a frame over the
/// frame limit from its header alone, without reading any of its payload (ADR-0006: the maximum
/// framed message bounds a WebSocket frame, counted header included, as it bounds an MQTT packet).
/// It checks only what makes a frame invalid whatever the server wants - reserved opcodes, the
/// control frame rules, the length encoding and the close payload - and reports the <c>RSV</c>
/// bits and the <c>MASK</c> bit for the caller to judge. The payload's buffer grows as its bytes
/// arrive rather than being allocated at the announced length, so a length the peer never sends
/// costs at most 64 KiB and at most twice what did arrive. It is not safe for concurrent calls,
/// and after any outcome but <see cref="WebSocketFrameReadOutcome.FrameRead"/> the caller stops
/// reading.
/// </remarks>
internal sealed class WebSocketFrameReader
{
    private const int FirstTwoBytes = 2;
    private const int SixteenBitLengthBytes = 2;
    private const int SixtyFourBitLengthBytes = 8;
    private const int MaskingKeyBytes = 4;
    private const int MaxControlPayloadBytes = 125;
    private const int SixteenBitLengthMarker = 126;
    private const int SixtyFourBitLengthMarker = 127;
    private const int CloseCodeBytes = 2;

    /// <summary>
    /// How much of a payload is allocated before any of it arrives. The buffer doubles as bytes
    /// come, so a peer that announces a large length and sends nothing costs no more.
    /// </summary>
    private const int InitialPayloadBytes = 65536;

    private readonly IConnection connection;
    private readonly long maxFrameBytes;
    private readonly byte[] headerBytes = new byte[SixtyFourBitLengthBytes];
    private readonly byte[] maskingKey = new byte[MaskingKeyBytes];

    /// <summary>
    /// Creates a reader over <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">The connection to read from.</param>
    /// <param name="maxFrameBytes">The most bytes a frame may hold, header included; 0 means no limit beyond what an array can hold.</param>
    public WebSocketFrameReader(IConnection connection, long maxFrameBytes)
    {
        this.connection = connection;
        this.maxFrameBytes = maxFrameBytes;
    }

    /// <summary>
    /// Reads the next frame.
    /// </summary>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The frame, or the named reason there is none.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    public async ValueTask<WebSocketFrameReadResult> ReadFrameAsync(CancellationToken cancellationToken)
    {
        var filled = await FillAsync(headerBytes.AsMemory(0, FirstTwoBytes), cancellationToken);
        if (filled < FirstTwoBytes)
        {
            return WebSocketFrameReadResult.NoFrame(
                filled == 0 ? WebSocketFrameReadOutcome.ConnectionClosed : WebSocketFrameReadOutcome.ConnectionClosedMidFrame);
        }

        var firstByte = headerBytes[0];
        var secondByte = headerBytes[1];
        var headerOutcome = CheckFirstTwoBytes(firstByte, secondByte);
        return headerOutcome == WebSocketFrameReadOutcome.FrameRead
            ? await ReadAfterFirstTwoBytesAsync(firstByte, secondByte, cancellationToken)
            : WebSocketFrameReadResult.NoFrame(headerOutcome);
    }

    private async ValueTask<WebSocketFrameReadResult> ReadAfterFirstTwoBytesAsync(
        byte firstByte, byte secondByte, CancellationToken cancellationToken)
    {
        var (lengthOutcome, payloadLength, lengthBytes) = await ReadPayloadLengthAsync(secondByte & 0x7F, cancellationToken);
        if (lengthOutcome != WebSocketFrameReadOutcome.FrameRead)
        {
            return WebSocketFrameReadResult.NoFrame(lengthOutcome);
        }

        var masked = (secondByte & 0x80) != 0;
        var headerLength = FirstTwoBytes + lengthBytes + (masked ? MaskingKeyBytes : 0);
        return IsTooLarge(headerLength, payloadLength)
            ? WebSocketFrameReadResult.NoFrame(WebSocketFrameReadOutcome.FrameTooLarge)
            : await ReadMaskingKeyAndPayloadAsync(firstByte, masked, (int)payloadLength, cancellationToken);
    }

    private static WebSocketFrameReadOutcome CheckFirstTwoBytes(byte firstByte, byte secondByte)
    {
        var opcode = firstByte & 0x0F;
        if (!WebSocketOpcodes.IsDefined(opcode))
        {
            return WebSocketFrameReadOutcome.ReservedOpcode;
        }

        if (!WebSocketOpcodes.IsControl((WebSocketOpcode)opcode))
        {
            return WebSocketFrameReadOutcome.FrameRead;
        }

        if ((firstByte & 0x80) == 0)
        {
            return WebSocketFrameReadOutcome.FragmentedControlFrame;
        }

        return (secondByte & 0x7F) > MaxControlPayloadBytes
            ? WebSocketFrameReadOutcome.ControlFramePayloadTooLong
            : WebSocketFrameReadOutcome.FrameRead;
    }

    private static WebSocketFrameReadResult CheckClosePayload(WebSocketFrame frame)
    {
        var payload = frame.Payload;
        if (payload.Length == 0)
        {
            return WebSocketFrameReadResult.Read(frame);
        }

        if (payload.Length < CloseCodeBytes)
        {
            return WebSocketFrameReadResult.NoFrame(WebSocketFrameReadOutcome.ClosePayloadOneByte);
        }

        if (!WebSocketCloseCodes.IsAllowedOnTheWire(BinaryPrimitives.ReadUInt16BigEndian(payload)))
        {
            return WebSocketFrameReadResult.NoFrame(WebSocketFrameReadOutcome.CloseCodeNotAllowed);
        }

        return Utf8.IsValid(payload.AsSpan(CloseCodeBytes))
            ? WebSocketFrameReadResult.Read(frame)
            : WebSocketFrameReadResult.NoFrame(WebSocketFrameReadOutcome.CloseReasonNotUtf8);
    }

    private static void Unmask(byte[] payload, byte[] key)
    {
        for (var index = 0; index < payload.Length; index++)
        {
            payload[index] ^= key[index % MaskingKeyBytes];
        }
    }

    private bool IsTooLarge(int headerLength, long payloadLength) =>
        payloadLength > Array.MaxLength || (maxFrameBytes > 0 && headerLength + payloadLength > maxFrameBytes);

    // Reads the extended payload length when the 7-bit length is one of its two markers;
    // returns the length and how many bytes after the first two carried it.
    private async ValueTask<(WebSocketFrameReadOutcome Outcome, long PayloadLength, int LengthBytes)> ReadPayloadLengthAsync(
        int sevenBitLength, CancellationToken cancellationToken) =>
        sevenBitLength switch
        {
            SixteenBitLengthMarker => await ReadSixteenBitLengthAsync(cancellationToken),
            SixtyFourBitLengthMarker => await ReadSixtyFourBitLengthAsync(cancellationToken),
            _ => (WebSocketFrameReadOutcome.FrameRead, sevenBitLength, 0),
        };

    private async ValueTask<(WebSocketFrameReadOutcome Outcome, long PayloadLength, int LengthBytes)> ReadSixteenBitLengthAsync(
        CancellationToken cancellationToken)
    {
        if (await FillAsync(headerBytes.AsMemory(0, SixteenBitLengthBytes), cancellationToken) < SixteenBitLengthBytes)
        {
            return (WebSocketFrameReadOutcome.ConnectionClosedMidFrame, 0, 0);
        }

        var length = BinaryPrimitives.ReadUInt16BigEndian(headerBytes);
        return length < SixteenBitLengthMarker
            ? (WebSocketFrameReadOutcome.PayloadLengthNotMinimal, 0, 0)
            : (WebSocketFrameReadOutcome.FrameRead, length, SixteenBitLengthBytes);
    }

    private async ValueTask<(WebSocketFrameReadOutcome Outcome, long PayloadLength, int LengthBytes)> ReadSixtyFourBitLengthAsync(
        CancellationToken cancellationToken)
    {
        if (await FillAsync(headerBytes.AsMemory(0, SixtyFourBitLengthBytes), cancellationToken) < SixtyFourBitLengthBytes)
        {
            return (WebSocketFrameReadOutcome.ConnectionClosedMidFrame, 0, 0);
        }

        var length = BinaryPrimitives.ReadUInt64BigEndian(headerBytes);
        if (length > long.MaxValue)
        {
            return (WebSocketFrameReadOutcome.PayloadLengthMostSignificantBitSet, 0, 0);
        }

        return length <= ushort.MaxValue
            ? (WebSocketFrameReadOutcome.PayloadLengthNotMinimal, 0, 0)
            : (WebSocketFrameReadOutcome.FrameRead, (long)length, SixtyFourBitLengthBytes);
    }

    private async ValueTask<WebSocketFrameReadResult> ReadMaskingKeyAndPayloadAsync(
        byte firstByte, bool masked, int payloadLength, CancellationToken cancellationToken)
    {
        if (masked && await FillAsync(maskingKey, cancellationToken) < MaskingKeyBytes)
        {
            return WebSocketFrameReadResult.NoFrame(WebSocketFrameReadOutcome.ConnectionClosedMidFrame);
        }

        var payload = await ReadPayloadAsync(payloadLength, cancellationToken);
        if (payload is null)
        {
            return WebSocketFrameReadResult.NoFrame(WebSocketFrameReadOutcome.ConnectionClosedMidFrame);
        }

        if (masked)
        {
            Unmask(payload, maskingKey);
        }

        var frame = new WebSocketFrame(
            Fin: (firstByte & 0x80) != 0,
            Rsv1: (firstByte & 0x40) != 0,
            Rsv2: (firstByte & 0x20) != 0,
            Rsv3: (firstByte & 0x10) != 0,
            Opcode: (WebSocketOpcode)(firstByte & 0x0F),
            Masked: masked,
            Payload: payload);
        return frame.Opcode == WebSocketOpcode.Close ? CheckClosePayload(frame) : WebSocketFrameReadResult.Read(frame);
    }

    // Reads exactly payloadLength bytes, growing the buffer as they arrive; null when the peer
    // closed first.
    private async ValueTask<byte[]?> ReadPayloadAsync(int payloadLength, CancellationToken cancellationToken)
    {
        var payload = new byte[Math.Min(payloadLength, InitialPayloadBytes)];
        var filled = 0;
        while (filled < payloadLength)
        {
            if (filled == payload.Length)
            {
                Array.Resize(ref payload, (int)Math.Min(payloadLength, 2L * payload.Length));
            }

            var read = await connection.ReadAsync(payload.AsMemory(filled, payload.Length - filled), cancellationToken);
            if (read == 0)
            {
                return null;
            }

            filled += read;
        }

        return payload;
    }

    // Reads until buffer is full or the peer closes; returns how many bytes arrived.
    private async ValueTask<int> FillAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var filled = 0;
        while (filled < buffer.Length)
        {
            var read = await connection.ReadAsync(buffer[filled..], cancellationToken);
            if (read == 0)
            {
                break;
            }

            filled += read;
        }

        return filled;
    }
}
