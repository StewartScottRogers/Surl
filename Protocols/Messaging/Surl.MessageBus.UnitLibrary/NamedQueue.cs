namespace Surl.MessageBus;

/// <summary>
/// One named queue's state (ADR-0100, decision 3.3, as ADR-0112 decision 6 amends it): its
/// messages in two priority levels, each in publish order, its bindings to canonical filters
/// and its competing consumers. Every member is called with the bus's lock held.
/// </summary>
internal sealed class NamedQueue(string name, BusQueueFlags flags, BusConnection? owner)
{
    private readonly SortedDictionary<long, QueuedMessage> readyHighPriority = [];
    private readonly SortedDictionary<long, QueuedMessage> readyNormalPriority = [];
    private readonly Dictionary<string, string[]> bindings = new(StringComparer.Ordinal);
    private readonly List<BusQueueConsumer> consumers = [];
    private long nextSequence;
    private long nextDeliveryTag;
    private int nextConsumer;

    public string Name { get; } = name;

    public BusQueueFlags Flags { get; } = flags;

    /// <summary>The connection an exclusive queue belongs to; <see langword="null"/> for any other.</summary>
    public BusConnection? Owner { get; } = owner;

    public int ReadyCount => readyHighPriority.Count + readyNormalPriority.Count;

    public int MessageCount => ReadyCount + consumers.Sum(consumer => consumer.UnacknowledgedCount);

    /// <summary>Every consumer the queue holds, stopped ones and get holders included.</summary>
    public IReadOnlyList<BusQueueConsumer> Consumers => consumers;

    /// <summary>How many consumers still take deliveries: not stopped, and not a get holder.</summary>
    public int ActiveConsumerCount => consumers.Count(consumer => consumer.TakesDeliveries);

    /// <summary>Whether a consumer could take a message now.</summary>
    public bool HasConsumerWithRoom => consumers.Any(consumer => consumer.HasRoom);

    /// <summary>Whether a consumer still taking deliveries asked to be the queue's only one.</summary>
    public bool IsConsumedExclusively => consumers.Any(consumer => consumer.TakesDeliveries && consumer.IsExclusive);

    /// <summary>Why a delete asked only if unused or only if empty must be refused, or
    /// <see cref="MessageBusOutcome.Succeeded"/> when it may go ahead.</summary>
    public MessageBusOutcome DeleteRefusal(bool ifUnused, bool ifEmpty) =>
        ifUnused && ActiveConsumerCount > 0 ? MessageBusOutcome.QueueInUse
        : ifEmpty && MessageCount > 0 ? MessageBusOutcome.QueueNotEmpty
        : MessageBusOutcome.Succeeded;

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

    /// <summary>Every message the queue holds, ready or unacknowledged, in publish order.</summary>
    public List<QueuedMessage> AllMessages =>
        [.. readyHighPriority.Values.Concat(readyNormalPriority.Values).Concat(consumers.SelectMany(consumer => consumer.Unacknowledged)).OrderBy(message => message.Sequence)];

    public void Enqueue(BusMessage message)
    {
        var sequence = nextSequence++;
        Requeue(new QueuedMessage(sequence, message));
    }

    /// <summary>Puts a message loaded from the queue's files at the end of its level.</summary>
    public void Restore(BusMessage message, bool redelivered, ulong fileNumber)
    {
        var sequence = nextSequence++;
        Requeue(new QueuedMessage(sequence, message) { HandedOut = redelivered, FileNumber = fileNumber });
    }

    /// <summary>Puts a message back at its own place in its level, ahead of every later one.</summary>
    public void Requeue(QueuedMessage message) => ReadyLevelOf(message.Message).Add(message.Sequence, message);

    public void AddConsumer(BusQueueConsumer consumer) => consumers.Add(consumer);

    public void RemoveConsumer(BusQueueConsumer consumer) => consumers.Remove(consumer);

    /// <summary>The get holder <paramref name="connection"/> has on this queue, if any.</summary>
    public BusQueueConsumer? GetHolderOf(BusConnection connection) =>
        consumers.FirstOrDefault(consumer => consumer.IsGetHolder && consumer.Connection == connection);

    /// <summary>Empties the queue of the messages no consumer holds.</summary>
    /// <returns>How many messages were removed, and their payload and native-form bytes.</returns>
    public (int Count, long Bytes) RemoveAllReady()
    {
        var count = ReadyCount;
        var bytes = QueuedBytes(readyHighPriority.Values) + QueuedBytes(readyNormalPriority.Values);
        readyHighPriority.Clear();
        readyNormalPriority.Clear();
        return (count, bytes);
    }

    /// <summary>
    /// Hands ready messages, high priority first and each level first to last, to consumers
    /// with room, round robin, and drops each expired one it reaches instead.
    /// </summary>
    /// <returns>The deliveries made, for the bus to hand over outside its lock, and the payload
    /// and native-form bytes of the expired messages dropped.</returns>
    public (List<(BusQueueConsumer Consumer, BusQueueDelivery Delivery)> Deliveries, long DroppedBytes) Dispatch(DateTimeOffset now)
    {
        var deliveries = new List<(BusQueueConsumer Consumer, BusQueueDelivery Delivery)>();
        var droppedBytes = DropExpiredAtHead(now);
        while (ReadyCount > 0 && NextConsumerWithRoom() is { } consumer)
        {
            deliveries.Add((consumer, TakeFirstReady(consumer)));
            droppedBytes += DropExpiredAtHead(now);
        }

        return (deliveries, droppedBytes);
    }

    /// <summary>
    /// Hands the first ready message that has not expired to <paramref name="holder"/>, as
    /// <see cref="Dispatch"/> would to a consumer, dropping the expired ones before it.
    /// </summary>
    /// <returns>The delivery, or <see langword="null"/> when no message is ready, and the bytes of
    /// the expired messages dropped.</returns>
    public (BusQueueDelivery? Delivery, long DroppedBytes) TakeOne(BusQueueConsumer holder, DateTimeOffset now)
    {
        var droppedBytes = DropExpiredAtHead(now);
        return (ReadyCount > 0 ? TakeFirstReady(holder) : null, droppedBytes);
    }

    public static long QueuedBytes(IEnumerable<QueuedMessage> messages) =>
        messages.Sum(message => message.Message.PayloadAndNativeFormBytes);

    private SortedDictionary<long, QueuedMessage> ReadyLevelOf(BusMessage message) =>
        message.HighPriority ? readyHighPriority : readyNormalPriority;

    private SortedDictionary<long, QueuedMessage> FirstReadyLevel =>
        readyHighPriority.Count > 0 ? readyHighPriority : readyNormalPriority;

    private long DropExpiredAtHead(DateTimeOffset now)
    {
        long droppedBytes = 0;
        while (ReadyCount > 0)
        {
            var level = FirstReadyLevel;
            var (sequence, first) = level.First();
            if (!(first.Message.ExpiresAt <= now))
            {
                break;
            }

            level.Remove(sequence);
            droppedBytes += first.Message.PayloadAndNativeFormBytes;
        }

        return droppedBytes;
    }

    private BusQueueDelivery TakeFirstReady(BusQueueConsumer consumer)
    {
        var level = FirstReadyLevel;
        var (sequence, first) = level.First();
        level.Remove(sequence);
        return consumer.Take(first, ++nextDeliveryTag);
    }

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
