namespace Surl.Protocol.Ws;

/// <summary>
/// The result of reading one frame: the frame, or the named reason there is none.
/// </summary>
/// <param name="Outcome">What the reader found.</param>
/// <param name="Frame">The frame when <paramref name="Outcome"/> is <see cref="WebSocketFrameReadOutcome.FrameRead"/>; otherwise <see langword="null"/>.</param>
internal sealed record WebSocketFrameReadResult(WebSocketFrameReadOutcome Outcome, WebSocketFrame? Frame)
{
    /// <summary>
    /// A result carrying <paramref name="frame"/>.
    /// </summary>
    /// <param name="frame">The frame read.</param>
    /// <returns>A <see cref="WebSocketFrameReadOutcome.FrameRead"/> result.</returns>
    public static WebSocketFrameReadResult Read(WebSocketFrame frame) => new(WebSocketFrameReadOutcome.FrameRead, frame);

    /// <summary>
    /// A result with no frame.
    /// </summary>
    /// <param name="outcome">Why there is no frame.</param>
    /// <returns>A result whose <see cref="Frame"/> is <see langword="null"/>.</returns>
    public static WebSocketFrameReadResult NoFrame(WebSocketFrameReadOutcome outcome) => new(outcome, null);
}
