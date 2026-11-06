namespace Surl.Http2;

/// <summary>
/// A <c>CONTINUATION</c> frame (RFC 9113 section 6.10): more of the field block a
/// <c>HEADERS</c> or <c>PUSH_PROMISE</c> began.
/// </summary>
/// <param name="StreamId">The stream, never 0.</param>
/// <param name="FieldBlockFragment">The field block fragment.</param>
/// <param name="EndHeaders">The <c>END_HEADERS</c> flag: the field block ends here.</param>
internal sealed record Http2ContinuationFrame(int StreamId, byte[] FieldBlockFragment, bool EndHeaders)
    : Http2Frame(StreamId)
{
    /// <inheritdoc />
    public override Http2FrameType Type => Http2FrameType.Continuation;

    /// <inheritdoc />
    public override byte Flags => Http2FrameFlags.When(EndHeaders, Http2FrameFlags.EndHeaders);

    /// <inheritdoc />
    public override int PayloadLength => FieldBlockFragment.Length;

    /// <inheritdoc />
    public override void WritePayload(Span<byte> payload) => FieldBlockFragment.CopyTo(payload);
}
