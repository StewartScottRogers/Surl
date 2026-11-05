namespace Surl.MessageBus;

/// <summary>
/// The <c>/topic/&lt;t&gt;</c> and <c>/queue/&lt;q&gt;</c> convention AMQP 1.0 link addresses
/// and STOMP destinations share (ADR-0100, decision 3.2): both specifications leave the
/// address opaque, so one convention lets a STOMP producer and an AMQP 1.0 consumer meet.
/// </summary>
internal static class SlashPrefixedDestinations
{
    internal const string TopicPrefix = "/topic/";
    internal const string QueuePrefix = "/queue/";

    internal static BusDestinationMapping ToBusDestination(string? address, bool topicIsFilter)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.StartsWith(TopicPrefix, StringComparison.Ordinal))
        {
            var topic = address[TopicPrefix.Length..];
            return Named(BusDestinationKind.Topic, topic, topicIsFilter ? CanonicalTopicName.IsValidFilter(topic) : CanonicalTopicName.IsValidTopic(topic));
        }

        if (address.StartsWith(QueuePrefix, StringComparison.Ordinal))
        {
            var queue = address[QueuePrefix.Length..];
            return Named(BusDestinationKind.Queue, queue, queue.Length > 0 && !queue.Contains('\0'));
        }

        return new(TopicNameMappingOutcome.UnknownDestination, null);
    }

    internal static string ToAddress(BusDestination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return (destination.Kind == BusDestinationKind.Topic ? TopicPrefix : QueuePrefix) + destination.Name;
    }

    private static BusDestinationMapping Named(BusDestinationKind kind, string name, bool isValid) =>
        isValid ? new(TopicNameMappingOutcome.Mapped, new(kind, name)) : new(TopicNameMappingOutcome.InvalidName, null);
}
