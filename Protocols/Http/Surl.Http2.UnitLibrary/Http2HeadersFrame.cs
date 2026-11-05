namespace Surl.Http2;

/// <summary>
/// A <c>HEADERS</c> frame (RFC 9113 section 6.2). The field block fragment is HPACK-encoded and
/// is decoded by the connection, not here.
/// </summary>
/// <param name="StreamId">The stream, never 0.</param>
/// <param name="FieldBlockFragment">The field block fragment, padding and priority fields removed.</param>
/// <param name="EndStream">The <c>END_STREAM</c> flag.</param>
/// <param name="EndHeaders">The <c>END_HEADERS</c> flag: no <c>CONTINUATION</c> follows.</param>
/// <param name="Priority">The priority fields, or <see langword="null"/> without the <c>PRIORITY</c> flag.</param>
/// <param name="PaddingLength">The padding length, or <see langword="null"/> when the frame is not padded.</param>
internal sealed record Http2HeadersFrame(
    int StreamId,
    byte[] FieldBlockFragment,
    bool EndStream,
    bool EndHeaders,
    Http2Priority? Priority = null,
    int? PaddingLength = null)
    : Http2Frame(StreamId)
{
    /// <inheritdoc />
    public override Http2FrameType Type => Http2FrameType.Headers;

    /// <inheritdoc />
    public override byte Flags => (byte)(Http2FrameFlags.When(EndStream, Http2FrameFlags.EndStream)
        | Http2FrameFlags.When(EndHeaders, Http2FrameFlags.EndHeaders)
        | Http2FrameFlags.When(PaddingLength is not null, Http2FrameFlags.Padded)
        | Http2FrameFlags.When(Priority is not null, Http2FrameFlags.Priority));

    /// <inheritdoc />
    public override int PayloadLength => FieldBlockFragment.Length
        + Http2Padding.AddedLength(PaddingLength)
        + (Priority is null ? 0 : Http2Priority.EncodedLength);

    /// <inheritdoc />
    public override void WritePayload(Span<byte> payload)
    {
        var offset = Http2Padding.WriteLength(payload, PaddingLength);
        if (Priority is { } priority)
        {
            priority.Write(payload[offset..]);
            offset += Http2Priority.EncodedLength;
        }

        FieldBlockFragment.CopyTo(payload[offset..]);
    }
}
