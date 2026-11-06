using System.Diagnostics;

namespace Surl.MessageBus;

/// <summary>
/// A batch publish, all or none (ADR-0146 decision 1.6): the AMQP 1.0 container's transaction
/// commit. Each entry is checked as its own publish would be, against the bus as the entries
/// before it would leave it, and only when every entry would be taken is any published - in
/// batch order, each raising the same events and store changes as its own publish. The check and
/// the publish are made under one hold of the bus's lock, so no other change interleaves.
/// </summary>
public sealed partial class MessageBus
{
    internal BusBatchPublishResult PublishBatch(BusConnection publisher, IReadOnlyList<BusBatchEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Any(entry => entry is null))
        {
            throw new ArgumentException("A batch entry is null.", nameof(entries));
        }

        // What needs no lock is checked first, the fault plan included, which may run a test's
        // code; it stops at the first entry refused.
        var now = timeProvider.GetUtcNow();
        var prepared = new List<PreparedBatchEntry>(entries.Count);
        var firstRefusal = new BusBatchPublishResult(MessageBusOutcome.Succeeded, null, []);
        foreach (var entry in entries)
        {
            var (outcome, preparedEntry) = Prepare(entry, publisher.Account, now);
            if (outcome is not MessageBusOutcome.Succeeded)
            {
                firstRefusal = new(outcome, prepared.Count, []);
                break;
            }

            prepared.Add(preparedEntry);
        }

        return ChangeState(() => PublishPrepared(prepared, firstRefusal, publisher, now));
    }

    private (MessageBusOutcome Outcome, PreparedBatchEntry Entry) Prepare(BusBatchEntry entry, string? account, DateTimeOffset now) =>
        entry.QueueName is { } queueName ? PrepareQueueEntry(queueName, entry.Message, account, now) : PrepareTopicEntry(entry.Message, account, now);

    // As PublishToQueue checks before taking the lock.
    private (MessageBusOutcome Outcome, PreparedBatchEntry Entry) PrepareQueueEntry(string queueName, BusMessage message, string? account, DateTimeOffset now)
    {
        var published = Copy(message, now);
        var prepared = new PreparedBatchEntry(queueName, published, IsExpired(published, now));
        var outcome = message.PayloadAndNativeFormBytes > MaxMessageBytes ? MessageBusOutcome.MessageTooLarge
            : !accessPolicy.MayUseQueue(account, TopicAccessRights.Publish, queueName) ? MessageBusOutcome.AccessDenied
            : !prepared.IsExpired && HasQueue(queueName) && IsOutOfSpace(account, QueueChangeName(queueName)) ? MessageBusOutcome.QueueFull
            : MessageBusOutcome.Succeeded;
        return (outcome, prepared);
    }

    // As Publish checks before taking the lock.
    private (MessageBusOutcome Outcome, PreparedBatchEntry Entry) PrepareTopicEntry(BusMessage message, string? account, DateTimeOffset now)
    {
        var refusal = Refusal(message);
        if (refusal is not MessageBusOutcome.Succeeded || !accessPolicy.MayPublish(account, message.Topic))
        {
            return (refusal is MessageBusOutcome.Succeeded ? MessageBusOutcome.AccessDenied : refusal, default);
        }

        var prepared = new PreparedBatchEntry(null, WithDeclaredAttributes(Copy(message, now)), IsExpired(message, now));
        var outOfSpace = !prepared.IsExpired && IsOutOfSpace(account, message.Topic);
        return (outOfSpace ? MessageBusOutcome.StoreFull : MessageBusOutcome.Succeeded, prepared);
    }

    // Under the lock: plans every prepared entry against the bus as the ones before it leave it,
    // and publishes them all only when none is refused.
    private BusBatchPublishResult PublishPrepared(List<PreparedBatchEntry> prepared, BusBatchPublishResult firstRefusal, BusConnection publisher, DateTimeOffset now)
    {
        var plan = new BatchPlan(this);
        var queuesFullByPlan = new List<HashSet<NamedQueue>>(prepared.Count);
        foreach (var entry in prepared)
        {
            var (outcome, fullQueues) = plan.Admit(entry);
            if (outcome is not MessageBusOutcome.Succeeded)
            {
                return new(outcome, queuesFullByPlan.Count, []);
            }

            queuesFullByPlan.Add(fullQueues);
        }

        return firstRefusal.Outcome is not MessageBusOutcome.Succeeded
            ? firstRefusal
            : new(MessageBusOutcome.Succeeded, null, [.. prepared.Select((entry, index) => PublishPlanned(entry, queuesFullByPlan[index], publisher, now))]);
    }

    private BusPublishResult PublishPlanned(PreparedBatchEntry entry, HashSet<NamedQueue> queuesFullByPlan, BusConnection publisher, DateTimeOffset now)
    {
        if (entry.IsExpired)
        {
            return new(MessageBusOutcome.Succeeded, 0);
        }

        if (entry.QueueName is { } queueName)
        {
            var enqueued = Enqueue(queues[queueName], entry.Published, now);
            Debug.Assert(enqueued is MessageBusOutcome.Succeeded, "The plan counted on the queue taking the message.");
            return new(MessageBusOutcome.Succeeded, 0) { QueuedCount = 1 };
        }

        var result = Accept(entry.Published, now, publisher, onlyToConsumedQueues: false, seed: false, queuesFullByPlan);
        Debug.Assert(result.Outcome is MessageBusOutcome.Succeeded, "The plan counted on the topic taking the message.");
        return result;
    }

    /// <summary>One batch entry checked outside the lock: its queue (<see langword="null"/> for a
    /// topic publish), the bus's own copy of its message, and whether that had already expired.</summary>
    private readonly record struct PreparedBatchEntry(string? QueueName, BusMessage Published, bool IsExpired);

    /// <summary>
    /// The bus as a batch's entries so far would leave it, counted without changing it: each
    /// topic's log, the retained bytes and each queue's messages and bytes. Dispatch only ever
    /// takes messages out of a queue, so a queue holds no more, when the batch is published, than
    /// the plan counted.
    /// </summary>
    private sealed class BatchPlan(MessageBus bus)
    {
        private readonly Dictionary<string, PlannedLog> plannedLogs = new(StringComparer.Ordinal);
        private readonly Dictionary<string, long> plannedRetainedBytes = new(StringComparer.Ordinal);
        private readonly Dictionary<NamedQueue, int> plannedQueueAdditions = [];
        private long logBytes = bus.logBytes;
        private int newTopicCount;
        private long retainedPayloadBytes = bus.retainedPayloadBytes;
        private long queuedBytes = bus.queuedBytes;

        /// <summary>Counts <paramref name="entry"/> in, or answers why its own publish would be
        /// refused; with the bound queues that would refuse its copy as full.</summary>
        public (MessageBusOutcome Outcome, HashSet<NamedQueue> FullQueues) Admit(PreparedBatchEntry entry) =>
            entry.QueueName is { } queueName ? (AdmitToQueue(queueName, entry), [])
            : entry.IsExpired ? (MessageBusOutcome.Succeeded, [])
            : AdmitToTopic(entry.Published);

        private MessageBusOutcome AdmitToQueue(string queueName, PreparedBatchEntry entry) =>
            !bus.queues.TryGetValue(queueName, out var queue) ? MessageBusOutcome.UnknownQueue
            : entry.IsExpired || TryAddToQueue(queue, entry.Published) ? MessageBusOutcome.Succeeded
            : MessageBusOutcome.QueueFull;

        private (MessageBusOutcome Outcome, HashSet<NamedQueue> FullQueues) AdmitToTopic(BusMessage published)
        {
            var topic = published.Topic;
            var isNewTopic = !bus.logs.ContainsKey(topic) && !plannedLogs.ContainsKey(topic);
            if (isNewTopic && bus.logs.Count + newTopicCount >= bus.MaxTopics)
            {
                return (MessageBusOutcome.TooManyTopics, []);
            }

            var log = plannedLogs.TryGetValue(topic, out var planned) ? planned : new PlannedLog(bus.logs.GetValueOrDefault(topic));
            var recordBytes = LogRecordBytes(published);
            var (dropCount, droppedBytes) = log.OldestToDrop(recordBytes, bus.MaxLogBytesPerTopic);
            var newLogBytes = logBytes - droppedBytes + recordBytes;
            var (retainFits, newRetainedBytes) = PlanRetain(published);
            if (newLogBytes > bus.MaxLogBytes || !retainFits)
            {
                return (MessageBusOutcome.StoreFull, []);
            }

            log.Append(recordBytes, dropCount, droppedBytes);
            plannedLogs[topic] = log;
            newTopicCount += isNewTopic ? 1 : 0;
            logBytes = newLogBytes;
            CommitRetain(published, newRetainedBytes);
            return (MessageBusOutcome.Succeeded, AddToBoundQueues(published));
        }

        // As MessageBus.Retain: an empty payload removes, any other replaces within the bound.
        private (bool Fits, long NewTotal) PlanRetain(BusMessage published)
        {
            if (!published.Retain)
            {
                return (true, retainedPayloadBytes);
            }

            var newTotal = retainedPayloadBytes - RetainedBytesOf(published.Topic) + published.Payload.Length;
            return (published.Payload.IsEmpty || newTotal <= bus.MaxRetainedPayloadBytes, newTotal);
        }

        private void CommitRetain(BusMessage published, long newTotal)
        {
            if (published.Retain)
            {
                plannedRetainedBytes[published.Topic] = published.Payload.Length;
                retainedPayloadBytes = newTotal;
            }
        }

        private long RetainedBytesOf(string topic) =>
            plannedRetainedBytes.TryGetValue(topic, out var planned) ? planned
            : bus.retainedMessages.TryGetValue(topic, out var retained) ? retained.Payload.Length
            : 0;

        // As MessageBus.CopyIntoBoundQueues, in the same order of queues.
        private HashSet<NamedQueue> AddToBoundQueues(BusMessage published)
        {
            var topicLevels = CanonicalTopicName.SplitLevels(published.Topic);
            return [.. bus.queues.Values.Where(queue => queue.IsBoundTo(topicLevels) && !TryAddToQueue(queue, published))];
        }

        // As MessageBus.Enqueue's bounds.
        private bool TryAddToQueue(NamedQueue queue, BusMessage published)
        {
            var length = published.PayloadAndNativeFormBytes;
            var added = plannedQueueAdditions.GetValueOrDefault(queue);
            if (queue.MessageCount + added >= bus.MaxMessagesPerQueue || queuedBytes + length > bus.MaxQueuedBytes)
            {
                return false;
            }

            plannedQueueAdditions[queue] = added + 1;
            queuedBytes += length;
            return true;
        }
    }

    /// <summary>
    /// One topic's log as a batch's entries so far would leave it: the held records of
    /// <see cref="TopicLog"/> (none for a new topic) followed by the planned ones, the oldest
    /// <see cref="droppedCount"/> of them dropped by retention.
    /// </summary>
    private sealed class PlannedLog(TopicLog? log)
    {
        private readonly List<long> appendedRecordBytes = [];
        private long bytes = log?.Bytes ?? 0;
        private int droppedCount;

        private int ExistingCount => log?.HeldCount ?? 0;

        private int HeldCount => ExistingCount + appendedRecordBytes.Count - droppedCount;

        /// <summary>As <see cref="TopicLog.OldestToDrop"/>.</summary>
        public (int Count, long Bytes) OldestToDrop(long incomingBytes, long maxBytes)
        {
            var count = 0;
            var dropped = 0L;
            while (count < HeldCount && bytes - dropped + incomingBytes > maxBytes)
            {
                dropped += RecordBytesAt(droppedCount + count);
                count++;
            }

            return (count, dropped);
        }

        public void Append(long recordBytes, int dropCount, long droppedBytes)
        {
            droppedCount += dropCount;
            bytes += recordBytes - droppedBytes;
            appendedRecordBytes.Add(recordBytes);
        }

        // Every record, the log's held ones then the planned ones, by its place from the oldest held.
        private long RecordBytesAt(int index) =>
            index < ExistingCount ? log!.HeldRecordBytes(index) : appendedRecordBytes[index - ExistingCount];
    }
}
