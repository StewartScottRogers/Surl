namespace Surl.Http2;

/// <summary>
/// A <c>DATA</c> frame (RFC 9113 section 6.1).
/// </summary>
/// <param name="StreamId">The stream, never 0.</param>
/// <param name="Data">The content bytes, padding removed.</param>
/// <param name="EndStream">The <c>END_STREAM</c> flag.</param>
/// <param name="PaddingLength">The padding length, or <see langword="null"/> when the frame is not padded.</param>
internal sealed record Http2DataFrame(int StreamId, byte[] Data, bool EndStream, int? PaddingLength = null)
    : Http2Frame(StreamId)
{
    /// <inheritdoc />
    public override Http2FrameType Type => Http2FrameType.Data;

    /// <inheritdoc />
    public override byte Flags => (byte)(Http2FrameFlags.When(EndStream, Http2FrameFlags.EndStream)
        | Http2FrameFlags.When(PaddingLength is not null, Http2FrameFlags.Padded));

    /// <summary>
    /// The whole payload length, padding included, which is what flow control counts
    /// (section 6.1).
    /// </summary>
    public override int PayloadLength => Data.Length + Http2Padding.AddedLength(PaddingLength);

    /// <inheritdoc />
    public override void WritePayload(Span<byte> payload) =>
        Data.CopyTo(payload[Http2Padding.WriteLength(payload, PaddingLength)..]);
}
