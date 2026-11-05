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
    /// Any connection may send to any queue, an exclusive one included.
    /// </summary>
    /// <param name="queueName">The queue's name.</param>
    /// <param name="message">The message. The bus keeps its own copy of every byte.</param>
    /// <returns><see cref="MessageBusOutcome.Succeeded"/>, <see cref="MessageBusOutcome.MessageTooLarge"/>,
    /// <see cref="MessageBusOutcome.UnknownQueue"/> or <see cref="MessageBusOutcome.QueueFull"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public MessageBusOutcome PublishToQueue(string queueName, BusMessage message)
    {
        ArgumentNullException.ThrowIfNull(queueName);
        ArgumentNullException.ThrowIfNull(message);
        if (message.Payload.Length > MaxMessageBytes)
        {
            return MessageBusOutcome.MessageTooLarge;
        }

        var now = timeProvider.GetUtcNow();
        var published = Copy(message, now);
        return ChangeState(() =>
            !queues.TryGetValue(queueName, out var queue) ? MessageBusOutcome.UnknownQueue
            : IsExpired(published, now) ? MessageBusOutcome.Succeeded
            : Enqueue(queue, published, now));
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
            return new(MessageBusOutcome.Succeeded, 0, 0);
        }
    }

    internal MessageBusOutcome BindQueue(BusConnection connection, string queueName, string filter, bool bind)
    {
        if (!CanonicalTopicName.IsValidFilter(filter))
        {
            return MessageBusOutcome.InvalidFilter;
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

            return outcome;
        }
    }

    internal MessageBusOutcome DeleteQueue(BusConnection connection, string queueName)
    {
        lock (stateLock)
        {
            var (outcome, queue) = Access(connection, queueName);
            if (queue is not null)
            {
                Delete(queue);
            }

            return outcome;
        }
    }

    internal BusConsumeResult Consume(BusConnection connection, string queueName, Action<BusQueueDelivery> deliver, int maxUnacknowledged)
    {
        lock (stateLock)
        {
            var (outcome, queue) = Access(connection, queueName);
            if (queue is null)
            {
                return new(outcome, null);
            }

            if (connection.SubscriptionCount >= MaxSubscriptionsPerConnection)
            {
                return new(MessageBusOutcome.TooManySubscriptions, null);
            }

            var consumer = new BusQueueConsumer(this, connection, queue, maxUnacknowledged, deliver);
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
        Settle(consumer, deliveryTag, requeue: false);

    internal MessageBusOutcome Reject(BusQueueConsumer consumer, long deliveryTag, bool requeue) =>
        Settle(consumer, deliveryTag, requeue);

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
        : new(MessageBusOutcome.Succeeded, existing.ReadyCount, existing.Consumers.Count);

    private (int QueuedCount, int FullQueueCount) CopyIntoBoundQueues(BusMessage published, string[] topicLevels, DateTimeOffset now)
    {
        var queuedCount = 0;
        var fullQueueCount = 0;
        foreach (var queue in queues.Values.Where(queue => queue.IsBoundTo(topicLevels)))
        {
            if (Enqueue(queue, published, now) is MessageBusOutcome.Succeeded)
            {
                queuedCount++;
            }
            else
            {
                fullQueueCount++;
            }
        }

        return (queuedCount, fullQueueCount);
    }

    private MessageBusOutcome Enqueue(NamedQueue queue, BusMessage published, DateTimeOffset now)
    {
        var length = published.Payload.Length;
        if (queue.MessageCount >= MaxMessagesPerQueue || queuedBytes + length > MaxQueuedBytes)
        {
            return MessageBusOutcome.QueueFull;
        }

        queue.Enqueue(published);
        queuedBytes += length;
        Dispatch(queue, now);
        return MessageBusOutcome.Succeeded;
    }

    private void Dispatch(NamedQueue queue, DateTimeOffset now)
    {
        var (deliveries, droppedBytes) = queue.Dispatch(now);
        queuedBytes -= droppedBytes;
        if (deliveries.Count > 0)
        {
            waitingDeliveries.Enqueue(() => DeliverToConsumers(deliveries));
        }
    }

    private static void DeliverToConsumers(List<(BusQueueConsumer Consumer, BusQueueDelivery Delivery)> deliveries)
    {
        foreach (var (consumer, delivery) in deliveries)
        {
            consumer.Deliver(delivery);
        }
    }

    private MessageBusOutcome Settle(BusQueueConsumer consumer, long deliveryTag, bool requeue) => ChangeState(() =>
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
            queuedBytes -= message.Message.Payload.Length;
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
        consumer.Connection.SubscriptionCount--;
        if (queue.Flags.AutoDelete && queue.Consumers.Count == 0)
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
        foreach (var consumer in queue.Consumers.ToList())
        {
            consumer.Deactivate();
            queuedBytes -= NamedQueue.PayloadBytes(consumer.RemoveAllUnacknowledged());
            queue.RemoveConsumer(consumer);
            consumer.Connection.SubscriptionCount--;
        }

        queuedBytes -= queue.RemoveAllReady();
        queues.Remove(queue.Name);
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
