using Surl.MessageBus;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// Reads the retained messages an MQTT 3.1.1 <c>SUBSCRIBE</c> is answered with from the
/// <see cref="MessageBus.MessageBus"/> (ADR-0100, decision 3): it keeps none of its own.
/// </summary>
internal static class MqttBusRetainedMessages
{
    /// <summary>
    /// Every retained message on <paramref name="bus"/> whose topic any of
    /// <paramref name="filters"/> matches, each once, in ordinal order of topic. Each filter is
    /// subscribed to for the moment it takes to read its retained messages, then unsubscribed:
    /// an MQTT 3.1.1 subscribe here is one-shot (ADR-0014).
    /// </summary>
    /// <param name="bus">The bus the server publishes to.</param>
    /// <param name="filters">Valid MQTT topic filters, already canonical (<see cref="MqttTopicNames.ToCanonicalFilter"/>).</param>
    /// <returns>Each matching topic with its payload.</returns>
    public static IReadOnlyList<KeyValuePair<string, byte[]>> MatchingAny(MessageBus.MessageBus bus, IReadOnlyList<string> filters)
    {
        var matching = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        using var connection = bus.Connect();
        foreach (var filter in filters.Distinct(StringComparer.Ordinal))
        {
            var subscribed = connection.Subscribe(filter, IgnoreLiveMessage, includeRetained: true);
            // Never refused: every filter is a valid MQTT filter, so canonical, and the
            // connection holds one subscription at a time, far below the bus's bound.
            subscribed.Subscription!.Dispose();
            foreach (var message in subscribed.RetainedMessages)
            {
                matching[message.Topic] = message.Payload.ToArray();
            }
        }

        return [.. matching];
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
