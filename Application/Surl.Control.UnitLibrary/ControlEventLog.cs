using Surl.Protocol.Abstractions;

namespace Surl.Control;

/// <summary>
/// The events the control endpoint holds from its own subscription (ADR-0124 decision 7): the last
/// <see cref="DefaultMaxEvents"/> events and at most <see cref="DefaultMaxBytes"/> exchange bytes, the oldest
/// dropped; and a signal each time one is added, for a wait.
/// </summary>
/// <remarks>
/// An event is lost when this log drops it past its bounds or when the subscription feeding it skipped it (a gap in
/// <see cref="SurlEvent.Sequence"/>); both count toward <see cref="ControlEventLogView.DroppedEventCount"/>.
/// </remarks>
internal sealed class ControlEventLog
{
    /// <summary>The most events held.</summary>
    public const int DefaultMaxEvents = 10000;

    /// <summary>The most exchange bytes held across every held event.</summary>
    public const long DefaultMaxBytes = 64L * 1024 * 1024;

    private readonly int maxEvents;
    private readonly long maxBytes;
    private readonly Lock gate = new();
    private readonly Queue<SurlEvent> events = new();
    private readonly List<ControlSequenceGap> gaps = [];
    private TaskCompletionSource added = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long heldBytes;
    private long lastSequence;
    private long droppedEventCount;
    private long droppedExchangeCount;

    /// <summary>Creates a log with the endpoint's bounds.</summary>
    public ControlEventLog()
        : this(DefaultMaxEvents, DefaultMaxBytes)
    {
    }

    /// <summary>Creates a log with the given bounds.</summary>
    /// <param name="maxEvents">The most events held.</param>
    /// <param name="maxBytes">The most exchange bytes held.</param>
    public ControlEventLog(int maxEvents, long maxBytes)
    {
        this.maxEvents = maxEvents;
        this.maxBytes = maxBytes;
    }

    /// <summary>Adds the next event from the subscription, dropping the oldest past the bounds, and signals waits.</summary>
    /// <param name="surlEvent">The event.</param>
    public void Add(SurlEvent surlEvent)
    {
        TaskCompletionSource signal;
        lock (gate)
        {
            RecordGapBefore(surlEvent.Sequence);
            events.Enqueue(surlEvent);
            heldBytes += BytesOf(surlEvent);
            while (events.Count > 1 && (events.Count > maxEvents || heldBytes > maxBytes))
            {
                DropOldest();
            }

            gaps.RemoveAll(gap => gap.Last < events.Peek().Sequence);
            signal = added;
            added = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        signal.TrySetResult();
    }

    /// <summary>
    /// A consistent copy of what the log holds, with a task that completes when the next event is added.
    /// </summary>
    /// <returns>The view.</returns>
    public ControlEventLogView View()
    {
        lock (gate)
        {
            return new ControlEventLogView([.. events], [.. gaps], droppedEventCount, droppedExchangeCount, added.Task);
        }
    }

    private static int BytesOf(SurlEvent surlEvent) => surlEvent is ExchangeBytesEvent bytes ? bytes.Bytes.Length : 0;

    private void RecordGapBefore(long sequence)
    {
        if (lastSequence > 0 && sequence > lastSequence + 1)
        {
            gaps.Add(new ControlSequenceGap(lastSequence + 1, sequence - 1));
            droppedEventCount += sequence - 1 - lastSequence;
        }

        lastSequence = Math.Max(lastSequence, sequence);
    }

    private void DropOldest()
    {
        var dropped = events.Dequeue();
        heldBytes -= BytesOf(dropped);
        droppedEventCount++;
        if (dropped is ExchangeStartedEvent)
        {
            droppedExchangeCount++;
        }
    }
}
