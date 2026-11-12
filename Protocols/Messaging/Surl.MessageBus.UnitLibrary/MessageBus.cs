using Surl.Protocol.Abstractions;

namespace Surl.MessageBus;

/// <summary>
/// The one topic space the messaging servers of a <c>surl</c> process share (ADR-0100,
/// decision 3): a publish to a canonical topic is appended to the topic's log (its log
/// operations are in <c>MessageBus.Logs.cs</c>), handed to every live subscription whose
/// filter matches, a retained publish keeps the topic's one retained message, and a copy goes
/// into every named queue bound by a matching filter (its queue operations are in
/// <c>MessageBus.Queues.cs</c>). Created once
/// per process by the composition and handed to every server of the job. Safe for concurrent
/// use: several connections publish and subscribe at once.
/// </summary>
/// <remarks>
/// <para>
/// Every store a peer can fill is bounded (ADR-0006, ADR-0100 decision 3.6): the bounds are
/// constructor parameters with <c>Default…</c> constants. Past a bound nothing changes and the
/// publish or subscribe returns a <see cref="MessageBusOutcome"/>, which each server words in
/// its own protocol.
/// </para>
/// <para>
/// Delivery is serialised: messages are handed to subscriptions in publish order, one delivery
/// callback at a time, and never while the bus holds its lock, so a callback may publish or
/// subscribe. The publisher whose call finds deliveries waiting makes them; a publish made while
/// another thread is delivering returns at once and its deliveries follow.
/// </para>
/// </remarks>
public sealed partial class MessageBus
{
    /// <summary>The default for <see cref="MaxTopics"/>: 10000 (ADR-0014 decision 7's number).</summary>
    public const int DefaultMaxTopics = 10_000;

    /// <summary>The default for <see cref="MaxRetainedPayloadBytes"/>: 104857600 (100 MiB).</summary>
    public const long DefaultMaxRetainedPayloadBytes = 104_857_600;

    /// <summary>The default for <see cref="MaxSubscriptionsPerConnection"/>: 1000.</summary>
    public const int DefaultMaxSubscriptionsPerConnection = 1_000;

    /// <summary>The default for <see cref="MaxMessageBytes"/>: 104857600 (100 MiB), the default
    /// <c>--max-filesize</c>; the composition passes the run's own.</summary>
    public const long DefaultMaxMessageBytes = 104_857_600;

    /// <summary>The default for <see cref="MaxQueues"/>: 1000.</summary>
    public const int DefaultMaxQueues = 1_000;

    /// <summary>The default for <see cref="MaxMessagesPerQueue"/>: 100000.</summary>
    public const int DefaultMaxMessagesPerQueue = 100_000;

    /// <summary>The default for <see cref="MaxQueuedBytes"/>: 268435456 (256 MiB, ADR-0050
    /// decision 6's memory bound).</summary>
    public const long DefaultMaxQueuedBytes = 268_435_456;

    private readonly TimeProvider timeProvider;
    private readonly Lock stateLock = new();
    private readonly Dictionary<string, BusMessage> retainedMessages = new(StringComparer.Ordinal);
    private readonly List<BusSubscription> subscriptions = [];
    private readonly Dictionary<(string Group, string Filter), int> nextGroupMember = [];
    private readonly Queue<Action> waitingDeliveries = new();
    private long retainedPayloadBytes;
    private bool delivering;

    /// <summary>
    /// Creates an empty bus with the given bounds.
    /// </summary>
    /// <param name="timeProvider">The clock that stamps <see cref="BusMessage.PublishedAt"/> and
    /// judges <see cref="BusMessage.ExpiresAt"/>.</param>
    /// <param name="maxTopics">The value of <see cref="MaxTopics"/>; at least 1.</param>
    /// <param name="maxRetainedPayloadBytes">The value of <see cref="MaxRetainedPayloadBytes"/>; at least 1.</param>
    /// <param name="maxSubscriptionsPerConnection">The value of <see cref="MaxSubscriptionsPerConnection"/>; at least 1.</param>
    /// <param name="maxMessageBytes">The value of <see cref="MaxMessageBytes"/>; at least 0.</param>
    /// <param name="maxQueues">The value of <see cref="MaxQueues"/>; at least 1.</param>
    /// <param name="maxMessagesPerQueue">The value of <see cref="MaxMessagesPerQueue"/>; at least 1.</param>
    /// <param name="maxQueuedBytes">The value of <see cref="MaxQueuedBytes"/>; at least 1.</param>
    /// <param name="maxLogBytesPerTopic">The value of <see cref="MaxLogBytesPerTopic"/>; at least 1.</param>
    /// <param name="maxLogBytes">The value of <see cref="MaxLogBytes"/>; at least 1.</param>
    /// <param name="configuration">The declared topics and queues, name aliases and access
    /// grants (ADR-0102, decision 3); <see langword="null"/> for <see cref="MessageBusConfiguration.None"/>.
    /// Declared topics without wildcards get their empty logs, and declared queues are made, at once.</param>
    /// <param name="events">Where every change is published (ADR-0124 decision 1), or
    /// <see langword="null"/> to publish nothing.</param>
    /// <param name="faults">The fault plan a publish consults for an out-of-space rule (ADR-0124
    /// decision 4), or <see langword="null"/> for none.</param>
    /// <param name="maxCommittedOffsets">The value of <see cref="MaxCommittedOffsets"/>; at least 1.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A bound is below its least value.</exception>
    /// <exception cref="ArgumentException">The configuration declares more topics without
    /// wildcards than <paramref name="maxTopics"/>, more queues than <paramref name="maxQueues"/>,
    /// a queue twice, or an alias <see cref="TopicNameLookup"/> refuses.</exception>
    public MessageBus(
        TimeProvider timeProvider,
        int maxTopics = DefaultMaxTopics,
        long maxRetainedPayloadBytes = DefaultMaxRetainedPayloadBytes,
        int maxSubscriptionsPerConnection = DefaultMaxSubscriptionsPerConnection,
        long maxMessageBytes = DefaultMaxMessageBytes,
        int maxQueues = DefaultMaxQueues,
        int maxMessagesPerQueue = DefaultMaxMessagesPerQueue,
        long maxQueuedBytes = DefaultMaxQueuedBytes,
        long maxLogBytesPerTopic = DefaultMaxLogBytesPerTopic,
        long maxLogBytes = DefaultMaxLogBytes,
        MessageBusConfiguration? configuration = null,
        ISurlEventPublisher? events = null,
        IFaultPlan? faults = null,
        int maxCommittedOffsets = DefaultMaxCommittedOffsets)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCommittedOffsets, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxTopics, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRetainedPayloadBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSubscriptionsPerConnection, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(maxMessageBytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxQueues, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxMessagesPerQueue, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxQueuedBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLogBytesPerTopic, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLogBytes, 1);

        this.timeProvider = timeProvider;
        this.events = events ?? DiscardingSurlEventPublisher.Instance;
        this.faults = faults ?? EmptyFaultPlan.Instance;
        MaxTopics = maxTopics;
        MaxCommittedOffsets = maxCommittedOffsets;
        MaxRetainedPayloadBytes = maxRetainedPayloadBytes;
        MaxSubscriptionsPerConnection = maxSubscriptionsPerConnection;
        MaxMessageBytes = maxMessageBytes;
        MaxQueues = maxQueues;
        MaxMessagesPerQueue = maxMessagesPerQueue;
        MaxQueuedBytes = maxQueuedBytes;
        MaxLogBytesPerTopic = maxLogBytesPerTopic;
        MaxLogBytes = maxLogBytes;
        Configuration = configuration ?? MessageBusConfiguration.None;
        Names = new TopicNameLookup(Configuration.TopicMaps);
        accessPolicy = new TopicAccessPolicy(Configuration.AccessGrants);
        declaredTopics = SplitDeclaredTopics(Configuration);
        declaredLogTopics = DeclaredLogTopicsOf(Configuration, maxTopics);
        declaredQueueNames = DeclaredQueueNamesOf(Configuration, maxQueues);
        DeclareConfiguredTopicsAndQueues();
    }

    /// <summary>The most topics with a log - every topic a message was published on, so every
    /// topic holding a retained message too (ADR-0100, decision 3.6).</summary>
    public int MaxTopics { get; }

    /// <summary>The most retained payload bytes, all topics together.</summary>
    public long MaxRetainedPayloadBytes { get; }

    /// <summary>The most live subscriptions one <see cref="BusConnection"/> holds.</summary>
    public int MaxSubscriptionsPerConnection { get; }

    /// <summary>The most bytes one message's payload and native form (<see cref="BusMessage.NativeForm"/>) hold together.</summary>
    public long MaxMessageBytes { get; }

    /// <summary>The most named queues.</summary>
    public int MaxQueues { get; }

    /// <summary>The most messages one named queue holds, unacknowledged ones included.</summary>
    public int MaxMessagesPerQueue { get; }

    /// <summary>The most payload and native-form bytes all named queues hold together, unacknowledged ones included.</summary>
    public long MaxQueuedBytes { get; }

    /// <summary>
    /// Gives an anonymous server connection its place on the bus, through which it subscribes;
    /// <see cref="Connect(string?)"/> with no account.
    /// </summary>
    /// <returns>A connection holding no subscriptions; dispose it when the client goes.</returns>
    public BusConnection Connect() => Connect(account: null);

    /// <summary>
    /// Publishes <paramref name="message"/>: it is appended to its topic's log at the next
    /// offset; when it is retained, it replaces the topic's
    /// retained message (or removes it, for an empty payload); then it is handed to every live
    /// subscription whose filter matches its topic, once each, and to one member of each
    /// matching group, round robin. A message already expired is dropped: nothing is stored or
    /// delivered. A declared topic matching it makes it retained or lowers its QoS (ADR-0102,
    /// decision 3.1). It is published for an anonymous caller: <see cref="Publish(BusMessage, string?)"/>
    /// with no account.
    /// </summary>
    /// <param name="message">The message. The bus keeps its own copy of every byte, so the
    /// caller may reuse its buffers afterwards.</param>
    /// <returns>The outcome and how many subscriptions it was handed to.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public BusPublishResult Publish(BusMessage message) => Publish(message, account: null);

    internal BusSubscribeResult Subscribe(BusConnection connection, string filter, Action<BusMessage> deliver, string? group, bool includeRetained, bool noLocal)
    {
        if (!CanonicalTopicName.IsValidFilter(filter))
        {
            return new(MessageBusOutcome.InvalidFilter, null, []);
        }

        if (!accessPolicy.MaySubscribe(connection.Account, filter))
        {
            return new(MessageBusOutcome.AccessDenied, null, []);
        }

        var now = timeProvider.GetUtcNow();
        lock (stateLock)
        {
            ObjectDisposedException.ThrowIf(connection.IsClosed, connection);
            if (connection.SubscriptionCount >= MaxSubscriptionsPerConnection)
            {
                return new(MessageBusOutcome.TooManySubscriptions, null, []);
            }

            var subscription = new BusSubscription(this, connection, filter, group, deliver) { NoLocal = noLocal };
            subscriptions.Add(subscription);
            connection.SubscriptionCount++;
            var retained = includeRetained ? RetainedMatching(subscription.FilterLevels, now) : [];
            return new(MessageBusOutcome.Succeeded, subscription, retained);
        }
    }

    internal void Unsubscribe(BusSubscription subscription)
    {
        lock (stateLock)
        {
            subscription.Deactivate();
            if (subscriptions.Remove(subscription))
            {
                subscription.Connection.SubscriptionCount--;
            }
        }
    }

    internal void Disconnect(BusConnection connection) => ChangeState(() =>
    {
        connection.IsClosed = true;
        foreach (var subscription in subscriptions.Where(subscription => subscription.Connection == connection))
        {
            subscription.Deactivate();
        }

        subscriptions.RemoveAll(subscription => subscription.Connection == connection);
        EndQueuesOf(connection);
        connection.SubscriptionCount = 0;
        return true;
    });

    /// <summary>
    /// Makes a change under the bus's lock, then, when the change left deliveries waiting and
    /// nobody is delivering, makes them outside the lock before returning.
    /// </summary>
    private T ChangeState<T>(Func<T> change)
    {
        T result;
        bool startDelivering;
        lock (stateLock)
        {
            result = change();
            startDelivering = !delivering && waitingDeliveries.Count > 0;
            delivering |= startDelivering;
        }

        if (startDelivering)
        {
            DeliverWaiting();
        }

        return result;
    }

    private static bool IsExpired(BusMessage message, DateTimeOffset now) => message.ExpiresAt <= now;

    private static BusMessage Copy(BusMessage message, DateTimeOffset now) => message with
    {
        Payload = message.Payload.ToArray(),
        CorrelationData = CopyOf(message.CorrelationData),
        Key = CopyOf(message.Key),
        NativeForm = message.NativeForm is { } nativeForm ? nativeForm with { Bytes = nativeForm.Bytes.ToArray() } : null,
        Headers = [.. message.Headers],
        PublishedAt = now,
    };

    private static ReadOnlyMemory<byte>? CopyOf(ReadOnlyMemory<byte>? bytes) =>
        bytes is { } present ? present.ToArray() : (ReadOnlyMemory<byte>?)null;

    private MessageBusOutcome Refusal(BusMessage message) =>
        !CanonicalTopicName.IsValidTopic(message.Topic) ? MessageBusOutcome.InvalidTopic
        : message.PayloadAndNativeFormBytes > MaxMessageBytes ? MessageBusOutcome.MessageTooLarge
        : MessageBusOutcome.Succeeded;

    private BusPublishResult Accept(BusMessage published, DateTimeOffset now, BusConnection? publisher, bool onlyToConsumedQueues, bool seed, HashSet<NamedQueue>? queuesFullByPlan)
    {
        var (outcome, logAppend) = PlanLogAppend(published);
        if (outcome is MessageBusOutcome.Succeeded && published.Retain)
        {
            outcome = Retain(published);
        }

        if (outcome is not MessageBusOutcome.Succeeded)
        {
            return new(outcome, 0);
        }

        var logOffset = AppendToLog(published, logAppend);
        PublishChange(StoreChange.Published, published.Topic, published.Payload.Length);
        var topicLevels = CanonicalTopicName.SplitLevels(published.Topic);
        var targets = SelectTargets(topicLevels, publisher);
        if (targets.Count > 0)
        {
            EnqueueDelivery(() => DeliverToSubscriptions(published, targets));
        }

        var (queuedCount, fullQueueCount, unconsumedQueueCount) = CopyIntoBoundQueues(published, topicLevels, now, onlyToConsumedQueues, queuesFullByPlan);
        if (seed)
        {
            PublishChange(StoreChange.Seeded, published.Topic, published.Payload.Length);
        }

        return new(outcome, targets.Count) { QueuedCount = queuedCount, FullQueueCount = fullQueueCount, UnconsumedQueueCount = unconsumedQueueCount, LogOffset = logOffset };
    }

    private void DeliverToSubscriptions(BusMessage message, List<BusSubscription> targets)
    {
        foreach (var subscription in targets)
        {
            if (subscription.Deliver(message))
            {
                PublishChange(StoreChange.Delivered, message.Topic, message.Payload.Length);
            }
        }

        foreach (var connection in targets.Select(subscription => subscription.Connection).Distinct())
        {
            connection.AfterMessageDelivered?.Invoke(message);
        }
    }

    private MessageBusOutcome Retain(BusMessage published)
    {
        var topic = published.Topic;
        if (published.Payload.IsEmpty)
        {
            RemoveRetained(topic);
            return MessageBusOutcome.Succeeded;
        }

        // A retained topic always has a log, so PlanLogAppend has already applied MaxTopics.
        var replacedBytes = retainedMessages.TryGetValue(topic, out var replaced) ? replaced.Payload.Length : 0;
        var newTotal = retainedPayloadBytes - replacedBytes + published.Payload.Length;
        if (newTotal > MaxRetainedPayloadBytes)
        {
            return MessageBusOutcome.StoreFull;
        }

        retainedMessages[topic] = published;
        retainedPayloadBytes = newTotal;
        retainedVersion++;
        PublishChange(StoreChange.Written, topic, published.Payload.Length);
        return MessageBusOutcome.Succeeded;
    }

    private void RemoveRetained(string topic)
    {
        if (retainedMessages.Remove(topic, out var removed))
        {
            retainedPayloadBytes -= removed.Payload.Length;
            retainedVersion++;
            PublishChange(StoreChange.Deleted, topic, removed.Payload.Length);
        }
    }

    private List<BusSubscription> SelectTargets(string[] topicLevels, BusConnection? publisher)
    {
        var targets = new List<BusSubscription>();
        var groups = new Dictionary<(string Group, string Filter), List<BusSubscription>>();
        foreach (var subscription in subscriptions.Where(subscription => CanonicalTopicName.FilterMatches(subscription.FilterLevels, topicLevels) && !subscription.KeepsFrom(publisher)))
        {
            if (subscription.Group is { } group)
            {
                AddGroupMember(groups, (group, subscription.Filter), subscription);
            }
            else
            {
                targets.Add(subscription);
            }
        }

        foreach (var (key, members) in groups)
        {
            var next = nextGroupMember.GetValueOrDefault(key) % members.Count;
            targets.Add(members[next]);
            nextGroupMember[key] = next + 1;
        }

        return targets;
    }

    private static void AddGroupMember(
        Dictionary<(string Group, string Filter), List<BusSubscription>> groups,
        (string Group, string Filter) key,
        BusSubscription subscription)
    {
        if (!groups.TryGetValue(key, out var members))
        {
            members = [];
            groups.Add(key, members);
        }

        members.Add(subscription);
    }

    private List<BusMessage> RetainedMatching(string[] filterLevels, DateTimeOffset now)
    {
        foreach (var expired in retainedMessages.Values.Where(message => IsExpired(message, now)).ToList())
        {
            RemoveRetained(expired.Topic);
        }

        return retainedMessages.Values
            .Where(message => CanonicalTopicName.FilterMatches(filterLevels, CanonicalTopicName.SplitLevels(message.Topic)))
            .OrderBy(message => message.Topic, StringComparer.Ordinal)
            .ToList();
    }

    private void DeliverWaiting()
    {
        try
        {
            while (TryTakeWaitingDelivery(out var delivery))
            {
                delivery();
            }
        }
        catch (Exception)
        {
            lock (stateLock)
            {
                delivering = false;
            }

            throw;
        }
    }

    private bool TryTakeWaitingDelivery([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Action? delivery)
    {
        lock (stateLock)
        {
            if (waitingDeliveries.TryDequeue(out delivery))
            {
                return true;
            }

            delivering = false;
            return false;
        }
    }
}
