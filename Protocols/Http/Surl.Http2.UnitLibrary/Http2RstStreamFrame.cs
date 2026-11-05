using System.Buffers.Binary;

namespace Surl.Http2;

/// <summary>
/// An <c>RST_STREAM</c> frame (RFC 9113 section 6.4).
/// </summary>
/// <param name="StreamId">The stream ended, never 0.</param>
/// <param name="ErrorCode">Why it ended.</param>
internal sealed record Http2RstStreamFrame(int StreamId, Http2ErrorCode ErrorCode) : Http2Frame(StreamId)
{
    /// <summary>The payload length RFC 9113 requires.</summary>
    public const int EncodedPayloadLength = 4;

    /// <inheritdoc />
    public override Http2FrameType Type => Http2FrameType.RstStream;

    /// <inheritdoc />
    public override byte Flags => 0;

    /// <inheritdoc />
    public override int PayloadLength => EncodedPayloadLength;

    /// <inheritdoc />
    public override void WritePayload(Span<byte> payload) =>
        BinaryPrimitives.WriteUInt32BigEndian(payload, (uint)ErrorCode);
}
