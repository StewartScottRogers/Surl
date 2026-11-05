using Surl.Protocol.Abstractions;

namespace Surl.MessageBus;

/// <summary>
/// The bus's named queues (ADR-0100, decision 3.3): FIFOs of messages with competing
/// consumers, each delivery unacknowledged until acknowledged, and put back at its place,
/// marked redelivered, by a reject with requeue, a recover, or its consumer's end.
/// </summary>
public sealed partial class MessageBus
{
    private readonly Dictionary<string, NamedQueue> queues = new(StringComparer.Ordinal);
    private long queuedBytes;

    /// <summary>
    /// Puts <paramref name="message"/> straight into one named queue, as AMQP 0-9-1's default
    /// exchange and a STOMP <c>SEND</c> to <c>/queue/&lt;q&gt;</c> do. Its topic is not checked:
    /// a queue message is delivered by its queue's name. A message already expired is dropped.
    /// Any connection may send to any queue, an exclusive one included. It is sent for an
    /// anonymous caller: <see cref="PublishToQueue(string, BusMessage, string?)"/> with no account.
    /// </summary>
    /// <param name="queueName">The queue's name.</param>
    /// <param name="message">The message. The bus keeps its own copy of every byte.</param>
    /// <returns><see cref="MessageBusOutcome.Succeeded"/>, <see cref="MessageBusOutcome.MessageTooLarge"/>,
    /// <see cref="MessageBusOutcome.AccessDenied"/>, <see cref="MessageBusOutcome.UnknownQueue"/>
    /// or <see cref="MessageBusOutcome.QueueFull"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public MessageBusOutcome PublishToQueue(string queueName, BusMessage message) =>
        PublishToQueue(queueName, message, account: null);

    /// <summary>
    /// Puts <paramref name="message"/> straight into one named queue as
    /// <see cref="PublishToQueue(string, BusMessage)"/> says, for <paramref name="account"/>:
    /// refused with <see cref="MessageBusOutcome.AccessDenied"/>, whether or not the queue
    /// exists, when the configured grants give it no <c>pub</c> on <c>$queue/&lt;name&gt;</c>
    /// (ADR-0102, decision 3.4).
    /// </summary>
    /// <param name="queueName">The queue's name.</param>
    /// <param name="message">The message. The bus keeps its own copy of every byte.</param>
    /// <param name="account">The sender's account, or <see langword="null"/> for an anonymous one.</param>
    /// <returns><see cref="MessageBusOutcome.Succeeded"/>, <see cref="MessageBusOutcome.MessageTooLarge"/>,
    /// <see cref="MessageBusOutcome.AccessDenied"/>, <see cref="MessageBusOutcome.UnknownQueue"/>
    /// or <see cref="MessageBusOutcome.QueueFull"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="queueName"/> or
    /// <paramref name="message"/> is <see langword="null"/>.</exception>
    public MessageBusOutcome PublishToQueue(string queueName, BusMessage message, string? account) =>
        PublishToQueue(queueName, message, account, onlyWhenConsumed: false);

    /// <summary>
    /// Puts <paramref name="message"/> straight into one named queue as
    /// <see cref="PublishToQueue(string, BusMessage, string?)"/> says, and with
    /// <paramref name="onlyWhenConsumed"/> only when one of the queue's consumers could take it now
    /// (AMQP 0-9-1 <c>basic.publish</c>'s <c>immediate</c>, ADR-0112 decision 6).
    /// </summary>
    /// <param name="queueName">The queue's name.</param>
    /// <param name="message">The message. The bus keeps its own copy of every byte.</param>
    /// <param name="account">The sender's account, or <see langword="null"/> for an anonymous one.</param>
    /// <param name="onlyWhenConsumed">Whether the queue takes the message only when a started,
    /// unstopped consumer of it has room.</param>
    /// <returns>As <see cref="PublishToQueue(string, BusMessage, string?)"/>, and
    /// <see cref="MessageBusOutcome.NoReadyConsumer"/> when <paramref name="onlyWhenConsumed"/> found
    /// no consumer able to take it. <see cref="MessageBusOutcome.QueueFull"/> also answers a send to an
    /// existing queue whose <see cref="QueueChangeName"/> an out-of-space rule of the fault plan
    /// matches (ADR-0124, decision 4); the plan is asked only when the queue exists and the message
    /// has not expired.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="queueName"/> or
    /// <paramref name="message"/> is <see langword="null"/>.</exception>
    public MessageBusOutcome PublishToQueue(string queueName, BusMessage message, string? account, bool onlyWhenConsumed)
    {
        ArgumentNullException.ThrowIfNull(queueName);
        ArgumentNullException.ThrowIfNull(message);
        if (message.PayloadAndNativeFormBytes > MaxMessageBytes)
        {
            return MessageBusOutcome.MessageTooLarge;
        }

        if (!accessPolicy.MayUseQueue(account, TopicAccessRights.Publish, queueName))
        {
            return MessageBusOutcome.AccessDenied;
        }

        var now = timeProvider.GetUtcNow();
        var published = Copy(message, now);
        var isOutOfSpace = !IsExpired(published, now) && HasQueue(queueName) && IsOutOfSpace(account, QueueChangeName(queueName));
        return ChangeState(() =>
            !queues.TryGetValue(queueName, out var queue) ? MessageBusOutcome.UnknownQueue
            : IsExpired(published, now) ? MessageBusOutcome.Succeeded
            : isOutOfSpace ? MessageBusOutcome.QueueFull
            : onlyWhenConsumed && !queue.HasConsumerWithRoom ? MessageBusOutcome.NoReadyConsumer
            : Enqueue(queue, published, now));
    }

    private bool HasQueue(string queueName)
    {
        lock (stateLock)
        {
            return queues.ContainsKey(queueName);
        }
    }

    internal BusQueueDeclareResult DeclareQueue(BusConnection connection, string name, BusQueueFlags flags)
    {
        if (name.Length == 0)
        {
            return new(MessageBusOutcome.InvalidQueueName, 0, 0);
        }

        lock (stateLock)
        {
            ObjectDisposedException.ThrowIf(connection.IsClosed, connection);
            if (queues.TryGetValue(name, out var existing))
            {
                return Redeclare(connection, existing, flags);
            }

            if (queues.Count >= MaxQueues)
            {
                return new(MessageBusOutcome.TooManyQueues, 0, 0);
            }

            queues.Add(name, new NamedQueue(name, flags, flags.Exclusive ? connection : null));
            queuesVersion++;
            PublishQueueChange(StoreChange.Written, name, byteCount: null);
            return new(MessageBusOutcome.Succeeded, 0, 0);
        }
    }

    internal MessageBusOutcome BindQueue(BusConnection connection, string queueName, string filter, bool bind)
    {
        if (!CanonicalTopicName.IsValidFilter(filter))
        {
            return MessageBusOutcome.InvalidFilter;
        }

        if (bind && !accessPolicy.MaySubscribe(connection.Account, filter))
        {
            return MessageBusOutcome.AccessDenied;
        }

        lock (stateLock)
        {
            var (outcome, queue) = Access(connection, queueName);
            if (queue is null)
            {
                return outcome;
            }

            if (bind)
            {
                queue.Bind(filter);
            }
            else
            {
                queue.Unbind(filter);
            }

            queuesVersion++;

            return outcome;
        }
    }

    internal BusQueueInspectResult InspectQueue(BusConnection connection, string queueName)
    {
        lock (stateLock)
        {
            var (outcome, queue) = Access(connection, queueName);
            return queue is null ? new(outcome, 0, 0) : new(outcome, queue.ReadyCount, queue.ActiveConsumerCount);
        }
    }

    internal BusQueueRemovalResult PurgeQueue(BusConnection connection, string queueName)
    {
        lock (stateLock)
        {
            var (outcome, queue) = Access(connection, queueName);
            if (queue is null)
            {
                return new(outcome, 0);
            }

            var (count, bytes) = queue.RemoveAllReady();
            queuedBytes -= bytes;
            queuesVersion++;
            if (count > 0)
            {
                PublishQueueChange(StoreChange.Deleted, queueName, bytes);
            }

            return new(outcome, count);
        }
    }

    internal BusQueueRemovalResult DeleteQueue(BusConnection connection, string queueName, bool ifUnused, bool ifEmpty)
    {
        if (declaredQueueNames.Contains(queueName))
        {
            return new(MessageBusOutcome.AccessDenied, 0);
        }

        lock (stateLock)
        {
            var (outcome, queue) = Access(connection, queueName);
            if (queue is null)
            {
                return new(outcome, 0);
            }

            var refusal = queue.DeleteRefusal(ifUnused, ifEmpty);
            if (refusal is not MessageBusOutcome.Succeeded)
            {
                return new(refusal, 0);
            }

            var count = queue.MessageCount;
            Delete(queue);
            return new(outcome, count);
        }
    }

    internal BusGetResult GetFromQueue(BusConnection connection, string queueName)
    {
        if (!accessPolicy.MayUseQueue(connection.Account, TopicAccessRights.Subscribe, queueName))
        {
            return new(MessageBusOutcome.AccessDenied, null, 0);
        }

        return ChangeState(() =>
        {
            var (outcome, queue) = Access(connection, queueName);
            if (queue is null)
            {
                return new BusGetResult(outcome, null, 0);
            }

            var holder = queue.GetHolderOf(connection) ?? AddGetHolder(connection, queue);
            var (delivery, droppedBytes) = queue.TakeOne(holder, timeProvider.GetUtcNow());
            queuedBytes -= droppedBytes;
            queuesVersion++;
            if (delivery is not null)
            {
                PublishQueueChange(StoreChange.Delivered, queueName, delivery.Message.Payload.Length);
            }

            return new BusGetResult(outcome, delivery, queue.ReadyCount);
        });
    }

    internal MessageBusOutcome SettleGet(BusConnection connection, string queueName, long deliveryTag, bool acknowledge, bool requeue)
    {
        BusQueueConsumer? holder;
        lock (stateLock)
        {
            ObjectDisposedException.ThrowIf(connection.IsClosed, connection);
            holder = queues.GetValueOrDefault(queueName)?.GetHolderOf(connection);
        }

        return holder is null ? MessageBusOutcome.UnknownDeliveryTag
            : acknowledge ? Acknowledge(holder, deliveryTag)
            : Reject(holder, deliveryTag, requeue);
    }

    internal BusConsumeResult Consume(BusConnection connection, string queueName, Action<BusQueueDelivery> deliver, int maxUnacknowledged, bool exclusive)
    {
        if (!accessPolicy.MayUseQueue(connection.Account, TopicAccessRights.Subscribe, queueName))
        {
            return new(MessageBusOutcome.AccessDenied, null);
        }

        lock (stateLock)
        {
            var (outcome, queue) = Access(connection, queueName);
            if (queue is null)
            {
                return new(outcome, null);
            }

            if (exclusive ? queue.ActiveConsumerCount > 0 : queue.IsConsumedExclusively)
            {
                return new(MessageBusOutcome.ExclusiveConsumeRefused, null);
            }

            if (connection.SubscriptionCount >= MaxSubscriptionsPerConnection)
            {
                return new(MessageBusOutcome.TooManySubscriptions, null);
            }

            var consumer = new BusQueueConsumer(this, connection, queue, maxUnacknowledged, deliver) { IsExclusive = exclusive };
            queue.AddConsumer(consumer);
            connection.SubscriptionCount++;
            return new(MessageBusOutcome.Succeeded, consumer);
        }
    }

    internal void StartConsumer(BusQueueConsumer consumer) => ChangeState(() =>
    {
        consumer.IsStarted = true;
        Dispatch(consumer.Queue, timeProvider.GetUtcNow());
        return true;
    });

    internal MessageBusOutcome Acknowledge(BusQueueConsumer consumer, long deliveryTag) =>
        Settle(consumer, deliveryTag, requeue: false, StoreChange.Acknowledged);

    internal MessageBusOutcome Reject(BusQueueConsumer consumer, long deliveryTag, bool requeue) =>
        Settle(consumer, deliveryTag, requeue, StoreChange.Deleted);

    internal void Recover(BusQueueConsumer consumer) => ChangeState(() =>
    {
        RequeueAll(consumer);
        Dispatch(consumer.Queue, timeProvider.GetUtcNow());
        return true;
    });

    internal void Cancel(BusQueueConsumer consumer) => ChangeState(() =>
    {
        End(consumer);
        return true;
    });

    internal void StopConsumer(BusQueueConsumer consumer)
    {
        lock (stateLock)
        {
            consumer.IsStopped = true;
        }
    }

    private BusQueueConsumer AddGetHolder(BusConnection connection, NamedQueue queue)
    {
        var holder = new BusQueueConsumer(this, connection, queue, 0, deliver: null) { IsGetHolder = true };
        queue.AddConsumer(holder);
        return holder;
    }

    private (MessageBusOutcome Outcome, NamedQueue? Queue) Access(BusConnection connection, string queueName)
    {
        ObjectDisposedException.ThrowIf(connection.IsClosed, connection);
        return !queues.TryGetValue(queueName, out var queue) ? (MessageBusOutcome.UnknownQueue, null)
            : queue.IsLockedAgainst(connection) ? (MessageBusOutcome.QueueLocked, null)
            : (MessageBusOutcome.Succeeded, queue);
    }

    private static BusQueueDeclareResult Redeclare(BusConnection connection, NamedQueue existing, BusQueueFlags flags) =>
        existing.IsLockedAgainst(connection) ? new(MessageBusOutcome.QueueLocked, 0, 0)
        : existing.Flags != flags ? new(MessageBusOutcome.QueueFlagsDiffer, 0, 0)
        : new(MessageBusOutcome.Succeeded, existing.ReadyCount, existing.ActiveConsumerCount);

    private (int QueuedCount, int FullQueueCount, int UnconsumedQueueCount) CopyIntoBoundQueues(BusMessage published, string[] topicLevels, DateTimeOffset now, bool onlyToConsumedQueues)
    {
        var queuedCount = 0;
        var fullQueueCount = 0;
        var unconsumedQueueCount = 0;
        foreach (var queue in queues.Values.Where(queue => queue.IsBoundTo(topicLevels)))
        {
            if (onlyToConsumedQueues && !queue.HasConsumerWithRoom)
            {
                unconsumedQueueCount++;
            }
            else if (Enqueue(queue, published, now) is MessageBusOutcome.Succeeded)
            {
                queuedCount++;
            }
            else
            {
                fullQueueCount++;
            }
        }

        return (queuedCount, fullQueueCount, unconsumedQueueCount);
    }

    private MessageBusOutcome Enqueue(NamedQueue queue, BusMessage published, DateTimeOffset now)
    {
        var length = published.PayloadAndNativeFormBytes;
        if (queue.MessageCount >= MaxMessagesPerQueue || queuedBytes + length > MaxQueuedBytes)
        {
            return MessageBusOutcome.QueueFull;
        }

        queue.Enqueue(published);
        queuedBytes += length;
        PublishQueueChange(StoreChange.Enqueued, queue.Name, published.Payload.Length);
        Dispatch(queue, now);
        return MessageBusOutcome.Succeeded;
    }

    private void Dispatch(NamedQueue queue, DateTimeOffset now)
    {
        queuesVersion++;
        var (deliveries, droppedBytes) = queue.Dispatch(now);
        queuedBytes -= droppedBytes;
        if (deliveries.Count > 0)
        {
            waitingDeliveries.Enqueue(() => DeliverToConsumers(deliveries));
        }
    }

    private void DeliverToConsumers(List<(BusQueueConsumer Consumer, BusQueueDelivery Delivery)> deliveries)
    {
        foreach (var (consumer, delivery) in deliveries)
        {
            if (consumer.Deliver(delivery))
            {
                PublishQueueChange(StoreChange.Delivered, delivery.QueueName, delivery.Message.Payload.Length);
            }
        }
    }

    // The change a settle that drops the message publishes: Acknowledged, or Deleted for a reject.
    private MessageBusOutcome Settle(BusQueueConsumer consumer, long deliveryTag, bool requeue, StoreChange droppedChange) => ChangeState(() =>
    {
        if (consumer.RemoveUnacknowledged(deliveryTag) is not { } message)
        {
            return MessageBusOutcome.UnknownDeliveryTag;
        }

        if (requeue)
        {
            consumer.Queue.Requeue(message);
        }
        else
        {
            queuedBytes -= message.Message.PayloadAndNativeFormBytes;
            PublishQueueChange(droppedChange, consumer.QueueName, message.Message.Payload.Length);
        }

        Dispatch(consumer.Queue, timeProvider.GetUtcNow());
        return MessageBusOutcome.Succeeded;
    });

    private static void RequeueAll(BusQueueConsumer consumer)
    {
        foreach (var message in consumer.RemoveAllUnacknowledged())
        {
            consumer.Queue.Requeue(message);
        }
    }

    private void End(BusQueueConsumer consumer)
    {
        if (!consumer.IsActive)
        {
            return;
        }

        consumer.Deactivate();
        RequeueAll(consumer);
        var queue = consumer.Queue;
        queue.RemoveConsumer(consumer);
        CountEnded(consumer);
        if (queue.Flags.AutoDelete && queue.Consumers.All(remaining => remaining.IsGetHolder))
        {
            Delete(queue);
        }
        else
        {
            Dispatch(queue, timeProvider.GetUtcNow());
        }
    }

    private void Delete(NamedQueue queue)
    {
        queuesVersion++;
        foreach (var consumer in queue.Consumers.ToList())
        {
            consumer.Deactivate();
            queuedBytes -= NamedQueue.QueuedBytes(consumer.RemoveAllUnacknowledged());
            queue.RemoveConsumer(consumer);
            CountEnded(consumer);
        }

        queuedBytes -= queue.RemoveAllReady().Bytes;
        queues.Remove(queue.Name);
        PublishQueueChange(StoreChange.Deleted, queue.Name, byteCount: null);
    }

    private static void CountEnded(BusQueueConsumer consumer)
    {
        if (!consumer.IsGetHolder)
        {
            consumer.Connection.SubscriptionCount--;
        }
    }

    private void EndQueuesOf(BusConnection connection)
    {
        foreach (var consumer in queues.Values.SelectMany(queue => queue.Consumers).Where(consumer => consumer.Connection == connection).ToList())
        {
            End(consumer);
        }

        foreach (var queue in queues.Values.Where(queue => queue.Owner == connection).ToList())
        {
            Delete(queue);
        }
    }
}
