using System.Diagnostics.CodeAnalysis;

namespace Surl.MessageBus;

/// <summary>
/// MQTT 3.1.1 and MQTT 5 topic names and filters on the bus (ADR-0100, decision 3.2): every
/// valid MQTT name is canonical as it stands, and an MQTT 5 shared subscription
/// <c>$share/&lt;group&gt;/&lt;filter&gt;</c> (MQTT 5.0 section 4.8.2) is the canonical filter
/// with a fan-out group.
/// </summary>
public static class MqttTopicNames
{
    private const string SharedSubscriptionPrefix = "$share/";

    /// <summary>
    /// Maps an MQTT topic name (MQTT 3.1.1 and 5.0 section 4.7) onto the bus.
    /// </summary>
    /// <returns>The same name, or <see cref="TopicNameMappingOutcome.InvalidName"/> when it is
    /// no valid MQTT topic name.</returns>
    public static TopicNameMapping ToCanonicalTopic(string? topic) =>
        CanonicalTopicName.IsValidTopic(topic) ? TopicNameMapping.Of(topic) : Refused(TopicNameMappingOutcome.InvalidName);

    /// <summary>
    /// Maps an MQTT topic filter onto the bus. <c>$share/</c> means nothing special here; MQTT 5
    /// servers call <see cref="ToCanonicalSharedSubscription"/>.
    /// </summary>
    /// <returns>The same filter, or <see cref="TopicNameMappingOutcome.InvalidName"/> when it is
    /// no valid MQTT filter (a <c>#</c> before the last level among them).</returns>
    public static TopicNameMapping ToCanonicalFilter(string? filter) =>
        IsMqttFilter(filter) ? TopicNameMapping.Of(filter) : Refused(TopicNameMappingOutcome.InvalidName);

    /// <summary>
    /// Maps an MQTT 5 subscription's topic filter onto the bus: a shared subscription
    /// <c>$share/&lt;group&gt;/&lt;filter&gt;</c> becomes <c>&lt;filter&gt;</c> in group
    /// <c>&lt;group&gt;</c>; any other filter is mapped as <see cref="ToCanonicalFilter"/> does.
    /// </summary>
    /// <returns>The filter and its group, or <see cref="TopicNameMappingOutcome.InvalidName"/>
    /// when the share name is empty or holds <c>+</c> or <c>#</c>, or the filter is invalid.</returns>
    public static TopicNameMapping ToCanonicalSharedSubscription(string? filter)
    {
        if (filter is null || !filter.StartsWith(SharedSubscriptionPrefix, StringComparison.Ordinal))
        {
            return ToCanonicalFilter(filter);
        }

        var rest = filter[SharedSubscriptionPrefix.Length..];
        var end = rest.IndexOf('/');
        var group = end < 0 ? string.Empty : rest[..end];
        var mapped = ToCanonicalFilter(rest[(end + 1)..]);
        return group.Length == 0 || group.AsSpan().IndexOfAny('+', '#') >= 0 || !mapped.IsMapped
            ? Refused(TopicNameMappingOutcome.InvalidName)
            : mapped with { Group = group };
    }

    /// <summary>
    /// Maps a canonical topic name to MQTT's.
    /// </summary>
    /// <returns>The same name, or <see cref="TopicNameMappingOutcome.InvalidName"/> when it is
    /// not canonical.</returns>
    public static TopicNameMapping FromCanonicalTopic(string? topic) => ToCanonicalTopic(topic);

    /// <summary>
    /// Maps a canonical filter to MQTT's.
    /// </summary>
    /// <returns>The same filter; <see cref="TopicNameMappingOutcome.Unreachable"/> when a
    /// <c>#</c> stands before its last level, which MQTT cannot name; or
    /// <see cref="TopicNameMappingOutcome.InvalidName"/> when it is not canonical.</returns>
    public static TopicNameMapping FromCanonicalFilter(string? filter) =>
        !CanonicalTopicName.IsValidFilter(filter) ? Refused(TopicNameMappingOutcome.InvalidName)
        : IsMqttFilter(filter) ? TopicNameMapping.Of(filter)
        : Refused(TopicNameMappingOutcome.Unreachable);

    /// <summary>
    /// Maps a canonical filter and fan-out group to an MQTT 5 shared subscription's filter.
    /// </summary>
    /// <returns><c>$share/&lt;group&gt;/&lt;filter&gt;</c>, or the refusal
    /// <see cref="FromCanonicalFilter"/> gives, or <see cref="TopicNameMappingOutcome.Unreachable"/>
    /// when <paramref name="group"/> is empty or holds <c>/</c>, <c>+</c> or <c>#</c>.</returns>
    public static TopicNameMapping FromCanonicalSharedSubscription(string? filter, string group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var mapped = FromCanonicalFilter(filter);
        return !mapped.IsMapped ? mapped
            : group.Length == 0 || group.AsSpan().IndexOfAny('/', '+', '#') >= 0 ? Refused(TopicNameMappingOutcome.Unreachable)
            : TopicNameMapping.Of(SharedSubscriptionPrefix + group + "/" + mapped.Name);
    }

    private static bool IsMqttFilter([NotNullWhen(true)] string? filter) =>
        CanonicalTopicName.IsValidFilter(filter) && !CanonicalTopicName.SplitLevels(filter)[..^1].Contains("#");

    private static TopicNameMapping Refused(TopicNameMappingOutcome outcome) => TopicNameMapping.Refused(outcome);
}
