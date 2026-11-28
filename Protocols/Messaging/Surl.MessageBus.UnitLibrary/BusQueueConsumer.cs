using System.Diagnostics.CodeAnalysis;

namespace Surl.MessageBus;

/// <summary>
/// One consumer of a named queue, made by <see cref="BusConnection.Consume"/> (ADR-0100,
/// decision 3.3). Once started, it competes with the queue's other consumers: each message goes
/// to one of them, round robin among those with room, and stays unacknowledged until
/// <see cref="Acknowledge"/> removes it or <see cref="Reject"/>, <see cref="Recover"/> or the
/// consumer's end puts it back at its place, marked redelivered. A consumer given a delivery
/// credit (<see cref="SetDeliveryCredit"/>, ADR-0146 Amendment 7) only holds what it is assigned
/// until <see cref="TryClaimDelivery"/> takes it, and holds at most its credit.
/// </summary>
public sealed class BusQueueConsumer : IDisposable
{
    private readonly MessageBus bus;
    // Null for a get holder, which the bus never delivers to.
    private readonly Action<BusQueueDelivery>? deliver;
    private readonly Dictionary<long, QueuedMessage> unacknowledged = [];
    // Assigned and not yet claimed, first assigned first; only a consumer with a credit holds any.
    private readonly List<(BusQueueDelivery Delivery, QueuedMessage Message)> held = [];
    // Null until a delivery credit is set; never below the number held.
    private uint? deliveryCredit;
    private volatile bool active = true;

    internal BusQueueConsumer(MessageBus bus, BusConnection connection, NamedQueue queue, int maxUnacknowledged, Action<BusQueueDelivery>? deliver)
    {
        this.bus = bus;
        this.deliver = deliver;
        Connection = connection;
        Queue = queue;
        MaxUnacknowledged = maxUnacknowledged;
    }

    /// <summary>
    /// The name of the queue it consumes from.
    /// </summary>
    public string QueueName => Queue.Name;

    /// <summary>
    /// The most deliveries it holds unacknowledged at once, those held and not yet claimed
    /// included (AMQP 0-9-1 <c>basic.qos</c>'s <c>prefetch-count</c>); 0 for no limit.
    /// </summary>
    public int MaxUnacknowledged { get; }

    internal BusConnection Connection { get; }

    internal NamedQueue Queue { get; }

    internal bool IsActive => active;

    internal bool IsStarted { get; set; }

    /// <summary>Whether <see cref="Stop"/> has stopped its deliveries.</summary>
    internal bool IsStopped { get; set; }

    /// <summary>Whether it asked to be its queue's only consumer (AMQP 0-9-1 <c>basic.consume</c>'s
    /// <c>exclusive</c>).</summary>
    internal bool IsExclusive { get; init; }

    /// <summary>Whether it is no consumer at all but holds the messages its connection took with
    /// <see cref="BusConnection.GetFromQueue"/>: never started, never counted.</summary>
    internal bool IsGetHolder { get; init; }

    /// <summary>Which messages it is offered (ADR-0146, decision 2.4); <see langword="null"/> for
    /// every one. Called under the bus's lock.</summary>
    internal Func<BusMessage, bool>? Predicate { get; init; }

    /// <summary>Whether a delivery of <paramref name="message"/> may go to it now: it has room and
    /// its predicate, if any, accepts the message.</summary>
    internal bool HasRoomFor(BusMessage message) => HasRoom && (Predicate is null || Predicate(message));

    /// <summary>Whether it is a consumer still taking deliveries: not stopped, not a get holder.</summary>
    internal bool TakesDeliveries => !IsStopped && !IsGetHolder;

    /// <summary>How many messages it has not settled: those held and not yet claimed included.</summary>
    internal int UnacknowledgedCount => unacknowledged.Count + held.Count;

    /// <summary>Whether a delivery may go to it now: it is started and not stopped, is below
    /// <see cref="MaxUnacknowledged"/> unacknowledged (0 for no limit) and, with a delivery credit,
    /// holds fewer unclaimed than the credit. An ended consumer is no longer among its queue's
    /// consumers, so it is never asked.</summary>
    internal bool HasRoom => IsStarted && !IsStopped && HasUnacknowledgedRoom && HasCreditLeft;

    /// <summary>Whether it has a delivery credit, so what it is assigned is held until claimed.</summary>
    internal bool HasDeliveryCredit => deliveryCredit is not null;

    private bool HasUnacknowledgedRoom => MaxUnacknowledged == 0 || UnacknowledgedCount < MaxUnacknowledged;

    // With no credit nothing is ever held, so any limit lets it through.
    private bool HasCreditLeft => held.Count < (deliveryCredit ?? uint.MaxValue);

    /// <summary>
    /// Starts deliveries to it. A server calls this after telling its client the consumer exists
    /// (AMQP 0-9-1 <c>basic.consume-ok</c>), so no delivery overtakes that reply. Starting it
    /// again, or after it has ended, does nothing.
    /// </summary>
    public void Start() => bus.RunAs(Connection.Exchange, () => bus.StartConsumer(this));

    /// <summary>
    /// Sets its delivery credit: the most deliveries the bus holds for it that it has not yet
    /// claimed (ADR-0146, Amendment 7; AMQP 1.0 link credit). Until this is first called it has no
    /// credit and every delivery it is assigned is handed out at once; those stay handed out. With
    /// one, the bus assigns it a message only while it has room and holds fewer than
    /// <paramref name="credit"/>, and calls its callback, which says only that a delivery is held:
    /// the message counts as unacknowledged, is not marked redelivered for a later delivery,
    /// publishes no <c>Delivered</c> change and cannot yet be acknowledged or rejected, until
    /// <see cref="TryClaimDelivery"/> takes it. So a held message does not expire. Lowering the
    /// credit below the number held puts those assigned last back at their places, publishing no
    /// change for that; one that has expired meanwhile is dropped when the queue reaches it, as any
    /// ready message is. While the consumer is started, not stopped and below
    /// <see cref="MaxUnacknowledged"/>, what the queue has ready that it accepts is assigned, until
    /// it holds <paramref name="credit"/>, before this returns. After the consumer has ended, does
    /// nothing.
    /// </summary>
    /// <param name="credit">The credit; 0 for none, so it is assigned nothing.</param>
    public void SetDeliveryCredit(uint credit) => bus.RunAs(Connection.Exchange, () => bus.SetDeliveryCredit(this, credit));

    /// <summary>
    /// Claims the held delivery assigned first: lowers the delivery credit by one, marks the
    /// message handed out and publishes <c>Delivered</c>. From then on the delivery is
    /// unacknowledged as any other, until it is settled or goes back marked redelivered.
    /// </summary>
    /// <param name="delivery">The delivery claimed, the same one its callback is given for that
    /// assignment; <see langword="null"/> when this answers <see langword="false"/>.</param>
    /// <returns><see langword="false"/> when no delivery is held: none was assigned, each went back,
    /// was claimed or was dropped by a reset or restore, the consumer or its queue has ended, or it
    /// never had a credit.</returns>
    public bool TryClaimDelivery([NotNullWhen(true)] out BusQueueDelivery? delivery)
    {
        delivery = bus.RunAs(Connection.Exchange, () => bus.ClaimDelivery(this));
        return delivery is not null;
    }

    /// <summary>
    /// Acknowledges one delivery: its message leaves the queue.
    /// </summary>
    /// <param name="deliveryTag">The delivery's <see cref="BusQueueDelivery.DeliveryTag"/>.</param>
    /// <returns><see cref="MessageBusOutcome.Succeeded"/>, or
    /// <see cref="MessageBusOutcome.UnknownDeliveryTag"/> when the consumer holds no such
    /// delivery unacknowledged; one held and not yet claimed is none, and stays held.</returns>
    public MessageBusOutcome Acknowledge(long deliveryTag) => bus.RunAs(Connection.Exchange, () => bus.Acknowledge(this, deliveryTag));

    /// <summary>
    /// Rejects one delivery (AMQP 0-9-1 <c>basic.reject</c> and <c>basic.nack</c>, STOMP 1.2
    /// <c>NACK</c>): with <paramref name="requeue"/> its message goes back at its place, marked
    /// redelivered, for any consumer; without it, the message is dropped (the bus keeps no
    /// dead-letter queue).
    /// </summary>
    /// <param name="deliveryTag">The delivery's <see cref="BusQueueDelivery.DeliveryTag"/>.</param>
    /// <param name="requeue">Whether to put the message back rather than drop it.</param>
    /// <returns><see cref="MessageBusOutcome.Succeeded"/>, or
    /// <see cref="MessageBusOutcome.UnknownDeliveryTag"/> when the consumer holds no such
    /// delivery unacknowledged; one held and not yet claimed is none, and stays held.</returns>
    public MessageBusOutcome Reject(long deliveryTag, bool requeue) => bus.RunAs(Connection.Exchange, () => bus.Reject(this, deliveryTag, requeue));

    /// <summary>
    /// Puts every delivery it holds unacknowledged back at its place, marked redelivered (AMQP
    /// 0-9-1 <c>basic.recover</c> with <c>requeue</c> set). One held and not yet claimed goes back
    /// too, keeping the redelivered mark it had before it was assigned. Its delivery credit stays.
    /// </summary>
    public void Recover() => bus.RunAs(Connection.Exchange, () => bus.Recover(this));

    /// <summary>
    /// Stops deliveries to it (AMQP 0-9-1 <c>basic.cancel</c>): it takes no more, no longer counts
    /// as one of its queue's consumers, and keeps every delivery it holds unacknowledged until
    /// <see cref="Acknowledge"/> or <see cref="Reject"/> settles it or <see cref="Dispose"/>
    /// puts it back. A delivery the bus took for it before the stop may still reach its callback,
    /// and one held and not yet claimed can still be claimed.
    /// Stopping it again, or after it has ended, does nothing.
    /// </summary>
    public void Stop() => bus.RunAs(Connection.Exchange, () => bus.StopConsumer(this));

    /// <summary>
    /// Ends the consumer: every delivery it holds unacknowledged goes back at its place, marked
    /// redelivered (one held and not yet claimed keeping the mark it had before it was assigned),
    /// and an auto-delete queue left with no consumer is deleted. Disposing it again does nothing.
    /// </summary>
    public void Dispose() => bus.RunAs(Connection.Exchange, () => bus.Cancel(this));

    internal void Deactivate() => active = false;

    /// <summary>Assigns <paramref name="message"/> to it: held until claimed when it has a delivery
    /// credit, else handed out at once.</summary>
    internal BusQueueDelivery Take(QueuedMessage message, long deliveryTag)
    {
        var delivery = new BusQueueDelivery(Queue.Name, message.Message, deliveryTag, message.HandedOut);
        if (HasDeliveryCredit)
        {
            held.Add((delivery, message));
        }
        else
        {
            HandOut(delivery, message);
        }

        return delivery;
    }

    /// <summary>Hands out the held delivery assigned first, using one of its delivery credit.</summary>
    /// <returns>The delivery, or <see langword="null"/> when none is held.</returns>
    internal BusQueueDelivery? ClaimFirstHeld()
    {
        if (held.Count == 0)
        {
            return null;
        }

        var (delivery, message) = held[0];
        held.RemoveAt(0);
        // Only a consumer with a credit holds a delivery.
        deliveryCredit = deliveryCredit!.Value - 1;
        HandOut(delivery, message);
        return delivery;
    }

    /// <summary>Sets its delivery credit and lets go of the held deliveries past it.</summary>
    /// <returns>The messages of those assigned last, for the queue to take back.</returns>
    internal List<QueuedMessage> LimitDeliveryCredit(uint credit)
    {
        deliveryCredit = credit;
        var kept = (int)Math.Min(credit, held.Count);
        var returned = held.Skip(kept).Select(assigned => assigned.Message).ToList();
        held.RemoveRange(kept, returned.Count);
        return returned;
    }

    /// <summary>Every message it has not settled, held ones included.</summary>
    internal IEnumerable<QueuedMessage> Unacknowledged => unacknowledged.Values.Concat(held.Select(assigned => assigned.Message));

    /// <summary>Takes back one delivery to settle it; a held one is not yet to be settled.</summary>
    internal QueuedMessage? RemoveUnacknowledged(long deliveryTag) =>
        unacknowledged.Remove(deliveryTag, out var message) ? message : null;

    internal List<QueuedMessage> RemoveAllUnacknowledged()
    {
        var all = Unacknowledged.ToList();
        unacknowledged.Clear();
        held.Clear();
        return all;
    }

    private void HandOut(BusQueueDelivery delivery, QueuedMessage message)
    {
        unacknowledged.Add(delivery.DeliveryTag, message);
        message.HandedOut = true;
    }

    /// <summary>Hands <paramref name="delivery"/> to the callback while the consumer is live.</summary>
    /// <returns>Whether it was handed over.</returns>
    internal bool Deliver(BusQueueDelivery delivery)
    {
        var isLive = active;
        if (isLive)
        {
            deliver!(delivery);
        }

        return isLive;
    }
}
