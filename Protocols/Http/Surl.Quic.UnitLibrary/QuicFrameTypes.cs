namespace Surl.Quic;

/// <summary>The frame type values of RFC 9000 section 19 (table 3, section 12.4).</summary>
internal static class QuicFrameTypes
{
    public const byte Padding = 0x00;
    public const byte Ping = 0x01;
    public const byte Ack = 0x02;
    public const byte AckWithEcn = 0x03;
    public const byte ResetStream = 0x04;
    public const byte StopSending = 0x05;
    public const byte Crypto = 0x06;
    public const byte NewToken = 0x07;
    public const byte Stream = 0x08;
    public const byte StreamOffsetBit = 0x04;
    public const byte StreamLengthBit = 0x02;
    public const byte StreamFinBit = 0x01;
    public const byte MaxData = 0x10;
    public const byte MaxStreamData = 0x11;
    public const byte MaxStreamsBidirectional = 0x12;
    public const byte MaxStreamsUnidirectional = 0x13;
    public const byte DataBlocked = 0x14;
    public const byte StreamDataBlocked = 0x15;
    public const byte StreamsBlockedBidirectional = 0x16;
    public const byte StreamsBlockedUnidirectional = 0x17;
    public const byte NewConnectionId = 0x18;
    public const byte RetireConnectionId = 0x19;
    public const byte PathChallenge = 0x1a;
    public const byte PathResponse = 0x1b;
    public const byte ConnectionCloseTransport = 0x1c;
    public const byte ConnectionCloseApplication = 0x1d;
    public const byte HandshakeDone = 0x1e;

    /// <summary><c>DATAGRAM</c> without a Length field (RFC 9221 section 4).</summary>
    public const byte Datagram = 0x30;

    /// <summary><c>DATAGRAM</c> with a Length field (RFC 9221 section 4).</summary>
    public const byte DatagramWithLength = 0x31;

    /// <summary><c>RESET_STREAM_AT</c> (draft-ietf-quic-reliable-stream-reset-09 section 4).</summary>
    public const byte ResetStreamAt = 0x24;

    /// <summary>The largest stream count a <c>MAX_STREAMS</c> or <c>STREAMS_BLOCKED</c> frame may carry, 2^60.</summary>
    public const ulong MaxStreamCount = 1UL << 60;
}
