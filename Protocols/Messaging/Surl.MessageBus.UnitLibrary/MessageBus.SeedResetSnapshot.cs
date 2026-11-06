using Surl.Protocol.Abstractions;

namespace Surl.MessageBus;

/// <summary>
/// How a test prepares and returns the bus between tests (ADR-0124, decision 3): seed a message
/// through the publish or enqueue path a broker uses, reset the bus to its configured state, and
/// take and restore an in-memory snapshot. A bus loaded from a data directory is on disk and refuses
/// reset, snapshot and restore; seeding it is ordinary writing.
/// </summary>
public sealed partial class MessageBus
{
    /// <summary>
    /// The name a <see cref="StoreChange.Reset"/> or <see cref="StoreChange.Restored"/> change is
    /// published under: <c>#</c>, the canonical filter every topic matches, standing for the whole
    /// bus, since a reset or restore publishes one change for the store, not one per item.
    /// </summary>
    public const string WholeBusChangeName = "#";

    /// <summary>The default most bytes <see cref="TakeSnapshot(long)"/> copies: 268435456 (256 MiB),
    /// ADR-0124's <c>MaxSnapshotBytes</c>.</summary>
    public const long DefaultMaxSnapshotBytes = 268_435_456;

    /// <summary>
    /// Seeds <paramref name="message"/>: publishes it on its canonical topic exactly as
    /// <see cref="Publish(BusMessage)"/> does - retained when it says so, appended to its topic's log,
    /// handed to every live matching subscription on any protocol and copied into every bound queue,
    /// within the bus's bounds - but never refused by the access grants or the fault plan, and then
    /// publishes <see cref="StoreChange.Seeded"/> naming the topic.
    /// </summary>
    /// <param name="message">The message. The bus keeps its own copy of every byte.</param>
    /// <returns>The outcome as <see cref="Publish(BusMessage)"/> gives it, never
    /// <see cref="MessageBusOutcome.AccessDenied"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public BusPublishResult SeedPublish(BusMessage message) =>
        Publish(message, account: null, publisher: null, onlyToConsumedQueues: false, seed: true);

    /// <summary>
    /// Seeds <paramref name="message"/> into one named queue exactly as
    /// <see cref="PublishToQueue(string, BusMessage)"/> puts it there - handed to a consumer with room
    /// at once, within the bus's bounds - but never refused by the access grants or the fault plan,
    /// and then publishes <see cref="StoreChange.Seeded"/> naming <see cref="QueueChangeName"/>.
    /// </summary>
    /// <param name="queueName">The queue's name.</param>
    /// <param name="message">The message. The bus keeps its own copy of every byte.</param>
    /// <returns><see cref="MessageBusOutcome.Succeeded"/> (also for a message already expired, which
    /// is dropped), <see cref="MessageBusOutcome.MessageTooLarge"/>,
    /// <see cref="MessageBusOutcome.UnknownQueue"/> or <see cref="MessageBusOutcome.QueueFull"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="queueName"/> or
    /// <paramref name="message"/> is <see langword="null"/>.</exception>
    public MessageBusOutcome SeedEnqueue(string queueName, BusMessage message)
    {
        ArgumentNullException.ThrowIfNull(queueName);
        ArgumentNullException.ThrowIfNull(message);
        if (message.PayloadAndNativeFormBytes > MaxMessageBytes)
        {
            return MessageBusOutcome.MessageTooLarge;
        }

        var now = timeProvider.GetUtcNow();
        var published = Copy(message, now);
        return ChangeState(() =>
            !queues.TryGetValue(queueName, out var queue) ? MessageBusOutcome.UnknownQueue
            : IsExpired(published, now) ? MessageBusOutcome.Succeeded
            : SeedInto(queue, published, now));
    }

    /// <summary>
    /// Returns the bus to its configured state: no retained messages; only the declared topics'
    /// logs, each empty from offset 0; the declared queues, holding nothing. Live subscriptions stay.
    /// A queue a connection declared stays, emptied, while it is exclusive to a connection or has a
    /// consumer; any other goes. Every unacknowledged delivery is dropped, not redelivered, so a later
    /// settle of it is <see cref="MessageBusOutcome.UnknownDeliveryTag"/>; the consumer keeps
    /// receiving. Topic maps and access grants are configuration and do not change. Publishes one
    /// <see cref="StoreChange.Reset"/> named <see cref="WholeBusChangeName"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The bus was loaded from a data directory, so it is
    /// on disk.</exception>
    public void Reset()
    {
        var declaredQueues = Configuration.Queues
            .Select(declared => new BusQueueSnapshot(declared.Name, declared.Flags, Filters: null, []))
            .ToList();
        ChangeState(() =>
        {
            RequireInMemory();
            ReplaceContents([], [], declaredQueues);
            PublishChange(StoreChange.Reset, WholeBusChangeName, byteCount: null);
            return true;
        });
    }

    /// <summary>
    /// Copies the bus's contents into a <see cref="MessageBusSnapshot"/>: the retained messages,
    /// every topic log with its start offset and records, and every queue not exclusive to a
    /// connection with its flags, bindings and messages - an unacknowledged one as it would be
    /// redelivered. Changes nothing and publishes nothing.
    /// </summary>
    /// <param name="maxBytes">The most bytes the snapshot may hold (<see cref="MessageBusSnapshot.ByteCount"/>);
    /// at least 0.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxBytes"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">The bus is on disk, or holds more than
    /// <paramref name="maxBytes"/>; nothing is copied, never a truncated snapshot.</exception>
    public MessageBusSnapshot TakeSnapshot(long maxBytes = DefaultMaxSnapshotBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);
        lock (stateLock)
        {
            RequireInMemory();
            var byteCount = retainedPayloadBytes + logBytes + queuedBytes;
            if (byteCount > maxBytes)
            {
                throw new InvalidOperationException(
                    $"The message bus holds {byteCount} bytes, more than the {maxBytes} a snapshot may hold.");
            }

            return new MessageBusSnapshot(
                this,
                byteCount,
                [.. retainedMessages.Values],
                [.. logs.Select(entry => new BusLogSnapshot(entry.Key, entry.Value.LogStartOffset, entry.Value.MessagesFrom(entry.Value.LogStartOffset)))],
                [.. queues.Values.Where(queue => queue.Owner is null).Select(SnapshotOf)]);
        }
    }

    /// <summary>
    /// Replaces the bus's contents with <paramref name="snapshot"/>'s: its retained messages, its
    /// logs at the offsets they had, and its queues with their bindings and messages, a message
    /// then unacknowledged now ready and marked redelivered. Live subscriptions stay and consumers
    /// are handed the restored messages; a queue the snapshot does not hold is treated as
    /// <see cref="Reset"/> treats one, and every unacknowledged delivery is dropped. Publishes one
    /// <see cref="StoreChange.Restored"/> named <see cref="WholeBusChangeName"/>.
    /// </summary>
    /// <param name="snapshot">A snapshot this bus took.</param>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="snapshot"/> was taken from another bus.</exception>
    /// <exception cref="InvalidOperationException">The bus is on disk.</exception>
    public void Restore(MessageBusSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Bus != this)
        {
            throw new ArgumentException("The snapshot was taken from another message bus.", nameof(snapshot));
        }

        ChangeState(() =>
        {
            RequireInMemory();
            ReplaceContents(snapshot.Retained, snapshot.Logs, snapshot.Queues);
            PublishChange(StoreChange.Restored, WholeBusChangeName, byteCount: null);
            return true;
        });
    }

    private static BusQueueSnapshot SnapshotOf(NamedQueue queue) =>
        new(queue.Name, queue.Flags, [.. queue.Filters], [.. queue.AllMessages.Select(queued => (queued.Message, queued.HandedOut))]);

    private MessageBusOutcome SeedInto(NamedQueue queue, BusMessage published, DateTimeOffset now)
    {
        var outcome = Enqueue(queue, published, now);
        if (outcome is MessageBusOutcome.Succeeded)
        {
            PublishQueueChange(StoreChange.Seeded, queue.Name, published.Payload.Length);
        }

        return outcome;
    }

    private void RequireInMemory()
    {
        if (files is not null)
        {
            throw new InvalidOperationException(
                "The message bus is on disk under <path>/.surl/bus, so it is never reset, snapshotted or restored.");
        }
    }

    /// <summary>Makes the bus hold exactly <paramref name="retained"/>, <paramref name="topicLogs"/>
    /// (and each declared topic's log) and <paramref name="targetQueues"/>. Called with the lock held.</summary>
    private void ReplaceContents(IReadOnlyList<BusMessage> retained, IReadOnlyList<BusLogSnapshot> topicLogs, IReadOnlyList<BusQueueSnapshot> targetQueues)
    {
        retainedMessages.Clear();
        foreach (var message in retained)
        {
            retainedMessages.Add(message.Topic, message);
        }

        retainedPayloadBytes = retained.Sum(message => (long)message.Payload.Length);
        retainedVersion++;
        ReplaceLogs(topicLogs);
        ReplaceQueues(targetQueues);
    }

    private void ReplaceLogs(IReadOnlyList<BusLogSnapshot> topicLogs)
    {
        logs.Clear();
        logBytes = 0;
        foreach (var snapshot in topicLogs)
        {
            var log = new TopicLog();
            log.StartAt(snapshot.StartOffset);
            foreach (var message in snapshot.Messages)
            {
                log.Append(message, LogRecordBytes(message));
            }

            logs.Add(snapshot.Topic, log);
            logBytes += log.Bytes;
        }

        foreach (var topic in declaredLogTopics)
        {
            logs.TryAdd(topic, new TopicLog());
        }
    }

    private void ReplaceQueues(IReadOnlyList<BusQueueSnapshot> targetQueues)
    {
        var targetNames = targetQueues.Select(target => target.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var queue in queues.Values.ToList())
        {
            queue.DropAllMessages();
            if (!targetNames.Contains(queue.Name) && queue.Owner is null && queue.Consumers.Count == 0)
            {
                queues.Remove(queue.Name);
            }
        }

        queuedBytes = 0;
        queuesVersion++;
        var now = timeProvider.GetUtcNow();
        foreach (var target in targetQueues)
        {
            Refill(target, now);
        }
    }

    private void Refill(BusQueueSnapshot target, DateTimeOffset now)
    {
        if (!queues.TryGetValue(target.Name, out var queue))
        {
            queue = new NamedQueue(target.Name, target.Flags, owner: null);
            queues.Add(target.Name, queue);
        }

        if (target.Filters is { } filters)
        {
            queue.ReplaceBindings(filters);
        }

        foreach (var (message, redelivered) in target.Messages)
        {
            queue.Restore(message, redelivered, fileNumber: null);
            queuedBytes += message.PayloadAndNativeFormBytes;
        }

        Dispatch(queue, now);
    }
}
