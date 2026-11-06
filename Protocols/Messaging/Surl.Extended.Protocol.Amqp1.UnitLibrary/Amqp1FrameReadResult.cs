namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The outcome of reading one frame, and the frame when one was read.
/// </summary>
/// <param name="Outcome">What was found.</param>
/// <param name="Frame">The frame; <see langword="null"/> unless <paramref name="Outcome"/> is <see cref="Amqp1FrameReadOutcome.FrameRead"/>.</param>
public sealed record Amqp1FrameReadResult(Amqp1FrameReadOutcome Outcome, Amqp1Frame? Frame)
{
    /// <summary>
    /// The error condition the outcome closes the connection with once <c>open</c> has been
    /// exchanged: <see cref="Amqp1ErrorCondition.FramingError"/> for a frame too small, too large,
    /// with a bad DOFF or of an unknown type (ADR-0107 decisions 3 and 7); <see langword="null"/>
    /// when a frame was read or the client closed.
    /// </summary>
    public Amqp1Symbol? RefusalCondition => Outcome switch
    {
        Amqp1FrameReadOutcome.FrameRead or Amqp1FrameReadOutcome.ConnectionClosed or Amqp1FrameReadOutcome.ConnectionClosedMidFrame => null,
        _ => Amqp1ErrorCondition.FramingError,
    };
}
