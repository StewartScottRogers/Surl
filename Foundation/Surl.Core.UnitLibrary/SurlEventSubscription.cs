using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// One subscriber's view of a <see cref="SurlEventStream"/>: the events published after it was
/// made, in stream order, held in a bounded buffer that drops the oldest undelivered event when
/// full, so a slow reader loses events and never slows a server (ADR-0124, decision 1).
/// </summary>
/// <remarks>
/// Disposing it stops delivery: nothing published afterwards reaches it, and
/// <see cref="ReadAllAsync"/> ends once the events already buffered have been read.
/// </remarks>
public sealed class SurlEventSubscription : IAsyncDisposable
{
    private readonly Channel<SurlEvent> events;
    private readonly Action<SurlEventSubscription> unsubscribe;
    private readonly Func<SurlEvent, bool>? filter;
    private long droppedEventCount;

    internal SurlEventSubscription(SurlEventSubscriptionOptions options, Action<SurlEventSubscription> unsubscribe)
    {
        events = Channel.CreateBounded<SurlEvent>(
            new BoundedChannelOptions(options.Capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleWriter = true,
            },
            _ => Interlocked.Increment(ref droppedEventCount));
        IncludesBytes = options.IncludeBytes;
        filter = options.Filter;
        this.unsubscribe = unsubscribe;
    }

    /// <summary>
    /// How many events this subscription dropped because its buffer was full; the gap also shows
    /// in <see cref="SurlEvent.Sequence"/>.
    /// </summary>
    public long DroppedEventCount => Interlocked.Read(ref droppedEventCount);

    /// <summary>
    /// Whether this subscription receives <see cref="ExchangeBytesEvent"/>s.
    /// </summary>
    public bool IncludesBytes { get; }

    /// <summary>
    /// Reads the events as they arrive, in stream order, until the subscription is disposed and
    /// its buffer drained, or <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    /// <param name="cancellationToken">Stops the wait for the next event.</param>
    /// <returns>The events.</returns>
    public IAsyncEnumerable<SurlEvent> ReadAllAsync(CancellationToken cancellationToken) =>
        events.Reader.ReadAllAsync(cancellationToken);

    /// <summary>
    /// Takes the next buffered event without waiting.
    /// </summary>
    /// <param name="surlEvent">The event, when one was buffered.</param>
    /// <returns><see langword="true"/> when an event was taken.</returns>
    public bool TryRead(out SurlEvent? surlEvent) => events.Reader.TryRead(out surlEvent);

    /// <summary>
    /// Stops delivery to this subscription.
    /// </summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        unsubscribe(this);
        events.Writer.TryComplete();

        return ValueTask.CompletedTask;
    }

    // Called under the stream's lock. Never blocks: a full buffer drops its oldest event.
    internal void Offer(SurlEvent surlEvent)
    {
        if ((surlEvent is not ExchangeBytesEvent || IncludesBytes) && Accepts(surlEvent))
        {
            events.Writer.TryWrite(surlEvent);
        }
    }

    // A filter that throws refuses the event rather than throwing into the publisher.
    private bool Accepts(SurlEvent surlEvent)
    {
        try
        {
            return filter?.Invoke(surlEvent) ?? true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
