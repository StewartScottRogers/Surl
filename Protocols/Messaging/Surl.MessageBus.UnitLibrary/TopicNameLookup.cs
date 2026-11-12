namespace Surl.MessageBus;

/// <summary>
/// The one place a messaging server maps its own topic names onto the bus's canonical ones and
/// back (ADR-0100 decision 3.10, the seam planned as <c>ITopicNameLookup</c>): ADR-0100 decision
/// 3.2's default mapping for the server's scheme, with each <c>--topic-map</c> alias (ADR-0102
/// decision 3.3) applied on top of it. A scheme and its TLS twin share one map. Made by
/// <see cref="MessageBus"/> from its <see cref="MessageBusConfiguration.TopicMaps"/>; immutable,
/// so safe for concurrent use.
/// </summary>
/// <remarks>
/// The schemes it knows are the messaging servers': <c>mqtt</c> and <c>mqtts</c>;
/// <c>mqtt5</c>, <c>mqtt5s</c> and <c>mqtt5+ws</c> (MQTT names); <c>amqp</c> and <c>amqps</c>
/// (AMQP 0-9-1 topic exchange routing and binding keys); <c>amqp1</c> and <c>amqp1s</c>, and
/// <c>stomp</c> and <c>stomps</c> (a <c>/topic/&lt;t&gt;</c> address or destination); <c>kafka</c>
/// and <c>kafkas</c> (Kafka topic names, which have no filters); and <c>nats</c> and
/// <c>natss</c> (subjects); and <c>redis</c> and <c>rediss</c> (channels, canonical as they stand). An alias names one exact name: a filter with wildcards is mapped by
/// the default rules alone, and maps do not chain.
/// </remarks>
public sealed class TopicNameLookup
{
    private static readonly Dictionary<string, SchemeFamily> Families = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mqtt"] = SchemeFamily.Mqtt,
        ["mqtts"] = SchemeFamily.Mqtt,
        ["mqtt5"] = SchemeFamily.Mqtt5,
        ["mqtt5s"] = SchemeFamily.Mqtt5,
        ["mqtt5+ws"] = SchemeFamily.Mqtt5,
        ["amqp"] = SchemeFamily.Amqp0,
        ["amqps"] = SchemeFamily.Amqp0,
        ["amqp1"] = SchemeFamily.Amqp1,
        ["amqp1s"] = SchemeFamily.Amqp1,
        ["kafka"] = SchemeFamily.Kafka,
        ["kafkas"] = SchemeFamily.Kafka,
        ["nats"] = SchemeFamily.Nats,
        ["natss"] = SchemeFamily.Nats,
        ["stomp"] = SchemeFamily.Stomp,
        ["stomps"] = SchemeFamily.Stomp,
        ["redis"] = SchemeFamily.Redis,
        ["rediss"] = SchemeFamily.Redis,
    };

    private readonly Dictionary<(SchemeFamily Family, string Name), string> canonicalTopicByName = [];
    private readonly Dictionary<(SchemeFamily Family, string Topic), string> nameByCanonicalTopic = [];

    /// <summary>
    /// Makes the lookup for <paramref name="topicMaps"/>.
    /// </summary>
    /// <param name="topicMaps">The aliases; empty for the default mapping alone.</param>
    /// <exception cref="ArgumentNullException"><paramref name="topicMaps"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">An alias names a scheme this lookup does not know,
    /// a name that is not a valid name for its scheme (ADR-0102 decision 3.3: <c>not a valid name
    /// for &lt;scheme&gt;</c>), or a name another alias on the same scheme already maps.</exception>
    public TopicNameLookup(IEnumerable<TopicMap> topicMaps)
    {
        ArgumentNullException.ThrowIfNull(topicMaps);
        foreach (var map in topicMaps)
        {
            var family = FamilyOf(map.Scheme);
            RequireValidName(family, map.Scheme, map.Name);
            var target = RequireValidName(FamilyOf(map.TargetScheme), map.TargetScheme, map.TargetName);
            if (!canonicalTopicByName.TryAdd((family, map.Name), target))
            {
                throw new ArgumentException($"{map.Scheme}:{map.Name} is mapped twice.", nameof(topicMaps));
            }

            nameByCanonicalTopic.TryAdd((family, target), map.Name);
        }
    }

    private enum SchemeFamily
    {
        Mqtt,
        Mqtt5,
        Amqp0,
        Amqp1,
        Kafka,
        Nats,
        Stomp,
        Redis,
    }

    /// <summary>
    /// Whether this lookup knows <paramref name="scheme"/>'s names.
    /// </summary>
    /// <param name="scheme">A URL scheme, compared ignoring case.</param>
    /// <returns><see langword="true"/> for the schemes the remarks list.</returns>
    public static bool IsKnownScheme(string scheme) => Families.ContainsKey(scheme);

    /// <summary>
    /// Maps a topic name a client of <paramref name="scheme"/> publishes on onto the bus.
    /// </summary>
    /// <param name="scheme">The server's scheme.</param>
    /// <param name="name">The name as the client sent it.</param>
    /// <returns>The alias's canonical topic, or the default mapping's result.</returns>
    /// <exception cref="ArgumentException"><paramref name="scheme"/> is not a known scheme.</exception>
    public TopicNameMapping ToCanonicalTopic(string scheme, string? name)
    {
        var family = FamilyOf(scheme);
        return Alias(family, name) ?? DefaultToCanonicalTopic(family, name);
    }

    /// <summary>
    /// Maps a filter a client of <paramref name="scheme"/> subscribes or binds with onto the bus.
    /// </summary>
    /// <param name="scheme">The server's scheme.</param>
    /// <param name="filter">The filter as the client sent it; Kafka's is a topic name.</param>
    /// <returns>The alias's canonical topic when the filter is exactly an aliased name, or the
    /// default mapping's result.</returns>
    /// <exception cref="ArgumentException"><paramref name="scheme"/> is not a known scheme.</exception>
    public TopicNameMapping ToCanonicalFilter(string scheme, string? filter)
    {
        var family = FamilyOf(scheme);
        return Alias(family, filter) ?? DefaultToCanonicalFilter(family, filter);
    }

    /// <summary>
    /// Maps a canonical topic a message was published on to the name a client of
    /// <paramref name="scheme"/> is sent.
    /// </summary>
    /// <param name="scheme">The server's scheme.</param>
    /// <param name="topic">The canonical topic.</param>
    /// <returns>The aliased name when an alias maps to <paramref name="topic"/>; otherwise the
    /// default mapping's result, but <see cref="TopicNameMappingOutcome.Unreachable"/> when that
    /// name is aliased to another topic, because the client's name no longer reaches it.</returns>
    /// <exception cref="ArgumentException"><paramref name="scheme"/> is not a known scheme.</exception>
    public TopicNameMapping FromCanonicalTopic(string scheme, string? topic)
    {
        var family = FamilyOf(scheme);
        if (topic is not null && nameByCanonicalTopic.TryGetValue((family, topic), out var name))
        {
            return TopicNameMapping.Of(name);
        }

        var mapped = DefaultFromCanonicalTopic(family, topic);
        return mapped.IsMapped && canonicalTopicByName.ContainsKey((family, mapped.Name))
            ? TopicNameMapping.Refused(TopicNameMappingOutcome.Unreachable)
            : mapped;
    }

    private static SchemeFamily FamilyOf(string scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        return Families.TryGetValue(scheme, out var family)
            ? family
            : throw new ArgumentException($"{scheme} is not a messaging scheme.", nameof(scheme));
    }

    private static string RequireValidName(SchemeFamily family, string scheme, string name)
    {
        var mapped = DefaultToCanonicalTopic(family, name);
        return mapped.IsMapped ? mapped.Name : throw new ArgumentException($"{name} is not a valid name for {scheme}", nameof(name));
    }

    private TopicNameMapping? Alias(SchemeFamily family, string? name) =>
        name is not null && canonicalTopicByName.TryGetValue((family, name), out var topic) ? TopicNameMapping.Of(topic) : null;

    private static TopicNameMapping DefaultToCanonicalTopic(SchemeFamily family, string? name) => family switch
    {
        SchemeFamily.Mqtt or SchemeFamily.Mqtt5 => MqttTopicNames.ToCanonicalTopic(name),
        SchemeFamily.Amqp0 => AmqpTopicExchangeNames.ToCanonicalTopic(name),
        SchemeFamily.Kafka => KafkaTopicNames.ToCanonicalTopic(name),
        SchemeFamily.Nats => NatsSubjectNames.ToCanonicalTopic(name),
        SchemeFamily.Redis => IdentityTopic(name),
        _ => SlashPrefixedTopicOf(name, isFilter: false),
    };

    private static TopicNameMapping DefaultToCanonicalFilter(SchemeFamily family, string? filter) => family switch
    {
        SchemeFamily.Mqtt => MqttTopicNames.ToCanonicalFilter(filter),
        SchemeFamily.Mqtt5 => MqttTopicNames.ToCanonicalSharedSubscription(filter),
        SchemeFamily.Amqp0 => AmqpTopicExchangeNames.ToCanonicalFilter(filter),
        SchemeFamily.Kafka => KafkaTopicNames.ToCanonicalTopic(filter),
        SchemeFamily.Nats => NatsSubjectNames.ToCanonicalFilter(filter),
        SchemeFamily.Redis => IdentityTopic(filter),
        _ => SlashPrefixedTopicOf(filter, isFilter: true),
    };

    private static TopicNameMapping DefaultFromCanonicalTopic(SchemeFamily family, string? topic) => family switch
    {
        SchemeFamily.Mqtt or SchemeFamily.Mqtt5 => MqttTopicNames.FromCanonicalTopic(topic),
        SchemeFamily.Amqp0 => AmqpTopicExchangeNames.FromCanonicalTopic(topic),
        SchemeFamily.Kafka => KafkaTopicNames.FromCanonicalTopic(topic),
        SchemeFamily.Nats => NatsSubjectNames.FromCanonicalTopic(topic),
        SchemeFamily.Redis => IdentityTopic(topic),
        _ => SlashPrefixedTopic(topic),
    };

    /// <summary>A Redis channel (ADR-0126 decision 1): a canonical topic name, the same text on both
    /// sides; a channel with a wildcard, empty or too long is <see cref="TopicNameMappingOutcome.InvalidName"/>.</summary>
    private static TopicNameMapping IdentityTopic(string? name) =>
        CanonicalTopicName.IsValidTopic(name) ? TopicNameMapping.Of(name) : TopicNameMapping.Refused(TopicNameMappingOutcome.InvalidName);

    /// <summary>An AMQP 1.0 address or STOMP destination naming a topic: <c>/topic/&lt;t&gt;</c>
    /// is <c>&lt;t&gt;</c>; a <c>/queue/</c> one, or one with neither prefix, is
    /// <see cref="TopicNameMappingOutcome.UnknownDestination"/>, because it names no topic.</summary>
    private static TopicNameMapping SlashPrefixedTopicOf(string? address, bool isFilter)
    {
        if (address is null)
        {
            return TopicNameMapping.Refused(TopicNameMappingOutcome.InvalidName);
        }

        var mapping = SlashPrefixedDestinations.ToBusDestination(address, isFilter);
        return !mapping.IsMapped ? TopicNameMapping.Refused(mapping.Outcome)
            : mapping.Destination.Kind == BusDestinationKind.Topic ? TopicNameMapping.Of(mapping.Destination.Name)
            : TopicNameMapping.Refused(TopicNameMappingOutcome.UnknownDestination);
    }

    private static TopicNameMapping SlashPrefixedTopic(string? topic) =>
        CanonicalTopicName.IsValidTopic(topic)
            ? TopicNameMapping.Of(SlashPrefixedDestinations.ToAddress(new(BusDestinationKind.Topic, topic)))
            : TopicNameMapping.Refused(TopicNameMappingOutcome.InvalidName);
}
