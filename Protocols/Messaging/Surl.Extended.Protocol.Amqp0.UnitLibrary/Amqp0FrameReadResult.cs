namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The outcome of reading one frame, and the frame when one was read.
/// </summary>
/// <param name="Outcome">What was found.</param>
/// <param name="Frame">The frame; <see langword="null"/> unless <paramref name="Outcome"/> is <see cref="Amqp0FrameReadOutcome.FrameRead"/>.</param>
public sealed record Amqp0FrameReadResult(Amqp0FrameReadOutcome Outcome, Amqp0Frame? Frame)
{
    /// <summary>
    /// The connection exception the outcome calls for: <see cref="Amqp0ReplyCode.FrameError"/>
    /// (501) for an unknown type, a frame above frame-max, a heartbeat off channel 0 or a bad
    /// frame-end (ADR-0112 decision 7); <see langword="null"/> when a frame was read or the
    /// client closed.
    /// </summary>
    public Amqp0ReplyCode? RefusalReplyCode => Outcome switch
    {
        Amqp0FrameReadOutcome.FrameRead or Amqp0FrameReadOutcome.ConnectionClosed or Amqp0FrameReadOutcome.ConnectionClosedMidFrame => null,
        _ => Amqp0ReplyCode.FrameError,
    };
}
