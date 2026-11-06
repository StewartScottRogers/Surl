namespace Surl.Http2;

/// <summary>
/// A <c>PRIORITY</c> frame (RFC 9113 section 6.3), deprecated: parsed and ignored.
/// </summary>
/// <param name="StreamId">The stream, never 0.</param>
/// <param name="Priority">The priority fields.</param>
internal sealed record Http2PriorityFrame(int StreamId, Http2Priority Priority) : Http2Frame(StreamId)
{
    /// <inheritdoc />
    public override Http2FrameType Type => Http2FrameType.Priority;

    /// <inheritdoc />
    public override byte Flags => 0;

    /// <inheritdoc />
    public override int PayloadLength => Http2Priority.EncodedLength;

    /// <inheritdoc />
    public override void WritePayload(Span<byte> payload) => Priority.Write(payload);
}
