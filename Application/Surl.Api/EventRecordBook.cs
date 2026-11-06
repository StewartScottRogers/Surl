using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// Keeps an instance's events, without their bytes, for <see cref="SurlServer.FindEvents"/> (ADR-0124
/// decision 1, ADR-0143): every event but <see cref="ExchangeBytesEvent"/>, in stream order, at most
/// <paramref name="maxEvents"/> of them, the oldest dropped and counted in <see cref="DroppedEventCount"/>
/// past it.
/// </summary>
/// <remarks>Not safe for concurrent use: <see cref="ExchangeRecordSubscriber"/> calls it under its lock.</remarks>
/// <param name="maxEvents">The most events kept; positive.</param>
internal sealed class EventRecordBook(int maxEvents)
{
    private readonly Queue<SurlEvent> kept = new();

    /// <summary>How many kept events were dropped, oldest first, to keep at most the cap.</summary>
    public long DroppedEventCount { get; private set; }

    /// <summary>How many events were ever kept, dropped ones included, so a wait can count what arrived while it waited.</summary>
    public long KeptEventTotal { get; private set; }

    /// <summary>Keeps <paramref name="surlEvent"/> unless it is exchange bytes, dropping the oldest past the cap.</summary>
    /// <param name="surlEvent">The next event, in stream order.</param>
    public void Apply(SurlEvent surlEvent)
    {
        if (surlEvent is ExchangeBytesEvent)
        {
            return;
        }

        if (kept.Count == maxEvents)
        {
            kept.Dequeue();
            DroppedEventCount++;
        }

        kept.Enqueue(surlEvent);
        KeptEventTotal++;
    }

    /// <summary>The kept events <paramref name="query"/> asks for, oldest first.</summary>
    /// <param name="query">The query.</param>
    /// <param name="exchanges">The exchange records, for an event's scheme, listen URL and accounts.</param>
    /// <returns>A copy; events applied later do not change it.</returns>
    public IReadOnlyList<SurlEvent> FindEvents(SurlEventQuery query, ExchangeRecordBook exchanges) =>
        [.. kept.Where(surlEvent =>
            SurlEventFacts.Matches(surlEvent, exchanges.FactsOf(SurlEventFacts.ExchangeIdOf(surlEvent)), query))];
}
