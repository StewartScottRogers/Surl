using Surl.MessageBus;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// Makes the message bus an MQTT server is built on in a test, and retains and reads messages
/// on it as the server does.
/// </summary>
internal static class TestMessageBus
{
    /// <summary>An empty bus on the system clock, with the default bounds but <paramref name="maxTopics"/>.</summary>
    public static MessageBus.MessageBus New(int maxTopics = MessageBus.MessageBus.DefaultMaxTopics) =>
        new(TimeProvider.System, maxTopics);

    /// <summary>Publishes <paramref name="payload"/> on <paramref name="topic"/> as a retained message.</summary>
    public static void Retain(this MessageBus.MessageBus bus, string topic, ReadOnlySpan<byte> payload)
    {
        var published = bus.Publish(new BusMessage(topic, payload.ToArray()) { Retain = true });

        Assert.AreEqual(MessageBusOutcome.Succeeded, published.Outcome);
    }

    /// <summary>The retained messages <paramref name="filters"/> match, read as a subscribe reads them.</summary>
    public static IReadOnlyList<KeyValuePair<string, byte[]>> MatchingAny(this MessageBus.MessageBus bus, IReadOnlyList<string> filters) =>
        MqttBusRetainedMessages.MatchingAny(bus, filters);
}
