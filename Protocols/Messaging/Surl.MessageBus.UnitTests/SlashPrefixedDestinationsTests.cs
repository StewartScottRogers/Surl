namespace Surl.MessageBus;

[TestClass]
public sealed class SlashPrefixedDestinationsTests
{
    public static IEnumerable<object[]> Protocols =>
    [
        ["AMQP 1.0", (Func<string, BusDestinationMapping>)Amqp1AddressNames.ToPublishDestination, (Func<string, BusDestinationMapping>)Amqp1AddressNames.ToSubscribeDestination, (Func<BusDestination, string>)Amqp1AddressNames.ToAddress],
        ["STOMP 1.2", (Func<string, BusDestinationMapping>)StompDestinationNames.ToPublishDestination, (Func<string, BusDestinationMapping>)StompDestinationNames.ToSubscribeDestination, (Func<BusDestination, string>)StompDestinationNames.ToDestination],
    ];

    [TestMethod]
    [DynamicData(nameof(Protocols))]
    public void ToPublishDestination_TopicAddress_IsTheCanonicalTopicAndMapsBack(
        string protocol, Func<string, BusDestinationMapping> toPublish, Func<string, BusDestinationMapping> toSubscribe, Func<BusDestination, string> toAddress)
    {
        _ = toSubscribe;
        var mapped = toPublish("/topic/a/b/c");

        Assert.AreEqual(new BusDestinationMapping(TopicNameMappingOutcome.Mapped, new(BusDestinationKind.Topic, "a/b/c")), mapped, protocol);
        Assert.IsTrue(mapped.IsMapped, protocol);
        Assert.AreEqual("/topic/a/b/c", toAddress(mapped.Destination), protocol);
    }

    [TestMethod]
    [DynamicData(nameof(Protocols))]
    public void ToPublishDestination_QueueAddress_IsTheBusQueueAndMapsBack(
        string protocol, Func<string, BusDestinationMapping> toPublish, Func<string, BusDestinationMapping> toSubscribe, Func<BusDestination, string> toAddress)
    {
        foreach (var map in new[] { toPublish, toSubscribe })
        {
            var mapped = map("/queue/orders.new");

            Assert.AreEqual(new BusDestinationMapping(TopicNameMappingOutcome.Mapped, new(BusDestinationKind.Queue, "orders.new")), mapped, protocol);
            Assert.AreEqual("/queue/orders.new", toAddress(mapped.Destination!), protocol);
        }
    }

    [TestMethod]
    [DynamicData(nameof(Protocols))]
    public void ToSubscribeDestination_TopicFilterAddress_IsTheCanonicalFilterAndMapsBack(
        string protocol, Func<string, BusDestinationMapping> toPublish, Func<string, BusDestinationMapping> toSubscribe, Func<BusDestination, string> toAddress)
    {
        _ = toPublish;
        var mapped = toSubscribe("/topic/a/+/#");

        Assert.AreEqual(new BusDestinationMapping(TopicNameMappingOutcome.Mapped, new(BusDestinationKind.Topic, "a/+/#")), mapped, protocol);
        Assert.AreEqual("/topic/a/+/#", toAddress(mapped.Destination!), protocol);
    }

    [TestMethod]
    [DynamicData(nameof(Protocols))]
    public void ToDestination_AddressWithNeitherPrefix_IsUnknownDestination(
        string protocol, Func<string, BusDestinationMapping> toPublish, Func<string, BusDestinationMapping> toSubscribe, Func<BusDestination, string> toAddress)
    {
        _ = toAddress;
        foreach (var address in new[] { "/exchange/amq.topic", "topic/a", "a/b", "", "/topic", "/queue" })
        {
            Assert.AreEqual(new BusDestinationMapping(TopicNameMappingOutcome.UnknownDestination, null), toPublish(address), $"{protocol} {address}");
            Assert.AreEqual(TopicNameMappingOutcome.UnknownDestination, toSubscribe(address).Outcome, $"{protocol} {address}");
        }
    }

    [TestMethod]
    [DynamicData(nameof(Protocols))]
    public void ToDestination_PrefixFollowedByANameTheBusCannotHold_IsInvalidName(
        string protocol, Func<string, BusDestinationMapping> toPublish, Func<string, BusDestinationMapping> toSubscribe, Func<BusDestination, string> toAddress)
    {
        _ = toAddress;
        Assert.AreEqual(new BusDestinationMapping(TopicNameMappingOutcome.InvalidName, null), toPublish("/topic/a/+"), protocol);
        Assert.AreEqual(TopicNameMappingOutcome.InvalidName, toPublish("/topic/").Outcome, protocol);
        Assert.AreEqual(TopicNameMappingOutcome.InvalidName, toSubscribe("/topic/a+").Outcome, protocol);
        Assert.AreEqual(TopicNameMappingOutcome.InvalidName, toSubscribe("/queue/").Outcome, protocol);
        Assert.IsFalse(toSubscribe("/queue/").IsMapped, protocol);
        Assert.AreEqual(TopicNameMappingOutcome.InvalidName, toPublish("/queue/a\u0000").Outcome, protocol);
    }

    [TestMethod]
    [DynamicData(nameof(Protocols))]
    public void ToDestination_Null_Throws(
        string protocol, Func<string, BusDestinationMapping> toPublish, Func<string, BusDestinationMapping> toSubscribe, Func<BusDestination, string> toAddress)
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => toPublish(null!), protocol);
        Assert.ThrowsExactly<ArgumentNullException>(() => toSubscribe(null!), protocol);
        Assert.ThrowsExactly<ArgumentNullException>(() => toAddress(null!), protocol);
    }
}
