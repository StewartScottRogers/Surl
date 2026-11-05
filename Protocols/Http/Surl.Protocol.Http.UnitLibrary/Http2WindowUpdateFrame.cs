using System.Buffers.Binary;

namespace Surl.Protocol.Http;

/// <summary>
/// A <c>WINDOW_UPDATE</c> frame (RFC 9113 section 6.9).
/// </summary>
/// <param name="StreamId">The stream whose window grows, or 0 for the connection's.</param>
/// <param name="Increment">The window increment, 1 to 2^31-1.</param>
internal sealed record Http2WindowUpdateFrame(int StreamId, int Increment) : Http2Frame(StreamId)
{
    /// <summary>The payload length RFC 9113 requires.</summary>
    public const int EncodedPayloadLength = 4;

    /// <inheritdoc />
    public override Http2FrameType Type => Http2FrameType.WindowUpdate;

    /// <inheritdoc />
    public override byte Flags => 0;

    /// <inheritdoc />
    public override int PayloadLength => EncodedPayloadLength;

    /// <inheritdoc />
    public override void WritePayload(Span<byte> payload) =>
        BinaryPrimitives.WriteUInt32BigEndian(payload, (uint)Increment);
}
