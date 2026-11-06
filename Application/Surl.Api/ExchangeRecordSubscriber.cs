using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// Records each exchange for <see cref="SurlServer.Exchanges"/>, and keeps each event for
/// <see cref="SurlServer.FindEvents"/>, as one subscriber of the instance's event stream (ADR-0124
/// decision 1, amending ADR-0099 decision 7; ADR-0143): it subscribes with bytes on the moment it is
/// made, and a pump task hands every event, in stream order, to an <see cref="ExchangeRecordBook"/> and
/// an <see cref="EventRecordBook"/>. When the subscription drops events past its capacity, every
/// exchange still open is marked truncated rather than left silently short.
/// </summary>
/// <remarks>
/// Every read first waits until the pump has applied every event the stream had published when the
/// read began, so a test that saw an exchange end reads all of its bytes. A wait checks what is kept
/// before it waits for more, so it never misses an event that came before it. Every member is safe to
/// call from any thread.
/// </remarks>
internal sealed class ExchangeRecordSubscriber : IAsyncDisposable
{
    private readonly SurlEventStream stream;
    private readonly TimeProvider timeProvider;
    private readonly ExchangeRecordBook book;
    private readonly EventRecordBook events;
    private readonly SurlEventSubscription subscription;
    private readonly Task pump;
    private TaskCompletionSource applied = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long appliedThroughSequence;
    private long seenDroppedEventCount;
    private bool isPumpDone;

    /// <summary>Subscribes to <paramref name="stream"/> and starts the pump.</summary>
    /// <param name="stream">The instance's event stream.</param>
    /// <param name="timeProvider">The clock a wait's timeout runs on: the instance's.</param>
    /// <param name="bounds">How many exchanges, bytes of each and events are kept; each positive.</param>
    /// <param name="capacity">How many undelivered events the subscription holds before it drops the oldest.</param>
    public ExchangeRecordSubscriber(
        SurlEventStream stream,
        TimeProvider timeProvider,
        ExchangeRecordBounds bounds,
        int capacity = SurlEventSubscriptionOptions.MaxCapacity)
    {
        this.stream = stream;
        this.timeProvider = timeProvider;
        book = new ExchangeRecordBook(bounds.MaxExchanges, bounds.MaxBytesPerExchange);
        events = new EventRecordBook(bounds.MaxEvents);
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
    public long DroppedExchangeCount => Read(() => book.DroppedExchangeCount);

    /// <summary>How many kept events were dropped, oldest first, to keep at most the cap.</summary>
    public long DroppedEventCount => Read(() => events.DroppedEventCount);

    /// <summary>The exchanges kept, oldest first, once every event published before the call is applied.</summary>
    /// <returns>A snapshot; later events do not change it.</returns>
    public IReadOnlyList<SurlExchangeRecord> Snapshot() => Read(book.Snapshot);

    /// <summary>The kept exchanges <paramref name="query"/> asks for, oldest first.</summary>
    /// <param name="query">The query.</param>
    /// <returns>A snapshot; later events do not change it.</returns>
    public IReadOnlyList<SurlExchangeRecord> FindExchanges(SurlEventQuery query) => Read(() => book.FindExchanges(query));

    /// <summary>The kept events <paramref name="query"/> asks for, oldest first.</summary>
    /// <param name="query">The query.</param>
    /// <returns>A copy; later events do not change it.</returns>
    public IReadOnlyList<SurlEvent> FindEvents(SurlEventQuery query) => Read(() => events.FindEvents(query, book));

    /// <summary>The first kept exchange <paramref name="query"/> asks for, or the first that comes to match it.</summary>
    /// <param name="query">The query.</param>
    /// <param name="timeout">How long to wait on the instance's clock, or <see cref="Timeout.InfiniteTimeSpan"/>.</param>
    /// <param name="cancellationToken">Gives up the wait.</param>
    /// <returns>A snapshot of the exchange as it stood when it matched.</returns>
    public Task<SurlExchangeRecord> WaitForExchangeAsync(
        SurlEventQuery query, TimeSpan timeout, CancellationToken cancellationToken) =>
        WaitForFirstAsync(() => book.FindExchanges(query), "exchange", query, timeout, cancellationToken);

    /// <summary>The first kept event <paramref name="query"/> asks for, or the first that comes to match it.</summary>
    /// <param name="query">The query.</param>
    /// <param name="timeout">How long to wait on the instance's clock, or <see cref="Timeout.InfiniteTimeSpan"/>.</param>
    /// <param name="cancellationToken">Gives up the wait.</param>
    /// <returns>The event.</returns>
    public Task<SurlEvent> WaitForEventAsync(SurlEventQuery query, TimeSpan timeout, CancellationToken cancellationToken) =>
        WaitForFirstAsync(() => events.FindEvents(query, book), "event", query, timeout, cancellationToken);

    /// <summary>Stops the subscription and waits for the pump to apply what it had buffered.</summary>
    /// <returns>A task that completes once the pump has stopped.</returns>
    public async ValueTask DisposeAsync()
    {
        await subscription.DisposeAsync();
        await pump;
    }

    /// <summary>The message of the <see cref="TimeoutException"/> a wait throws.</summary>
    /// <param name="what">What was waited for: <c>exchange</c> or <c>event</c>.</param>
    /// <param name="query">What was asked.</param>
    /// <param name="timeout">How long the wait waited.</param>
    /// <param name="seenEventCount">How many events were kept while it waited.</param>
    /// <returns>The message.</returns>
    internal static string TimedOutMessage(string what, SurlEventQuery query, TimeSpan timeout, long seenEventCount) =>
        $"No {what} matching {query} within {timeout}; {seenEventCount} events were recorded while waiting and none matched.";

    private async Task<T> WaitForFirstAsync<T>(
        Func<IReadOnlyList<T>> find, string what, SurlEventQuery query, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = new CancellationTokenSource(timeout, timeProvider);
        using var deadlineOrCancelled = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var keptAtStart = Read(() => events.KeptEventTotal);
        while (true)
        {
            var (found, nextApplied) = Read(() => (find(), applied.Task));
            if (found.Count > 0)
            {
                return found[0];
            }

            try
            {
                await nextApplied.WaitAsync(deadlineOrCancelled.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(TimedOutMessage(what, query, timeout, Read(() => events.KeptEventTotal) - keptAtStart));
            }
        }
    }

    private T Read<T>(Func<T> read)
    {
        lock (Gate)
        {
            WaitUntilCaughtUp();
            return read();
        }
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
        events.Apply(surlEvent);
        MarkOpenExchangesTruncatedIfEventsWereDropped();
        appliedThroughSequence = Math.Max(appliedThroughSequence, surlEvent.Sequence);
        Monitor.PulseAll(Gate);
        var signal = applied;
        applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        signal.SetResult();
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
