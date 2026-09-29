namespace Surl.Protocol.Mqtt;

/// <summary>
/// The rules for topic names and topic filters (MQTT 3.1.1, section 4.7): levels split by
/// <c>/</c>, <c>+</c> matching exactly one level and <c>#</c> any number of levels at the end.
/// Names are compared ordinally, case and all.
/// </summary>
internal static class MqttTopicFilter
{
    /// <summary>
    /// Whether <paramref name="topic"/> is a topic a message may be published to: at least one
    /// character, and no wildcard (sections 4.7.1-1 and 4.7.3-1).
    /// </summary>
    /// <param name="topic">The topic name.</param>
    /// <returns><see langword="true"/> when it is a valid topic name.</returns>
    public static bool IsValidTopicName(string topic) => topic.Length > 0 && topic.IndexOfAny(['+', '#']) < 0;

    /// <summary>
    /// Whether <paramref name="filter"/> is a valid topic filter: at least one character,
    /// <c>#</c> only as the whole of the last level, and <c>+</c> only as the whole of a level
    /// (sections 4.7.1.2, 4.7.1.3 and 4.7.3-1).
    /// </summary>
    /// <param name="filter">The topic filter.</param>
    /// <returns><see langword="true"/> when it is a valid topic filter.</returns>
    public static bool IsValidFilter(string filter)
    {
        if (filter.Length == 0)
        {
            return false;
        }

        var levels = filter.Split('/');
        for (var index = 0; index < levels.Length; index++)
        {
            if (!IsValidFilterLevel(levels[index], index == levels.Length - 1))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether the valid topic filter <paramref name="filter"/> matches <paramref name="topic"/>.
    /// A filter that starts with a wildcard never matches a topic that starts with <c>$</c>
    /// (section 4.7.2).
    /// </summary>
    /// <param name="filter">A valid topic filter.</param>
    /// <param name="topic">A valid topic name.</param>
    /// <returns><see langword="true"/> when the filter matches the topic.</returns>
    public static bool Matches(string filter, string topic) => Matches(SplitLevels(filter), SplitLevels(topic));

    /// <summary>
    /// <see cref="Matches(string, string)"/> for a filter and a topic already split with
    /// <see cref="SplitLevels"/>, so a caller matching many pairs splits each string once.
    /// </summary>
    /// <param name="filterLevels">A valid topic filter's levels.</param>
    /// <param name="topicLevels">A valid topic name's levels.</param>
    /// <returns><see langword="true"/> when the filter matches the topic.</returns>
    public static bool Matches(string[] filterLevels, string[] topicLevels) =>
        !IsSystemTopicUnderLeadingWildcard(filterLevels[0], topicLevels[0]) && LevelsMatch(filterLevels, topicLevels);

    /// <summary>
    /// Splits a topic name or filter into its levels at each <c>/</c>.
    /// </summary>
    /// <param name="topicOrFilter">A topic name or topic filter.</param>
    /// <returns>Its levels, at least one.</returns>
    public static string[] SplitLevels(string topicOrFilter) => topicOrFilter.Split('/');

    private static bool IsSystemTopicUnderLeadingWildcard(string firstFilterLevel, string firstTopicLevel) =>
        firstTopicLevel.StartsWith('$') && firstFilterLevel is "+" or "#";

    private static bool LevelsMatch(string[] filterLevels, string[] topicLevels)
    {
        for (var index = 0; index < filterLevels.Length; index++)
        {
            if (filterLevels[index] == "#")
            {
                return true;
            }

            if (index >= topicLevels.Length || !LevelMatches(filterLevels[index], topicLevels[index]))
            {
                return false;
            }
        }

        return filterLevels.Length == topicLevels.Length;
    }

    private static bool IsValidFilterLevel(string level, bool isLastLevel)
    {
        if (level.Contains('#'))
        {
            return level == "#" && isLastLevel;
        }

        return !level.Contains('+') || level == "+";
    }

    private static bool LevelMatches(string filterLevel, string topicLevel) =>
        filterLevel == "+" || string.Equals(filterLevel, topicLevel, StringComparison.Ordinal);
}
