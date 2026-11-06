using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// Records each exchange for <see cref="SurlServer.Exchanges"/> as a subscriber of the instance's
/// event stream (ADR-0124 decision 1, amending ADR-0099 decision 7): it subscribes with bytes on
/// the moment it is made, and a pump task hands every event, in stream order, to an
/// <see cref="ExchangeRecordBook"/>. When the subscription drops events past its capacity, every
/// exchange still open is marked truncated rather than left silently short.
/// </summary>
/// <remarks>
/// Reading <see cref="Snapshot"/> or <see cref="DroppedExchangeCount"/> first waits until the pump has
/// applied every event the stream had published when the read began, so a test that saw an exchange
/// end reads all of its bytes. Every member is safe to call from any thread.
/// </remarks>
internal sealed class ExchangeRecordSubscriber : IAsyncDisposable
{
    private readonly SurlEventStream stream;
    private readonly ExchangeRecordBook book;
    private readonly SurlEventSubscription subscription;
    private readonly Task pump;
    private long appliedThroughSequence;
    private long seenDroppedEventCount;
    private bool isPumpDone;

    /// <summary>Subscribes to <paramref name="stream"/> and starts the pump.</summary>
    /// <param name="stream">The instance's event stream.</param>
    /// <param name="maxExchanges">The most exchanges kept; positive.</param>
    /// <param name="maxBytesPerExchange">The most bytes kept of one exchange; positive.</param>
    /// <param name="capacity">How many undelivered events the subscription holds before it drops the oldest.</param>
    public ExchangeRecordSubscriber(
        SurlEventStream stream, int maxExchanges, int maxBytesPerExchange, int capacity = SurlEventSubscriptionOptions.MaxCapacity)
    {
        this.stream = stream;
        book = new ExchangeRecordBook(maxExchanges, maxBytesPerExchange);
        subscription = stream.Subscribe(new SurlEventSubscriptionOptions { Capacity = capacity, IncludeBytes = true });

        // Read after subscribing: every event published later reaches the subscription, so a read
        // never waits for one that was published before it and will not arrive.
        appliedThroughSequence = stream.LastSequence;
        pump = Task.Run(PumpAsync);
    }

    /// <summary>
    /// The lock the pump applies each event under; a test holds it to keep the pump from applying
    /// while it publishes.
    /// </summary>
    internal object Gate { get; } = new();

    /// <summary>How many exchanges were dropped, oldest first, to keep at most the cap.</summary>
    public long DroppedExchangeCount
    {
        get
        {
            lock (Gate)
            {
                WaitUntilCaughtUp();
                return book.DroppedExchangeCount;
            }
        }
    }

    /// <summary>The exchanges kept, oldest first, once every event published before the call is applied.</summary>
    /// <returns>A snapshot; later events do not change it.</returns>
    public IReadOnlyList<SurlExchangeRecord> Snapshot()
    {
        lock (Gate)
        {
            WaitUntilCaughtUp();
            return book.Snapshot();
        }
    }

    /// <summary>Stops the subscription and waits for the pump to apply what it had buffered.</summary>
    /// <returns>A task that completes once the pump has stopped.</returns>
    public async ValueTask DisposeAsync()
    {
        await subscription.DisposeAsync();
        await pump;
    }

    private async Task PumpAsync()
    {
        try
        {
            await foreach (var surlEvent in subscription.ReadAllAsync(CancellationToken.None))
            {
                lock (Gate)
                {
                    Apply(surlEvent);
                }
            }
        }
        finally
        {
            lock (Gate)
            {
                isPumpDone = true;
                Monitor.PulseAll(Gate);
            }
        }
    }

    private void Apply(SurlEvent surlEvent)
    {
        // Drops seen before the event may have been an end it would close; drops seen after it may
        // have been bytes of the exchange it opens. Either way the open records are marked.
        MarkOpenExchangesTruncatedIfEventsWereDropped();
        book.Apply(surlEvent);
        MarkOpenExchangesTruncatedIfEventsWereDropped();
        appliedThroughSequence = Math.Max(appliedThroughSequence, surlEvent.Sequence);
        Monitor.PulseAll(Gate);
    }

    private void MarkOpenExchangesTruncatedIfEventsWereDropped()
    {
        var droppedEventCount = subscription.DroppedEventCount;
        if (droppedEventCount != seenDroppedEventCount)
        {
            seenDroppedEventCount = droppedEventCount;
            book.MarkOpenExchangesTruncated();
        }
    }

    private void WaitUntilCaughtUp()
    {
        var publishedThroughSequence = stream.LastSequence;
        while (appliedThroughSequence < publishedThroughSequence && !isPumpDone)
        {
            Monitor.Wait(Gate);
        }
    }
}
