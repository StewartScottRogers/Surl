using Surl.Protocol.Abstractions;

namespace Surl.Content;

/// <summary>
/// The content store's two stand-in seams (ADR-0124 decisions 1 and 4, ADR-0129): publishes each
/// change as a <see cref="StoreChangedEvent"/> of <see cref="StoreKind.Content"/>, and asks the fault
/// plan whether an upload runs out of space, publishing the <see cref="FaultAppliedEvent"/> when it
/// does. Shared by <see cref="ContentStore"/> and every <see cref="ContentUploadSession"/> it opens.
/// </summary>
internal sealed class ContentStoreEventsAndFaults
{
    private readonly ISurlEventPublisher events;
    private readonly IFaultPlan faults;

    // Null stands for the defaults a store given neither gets: publish nothing, find no fault.
    internal ContentStoreEventsAndFaults(ISurlEventPublisher? events, IFaultPlan? faults)
    {
        this.events = events ?? DiscardingSurlEventPublisher.Instance;
        this.faults = faults ?? EmptyFaultPlan.Instance;
    }

    /// <summary>
    /// Publishes one change to the content store, naming <paramref name="exchangeId"/> when a caller's
    /// exchange made it (null for a seed, reset or restore).
    /// </summary>
    internal void PublishChange(StoreChange change, string name, long? byteCount, string? previousName = null, long? exchangeId = null) =>
        events.Publish(new StoreChangedEvent(StoreKind.Content, change, name, byteCount, exchangeId) { PreviousName = previousName });

    /// <summary>
    /// Asks the plan at the <see cref="FaultStage.StoreWrite"/> point for <paramref name="name"/>;
    /// an <see cref="FaultKind.OutOfSpace"/> rule is applied and published, any other kind is not
    /// applied at a store write.
    /// </summary>
    internal async ValueTask<bool> RunsOutOfSpaceAsync(string name, ContentCaller caller, CancellationToken cancellationToken)
    {
        var point = new FaultPoint(FaultStage.StoreWrite) { Path = name, Scheme = caller.Scheme, Account = caller.AccountName, ExchangeId = caller.ExchangeId };
        FaultRule? rule = await faults.FindAsync(point, cancellationToken);
        if (rule is not { Kind: FaultKind.OutOfSpace })
        {
            return false;
        }

        events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.OutOfSpace, FaultStage.StoreWrite, caller.ExchangeId, name));
        return true;
    }
}
