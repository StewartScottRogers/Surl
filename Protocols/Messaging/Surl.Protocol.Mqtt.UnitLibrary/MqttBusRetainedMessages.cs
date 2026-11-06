using Surl.MessageBus;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// Reads the retained messages an MQTT 3.1.1 <c>SUBSCRIBE</c> is answered with from the
/// <see cref="MessageBus.MessageBus"/> (ADR-0100, decision 3): it keeps none of its own.
/// </summary>
internal static class MqttBusRetainedMessages
{
    /// <summary>
    /// Subscribes <paramref name="account"/> to each of <paramref name="filters"/> for the moment
    /// it takes to read the filter's retained messages, then unsubscribes: an MQTT 3.1.1 subscribe
    /// here is one-shot (ADR-0014). The bus checks each subscribe against its
    /// <c>--topic-access</c> grants (ADR-0102, decision 3.4), so a filter the account may not
    /// subscribe to is not granted and reads nothing.
    /// </summary>
    /// <param name="bus">The bus the server publishes to.</param>
    /// <param name="account">The connection's checked account, or <see langword="null"/> for an anonymous caller.</param>
    /// <param name="filters">Valid MQTT topic filters, already canonical (<see cref="MqttTopicNames.ToCanonicalFilter"/>).</param>
    /// <returns>Whether each filter was granted, in the order given, and every retained message
    /// a granted filter matches, each topic once, in ordinal order of topic.</returns>
    public static MqttRetainedReading Read(MessageBus.MessageBus bus, string? account, IReadOnlyList<string> filters)
    {
        var matching = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var granted = new List<bool>(filters.Count);
        using var connection = bus.Connect(account);
        foreach (var filter in filters)
        {
            var subscribed = connection.Subscribe(filter, IgnoreLiveMessage, includeRetained: true);
            // Refused only for access: every filter is a valid MQTT filter, so canonical, and the
            // connection holds one subscription at a time, far below the bus's bound.
            granted.Add(subscribed.Outcome == MessageBusOutcome.Succeeded);
            subscribed.Subscription?.Dispose();
            foreach (var message in subscribed.RetainedMessages)
            {
                matching[message.Topic] = message.Payload.ToArray();
            }
        }

        return new(granted, [.. matching]);
    }

    /// <summary>
    /// The live delivery of a one-shot subscription: it lives only while its retained messages
    /// are read, and a publish that lands in that moment is not the subscriber's to receive.
    /// </summary>
    /// <param name="message">The message published in that moment.</param>
    internal static void IgnoreLiveMessage(BusMessage message)
    {
    }
}
