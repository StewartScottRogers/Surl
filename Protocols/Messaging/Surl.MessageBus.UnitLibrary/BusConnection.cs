namespace Surl.MessageBus;

/// <summary>
/// One server connection's place on a <see cref="MessageBus"/>, made by
/// <see cref="MessageBus.Connect(string?)"/>: it holds that connection's subscriptions, counts them
/// against <see cref="MessageBus.MaxSubscriptionsPerConnection"/>, and ends them all when
/// disposed, as the server does when its client goes.
/// </summary>
public sealed class BusConnection : IDisposable
{
    private readonly MessageBus bus;

    internal BusConnection(MessageBus bus, string? account, BusExchange? exchange)
    {
        this.bus = bus;
        Account = account;
        Exchange = exchange;
    }

    /// <summary>
    /// The exchange the connection's server is serving, which every change and fault this
    /// connection's operations cause names (ADR-0124); <see langword="null"/> when anonymous.
    /// </summary>
    public BusExchange? Exchange { get; }

    /// <summary>
    /// The account the connection's client logged in as, which its subscribes, bindings and
    /// consumes are checked for; <see langword="null"/> for an anonymous client.
    /// </summary>
    public string? Account { get; }

    /// <summary>
    /// Called with each live message once it has been handed to every one of this connection's
    /// subscriptions it reached, after their callbacks and on the same terms; <see langword="null"/>
    /// for none. A server whose protocol sends a message matching several of a client's
    /// subscriptions once (MQTT 5.0 section 3.3.4) gathers the callbacks and sends here.
    /// </summary>
    public Action<BusMessage>? AfterMessageDelivered { get; set; }

    internal int SubscriptionCount { get; set; }

    internal bool IsClosed { get; set; }

    /// <summary>
    /// Subscribes to every message later published on a topic <paramref name="filter"/> matches.
    /// </summary>
    /// <param name="filter">A canonical topic filter (<see cref="CanonicalTopicName.IsValidFilter"/>).</param>
    /// <param name="deliver">Called with each matching message, in publish order, one call at a
    /// time across the whole bus and never while the bus holds its lock. It must return quickly
    /// and not block: it hands the message to its connection's own outgoing queue. It may
    /// publish or subscribe. An exception it throws reaches the publisher whose call was
    /// delivering, and the deliveries still waiting go out with the next publish.</param>
    /// <param name="group">The subscription's fan-out group, or <see langword="null"/> for none
    /// (<see cref="BusSubscription.Group"/>).</param>
    /// <param name="includeRetained">Whether to return the retained messages the filter matches,
    /// as MQTT's subscribe does; no other protocol asks (ADR-0100, decision 3.3).</param>
    /// <param name="noLocal">Whether messages this connection publishes through
    /// <see cref="Publish"/> are kept from the subscription, as MQTT 5's No Local option asks
    /// (<see cref="BusSubscription.NoLocal"/>).</param>
    /// <returns>The outcome - <see cref="MessageBusOutcome.AccessDenied"/> when the configured
    /// grants give <see cref="Account"/> no <c>sub</c> covering the filter - the subscription when
    /// made, and the retained messages asked for.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="deliver"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The connection has been disposed.</exception>
    public BusSubscribeResult Subscribe(string filter, Action<BusMessage> deliver, string? group = null, bool includeRetained = false, bool noLocal = false)
    {
        ArgumentNullException.ThrowIfNull(deliver);
        return bus.RunAs(Exchange, () => bus.Subscribe(this, filter, deliver, group, includeRetained, noLocal));
    }

    /// <summary>
    /// Publishes <paramref name="message"/> as <see cref="MessageBus.Publish(BusMessage, string?)"/>
    /// does for <see cref="Account"/>, marked as this connection's own, so this connection's
    /// <see cref="BusSubscription.NoLocal"/> subscriptions do not receive it.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The outcome and how many subscriptions it was handed to.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public BusPublishResult Publish(BusMessage message) => bus.RunAs(Exchange, () => bus.Publish(message, Account, this, onlyToConsumedQueues: false));

    /// <summary>
    /// Publishes <paramref name="message"/> as <see cref="Publish(BusMessage)"/> does, but copies it
    /// only into the bound queues one of whose consumers could take it now (AMQP 0-9-1
    /// <c>basic.publish</c>'s <c>immediate</c>, ADR-0112 decision 6). Live subscriptions receive it
    /// as ever.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The outcome, how many subscriptions it was handed to,
    /// <see cref="BusPublishResult.QueuedCount"/> the queues that took it, and
    /// <see cref="BusPublishResult.UnconsumedQueueCount"/> the bound queues passed over.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public BusPublishResult PublishToConsumedQueues(BusMessage message) => bus.RunAs(Exchange, () => bus.Publish(message, Account, this, onlyToConsumedQueues: true));

    /// <summary>
    /// Publishes an ordered batch all or none, as the AMQP 1.0 container commits a transaction
    /// (ADR-0146, decision 1.6). Each entry is checked as its own <see cref="Publish(BusMessage)"/>
    /// or <see cref="MessageBus.PublishToQueue(string, BusMessage, string?)"/> for
    /// <see cref="Account"/> would be, against the bus as the entries before it would leave it; when
    /// any one would be refused, nothing is published and no store change is raised. Otherwise every
    /// entry is published in batch order, with the same deliveries, queue copies, log appends,
    /// retained changes and events as its own publish. The check and the publish happen under one
    /// hold of the bus's lock, so no other publish interleaves. A bound queue too full for a topic
    /// entry's copy refuses only that copy, as for a single publish
    /// (<see cref="BusPublishResult.FullQueueCount"/>).
    /// </summary>
    /// <param name="entries">The entries, in the order they are published.</param>
    /// <returns>The outcome; when refused, the index of the first entry refused and why, as its own
    /// publish would have answered; when published, each entry's result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">An entry is <see langword="null"/>.</exception>
    public BusBatchPublishResult PublishBatch(IReadOnlyList<BusBatchEntry> entries) => bus.RunAs(Exchange, () => bus.PublishBatch(this, entries));

    /// <summary>
    /// Declares a named queue (AMQP 0-9-1 <c>queue.declare</c>): makes it when absent, and
    /// otherwise checks it was made with the same <paramref name="flags"/> and, when
    /// <paramref name="subscriptionDescription"/> is given, the same description. An exclusive queue
    /// belongs to this connection.
    /// </summary>
    /// <param name="name">The queue's name; not empty.</param>
    /// <param name="flags">Its flags.</param>
    /// <param name="subscriptionDescription">Opaque text kept beside the queue, as the AMQP 1.0
    /// container keeps a durable subscription's address and filters (ADR-0146, decision 3.2):
    /// persisted, reset, snapshotted and restored with the queue and answered by
    /// <see cref="InspectQueue"/>. <see langword="null"/> makes a queue with none and, on an existing
    /// queue, checks nothing.</param>
    /// <returns>The outcome - <see cref="MessageBusOutcome.InvalidQueueName"/>,
    /// <see cref="MessageBusOutcome.TooManyQueues"/>, <see cref="MessageBusOutcome.QueueLocked"/>,
    /// <see cref="MessageBusOutcome.QueueFlagsDiffer"/> or
    /// <see cref="MessageBusOutcome.QueueDescriptionDiffers"/> when refused - and the queue's counts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The connection has been disposed.</exception>
    public BusQueueDeclareResult DeclareQueue(string name, BusQueueFlags flags = default, string? subscriptionDescription = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        return bus.RunAs(Exchange, () => bus.DeclareQueue(this, name, flags, subscriptionDescription));
    }

    /// <summary>
    /// Binds a queue to a canonical filter: every later publish on a matching topic is copied
    /// into it, once however many of its filters match. Binding the same filter again does nothing.
    /// </summary>
    /// <param name="queueName">The queue's name.</param>
    /// <param name="filter">A canonical topic filter (<see cref="CanonicalTopicName.IsValidFilter"/>).</param>
    /// <returns><see cref="MessageBusOutcome.Succeeded"/>, <see cref="MessageBusOutcome.InvalidFilter"/>,
    /// <see cref="MessageBusOutcome.AccessDenied"/> (no <c>sub</c> covering the filter),
    /// <see cref="MessageBusOutcome.UnknownQueue"/> or <see cref="MessageBusOutcome.QueueLocked"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The connection has been disposed.</exception>
    public MessageBusOutcome BindQueue(string queueName, string filter)
    {
        ArgumentNullException.ThrowIfNull(queueName);
        ArgumentNullException.ThrowIfNull(filter);
        return bus.RunAs(Exchange, () => bus.BindQueue(this, queueName, filter, bind: true));
    }

    /// <summary>
    /// Removes a queue's binding to a filter; a filter it is not bound to is no error.
    /// </summary>
    /// <param name="queueName">The queue's name.</param>
    /// <param name="filter">The filter it was bound with.</param>
    /// <returns><see cref="MessageBusOutcome.Succeeded"/>, <see cref="MessageBusOutcome.InvalidFilter"/>,
    /// <see cref="MessageBusOutcome.UnknownQueue"/> or <see cref="MessageBusOutcome.QueueLocked"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The connection has been disposed.</exception>
    public MessageBusOutcome UnbindQueue(string queueName, string filter)
    {
        ArgumentNullException.ThrowIfNull(queueName);
        ArgumentNullException.ThrowIfNull(filter);
        return bus.RunAs(Exchange, () => bus.BindQueue(this, queueName, filter, bind: false));
    }

    /// <summary>
    /// Deletes a queue with every message in it; its consumers receive nothing more.
    /// </summary>
    /// <param name="queueName">The queue's name.</param>
    /// <returns><see cref="MessageBusOutcome.Succeeded"/>, <see cref="MessageBusOutcome.UnknownQueue"/>
    /// or <see cref="MessageBusOutcome.QueueLocked"/>; <see cref="MessageBusOutcome.AccessDenied"/>
    /// for a declared queue, which no client may delete (ADR-0102, decision 3.2).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="queueName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The connection has been disposed.</exception>
    public MessageBusOutcome DeleteQueue(string queueName) => DeleteQueue(queueName, ifUnused: false, ifEmpty: false).Outcome;

    /// <summary>
    /// Deletes a queue as <see cref="DeleteQueue(string)"/> does, unless a condition asked for
    /// fails (AMQP 0-9-1 <c>queue.delete</c>); the check and the delete are one bus operation.
    /// </summary>
    /// <param name="queueName">The queue's name.</param>
    /// <param name="ifUnused">Whether to refuse while a consumer still takes deliveries from it.</param>
    /// <param name="ifEmpty">Whether to refuse while it holds any message, ready or unacknowledged.</param>
    /// <returns>The outcome - as <see cref="DeleteQueue(string)"/>'s, or
    /// <see cref="MessageBusOutcome.QueueInUse"/> or <see cref="MessageBusOutcome.QueueNotEmpty"/>
    /// when a condition failed - and how many messages, ready and unacknowledged, went with it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="queueName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The connection has been disposed.</exception>
    public BusQueueRemovalResult DeleteQueue(string queueName, bool ifUnused, bool ifEmpty)
    {
        ArgumentNullException.ThrowIfNull(queueName);
        return bus.RunAs(Exchange, () => bus.DeleteQueue(this, queueName, ifUnused, ifEmpty));
    }

    /// <summary>
    /// Looks a queue up without creating it (AMQP 0-9-1 <c>queue.declare</c> with <c>passive</c>).
    /// </summary>
    /// <param name="queueName">The queue's name.</param>
    /// <returns>The outcome - <see cref="MessageBusOutcome.UnknownQueue"/> or
    /// <see cref="MessageBusOutcome.QueueLocked"/> when refused - and the queue's counts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="queueName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The connection has been disposed.</exception>
    public BusQueueInspectResult InspectQueue(string queueName)
    {
        ArgumentNullException.ThrowIfNull(queueName);
        return bus.RunAs(Exchange, () => bus.InspectQueue(this, queueName));
    }

    /// <summary>
    /// Removes every ready message from a queue; unacknowledged ones stay with their consumers
    /// (AMQP 0-9-1 <c>queue.purge</c>).
    /// </summary>
    /// <param name="queueName">The queue's name.</param>
    /// <returns>The outcome - <see cref="MessageBusOutcome.UnknownQueue"/> or
    /// <see cref="MessageBusOutcome.QueueLocked"/> when refused - and how many messages it removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="queueName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The connection has been disposed.</exception>
    public BusQueueRemovalResult PurgeQueue(string queueName)
    {
        ArgumentNullException.ThrowIfNull(queueName);
        return bus.RunAs(Exchange, () => bus.PurgeQueue(this, queueName));
    }

    /// <summary>
    /// Takes the queue's next ready message, high priority first, without a consumer (AMQP 0-9-1
    /// <c>basic.get</c>). It is held unacknowledged for this connection, counted in the queue, until
    /// <see cref="AcknowledgeGet"/> or <see cref="RejectGet"/> settles it or the connection ends,
    /// which puts it back at its place, marked redelivered.
    /// </summary>
    /// <param name="queueName">The queue's name.</param>
    /// <returns>The outcome - <see cref="MessageBusOutcome.UnknownQueue"/>,
    /// <see cref="MessageBusOutcome.QueueLocked"/>, or <see cref="MessageBusOutcome.AccessDenied"/>
    /// when the grants give <see cref="Account"/> no <c>sub</c> on <c>$queue/&lt;name&gt;</c> - the
    /// delivery, or none when no message was ready, and how many messages are still ready.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="queueName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The connection has been disposed.</exception>
    public BusGetResult GetFromQueue(string queueName)
    {
        ArgumentNullException.ThrowIfNull(queueName);
        return bus.RunAs(Exchange, () => bus.GetFromQueue(this, queueName));
    }

    /// <summary>
    /// Acknowledges a delivery <see cref="GetFromQueue"/> returned: its message leaves the queue.
    /// </summary>
    /// <param name="queueName">The delivery's <see cref="BusQueueDelivery.QueueName"/>.</param>
    /// <param name="deliveryTag">The delivery's <see cref="BusQueueDelivery.DeliveryTag"/>.</param>
    /// <returns><see cref="MessageBusOutcome.Succeeded"/>, or
    /// <see cref="MessageBusOutcome.UnknownDeliveryTag"/> when this connection holds no such
    /// delivery from that queue.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="queueName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The connection has been disposed.</exception>
    public MessageBusOutcome AcknowledgeGet(string queueName, long deliveryTag)
    {
        ArgumentNullException.ThrowIfNull(queueName);
        return bus.RunAs(Exchange, () => bus.SettleGet(this, queueName, deliveryTag, acknowledge: true, requeue: false));
    }

    /// <summary>
    /// Rejects a delivery <see cref="GetFromQueue"/> returned, as
    /// <see cref="BusQueueConsumer.Reject"/> rejects a consumer's.
    /// </summary>
    /// <param name="queueName">The delivery's <see cref="BusQueueDelivery.QueueName"/>.</param>
    /// <param name="deliveryTag">The delivery's <see cref="BusQueueDelivery.DeliveryTag"/>.</param>
    /// <param name="requeue">Whether to put the message back, marked redelivered, rather than drop it.</param>
    /// <returns><see cref="MessageBusOutcome.Succeeded"/>, or
    /// <see cref="MessageBusOutcome.UnknownDeliveryTag"/> when this connection holds no such
    /// delivery from that queue.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="queueName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The connection has been disposed.</exception>
    public MessageBusOutcome RejectGet(string queueName, long deliveryTag, bool requeue)
    {
        ArgumentNullException.ThrowIfNull(queueName);
        return bus.RunAs(Exchange, () => bus.SettleGet(this, queueName, deliveryTag, acknowledge: false, requeue));
    }

    /// <summary>
    /// Makes a consumer of a queue, counted against
    /// <see cref="MessageBus.MaxSubscriptionsPerConnection"/>. It receives nothing until
    /// <see cref="BusQueueConsumer.Start"/>.
    /// </summary>
    /// <param name="queueName">The queue's name.</param>
    /// <param name="deliver">Called with each delivery, on the same terms as a subscription's
    /// callback (<see cref="Subscribe"/>). It may acknowledge or reject.</param>
    /// <param name="maxUnacknowledged">The most deliveries the consumer holds unacknowledged at
    /// once; 0 for no limit.</param>
    /// <param name="exclusive">Whether it must be the queue's only consumer still taking
    /// deliveries (AMQP 0-9-1 <c>basic.consume</c>'s <c>exclusive</c>): refused while the queue has
    /// one, and while it lasts every other consume of the queue is refused.</param>
    /// <param name="accepts">Which messages the consumer is offered (ADR-0146, decision 2.4), or
    /// <see langword="null"/> for every one. A message no consumer with room accepts stays ready, in
    /// its place, and round robin runs among the consumers that accept it. The bus calls it under its
    /// lock, any number of times per message: it must be pure and quick, and never call the bus.</param>
    /// <returns>The outcome - <see cref="MessageBusOutcome.UnknownQueue"/>,
    /// <see cref="MessageBusOutcome.QueueLocked"/>, <see cref="MessageBusOutcome.ExclusiveConsumeRefused"/>,
    /// <see cref="MessageBusOutcome.TooManySubscriptions"/>, or <see cref="MessageBusOutcome.AccessDenied"/>
    /// when the grants give <see cref="Account"/> no <c>sub</c> on <c>$queue/&lt;name&gt;</c>, when
    /// refused - and the consumer.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxUnacknowledged"/> is negative.</exception>
    /// <exception cref="ObjectDisposedException">The connection has been disposed.</exception>
    public BusConsumeResult Consume(string queueName, Action<BusQueueDelivery> deliver, int maxUnacknowledged = 0, bool exclusive = false, Func<BusMessage, bool>? accepts = null)
    {
        ArgumentNullException.ThrowIfNull(queueName);
        ArgumentNullException.ThrowIfNull(deliver);
        ArgumentOutOfRangeException.ThrowIfNegative(maxUnacknowledged);
        return bus.RunAs(Exchange, () => bus.Consume(this, queueName, deliver, maxUnacknowledged, exclusive, accepts));
    }

    /// <summary>
    /// Ends every subscription and consumer the connection holds - each consumer's
    /// unacknowledged deliveries go back to their queues, marked redelivered - and deletes the
    /// queues exclusive to it; it can make no more. Disposing it again does nothing.
    /// </summary>
    public void Dispose() => bus.RunAs(Exchange, () => bus.Disconnect(this));
}
