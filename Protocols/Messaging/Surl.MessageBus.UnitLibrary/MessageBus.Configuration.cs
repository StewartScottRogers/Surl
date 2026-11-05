namespace Surl.MessageBus;

/// <summary>
/// What the bus's <see cref="MessageBusConfiguration"/> changes (ADR-0102, decision 3): declared
/// topics' retain and QoS ceiling and their logs from start, declared queues from start and
/// never deleted by a client, the <see cref="Names"/> lookup with every <c>--topic-map</c>
/// alias, and the <c>--topic-access</c> checks on every publish, subscribe, binding, consume
/// and send to a queue.
/// </summary>
public sealed partial class MessageBus
{
    private readonly TopicAccessPolicy accessPolicy;
    private readonly (DeclaredTopic Topic, string[] Levels)[] declaredTopics;
    private readonly HashSet<string> declaredLogTopics;
    private readonly HashSet<string> declaredQueueNames;

    /// <summary>The configuration the bus was made with; <see cref="MessageBusConfiguration.None"/>
    /// when none was given.</summary>
    public MessageBusConfiguration Configuration { get; }

    /// <summary>The lookup every server maps its names through, with the configured aliases.</summary>
    public TopicNameLookup Names { get; }

    /// <summary>
    /// Gives a server connection, made for <paramref name="account"/>, its place on the bus.
    /// Every subscribe, binding and consume it makes is checked against the configured access
    /// grants for that account.
    /// </summary>
    /// <param name="account">The logged-in account's name, or <see langword="null"/> for an
    /// anonymous caller.</param>
    /// <returns>A connection holding no subscriptions; dispose it when the client goes.</returns>
    public BusConnection Connect(string? account) => new(this, account);

    /// <summary>
    /// What <paramref name="account"/> may do on <paramref name="topic"/> under the configured
    /// access grants (ADR-0102, decision 3.4): <see cref="TopicAccessRights.Publish"/> when it may
    /// publish there, <see cref="TopicAccessRights.Subscribe"/> when it may subscribe to the
    /// topic itself. The answer never depends on whether the topic exists, so a server can ask
    /// before it tells a caller anything about one, as a Kafka broker's Metadata answer does.
    /// </summary>
    /// <param name="account">The caller's account, or <see langword="null"/> for an anonymous one.</param>
    /// <param name="topic">The canonical topic name.</param>
    /// <returns>The rights; <see cref="TopicAccessRights.None"/> when it may do neither.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="topic"/> is <see langword="null"/>.</exception>
    public TopicAccessRights GetTopicAccess(string? account, string topic)
    {
        ArgumentNullException.ThrowIfNull(topic);

        return (accessPolicy.MayPublish(account, topic) ? TopicAccessRights.Publish : TopicAccessRights.None)
            | (accessPolicy.MaySubscribe(account, topic) ? TopicAccessRights.Subscribe : TopicAccessRights.None);
    }

    /// <summary>
    /// Publishes <paramref name="message"/> as <see cref="Publish(BusMessage)"/> does, for
    /// <paramref name="account"/>: refused with <see cref="MessageBusOutcome.AccessDenied"/>,
    /// nothing retained, delivered or logged, when the configured grants do not let it publish
    /// on the message's topic.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="account">The publisher's account, or <see langword="null"/> for an anonymous one.</param>
    /// <returns>The outcome and how many subscriptions it was handed to.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public BusPublishResult Publish(BusMessage message, string? account) => Publish(message, account, publisher: null);

    // The publisher, when a connection published through BusConnection.Publish, keeps the
    // message from that connection's No Local subscriptions.
    internal BusPublishResult Publish(BusMessage message, string? account, BusConnection? publisher)
    {
        ArgumentNullException.ThrowIfNull(message);
        var refusal = Refusal(message);
        if (refusal is MessageBusOutcome.Succeeded && !accessPolicy.MayPublish(account, message.Topic))
        {
            refusal = MessageBusOutcome.AccessDenied;
        }

        if (refusal is not MessageBusOutcome.Succeeded)
        {
            return new(refusal, 0);
        }

        var now = timeProvider.GetUtcNow();
        if (IsExpired(message, now))
        {
            return new(MessageBusOutcome.Succeeded, 0);
        }

        var published = WithDeclaredAttributes(Copy(message, now));
        return ChangeState(() => Accept(published, now, publisher));
    }

    private static (DeclaredTopic Topic, string[] Levels)[] SplitDeclaredTopics(MessageBusConfiguration configuration) =>
        [.. configuration.Topics.Select(topic => (topic, CanonicalTopicName.SplitLevels(topic.Filter)))];

    private static HashSet<string> DeclaredLogTopicsOf(MessageBusConfiguration configuration, int maxTopics)
    {
        var topics = configuration.Topics.Select(topic => topic.Filter).Where(CanonicalTopicName.IsValidTopic).ToHashSet(StringComparer.Ordinal);
        return topics.Count <= maxTopics
            ? topics
            : throw new ArgumentException($"More than {maxTopics} topics are declared.", nameof(configuration));
    }

    private static HashSet<string> DeclaredQueueNamesOf(MessageBusConfiguration configuration, int maxQueues)
    {
        var names = configuration.Queues.Select(queue => queue.Name).ToHashSet(StringComparer.Ordinal);
        return names.Count != configuration.Queues.Count ? throw new ArgumentException("A queue is declared twice.", nameof(configuration))
            : names.Count > maxQueues ? throw new ArgumentException($"More than {maxQueues} queues are declared.", nameof(configuration))
            : names;
    }

    /// <summary>The message with every matching declared topic's attributes: retained when any
    /// says so, its QoS no higher than the lowest ceiling.</summary>
    private BusMessage WithDeclaredAttributes(BusMessage message)
    {
        var topicLevels = CanonicalTopicName.SplitLevels(message.Topic);
        var matching = declaredTopics.Where(declared => CanonicalTopicName.FilterMatches(declared.Levels, topicLevels)).Select(declared => declared.Topic).ToList();
        var ceiling = matching.Min(topic => topic.MaximumQualityOfService) ?? BusQualityOfService.ExactlyOnce;
        return message with
        {
            Retain = message.Retain || matching.Any(topic => topic.Retain),
            QualityOfService = message.QualityOfService < ceiling ? message.QualityOfService : ceiling,
        };
    }

    /// <summary>Makes each declared topic's empty log and each declared queue that is missing,
    /// and remakes a declared queue that exists with other flags - one a former run persisted
    /// as durable - empty. Called with the lock held, or from the constructor.</summary>
    private void DeclareConfiguredTopicsAndQueues()
    {
        foreach (var topic in declaredLogTopics)
        {
            logs.TryAdd(topic, new TopicLog());
        }

        foreach (var declared in Configuration.Queues)
        {
            DeclareConfiguredQueue(declared);
        }
    }

    private void DeclareConfiguredQueue(DeclaredQueue declared)
    {
        if (queues.TryGetValue(declared.Name, out var existing))
        {
            if (existing.Flags == declared.Flags)
            {
                return;
            }

            Delete(existing);
        }

        queues.Add(declared.Name, new NamedQueue(declared.Name, declared.Flags, null));
        queuesVersion++;
    }

    /// <summary>Whether the bus holds nothing but its declarations, each still empty, as it is
    /// when made, so it may still be loaded.</summary>
    private bool HoldsOnlyDeclarations() =>
        logs.Count == declaredLogTopics.Count
        && logs.Values.All(log => log.HighWatermark == 0)
        && queues.Count == declaredQueueNames.Count
        && queues.Values.All(queue => queue.MessageCount == 0);
}
