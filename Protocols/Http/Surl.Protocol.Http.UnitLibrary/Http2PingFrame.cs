using System.Buffers.Binary;

namespace Surl.Protocol.Http;

/// <summary>
/// A <c>PING</c> frame (RFC 9113 section 6.7), always on stream 0.
/// </summary>
/// <param name="Ack">The <c>ACK</c> flag: this answers the peer's <c>PING</c>.</param>
/// <param name="OpaqueData">The 8 opaque bytes, read big-endian.</param>
internal sealed record Http2PingFrame(bool Ack, ulong OpaqueData) : Http2Frame(0)
{
    /// <summary>The payload length RFC 9113 requires.</summary>
    public const int EncodedPayloadLength = 8;

    /// <inheritdoc />
    public override Http2FrameType Type => Http2FrameType.Ping;

    /// <inheritdoc />
    public override byte Flags => Http2FrameFlags.When(Ack, Http2FrameFlags.Ack);

    /// <inheritdoc />
    public override int PayloadLength => EncodedPayloadLength;

    /// <inheritdoc />
    public override void WritePayload(Span<byte> payload) =>
        BinaryPrimitives.WriteUInt64BigEndian(payload, OpaqueData);
}
