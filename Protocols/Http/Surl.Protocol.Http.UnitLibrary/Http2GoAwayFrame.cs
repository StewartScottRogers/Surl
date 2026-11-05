using System.Buffers.Binary;

namespace Surl.Protocol.Http;

/// <summary>
/// A <c>GOAWAY</c> frame (RFC 9113 section 6.8), always on stream 0.
/// </summary>
/// <param name="LastStreamId">The highest stream the sender has processed or may still process.</param>
/// <param name="ErrorCode">Why the connection ends.</param>
/// <param name="DebugData">Opaque diagnostic bytes, possibly none.</param>
internal sealed record Http2GoAwayFrame(int LastStreamId, Http2ErrorCode ErrorCode, byte[] DebugData) : Http2Frame(0)
{
    /// <summary>The payload length before the debug data: last stream and error code.</summary>
    public const int FixedPayloadLength = 8;

    /// <inheritdoc />
    public override Http2FrameType Type => Http2FrameType.GoAway;

    /// <inheritdoc />
    public override byte Flags => 0;

    /// <inheritdoc />
    public override int PayloadLength => FixedPayloadLength + DebugData.Length;

    /// <inheritdoc />
    public override void WritePayload(Span<byte> payload)
    {
        BinaryPrimitives.WriteUInt32BigEndian(payload, (uint)LastStreamId);
        BinaryPrimitives.WriteUInt32BigEndian(payload[4..], (uint)ErrorCode);
        DebugData.CopyTo(payload[FixedPayloadLength..]);
    }
}
