namespace Surl.MessageBus;

[TestClass]
public sealed class MessageBusCrossProtocolTests
{
    private readonly SettableTimeProvider clock = new();

    [TestMethod]
    public void Publish_UnderAnMqttName_ReachesEachOtherProtocolsEquivalentSubscription()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var filters = new Dictionary<string, string>
        {
            ["MQTT a/+/c"] = MqttTopicNames.ToCanonicalFilter("a/+/c").Name!,
            ["NATS a.*.c"] = NatsSubjectNames.ToCanonicalFilter("a.*.c").Name!,
            ["NATS a.>"] = NatsSubjectNames.ToCanonicalFilter("a.>").Name!,
            ["AMQP 0-9-1 a.*.c"] = AmqpTopicExchangeNames.ToCanonicalFilter("a.*.c").Name!,
            ["AMQP 0-9-1 a.#"] = AmqpTopicExchangeNames.ToCanonicalFilter("a.#").Name!,
            ["AMQP 1.0 /topic/a/+/c"] = Amqp1AddressNames.ToSubscribeDestination("/topic/a/+/c").Destination!.Name,
            ["STOMP /topic/a/#"] = StompDestinationNames.ToSubscribeDestination("/topic/a/#").Destination!.Name,
            ["Kafka a.b.c"] = KafkaTopicNames.ToCanonicalTopic("a.b.c").Name!,
        };
        var received = new Dictionary<string, List<BusMessage>>();
        foreach (var (subscriber, filter) in filters)
        {
            received[subscriber] = [];
            connection.Subscribe(filter, received[subscriber].Add);
        }

        var shared = MqttTopicNames.ToCanonicalSharedSubscription("$share/workers/a/+/c");
        var sharedReceived = new List<BusMessage>();
        connection.Subscribe(shared.Name!, sharedReceived.Add, shared.Group);

        var result = bus.Publish(new BusMessage(MqttTopicNames.ToCanonicalTopic("a/b/c").Name!, "hello"u8.ToArray()));

        Assert.AreEqual(new BusPublishResult(MessageBusOutcome.Succeeded, filters.Count + 1), result);
        foreach (var (subscriber, messages) in received)
        {
            Assert.HasCount(1, messages, subscriber);
        }

        Assert.HasCount(1, sharedReceived);
        var topic = sharedReceived[0].Topic;
        Assert.AreEqual("a.b.c", NatsSubjectNames.FromCanonicalTopic(topic).Name);
        Assert.AreEqual("a.b.c", AmqpTopicExchangeNames.FromCanonicalTopic(topic).Name);
        Assert.AreEqual("a.b.c", KafkaTopicNames.FromCanonicalTopic(topic).Name);
        Assert.AreEqual("/topic/a/b/c", Amqp1AddressNames.ToAddress(new(BusDestinationKind.Topic, topic)));
        Assert.AreEqual("/topic/a/b/c", StompDestinationNames.ToDestination(new(BusDestinationKind.Topic, topic)));
    }

    [TestMethod]
    [DataRow("a//c")]
    [DataRow("a/b.x/c")]
    public void Publish_OnATopicNatsAndAmqpCannotName_MapsBackUnreachableForTheirSubscriptions(string topic)
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var nats = new List<BusMessage>();
        var amqp = new List<BusMessage>();
        connection.Subscribe(NatsSubjectNames.ToCanonicalFilter("a.*.c").Name!, nats.Add);
        connection.Subscribe(AmqpTopicExchangeNames.ToCanonicalFilter("a.*.c").Name!, amqp.Add);

        bus.Publish(new BusMessage(topic, "1"u8.ToArray()));

        Assert.HasCount(1, nats);
        Assert.HasCount(1, amqp);
        Assert.AreEqual(TopicNameMappingOutcome.Unreachable, NatsSubjectNames.FromCanonicalTopic(nats[0].Topic).Outcome);
        Assert.AreEqual(TopicNameMappingOutcome.Unreachable, AmqpTopicExchangeNames.FromCanonicalTopic(amqp[0].Topic).Outcome);
    }

    [TestMethod]
    public void FilterMatches_MqttSingleLevelWildcard_MatchesWhatTheNatsAndAmqpEquivalentsMatch()
    {
        var mqtt = MqttTopicNames.ToCanonicalFilter("a/+/c").Name;
        var nats = NatsSubjectNames.ToCanonicalFilter("a.*.c").Name;
        var amqp = AmqpTopicExchangeNames.ToCanonicalFilter("a.*.c").Name;

        Assert.AreEqual(mqtt, nats);
        Assert.AreEqual(mqtt, amqp);
    }
}
