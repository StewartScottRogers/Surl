using System.Diagnostics.CodeAnalysis;

namespace Surl.MessageBus;

/// <summary>
/// Kafka topic names on the bus (ADR-0100, decision 3.2; Kafka protocol guide, "Error
/// Codes"): a Kafka topic name is <c>[A-Za-z0-9._-]{1,249}</c> other than <c>.</c> and
/// <c>..</c>, and each <c>.</c> becomes <c>/</c>. Every bus topic has one partition, 0. Kafka
/// has no wildcards on the wire: a consumer names each topic, so a Kafka subscription's
/// canonical filter is the canonical topic itself.
/// </summary>
public static class KafkaTopicNames
{
    /// <summary>The most characters a Kafka topic name holds: 249.</summary>
    public const int MaxTopicLength = 249;

    /// <summary>The one partition every bus topic has: 0.</summary>
    public const int OnlyPartition = 0;

    /// <summary>
    /// Maps a Kafka topic name and partition onto a canonical topic name.
    /// </summary>
    /// <param name="topic">The Kafka topic name.</param>
    /// <param name="partition">The partition; only <see cref="OnlyPartition"/> exists.</param>
    /// <returns>The canonical topic; <see cref="TopicNameMappingOutcome.InvalidName"/> (Kafka's
    /// <c>INVALID_TOPIC_EXCEPTION</c>, 17) for a name outside Kafka's rule; or
    /// <see cref="TopicNameMappingOutcome.UnknownPartition"/> (<c>UNKNOWN_TOPIC_OR_PARTITION</c>,
    /// 3) for another partition.</returns>
    public static TopicNameMapping ToCanonicalTopic(string? topic, int partition = OnlyPartition) =>
        !IsKafkaTopic(topic, '.') ? Refused(TopicNameMappingOutcome.InvalidName)
        : partition != OnlyPartition ? Refused(TopicNameMappingOutcome.UnknownPartition)
        : TopicNameMapping.Of(topic.Replace('.', '/'));

    /// <summary>
    /// Maps a canonical topic name onto a Kafka topic name.
    /// </summary>
    /// <param name="topic">The canonical topic name.</param>
    /// <returns>The Kafka topic name; <see cref="TopicNameMappingOutcome.Unreachable"/> when a
    /// level holds a character outside <c>[A-Za-z0-9_-]</c>, the name is longer than
    /// <see cref="MaxTopicLength"/>, or it would be <c>.</c> or <c>..</c>; or
    /// <see cref="TopicNameMappingOutcome.InvalidName"/> when it is not canonical.</returns>
    public static TopicNameMapping FromCanonicalTopic(string? topic) =>
        !CanonicalTopicName.IsValidTopic(topic) ? Refused(TopicNameMappingOutcome.InvalidName)
        : IsKafkaTopic(topic, '/') ? TopicNameMapping.Of(topic.Replace('/', '.'))
        : Refused(TopicNameMappingOutcome.Unreachable);

    private static bool IsKafkaTopic([NotNullWhen(true)] string? name, char separator) =>
        name is { Length: > 0 and <= MaxTopicLength }
        && name.Replace(separator, '.') is not ("." or "..")
        && name.All(character => character == separator || IsKafkaLevelCharacter(character));

    private static bool IsKafkaLevelCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '_' or '-';

    private static TopicNameMapping Refused(TopicNameMappingOutcome outcome) => TopicNameMapping.Refused(outcome);
}
