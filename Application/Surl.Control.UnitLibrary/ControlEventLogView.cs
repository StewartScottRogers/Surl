using Surl.Protocol.Abstractions;

namespace Surl.Control;

/// <summary>What a <see cref="ControlEventLog"/> held at one moment.</summary>
/// <param name="Events">The held events, oldest first.</param>
/// <param name="Gaps">The sequence numbers skipped among the held events.</param>
/// <param name="DroppedEventCount">Every event lost so far: dropped past the bounds or skipped.</param>
/// <param name="DroppedExchangeCount">Every exchange whose start was dropped past the bounds.</param>
/// <param name="NextEventAdded">Completes when the next event is added.</param>
internal sealed record ControlEventLogView(
    IReadOnlyList<SurlEvent> Events,
    IReadOnlyList<ControlSequenceGap> Gaps,
    long DroppedEventCount,
    long DroppedExchangeCount,
    Task NextEventAdded)
{
    /// <summary>The held exchange starts, by exchange ID.</summary>
    /// <returns>The starts.</returns>
    public IReadOnlyDictionary<long, ExchangeStartedEvent> Exchanges() =>
        Events.OfType<ExchangeStartedEvent>().ToDictionary(started => started.ExchangeId);

    /// <summary>Whether an event was skipped after sequence number <paramref name="from"/>, up to <paramref name="to"/>.</summary>
    /// <param name="from">The first sequence number, excluded.</param>
    /// <param name="to">The last sequence number, excluded.</param>
    /// <returns><see langword="true"/> when a gap lies between them.</returns>
    public bool HasGapBetween(long from, long to) => Gaps.Any(gap => gap.Last > from && gap.First < to);
}
