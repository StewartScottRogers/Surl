namespace Surl.MessageBus;

/// <summary>
/// The bus's two reads that leave everything as it was (ADR-0164, decisions 1 to 3): browsing a
/// named queue's ready messages and looking up a topic's retained message. Each takes the bus's
/// lock, copies and releases; neither publishes an event, consults a fault, hands a message to a
/// consumer, changes a counter or drops an expired message.
/// </summary>
public sealed partial class MessageBus
{
    /// <summary>The most messages one <see cref="BrowseQueue"/> returns: 1000 (ADR-0164, decision 2).</summary>
    public const int MaxBrowseLimit = 1_000;

    /// <summary>The most payload and native-form bytes one <see cref="BrowseQueue"/> returns together:
    /// 67108864 (64 MiB), though the first message is listed whatever it counts.</summary>
    public const long MaxBrowseBytes = 64L * 1024 * 1024;

    /// <summary>
    /// Lists the ready messages of the named queue in delivery order, without consuming them.
    /// Messages a consumer holds unacknowledged are not ready and are not listed, and neither are
    /// ready messages that have expired, which no consumer will be handed.
    /// </summary>
    /// <param name="queueName">The queue's name.</param>
    /// <param name="afterPosition">The <see cref="BusBrowsedMessage.Position"/> of the last message
    /// already seen, or -1 to start at the first.</param>
    /// <param name="limit">The most messages to list, 1 to <see cref="MaxBrowseLimit"/>.</param>
    /// <returns>The outcome, the messages, and the queue's ready and in-flight counts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="queueName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="afterPosition"/> is below -1, or
    /// <paramref name="limit"/> is outside 1 to <see cref="MaxBrowseLimit"/>.</exception>
    public BusQueueBrowseResult BrowseQueue(string queueName, int afterPosition = -1, int limit = 100)
    {
        ArgumentNullException.ThrowIfNull(queueName);
        ArgumentOutOfRangeException.ThrowIfLessThan(afterPosition, -1);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxBrowseLimit);
        var now = timeProvider.GetUtcNow();
        lock (stateLock)
        {
            if (!queues.TryGetValue(queueName, out var queue))
            {
                return new(MessageBusOutcome.UnknownQueue, [], 0, 0, false);
            }

            var listed = new List<BusBrowsedMessage>();
            var bytes = 0L;
            var hasMore = false;
            var position = -1;
            foreach (var queued in queue.ReadyInDeliveryOrder.Where(queued => !IsExpired(queued.Message, now)))
            {
                position++;
                if (position <= afterPosition)
                {
                    continue;
                }

                var size = queued.Message.PayloadAndNativeFormBytes;
                if (listed.Count == limit || (listed.Count > 0 && bytes + size > MaxBrowseBytes))
                {
                    hasMore = true;
                    break;
                }

                bytes += size;
                listed.Add(new(position, queued.Message));
            }

            return new(MessageBusOutcome.Succeeded, listed, queue.ReadyCount, queue.MessageCount - queue.ReadyCount, hasMore);
        }
    }

    /// <summary>
    /// Returns the topic's retained message without dropping it, even when it has expired (an expired
    /// one is reported as none, and is dropped by the next subscribe that reads retained messages).
    /// </summary>
    /// <param name="topic">The canonical topic name.</param>
    /// <returns>The retained message, or <see langword="null"/> when the topic has none, its
    /// retained message has expired, or the name is not canonical.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="topic"/> is <see langword="null"/>.</exception>
    public BusMessage? GetRetainedMessage(string topic)
    {
        ArgumentNullException.ThrowIfNull(topic);
        var now = timeProvider.GetUtcNow();
        lock (stateLock)
        {
            return retainedMessages.TryGetValue(topic, out var retained) && !IsExpired(retained, now) ? retained : null;
        }
    }
}
