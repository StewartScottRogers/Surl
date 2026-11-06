using Surl.MessageBus;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// The topic checks Fetch and ListOffsets make of one partition, in order (ADR-0105 decisions 2
/// and 6; ADR-0102 decision 3.4's Kafka row): a name outside Kafka's rule is
/// <c>INVALID_TOPIC_EXCEPTION</c> (17); a partition other than 0 <c>UNKNOWN_TOPIC_OR_PARTITION</c>
/// (3); a topic the caller may not subscribe to <c>TOPIC_AUTHORIZATION_FAILED</c> (29), whether
/// or not it exists, as Metadata answers it; then a topic with no log
/// <c>UNKNOWN_TOPIC_OR_PARTITION</c> (3).
/// </summary>
internal static class KafkaFetchableTopic
{
    /// <summary>The canonical topic a partition is read from, or why it may not be.</summary>
    /// <param name="bus">The shared message bus.</param>
    /// <param name="name">The Kafka topic name the request gave.</param>
    /// <param name="partition">The partition index the request gave.</param>
    /// <param name="account">The account logged in on the connection, or null.</param>
    /// <param name="scheme">The scheme the connection came in on.</param>
    /// <returns>The canonical topic and <see cref="KafkaErrorCode.None"/>, or null and the error.</returns>
    public static (string? Topic, KafkaErrorCode Error) Find(MessageBus.MessageBus bus, string name, int partition, string? account, string scheme)
    {
        var mapping = bus.Names.ToCanonicalTopic(scheme, name);
        return !mapping.IsMapped ? (null, KafkaErrorCode.InvalidTopicException)
            : partition != KafkaTopicNames.OnlyPartition ? (null, KafkaErrorCode.UnknownTopicOrPartition)
            : !bus.GetTopicAccess(account, mapping.Name).HasFlag(TopicAccessRights.Subscribe) ? (null, KafkaErrorCode.TopicAuthorizationFailed)
            : !bus.GetLogTopics().Contains(mapping.Name, StringComparer.Ordinal) ? (null, KafkaErrorCode.UnknownTopicOrPartition)
            : (mapping.Name, KafkaErrorCode.None);
    }
}
