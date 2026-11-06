namespace Surl.MessageBus;

/// <summary>
/// One consumer of a named queue, made by <see cref="BusConnection.Consume"/> (ADR-0100,
/// decision 3.3). Once started, it competes with the queue's other consumers: each message goes
/// to one of them, round robin among those with room, and stays unacknowledged until
/// <see cref="Acknowledge"/> removes it or <see cref="Reject"/>, <see cref="Recover"/> or the
/// consumer's end puts it back at its place, marked redelivered.
/// </summary>
public sealed class BusQueueConsumer : IDisposable
{
    private readonly MessageBus bus;
    // Null for a get holder, which the bus never delivers to.
    private readonly Action<BusQueueDelivery>? deliver;
    private readonly Dictionary<long, QueuedMessage> unacknowledged = [];
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
    /// The most deliveries it holds unacknowledged at once (AMQP 0-9-1 <c>basic.qos</c>'s
    /// <c>prefetch-count</c>); 0 for no limit.
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

    internal int UnacknowledgedCount => unacknowledged.Count;

    /// <summary>Whether a delivery may go to it now. An ended consumer is no longer among its
    /// queue's consumers, so it is never asked.</summary>
    internal bool HasRoom => IsStarted && !IsStopped && (MaxUnacknowledged == 0 || unacknowledged.Count < MaxUnacknowledged);

    /// <summary>
    /// Starts deliveries to it. A server calls this after telling its client the consumer exists
    /// (AMQP 0-9-1 <c>basic.consume-ok</c>), so no delivery overtakes that reply. Starting it
    /// again, or after it has ended, does nothing.
    /// </summary>
    public void Start() => bus.StartConsumer(this);

    /// <summary>
    /// Acknowledges one delivery: its message leaves the queue.
    /// </summary>
    /// <param name="deliveryTag">The delivery's <see cref="BusQueueDelivery.DeliveryTag"/>.</param>
    /// <returns><see cref="MessageBusOutcome.Succeeded"/>, or
    /// <see cref="MessageBusOutcome.UnknownDeliveryTag"/> when the consumer holds no such
    /// delivery unacknowledged.</returns>
    public MessageBusOutcome Acknowledge(long deliveryTag) => bus.Acknowledge(this, deliveryTag);

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
    /// delivery unacknowledged.</returns>
    public MessageBusOutcome Reject(long deliveryTag, bool requeue) => bus.Reject(this, deliveryTag, requeue);

    /// <summary>
    /// Puts every delivery it holds unacknowledged back at its place, marked redelivered (AMQP
    /// 0-9-1 <c>basic.recover</c> with <c>requeue</c> set).
    /// </summary>
    public void Recover() => bus.Recover(this);

    /// <summary>
    /// Stops deliveries to it (AMQP 0-9-1 <c>basic.cancel</c>): it takes no more, no longer counts
    /// as one of its queue's consumers, and keeps every delivery it holds unacknowledged until
    /// <see cref="Acknowledge"/> or <see cref="Reject"/> settles it or <see cref="Dispose"/>
    /// puts it back. A delivery the bus took for it before the stop may still reach its callback.
    /// Stopping it again, or after it has ended, does nothing.
    /// </summary>
    public void Stop() => bus.StopConsumer(this);

    /// <summary>
    /// Ends the consumer: every delivery it holds unacknowledged goes back at its place, marked
    /// redelivered, and an auto-delete queue left with no consumer is deleted. Disposing it again
    /// does nothing.
    /// </summary>
    public void Dispose() => bus.Cancel(this);

    internal void Deactivate() => active = false;

    internal BusQueueDelivery Take(QueuedMessage message, long deliveryTag)
    {
        unacknowledged.Add(deliveryTag, message);
        var delivery = new BusQueueDelivery(Queue.Name, message.Message, deliveryTag, message.HandedOut);
        message.HandedOut = true;
        return delivery;
    }

    internal IEnumerable<QueuedMessage> Unacknowledged => unacknowledged.Values;

    internal QueuedMessage? RemoveUnacknowledged(long deliveryTag) =>
        unacknowledged.Remove(deliveryTag, out var message) ? message : null;

    internal List<QueuedMessage> RemoveAllUnacknowledged()
    {
        var all = unacknowledged.Values.ToList();
        unacknowledged.Clear();
        return all;
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
