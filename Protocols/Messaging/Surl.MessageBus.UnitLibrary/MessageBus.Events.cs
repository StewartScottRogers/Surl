using Surl.Protocol.Abstractions;

namespace Surl.MessageBus;

/// <summary>
/// What the bus publishes on its instance's event stream and how it answers an out-of-space
/// fault rule (ADR-0124 decisions 1 and 4). Every change is a <see cref="StoreChangedEvent"/>
/// with <see cref="StoreKind.Bus"/>, named by the canonical topic or by
/// <see cref="QueueChangeName"/>; a change carries the exchange of the <see cref="BusConnection"/> whose operation made it
/// (<see cref="StoreChangedEvent.ExchangeId"/>), and <see langword="null"/> when none did.
/// </summary>
public sealed partial class MessageBus
{
    /// <summary>
    /// What every queue's change name starts with: <c>$queue/</c>, the form the access grants
    /// already name a queue by (ADR-0102, decision 3.4), so a topic and a queue of the same name
    /// never share a change name or a fault rule's path.
    /// </summary>
    public const string QueueChangeNamePrefix = "$queue/";

    private readonly ISurlEventPublisher events;
    private readonly IFaultPlan faults;
    private readonly ThreadLocal<BusExchange?> currentExchange = new();

    /// <summary>
    /// Runs <paramref name="operation"/> with <paramref name="exchange"/> as the exchange the changes
    /// and faults it causes are named for on this thread, then puts back the one before.
    /// </summary>
    internal T RunAs<T>(BusExchange? exchange, Func<T> operation)
    {
        var previous = currentExchange.Value;
        currentExchange.Value = exchange;
        try
        {
            return operation();
        }
        finally
        {
            currentExchange.Value = previous;
        }
    }

    /// <summary>
    /// Queues a delivery to run later, outside the lock, as the exchange that caused it.
    /// </summary>
    private void EnqueueDelivery(Action delivery)
    {
        var exchange = currentExchange.Value;
        waitingDeliveries.Enqueue(() => RunAs(exchange, delivery));
    }

    /// <summary>
    /// Runs <paramref name="operation"/> as <see cref="RunAs{T}(BusExchange?, Func{T})"/> does, for one
    /// that returns nothing.
    /// </summary>
    internal void RunAs(BusExchange? exchange, Action operation) => RunAs(exchange, () =>
    {
        operation();
        return 0;
    });

    /// <summary>
    /// The name a queue's changes are published under, and the path an out-of-space rule names
    /// it by: <see cref="QueueChangeNamePrefix"/> and the queue's name.
    /// </summary>
    /// <param name="queueName">The queue's name.</param>
    /// <returns><c>$queue/&lt;name&gt;</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="queueName"/> is <see langword="null"/>.</exception>
    public static string QueueChangeName(string queueName)
    {
        ArgumentNullException.ThrowIfNull(queueName);
        return QueueChangeNamePrefix + queueName;
    }

    /// <summary>
    /// Publishes one change to the bus, naming the exchange running on this thread, if any. Called under the bus's lock, or
    /// from the one delivering thread, so a topic's or queue's events keep the order in which its
    /// changes happened.
    /// </summary>
    private void PublishChange(StoreChange change, string name, long? byteCount) =>
        events.Publish(new StoreChangedEvent(StoreKind.Bus, change, name, byteCount, currentExchange.Value?.ExchangeId));

    private void PublishQueueChange(StoreChange change, string queueName, long? byteCount) =>
        PublishChange(change, QueueChangeName(queueName), byteCount);

    /// <summary>
    /// Whether an out-of-space rule of the fault plan matches a write to <paramref name="path"/>
    /// - a canonical topic or a <see cref="QueueChangeName"/> - for <paramref name="account"/>,
    /// asked as a <see cref="FaultStage.StoreWrite"/> point. The rule that matches is published as
    /// a <see cref="FaultAppliedEvent"/> naming the path. Called outside the bus's lock, since the
    /// plan may run a test's code.
    /// </summary>
    private bool IsOutOfSpace(string? account, string path)
    {
        var exchange = currentExchange.Value;
        var rule = FindOutOfSpaceRule(new FaultPoint(FaultStage.StoreWrite) { ExchangeId = exchange?.ExchangeId, ListenUrl = exchange?.ListenUrl, Scheme = exchange?.Scheme, Account = account, Path = path });
        if (rule is null)
        {
            return false;
        }

        events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.OutOfSpace, FaultStage.StoreWrite, exchange?.ExchangeId, path));
        return true;
    }

    /// <summary>
    /// The plan's out-of-space rule for <paramref name="point"/>, or <see langword="null"/> when it
    /// names none or a rule of another kind. The bus's writes are synchronous, so it takes only an
    /// answer the plan gives at once - every plan does when no hook is registered - and serves a
    /// point whose answer is still pending, or that failed, as if no rule matched, as ADR-0124
    /// decision 5 serves a hook that times out or throws.
    /// </summary>
    private FaultRule? FindOutOfSpaceRule(FaultPoint point)
    {
        var answer = faults.FindAsync(point, CancellationToken.None);
        if (!answer.IsCompletedSuccessfully)
        {
            // Observed, so a later failure is never an unobserved task exception.
            _ = answer.AsTask().ContinueWith(
                static pending => pending.Exception,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return null;
        }

        // Completed already, so reading the result does not block.
        var rule = answer.Result;
        return rule?.Kind == FaultKind.OutOfSpace ? rule : null;
    }
}
