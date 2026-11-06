using System.Text;

namespace Surl.MessageBus;

[TestClass]
public sealed class TopicNameLookupTests
{
    private static readonly TopicNameLookup Orders = new([new TopicMap("amqp", "orders", "mqtt", "shop/orders")]);

    [TestMethod]
    public void TopicMap_AmqpOrdersToMqttShopOrders_DeliversAnMqttPublishToAnAmqpSubscriberOfOrders()
    {
        var bus = new MessageBus(new SettableTimeProvider(), configuration: new() { TopicMaps = [new TopicMap("amqp", "orders", "mqtt", "shop/orders")] });
        var received = new List<BusMessage>();
        var binding = bus.Names.ToCanonicalFilter("amqp", "orders");
        bus.Connect().Subscribe(binding.Name!, received.Add);

        bus.Publish(new BusMessage(bus.Names.ToCanonicalTopic("mqtt", "shop/orders").Name!, Encoding.UTF8.GetBytes("x")));

        Assert.AreEqual("shop/orders", binding.Name);
        Assert.AreEqual(TopicNameMapping.Of("orders"), bus.Names.FromCanonicalTopic("amqp", received.Single().Topic));
    }

    [TestMethod]
    public void ToCanonicalTopic_AnAliasedName_IsTheTargetOnThatSchemeAndItsTlsTwinOnly()
    {
        Assert.AreEqual(TopicNameMapping.Of("shop/orders"), Orders.ToCanonicalTopic("amqp", "orders"));
        Assert.AreEqual(TopicNameMapping.Of("shop/orders"), Orders.ToCanonicalTopic("AMQPS", "orders"));
        Assert.AreEqual(TopicNameMapping.Of("orders"), Orders.ToCanonicalTopic("mqtt", "orders"));
        Assert.AreEqual(TopicNameMapping.Of("a/b"), Orders.ToCanonicalTopic("amqp", "a.b"));
        Assert.AreEqual(TopicNameMappingOutcome.InvalidName, Orders.ToCanonicalTopic("amqp", null).Outcome);
    }

    [TestMethod]
    public void ToCanonicalFilter_AnAliasedNameOrAWildcardFilter_IsTheTargetOrTheDefault()
    {
        Assert.AreEqual(TopicNameMapping.Of("shop/orders"), Orders.ToCanonicalFilter("amqp", "orders"));
        Assert.AreEqual(TopicNameMapping.Of("orders/#"), Orders.ToCanonicalFilter("amqp", "orders.#"));
        Assert.AreEqual(TopicNameMappingOutcome.InvalidName, Orders.ToCanonicalFilter("amqp", null).Outcome);
    }

    [TestMethod]
    public void FromCanonicalTopic_TheTargetOrAnAliasedDefaultName_IsTheAliasOrUnreachable()
    {
        Assert.AreEqual(TopicNameMapping.Of("orders"), Orders.FromCanonicalTopic("amqps", "shop/orders"));
        Assert.AreEqual(TopicNameMappingOutcome.Unreachable, Orders.FromCanonicalTopic("amqp", "orders").Outcome);
        Assert.AreEqual(TopicNameMapping.Of("a.b"), Orders.FromCanonicalTopic("amqp", "a/b"));
        Assert.AreEqual(TopicNameMapping.Of("shop/orders"), Orders.FromCanonicalTopic("mqtt", "shop/orders"));
        Assert.AreEqual(TopicNameMappingOutcome.InvalidName, Orders.FromCanonicalTopic("amqp", null).Outcome);
    }

    [TestMethod]
    public void DefaultMapping_EverySchemeFamily_MapsByItsOwnRules()
    {
        var lookup = new TopicNameLookup([]);

        Assert.AreEqual(TopicNameMapping.Of("a/b"), lookup.ToCanonicalTopic("mqtts", "a/b"));
        Assert.AreEqual(TopicNameMapping.Of("a/b"), lookup.ToCanonicalTopic("mqtt5+ws", "a/b"));
        Assert.AreEqual(TopicNameMapping.Of("a/b"), lookup.ToCanonicalTopic("kafkas", "a.b"));
        Assert.AreEqual(TopicNameMapping.Of("a/b"), lookup.ToCanonicalTopic("natss", "a.b"));
        Assert.AreEqual(TopicNameMapping.Of("a/b"), lookup.ToCanonicalTopic("amqp1", "/topic/a/b"));
        Assert.AreEqual(TopicNameMapping.Of("a/b"), lookup.ToCanonicalTopic("stomps", "/topic/a/b"));

        Assert.AreEqual(TopicNameMapping.Of("a/+"), lookup.ToCanonicalFilter("mqtt", "a/+"));
        Assert.AreEqual(new TopicNameMapping(TopicNameMappingOutcome.Mapped, "a/+", "g"), lookup.ToCanonicalFilter("mqtt5s", "$share/g/a/+"));
        Assert.AreEqual(TopicNameMapping.Of("$share/g/a/+"), lookup.ToCanonicalFilter("mqtt", "$share/g/a/+"));
        Assert.AreEqual(TopicNameMapping.Of("a/b"), lookup.ToCanonicalFilter("kafka", "a.b"));
        Assert.AreEqual(TopicNameMapping.Of("a/+"), lookup.ToCanonicalFilter("nats", "a.*"));
        Assert.AreEqual(TopicNameMapping.Of("a/#"), lookup.ToCanonicalFilter("amqp1s", "/topic/a/#"));
        Assert.AreEqual(TopicNameMapping.Of("a/#"), lookup.ToCanonicalFilter("stomp", "/topic/a/#"));

        Assert.AreEqual(TopicNameMapping.Of("a/b"), lookup.FromCanonicalTopic("mqtt5", "a/b"));
        Assert.AreEqual(TopicNameMapping.Of("a.b"), lookup.FromCanonicalTopic("kafka", "a/b"));
        Assert.AreEqual(TopicNameMapping.Of("a.b"), lookup.FromCanonicalTopic("nats", "a/b"));
        Assert.AreEqual(TopicNameMapping.Of("/topic/a/b"), lookup.FromCanonicalTopic("amqp1", "a/b"));
        Assert.AreEqual(TopicNameMapping.Of("/topic/a/b"), lookup.FromCanonicalTopic("stomp", "a/b"));
        Assert.AreEqual(TopicNameMappingOutcome.InvalidName, lookup.FromCanonicalTopic("stomp", "a/#").Outcome);
    }

    [TestMethod]
    public void SlashPrefixedSchemes_ANullQueueOrUnprefixedAddress_IsRefused()
    {
        var lookup = new TopicNameLookup([]);

        Assert.AreEqual(TopicNameMappingOutcome.InvalidName, lookup.ToCanonicalTopic("stomp", null).Outcome);
        Assert.AreEqual(TopicNameMappingOutcome.UnknownDestination, lookup.ToCanonicalTopic("stomp", "/queue/q").Outcome);
        Assert.AreEqual(TopicNameMappingOutcome.UnknownDestination, lookup.ToCanonicalFilter("amqp1", "q").Outcome);
        Assert.AreEqual(TopicNameMappingOutcome.InvalidName, lookup.ToCanonicalTopic("amqp1", "/topic/a/#").Outcome);
    }

    [TestMethod]
    public void TopicMap_AStompAliasToAKafkaTopic_ResolvesBothWays()
    {
        var lookup = new TopicNameLookup([new TopicMap("stomp", "/topic/orders", "kafka", "shop.orders")]);

        Assert.AreEqual(TopicNameMapping.Of("shop/orders"), lookup.ToCanonicalTopic("stomp", "/topic/orders"));
        Assert.AreEqual(TopicNameMapping.Of("/topic/orders"), lookup.FromCanonicalTopic("stomps", "shop/orders"));
    }

    [TestMethod]
    public void Constructor_AnUnknownSchemeAnInvalidNameOrADuplicate_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new TopicNameLookup(null!));
        Assert.ThrowsExactly<ArgumentException>(() => new TopicNameLookup([new TopicMap("redis", "a", "mqtt", "b")]));
        Assert.ThrowsExactly<ArgumentException>(() => new TopicNameLookup([new TopicMap("amqp", "a", "smtp", "b")]));
        var invalidTarget = Assert.ThrowsExactly<ArgumentException>(() => new TopicNameLookup([new TopicMap("amqp", "a", "mqtt", "b/#")]));
        Assert.StartsWith("b/# is not a valid name for mqtt", invalidTarget.Message);
        Assert.ThrowsExactly<ArgumentException>(() => new TopicNameLookup([new TopicMap("amqp", "a..b", "mqtt", "b")]));
        Assert.ThrowsExactly<ArgumentException>(() => new TopicNameLookup([new TopicMap("amqp", "a", "mqtt", "b"), new TopicMap("amqps", "a", "mqtt", "c")]));
        Assert.ThrowsExactly<ArgumentNullException>(() => Orders.ToCanonicalTopic(null!, "a"));
        Assert.ThrowsExactly<ArgumentException>(() => Orders.FromCanonicalTopic("http", "a"));
    }

    [TestMethod]
    public void TopicMap_TwoAliasesOfOneTarget_TheFirstIsTheNameSentBack()
    {
        var lookup = new TopicNameLookup([new TopicMap("amqp", "a", "mqtt", "t"), new TopicMap("amqp", "b", "mqtt", "t")]);

        Assert.AreEqual(TopicNameMapping.Of("t"), lookup.ToCanonicalTopic("amqp", "b"));
        Assert.AreEqual(TopicNameMapping.Of("a"), lookup.FromCanonicalTopic("amqp", "t"));
    }

    [TestMethod]
    public void IsKnownScheme_MessagingAndOtherSchemes_SaysWhichItMaps()
    {
        Assert.IsTrue(TopicNameLookup.IsKnownScheme("NATSS"));
        Assert.IsFalse(TopicNameLookup.IsKnownScheme("redis"));
    }
}
