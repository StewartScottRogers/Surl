using Surl.Cli;
using Surl.MessageBus;
using Surl.Output;

namespace Surl.Composition;

/// <summary>
/// Every <c>--topic</c>, <c>--queue</c>, <c>--topic-map</c> and <c>--topic-access</c> composed into the
/// <see cref="MessageBusConfiguration"/> of the one message bus every broker shares (ADR-0102 decisions 3, 4
/// and 5): each filter checked, each map's schemes and names checked, and each grant's groups expanded to
/// account names, before any listener binds.
/// </summary>
public static class MessageBusComposition
{
    private const string WarningPrefix = "surl: warning: ";
    private const string TopicOption = "topic";
    private const string QueueOption = "queue";
    private const string TopicMapOption = "topic-map";
    private const string TopicAccessOption = "topic-access";

    /// <summary>
    /// Composes the message bus's configuration from <paramref name="commandLine"/>, refusing, in this order:
    /// a <c>--topic</c> filter that is not a canonical filter; more <c>--queue</c>s than the bus holds; a
    /// <c>--topic-map</c> naming a scheme no broker maps names for, a name not valid on its scheme, or a name
    /// a map on the same scheme or its TLS twin already aliases; then a <c>--topic-access</c> filter that is
    /// not a canonical filter or a member naming no account or group. Each is exit 2.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="groups">Every <c>--group</c>, expanded over the accounts.</param>
    /// <returns>The configuration, <see cref="MessageBusConfiguration.None"/> when none of the four options is
    /// given; or null and the refusal after <c>surl: </c>.</returns>
    public static (MessageBusConfiguration? Configuration, string? FailureMessage) Compose(
        SurlCommandLine commandLine, AccountGroupExpansion groups)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(groups);
        if (commandLine.Topics.Count + commandLine.Queues.Count + commandLine.TopicMaps.Count + commandLine.TopicAccess.Count == 0)
        {
            return (MessageBusConfiguration.None, null);
        }

        var (topics, topicFailure) = ComposeTopics(commandLine.Topics);
        if (topics is null)
        {
            return (null, topicFailure);
        }

        if (commandLine.Queues.Count > MessageBus.MessageBus.DefaultMaxQueues)
        {
            return (null, TooManyQueues);
        }

        var (maps, mapFailure) = ComposeTopicMaps(commandLine.TopicMaps);
        if (maps is null)
        {
            return (null, mapFailure);
        }

        var (grants, grantFailure) = ComposeGrants(commandLine.TopicAccess, groups);
        return grants is null
            ? (null, grantFailure)
            : (new MessageBusConfiguration
            {
                Topics = topics,
                Queues = [.. commandLine.Queues.Select(queue => new DeclaredQueue(queue.Name, queue.IsDurable))],
                TopicMaps = maps,
                AccessGrants = grants,
            }, null);
    }

    /// <summary>
    /// Writes ADR-0102 decision 3.4's warning for each <c>--topic-access</c> filter granting <c>@everyone</c>,
    /// from the info level up: <c>surl: warning: --topic-access: &lt;filter&gt; grants &lt;pub|sub|pubsub&gt; to
    /// every caller, anonymous included</c>.
    /// </summary>
    /// <param name="logLevel">The log level.</param>
    /// <param name="configuration">The composed configuration.</param>
    /// <param name="log">The log stream.</param>
    public static void WriteWarnings(LogLevel logLevel, MessageBusConfiguration configuration, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(log);
        if (logLevel < LogLevel.Info)
        {
            return;
        }

        foreach (var grant in configuration.AccessGrants.Where(grant => grant.Member == TopicAccessGrant.Everyone))
        {
            log.WriteLine(WarningPrefix + $"--{TopicAccessOption}: {grant.Filter} grants {AccessWord(grant.Rights)} to every caller, anonymous included");
        }
    }

    /// <summary>
    /// Writes ADR-0102 decision 5's notes, from the verbose level up: <c>* Topic &lt;filter&gt;[: retained][, QoS
    /// at most &lt;n&gt;]</c> per topic (<c>: </c> before the first attribute), <c>* Queue &lt;name&gt;[: durable]</c>
    /// per queue, <c>* Topic map &lt;scheme&gt;:&lt;name&gt; = &lt;scheme&gt;:&lt;name&gt;</c> per map, then
    /// <c>* Topic access: &lt;n&gt; grants</c> once any is configured, each in command-line order.
    /// </summary>
    /// <param name="logLevel">The log level.</param>
    /// <param name="configuration">The composed configuration.</param>
    /// <param name="log">The log stream.</param>
    public static void WriteStartLines(LogLevel logLevel, MessageBusConfiguration configuration, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(log);
        if (logLevel < LogLevel.Verbose)
        {
            return;
        }

        foreach (var topic in configuration.Topics)
        {
            log.WriteLine($"* Topic {topic.Filter}{TopicAttributes(topic)}");
        }

        foreach (var queue in configuration.Queues)
        {
            log.WriteLine($"* Queue {queue.Name}{(queue.Durable ? ": durable" : string.Empty)}");
        }

        foreach (var map in configuration.TopicMaps)
        {
            log.WriteLine($"* Topic map {map.Scheme}:{map.Name} = {map.TargetScheme}:{map.TargetName}");
        }

        if (configuration.AccessGrants.Count > 0)
        {
            log.WriteLine($"* Topic access: {configuration.AccessGrants.Count} grants");
        }
    }

    /// <summary>A <c>--topic</c> or <c>--topic-access</c> filter that is not a canonical filter (ADR-0101 decision 5 row 10).</summary>
    /// <param name="optionName">The option's long name, without <c>--</c>.</param>
    /// <param name="filter">The filter as written.</param>
    /// <returns><c>(2) option --&lt;option&gt;: topic &lt;filter&gt; is not a valid topic filter</c>.</returns>
    public static string FilterIsNotValid(string optionName, string filter) => $"(2) option --{optionName}: topic {filter} is not a valid topic filter";

    /// <summary>More <c>--queue</c>s than the bus holds (<see cref="MessageBus.MessageBus.DefaultMaxQueues"/>).</summary>
    public static string TooManyQueues => $"(2) option --{QueueOption}: more than {MessageBus.MessageBus.DefaultMaxQueues} queues";

    /// <summary>A <c>--topic-map</c> scheme no broker maps names for (ADR-0102 decision 3.5).</summary>
    /// <param name="scheme">The scheme as written.</param>
    /// <returns><c>(2) option --topic-map: &lt;scheme&gt; is not a messaging scheme</c>.</returns>
    public static string SchemeIsNotMessaging(string scheme) => $"(2) option --{TopicMapOption}: {scheme} is not a messaging scheme";

    /// <summary>A <c>--topic-map</c> name that is not valid on its scheme (ADR-0102 decision 3.3, row 10).</summary>
    /// <param name="scheme">The scheme the name was written for.</param>
    /// <returns><c>(2) option --topic-map: not a valid name for &lt;scheme&gt;</c>.</returns>
    public static string NameIsNotValidFor(string scheme) => $"(2) option --{TopicMapOption}: not a valid name for {scheme}";

    /// <summary>A <c>--topic-map</c> source a map on the same scheme or its TLS twin already aliases (row 14).</summary>
    /// <param name="source">The source as written, <c>&lt;scheme&gt;:&lt;name&gt;</c>.</param>
    /// <returns><c>(2) option --topic-map: topic map &lt;source&gt; is given twice</c>.</returns>
    public static string TopicMapGivenTwice(string source) => $"(2) option --{TopicMapOption}: topic map {source} is given twice";

    private static string TopicAttributes(DeclaredTopic topic)
    {
        var attributes = new List<string>(2);
        if (topic.Retain)
        {
            attributes.Add("retained");
        }

        if (topic.MaximumQualityOfService is { } qos)
        {
            attributes.Add($"QoS at most {(int)qos}");
        }

        return attributes.Count == 0 ? string.Empty : ": " + string.Join(", ", attributes);
    }

    private static string AccessWord(TopicAccessRights rights) => rights switch
    {
        TopicAccessRights.Publish => "pub",
        TopicAccessRights.Subscribe => "sub",
        _ => "pubsub",
    };

    private static TopicAccessRights RightsOf(string access) => access switch
    {
        "pub" => TopicAccessRights.Publish,
        "sub" => TopicAccessRights.Subscribe,
        _ => TopicAccessRights.PublishAndSubscribe,
    };

    private static (IReadOnlyList<DeclaredTopic>? Topics, string? FailureMessage) ComposeTopics(IReadOnlyList<ConfiguredTopic> configured)
    {
        var topics = new List<DeclaredTopic>(configured.Count);
        foreach (var topic in configured)
        {
            if (!CanonicalTopicName.IsValidFilter(topic.Filter))
            {
                return (null, FilterIsNotValid(TopicOption, topic.Filter));
            }

            topics.Add(new DeclaredTopic(topic.Filter, topic.IsRetained, (BusQualityOfService?)topic.MaxQos));
        }

        return (topics, null);
    }

    // Each map is checked alone through TopicNameLookup, which owns the names' rules; a name aliased twice on
    // one scheme family (a scheme and its TLS twin share one map) is found here, so the bus never throws.
    private static (IReadOnlyList<TopicMap>? Maps, string? FailureMessage) ComposeTopicMaps(IReadOnlyList<ConfiguredTopicMap> configured)
    {
        var maps = new List<TopicMap>(configured.Count);
        var aliased = new HashSet<(string Family, string Name)>();
        foreach (var configuredMap in configured)
        {
            var map = new TopicMap(configuredMap.FromScheme, configuredMap.FromName, configuredMap.ToScheme, configuredMap.ToName);
            if (RefusalOf(map) is { } refusal)
            {
                return (null, refusal);
            }

            if (!aliased.Add((FamilyOf(map.Scheme), map.Name)))
            {
                return (null, TopicMapGivenTwice(configuredMap.Source));
            }

            maps.Add(map);
        }

        return (maps, null);
    }

    private static string? RefusalOf(TopicMap map)
    {
        var unknown = new[] { map.Scheme, map.TargetScheme }.FirstOrDefault(scheme => !TopicNameLookup.IsKnownScheme(scheme));
        if (unknown is not null)
        {
            return SchemeIsNotMessaging(unknown);
        }

        try
        {
            _ = new TopicNameLookup([map]);
            return null;
        }
        catch (ArgumentException)
        {
            var lookup = new TopicNameLookup([]);
            return NameIsNotValidFor(lookup.ToCanonicalTopic(map.Scheme, map.Name).IsMapped ? map.TargetScheme : map.Scheme);
        }
    }

    // The scheme a TLS twin shares its map with: mqtts, mqtt5s and mqtt5+ws, amqps, amqp1s, kafkas, natss and
    // stomps each name their plain scheme's family. The schemes are TopicNameLookup's, all known here.
    private static string FamilyOf(string scheme)
    {
        var lowered = scheme.ToLowerInvariant();
        var withoutWebSocket = lowered.EndsWith("+ws", StringComparison.Ordinal) ? lowered[..^3] : lowered;
        var withoutTls = withoutWebSocket[..^1];
        return withoutWebSocket.EndsWith('s') && TopicNameLookup.IsKnownScheme(withoutTls) ? withoutTls : withoutWebSocket;
    }

    // Each --topic-access row's members expanded to account names, @everyone kept as its marker; a member reached
    // twice in one row keeps the union of its rights. The reader refuses a filter given twice.
    private static (IReadOnlyList<TopicAccessGrant>? Grants, string? FailureMessage) ComposeGrants(
        IReadOnlyList<ConfiguredTopicAccess> rows, AccountGroupExpansion groups)
    {
        var grants = new List<TopicAccessGrant>();
        foreach (var row in rows)
        {
            if (!CanonicalTopicName.IsValidFilter(row.Filter))
            {
                return (null, FilterIsNotValid(TopicAccessOption, row.Filter));
            }

            var rightsByMember = new Dictionary<string, TopicAccessRights>(StringComparer.Ordinal);
            foreach (var grant in row.Grants)
            {
                if (ReachedBy(grant.Member, groups, out var reached) is { } failure)
                {
                    return (null, failure);
                }

                foreach (var member in reached)
                {
                    rightsByMember[member] = rightsByMember.GetValueOrDefault(member) | RightsOf(grant.Access);
                }
            }

            grants.AddRange(rightsByMember.Select(pair => new TopicAccessGrant(row.Filter, pair.Key, pair.Value)));
        }

        return (grants, null);
    }

    private static string? ReachedBy(ConfiguredMember member, AccountGroupExpansion groups, out IReadOnlySet<string> reached)
    {
        if (member.IsGroup && member.Name == AccountGroupExpansion.Everyone)
        {
            reached = new HashSet<string>(StringComparer.Ordinal) { TopicAccessGrant.Everyone };
            return null;
        }

        return groups.Expand(TopicAccessOption, [member], out reached);
    }
}
