namespace Surl.MessageBus;

/// <summary>
/// One named queue's state (ADR-0100, decision 3.3): its messages in publish order, its
/// bindings to canonical filters and its competing consumers. Every member is called with the
/// bus's lock held.
/// </summary>
internal sealed class NamedQueue(string name, BusQueueFlags flags, BusConnection? owner)
{
    private readonly SortedDictionary<long, QueuedMessage> ready = [];
    private readonly Dictionary<string, string[]> bindings = new(StringComparer.Ordinal);
    private readonly List<BusQueueConsumer> consumers = [];
    private long nextSequence;
    private long nextDeliveryTag;
    private int nextConsumer;

    public string Name { get; } = name;

    public BusQueueFlags Flags { get; } = flags;

    /// <summary>The connection an exclusive queue belongs to; <see langword="null"/> for any other.</summary>
    public BusConnection? Owner { get; } = owner;

    public int ReadyCount => ready.Count;

    public int MessageCount => ready.Count + consumers.Sum(consumer => consumer.UnacknowledgedCount);

    public IReadOnlyList<BusQueueConsumer> Consumers => consumers;

    public bool IsLockedAgainst(BusConnection connection) => Owner is { } exclusiveOwner && exclusiveOwner != connection;

    public void Bind(string filter) => bindings.TryAdd(filter, CanonicalTopicName.SplitLevels(filter));

    public void Unbind(string filter) => bindings.Remove(filter);

    public bool IsBoundTo(string[] topicLevels) =>
        bindings.Values.Any(filterLevels => CanonicalTopicName.FilterMatches(filterLevels, topicLevels));

    /// <summary>Whether the queue outlives the process: durable, and neither exclusive nor
    /// auto-delete (ADR-0100, decision 3.7).</summary>
    public bool IsPersisted => Flags is { Durable: true, Exclusive: false, AutoDelete: false };

    /// <summary>The filters the queue is bound to, in no particular order.</summary>
    public IEnumerable<string> Filters => bindings.Keys;

    /// <summary>Every message the queue holds, ready or unacknowledged, in queue order.</summary>
    public List<QueuedMessage> AllMessages =>
        [.. ready.Values.Concat(consumers.SelectMany(consumer => consumer.Unacknowledged)).OrderBy(message => message.Sequence)];

    public void Enqueue(BusMessage message)
    {
        var sequence = nextSequence++;
        ready.Add(sequence, new QueuedMessage(sequence, message));
    }

    /// <summary>Puts a message loaded from the queue's files at the end of the queue.</summary>
    public void Restore(BusMessage message, bool redelivered, ulong fileNumber)
    {
        var sequence = nextSequence++;
        ready.Add(sequence, new QueuedMessage(sequence, message) { HandedOut = redelivered, FileNumber = fileNumber });
    }

    /// <summary>Puts a message back at its own place, ahead of every later one.</summary>
    public void Requeue(QueuedMessage message) => ready.Add(message.Sequence, message);

    public void AddConsumer(BusQueueConsumer consumer) => consumers.Add(consumer);

    public void RemoveConsumer(BusQueueConsumer consumer) => consumers.Remove(consumer);

    /// <summary>Empties the queue of the messages no consumer holds.</summary>
    /// <returns>The payload and native-form bytes removed.</returns>
    public long RemoveAllReady()
    {
        var bytes = QueuedBytes(ready.Values);
        ready.Clear();
        return bytes;
    }

    /// <summary>
    /// Hands ready messages, first to last, to consumers with room, round robin, and drops each
    /// expired one it reaches instead.
    /// </summary>
    /// <returns>The deliveries made, for the bus to hand over outside its lock, and the payload
    /// payload and native-form bytes of the expired messages dropped.</returns>
    public (List<(BusQueueConsumer Consumer, BusQueueDelivery Delivery)> Deliveries, long DroppedBytes) Dispatch(DateTimeOffset now)
    {
        var deliveries = new List<(BusQueueConsumer Consumer, BusQueueDelivery Delivery)>();
        long droppedBytes = 0;
        while (ready.Count > 0)
        {
            var (sequence, first) = ready.First();
            if (first.Message.ExpiresAt <= now)
            {
                ready.Remove(sequence);
                droppedBytes += first.Message.PayloadAndNativeFormBytes;
                continue;
            }

            if (NextConsumerWithRoom() is not { } consumer)
            {
                break;
            }

            ready.Remove(sequence);
            deliveries.Add((consumer, consumer.Take(first, ++nextDeliveryTag)));
        }

        return (deliveries, droppedBytes);
    }

    public static long QueuedBytes(IEnumerable<QueuedMessage> messages) =>
        messages.Sum(message => message.Message.PayloadAndNativeFormBytes);

    private BusQueueConsumer? NextConsumerWithRoom()
    {
        for (var offset = 0; offset < consumers.Count; offset++)
        {
            var index = (nextConsumer + offset) % consumers.Count;
            if (consumers[index].HasRoom)
            {
                nextConsumer = index + 1;
                return consumers[index];
            }
        }

        return null;
    }
}
