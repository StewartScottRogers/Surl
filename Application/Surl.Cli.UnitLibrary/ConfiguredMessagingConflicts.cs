namespace Surl.Cli;

/// <summary>
/// The checks ADR-0101 decision 5 makes on <c>--topic</c>, <c>--queue</c>, <c>--topic-map</c> and
/// <c>--topic-access</c> once the whole command line, every config file included, is read. Each item comes with
/// the option name it was written with (<c>--topic</c>, or <c>topic</c> on a config-file line), the name a
/// refusal starts with. In order: a topic filter, a queue name, a <c>--topic-map</c> source
/// (<c>&lt;scheme&gt;:&lt;name&gt;</c>) and a <c>--topic-access</c> filter given twice (row 14). Names compare ordinally.
/// </summary>
internal static class ConfiguredMessagingConflicts
{
    /// <summary>One refusal: the option name as written and the reason after <c>option &lt;name&gt;: </c>.</summary>
    /// <param name="OptionName">The option name the refused item was written with.</param>
    /// <param name="Reason">The refusal reason.</param>
    public sealed record Refusal(string OptionName, string Reason);

    /// <summary>Finds the first refusal, or none.</summary>
    /// <param name="topics">Each <c>--topic</c> with its option name, in configuration order.</param>
    /// <param name="queues">Each <c>--queue</c> with its option name, in configuration order.</param>
    /// <param name="topicMaps">Each <c>--topic-map</c> with its option name, in configuration order.</param>
    /// <param name="topicAccess">Each <c>--topic-access</c> with its option name, in configuration order.</param>
    /// <returns>The first refusal, or <see langword="null"/>.</returns>
    public static Refusal? Find(
        IReadOnlyList<(string OptionName, ConfiguredTopic Topic)> topics,
        IReadOnlyList<(string OptionName, ConfiguredQueue Queue)> queues,
        IReadOnlyList<(string OptionName, ConfiguredTopicMap Map)> topicMaps,
        IReadOnlyList<(string OptionName, ConfiguredTopicAccess Access)> topicAccess)
    {
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(queues);
        ArgumentNullException.ThrowIfNull(topicMaps);
        ArgumentNullException.ThrowIfNull(topicAccess);
        return FindRepeat(topics.Select(topic => (topic.OptionName, topic.Topic.Filter)), TopicGivenTwice)
            ?? FindRepeat(queues.Select(queue => (queue.OptionName, queue.Queue.Name)), QueueGivenTwice)
            ?? FindRepeat(topicMaps.Select(map => (map.OptionName, map.Map.Source)), TopicMapGivenTwice)
            ?? FindRepeat(topicAccess.Select(access => (access.OptionName, access.Access.Filter)), TopicGivenTwice);
    }

    private static readonly Func<string, string> TopicGivenTwice = ConfiguredTopicReader.TopicGivenTwice;

    private static readonly Func<string, string> QueueGivenTwice = ConfiguredQueueReader.QueueGivenTwice;

    private static readonly Func<string, string> TopicMapGivenTwice = ConfiguredTopicMapReader.TopicMapGivenTwice;

    private static Refusal? FindRepeat(IEnumerable<(string OptionName, string Name)> named, Func<string, string> givenTwice)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (optionName, name) in named)
        {
            if (!seen.Add(name))
            {
                return new(optionName, givenTwice(name));
            }
        }

        return null;
    }
}
