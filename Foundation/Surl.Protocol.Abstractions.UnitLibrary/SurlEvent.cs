namespace Surl.Protocol.Abstractions;

/// <summary>
/// One thing that happened on a Surl instance, published on its event stream (ADR-0124, decision 1).
/// No event carries a password, key, token or other secret.
/// </summary>
public abstract record SurlEvent
{
    /// <summary>
    /// The event's place on its stream: 1, 2, 3 ... with no gaps, stamped by the stream; 0 until stamped.
    /// </summary>
    public long Sequence { get; init; }

    /// <summary>
    /// When the stream received the event, stamped from the stream's <see cref="TimeProvider"/>;
    /// default until stamped.
    /// </summary>
    public DateTimeOffset At { get; init; }
}
