namespace Surl.MessageBus;

/// <summary>
/// The one topic space the messaging servers of a <c>surl</c> process share (ADR-0100,
/// decision 3): a publish to a canonical topic is handed to every live subscription whose
/// filter matches, and a retained publish keeps the topic's one retained message. Created once
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
public sealed class MessageBus
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

    private readonly TimeProvider timeProvider;
    private readonly Lock stateLock = new();
    private readonly Dictionary<string, BusMessage> retainedMessages = new(StringComparer.Ordinal);
    private readonly List<BusSubscription> subscriptions = [];
    private readonly Dictionary<(string Group, string Filter), int> nextGroupMember = [];
    private readonly Queue<(BusMessage Message, List<BusSubscription> Targets)> waitingDeliveries = new();
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
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A bound is below its least value.</exception>
    public MessageBus(
        TimeProvider timeProvider,
        int maxTopics = DefaultMaxTopics,
        long maxRetainedPayloadBytes = DefaultMaxRetainedPayloadBytes,
        int maxSubscriptionsPerConnection = DefaultMaxSubscriptionsPerConnection,
        long maxMessageBytes = DefaultMaxMessageBytes)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxTopics, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRetainedPayloadBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSubscriptionsPerConnection, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(maxMessageBytes);

        this.timeProvider = timeProvider;
        MaxTopics = maxTopics;
        MaxRetainedPayloadBytes = maxRetainedPayloadBytes;
        MaxSubscriptionsPerConnection = maxSubscriptionsPerConnection;
        MaxMessageBytes = maxMessageBytes;
    }

    /// <summary>The most topics holding a retained message.</summary>
    public int MaxTopics { get; }

    /// <summary>The most retained payload bytes, all topics together.</summary>
    public long MaxRetainedPayloadBytes { get; }

    /// <summary>The most live subscriptions one <see cref="BusConnection"/> holds.</summary>
    public int MaxSubscriptionsPerConnection { get; }

    /// <summary>The most bytes one message's payload holds.</summary>
    public long MaxMessageBytes { get; }

    /// <summary>
    /// Gives a server connection its place on the bus, through which it subscribes.
    /// </summary>
    /// <returns>A connection holding no subscriptions; dispose it when the client goes.</returns>
    public BusConnection Connect() => new(this);

    /// <summary>
    /// Publishes <paramref name="message"/>: when it is retained, it replaces the topic's
    /// retained message (or removes it, for an empty payload); then it is handed to every live
    /// subscription whose filter matches its topic, once each, and to one member of each
    /// matching group, round robin. A message already expired is dropped: nothing is stored or
    /// delivered.
    /// </summary>
    /// <param name="message">The message. The bus keeps its own copy of every byte, so the
    /// caller may reuse its buffers afterwards.</param>
    /// <returns>The outcome and how many subscriptions it was handed to.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public BusPublishResult Publish(BusMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var refusal = Refusal(message);
        if (refusal is not MessageBusOutcome.Succeeded)
        {
            return new(refusal, 0);
        }

        var now = timeProvider.GetUtcNow();
        if (IsExpired(message, now))
        {
            return new(MessageBusOutcome.Succeeded, 0);
        }

        var published = Copy(message, now);
        var (outcome, deliveredCount, startDelivering) = Accept(published);
        if (startDelivering)
        {
            DeliverWaiting();
        }

        return new(outcome, deliveredCount);
    }

    internal BusSubscribeResult Subscribe(BusConnection connection, string filter, Action<BusMessage> deliver, string? group, bool includeRetained)
    {
        if (!CanonicalTopicName.IsValidFilter(filter))
        {
            return new(MessageBusOutcome.InvalidFilter, null, []);
        }

        var now = timeProvider.GetUtcNow();
        lock (stateLock)
        {
            ObjectDisposedException.ThrowIf(connection.IsClosed, connection);
            if (connection.SubscriptionCount >= MaxSubscriptionsPerConnection)
            {
                return new(MessageBusOutcome.TooManySubscriptions, null, []);
            }

            var subscription = new BusSubscription(this, connection, filter, group, deliver);
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

    internal void Disconnect(BusConnection connection)
    {
        lock (stateLock)
        {
            connection.IsClosed = true;
            foreach (var subscription in subscriptions.Where(subscription => subscription.Connection == connection))
            {
                subscription.Deactivate();
            }

            subscriptions.RemoveAll(subscription => subscription.Connection == connection);
            connection.SubscriptionCount = 0;
        }
    }

    private static bool IsExpired(BusMessage message, DateTimeOffset now) => message.ExpiresAt <= now;

    private static BusMessage Copy(BusMessage message, DateTimeOffset now) => message with
    {
        Payload = message.Payload.ToArray(),
        CorrelationData = CopyOf(message.CorrelationData),
        Key = CopyOf(message.Key),
        Headers = [.. message.Headers],
        PublishedAt = now,
    };

    private static ReadOnlyMemory<byte>? CopyOf(ReadOnlyMemory<byte>? bytes) =>
        bytes is { } present ? present.ToArray() : (ReadOnlyMemory<byte>?)null;

    private MessageBusOutcome Refusal(BusMessage message) =>
        !CanonicalTopicName.IsValidTopic(message.Topic) ? MessageBusOutcome.InvalidTopic
        : message.Payload.Length > MaxMessageBytes ? MessageBusOutcome.MessageTooLarge
        : MessageBusOutcome.Succeeded;

    private (MessageBusOutcome Outcome, int DeliveredCount, bool StartDelivering) Accept(BusMessage published)
    {
        lock (stateLock)
        {
            var outcome = published.Retain ? Retain(published) : MessageBusOutcome.Succeeded;
            if (outcome is not MessageBusOutcome.Succeeded)
            {
                return (outcome, 0, false);
            }

            var targets = SelectTargets(CanonicalTopicName.SplitLevels(published.Topic));
            if (targets.Count == 0)
            {
                return (outcome, 0, false);
            }

            waitingDeliveries.Enqueue((published, targets));
            var startDelivering = !delivering;
            delivering = true;
            return (outcome, targets.Count, startDelivering);
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

        var replacedBytes = retainedMessages.TryGetValue(topic, out var replaced) ? replaced.Payload.Length : 0;
        var newTotal = retainedPayloadBytes - replacedBytes + published.Payload.Length;
        if (replaced is null && retainedMessages.Count >= MaxTopics)
        {
            return MessageBusOutcome.TooManyTopics;
        }

        if (newTotal > MaxRetainedPayloadBytes)
        {
            return MessageBusOutcome.StoreFull;
        }

        retainedMessages[topic] = published;
        retainedPayloadBytes = newTotal;
        return MessageBusOutcome.Succeeded;
    }

    private void RemoveRetained(string topic)
    {
        if (retainedMessages.Remove(topic, out var removed))
        {
            retainedPayloadBytes -= removed.Payload.Length;
        }
    }

    private List<BusSubscription> SelectTargets(string[] topicLevels)
    {
        var targets = new List<BusSubscription>();
        var groups = new Dictionary<(string Group, string Filter), List<BusSubscription>>();
        foreach (var subscription in subscriptions.Where(subscription => CanonicalTopicName.FilterMatches(subscription.FilterLevels, topicLevels)))
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
                foreach (var subscription in delivery.Targets)
                {
                    subscription.Deliver(delivery.Message);
                }
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

    private bool TryTakeWaitingDelivery(out (BusMessage Message, List<BusSubscription> Targets) delivery)
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
