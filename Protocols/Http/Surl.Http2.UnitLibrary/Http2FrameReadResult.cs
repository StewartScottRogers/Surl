namespace Surl.Http2;

/// <summary>
/// The result of reading one HTTP/2 frame: the frame, an unknown frame skipped, the malformation
/// and the error it maps to, or the peer closing.
/// </summary>
internal sealed class Http2FrameReadResult
{
    private Http2FrameReadResult(Http2FrameReadOutcome outcome, Http2Frame? frame, Http2FrameHeader? header, Http2FrameMalformation? malformation)
    {
        Outcome = outcome;
        Frame = frame;
        Header = header;
        Malformation = malformation;
    }

    /// <summary>What was found.</summary>
    public Http2FrameReadOutcome Outcome { get; }

    /// <summary>The frame, with <see cref="Http2FrameReadOutcome.FrameRead"/>; otherwise <see langword="null"/>.</summary>
    public Http2Frame? Frame { get; }

    /// <summary>
    /// The frame header that was read, with <see cref="Http2FrameReadOutcome.UnknownFrameSkipped"/>
    /// and <see cref="Http2FrameReadOutcome.Malformed"/>; otherwise <see langword="null"/>. Its
    /// stream identifier is the stream a stream error resets.
    /// </summary>
    public Http2FrameHeader? Header { get; }

    /// <summary>What was wrong, with <see cref="Http2FrameReadOutcome.Malformed"/>; otherwise <see langword="null"/>.</summary>
    public Http2FrameMalformation? Malformation { get; }

    /// <summary>The frame was read.</summary>
    /// <param name="frame">The frame.</param>
    /// <returns>The result.</returns>
    public static Http2FrameReadResult Read(Http2Frame frame) => new(Http2FrameReadOutcome.FrameRead, frame, null, null);

    /// <summary>A frame of an unknown type was skipped.</summary>
    /// <param name="header">Its header.</param>
    /// <returns>The result.</returns>
    public static Http2FrameReadResult UnknownSkipped(Http2FrameHeader header) =>
        new(Http2FrameReadOutcome.UnknownFrameSkipped, null, header, null);

    /// <summary>A frame was malformed.</summary>
    /// <param name="header">Its header.</param>
    /// <param name="malformation">What was wrong.</param>
    /// <returns>The result.</returns>
    public static Http2FrameReadResult Malformed(Http2FrameHeader header, Http2FrameMalformation malformation) =>
        new(Http2FrameReadOutcome.Malformed, null, header, malformation);

    /// <summary>The peer closed, between frames or partway through one.</summary>
    /// <param name="outcome"><see cref="Http2FrameReadOutcome.ConnectionClosed"/> or <see cref="Http2FrameReadOutcome.ConnectionClosedMidFrame"/>.</param>
    /// <returns>The result.</returns>
    public static Http2FrameReadResult Closed(Http2FrameReadOutcome outcome) => new(outcome, null, null, null);
}
