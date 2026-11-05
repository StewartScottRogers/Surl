namespace Surl.Quic;

/// <summary>
/// Reads a QUIC packet payload, in the clear, into its frames (RFC 9000 sections 12.4 and 19, RFC 9221 section 4, draft-ietf-quic-reliable-stream-reset-09 section 4).
/// Malformed input throws a <see cref="QuicTransportException"/> naming the transport error and
/// the frame type:
/// <list type="bullet">
/// <item>a payload with no frames, a frame type not in its shortest encoding, and a frame type the packet type does not permit: <see cref="QuicTransportErrorCode.ProtocolViolation"/>;</item>
/// <item>a frame type neither RFC 9000, RFC 9221 (<c>DATAGRAM</c>) nor draft-ietf-quic-reliable-stream-reset-09 (<c>RESET_STREAM_AT</c>) defines, a <c>RESET_STREAM_AT</c> whose Reliable Size is above its Final Size, a frame that ends early, and a field out of its range: <see cref="QuicTransportErrorCode.FrameEncodingError"/>;</item>
/// <item>a <c>CRYPTO</c> frame ending beyond offset 2^62-1: <see cref="QuicTransportErrorCode.CryptoBufferExceeded"/>.</item>
/// </list>
/// Byte strings in the frames returned are slices of <c>payload</c>, not copies.
/// </summary>
public static class QuicFrameReader
{
    private const int InitialBit = 1 << (int)QuicPacketType.Initial;
    private const int ZeroRttBit = 1 << (int)QuicPacketType.ZeroRtt;
    private const int HandshakeBit = 1 << (int)QuicPacketType.Handshake;
    private const int OneRttBit = 1 << (int)QuicPacketType.OneRtt;
    private const int AnyPacket = InitialBit | ZeroRttBit | HandshakeBit | OneRttBit;
    private const int NotZeroRtt = InitialBit | HandshakeBit | OneRttBit;
    private const int ApplicationData = ZeroRttBit | OneRttBit;

    /// <summary>Each frame type's reader and the packet types it may appear in (RFC 9000 section 12.4, table 3), indexed by type.</summary>
    private static readonly (Func<QuicReader, byte, QuicFrame> Read, int PacketTypes)[] FrameTypes =
    [
        (QuicPaddingFrame.ReadFrom, AnyPacket),
        (QuicPingFrame.ReadFrom, AnyPacket),
        (QuicAckFrame.ReadFrom, NotZeroRtt),
        (QuicAckFrame.ReadFrom, NotZeroRtt),
        (QuicResetStreamFrame.ReadFrom, ApplicationData),
        (QuicStopSendingFrame.ReadFrom, ApplicationData),
        (QuicCryptoFrame.ReadFrom, NotZeroRtt),
        (QuicNewTokenFrame.ReadFrom, OneRttBit),
        (QuicStreamFrame.ReadFrom, ApplicationData),
        (QuicStreamFrame.ReadFrom, ApplicationData),
        (QuicStreamFrame.ReadFrom, ApplicationData),
        (QuicStreamFrame.ReadFrom, ApplicationData),
        (QuicStreamFrame.ReadFrom, ApplicationData),
        (QuicStreamFrame.ReadFrom, ApplicationData),
        (QuicStreamFrame.ReadFrom, ApplicationData),
        (QuicStreamFrame.ReadFrom, ApplicationData),
        (QuicMaxDataFrame.ReadFrom, ApplicationData),
        (QuicMaxStreamDataFrame.ReadFrom, ApplicationData),
        (QuicMaxStreamsFrame.ReadFrom, ApplicationData),
        (QuicMaxStreamsFrame.ReadFrom, ApplicationData),
        (QuicDataBlockedFrame.ReadFrom, ApplicationData),
        (QuicStreamDataBlockedFrame.ReadFrom, ApplicationData),
        (QuicStreamsBlockedFrame.ReadFrom, ApplicationData),
        (QuicStreamsBlockedFrame.ReadFrom, ApplicationData),
        (QuicNewConnectionIdFrame.ReadFrom, ApplicationData),
        (QuicRetireConnectionIdFrame.ReadFrom, ApplicationData),
        (QuicPathChallengeFrame.ReadFrom, ApplicationData),
        (QuicPathResponseFrame.ReadFrom, OneRttBit),
        (QuicConnectionCloseFrame.ReadFrom, AnyPacket),
        (QuicConnectionCloseFrame.ReadFrom, ApplicationData),
        (QuicHandshakeDoneFrame.ReadFrom, OneRttBit),
    ];

    /// <summary>The <c>DATAGRAM</c> frame types (RFC 9221 section 4), permitted in 0-RTT and 1-RTT packets.</summary>
    private static readonly (Func<QuicReader, byte, QuicFrame> Read, int PacketTypes) DatagramFrameType = (QuicDatagramFrame.ReadFrom, ApplicationData);

    /// <summary>The <c>RESET_STREAM_AT</c> frame type (draft-ietf-quic-reliable-stream-reset-09 section 4), permitted in 0-RTT and 1-RTT packets.</summary>
    private static readonly (Func<QuicReader, byte, QuicFrame> Read, int PacketTypes) ResetStreamAtFrameType = (QuicResetStreamAtFrame.ReadFrom, ApplicationData);

    /// <summary>Reads every frame in a packet payload.</summary>
    /// <param name="payload">The payload, in the clear, with no AEAD tag.</param>
    /// <param name="packetType">The type of packet that carried it: Initial, 0-RTT, Handshake or 1-RTT.</param>
    /// <returns>The frames, in the order they were sent; consecutive <c>PADDING</c> frames as one <see cref="QuicPaddingFrame"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="packetType"/> is Retry or Version Negotiation, which carry no frames.</exception>
    /// <exception cref="QuicTransportException">The payload is malformed.</exception>
    public static IReadOnlyList<QuicFrame> Read(ReadOnlyMemory<byte> payload, QuicPacketType packetType)
    {
        var packetTypeBit = PacketTypeBit(packetType);
        if (payload.IsEmpty)
        {
            throw new QuicTransportException(QuicTransportErrorCode.ProtocolViolation, "A packet carries no frames.");
        }

        var reader = new QuicReader(payload, QuicTransportErrorCode.FrameEncodingError);
        var frames = new List<QuicFrame>();
        while (!reader.IsAtEnd)
        {
            frames.Add(ReadFrame(reader, packetType, packetTypeBit));
        }

        return frames;
    }

    private static QuicFrame ReadFrame(QuicReader reader, QuicPacketType packetType, int packetTypeBit)
    {
        reader.FrameType = null;
        var start = reader.Position;
        var frameType = reader.ReadVariableLengthInteger();
        reader.FrameType = frameType;
        var (read, packetTypes) = FrameTypeOf(frameType) ?? throw reader.Fault(QuicTransportErrorCode.FrameEncodingError, $"Frame type 0x{frameType:x} is unknown.");
        if (reader.Position - start != QuicVariableLengthInteger.EncodedLength(frameType))
        {
            throw reader.Fault(QuicTransportErrorCode.ProtocolViolation, $"Frame type 0x{frameType:x} is not in its shortest encoding.");
        }

        return (packetTypes & packetTypeBit) == 0
            ? throw reader.Fault(QuicTransportErrorCode.ProtocolViolation, $"Frame type 0x{frameType:x} is not permitted in a {packetType} packet.")
            : read(reader, (byte)frameType);
    }

    /// <summary>The reader and permitted packet types of a frame type RFC 9000, RFC 9221 or draft-ietf-quic-reliable-stream-reset-09 defines; null for any other.</summary>
    private static (Func<QuicReader, byte, QuicFrame> Read, int PacketTypes)? FrameTypeOf(ulong frameType) => frameType switch
    {
        < 0x1f => FrameTypes[frameType],
        QuicFrameTypes.Datagram or QuicFrameTypes.DatagramWithLength => DatagramFrameType,
        QuicFrameTypes.ResetStreamAt => ResetStreamAtFrameType,
        _ => null,
    };

    private static int PacketTypeBit(QuicPacketType packetType) => packetType switch
    {
        QuicPacketType.Initial or QuicPacketType.ZeroRtt or QuicPacketType.Handshake or QuicPacketType.OneRtt => 1 << (int)packetType,
        _ => throw new ArgumentOutOfRangeException(nameof(packetType), packetType, "Only Initial, 0-RTT, Handshake and 1-RTT packets carry frames."),
    };
}
