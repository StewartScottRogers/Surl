using System.Text;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// The MQTT 3.1.1 server maps its topic names through the bus's <see cref="TopicNameLookup"/>, so a
/// <c>--topic-map</c> naming <c>mqtt</c> or <c>mqtts</c> on its left applies both ways (ADR-0102,
/// decision 3.3). Every test but the default-rules one runs under
/// <c>--topic-map mqtt:orders/new=nats:shop.orders</c>.
/// </summary>
[TestClass]
public sealed class MqttTopicMapTests
{
    private static readonly byte[] ConnackAccepted = [0x20, 0x02, 0x00, 0x00];
    private static readonly byte[] SubackGranted = [0x90, 0x03, 0x00, 0x01, 0x00];
    private static readonly byte[] Disconnect = [0xE0, 0x00];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("mqtt")]
    [DataRow("mqtts")]
    public async Task ServeAsync_PublishOnAnAliasedName_LandsOnTheCanonicalTopic(string scheme)
    {
        var bus = MappedBus();

        await ServeAsync(bus, scheme, ClientPackets.Publish(0x31, "orders/new", "hi"), ClientPackets.Disconnect);

        var retained = bus.MatchingAny(["#"]).Single();
        Assert.AreEqual("shop/orders", retained.Key);
        Assert.AreEqual("hi", Encoding.ASCII.GetString(retained.Value));
    }

    [TestMethod]
    public async Task ServeAsync_PublishOnAnAliasedName_ReachesALiveBusSubscriberOnTheCanonicalTopic()
    {
        var bus = MappedBus();
        var delivered = new List<string>();
        using var subscriber = bus.Connect(null);
        subscriber.Subscribe("shop/orders", message => delivered.Add(message.Topic));

        await ServeAsync(bus, "mqtt", ClientPackets.Publish(0x30, "orders/new", "hi"), ClientPackets.Disconnect);

        CollectionAssert.AreEqual(new[] { "shop/orders" }, delivered);
    }

    [TestMethod]
    [DataRow("mqtt")]
    [DataRow("mqtts")]
    public async Task ServeAsync_SubscribeToAnAliasedName_GetsTheCanonicalTopicsRetainedMessageUnderThatName(string scheme)
    {
        var bus = MappedBus();
        bus.Retain("shop/orders", "hi"u8);

        var written = await SubscribeAsync(bus, scheme, "orders/new");

        CollectionAssert.AreEqual(Fetched("orders/new", "hi"), written);
    }

    [TestMethod]
    public async Task ServeAsync_SubscribeToAnAliasedName_GetsAMessagePublishedOnTheRunningBusUnderThatName()
    {
        var bus = MappedBus();
        await ServeAsync(bus, "mqtt", ClientPackets.Publish(0x31, "shop/orders", "from a client"), ClientPackets.Disconnect);

        var written = await SubscribeAsync(bus, "mqtt", "orders/new");

        CollectionAssert.AreEqual(Fetched("orders/new", "from a client"), written);
    }

    [TestMethod]
    public async Task ServeAsync_SubscribeToTheMapsTarget_IsSentTheAliasedName()
    {
        var bus = MappedBus();
        bus.Retain("shop/orders", "hi"u8);

        var written = await SubscribeAsync(bus, "mqtt", "shop/orders");

        CollectionAssert.AreEqual(Fetched("orders/new", "hi"), written);
    }

    [TestMethod]
    public async Task ServeAsync_SubscribeToEverything_GetsNothingForTheTopicTheMapHides()
    {
        var bus = MappedBus();
        bus.Retain("orders/new", "hidden"u8);
        var log = new RecordingExchangeLog();

        var written = await SubscribeAsync(bus, "mqtt", "#", log);

        byte[] expected = [.. ConnackAccepted, .. SubackGranted, .. Disconnect];
        CollectionAssert.AreEqual(expected, written);
        CollectionAssert.Contains(log.Notes.ToArray(), "SUBSCRIBE: 0 retained messages delivered; sent DISCONNECT and closed.");
    }

    [TestMethod]
    public async Task ServeAsync_AWildcardFilter_IsMappedByTheDefaultRulesAlone()
    {
        var bus = MappedBus();
        bus.Retain("shop/orders", "under shop"u8);
        bus.Retain("orders/old", "under orders"u8);

        var written = await SubscribeAsync(bus, "mqtt", "orders/+");

        CollectionAssert.AreEqual(Fetched("orders/old", "under orders"), written);
    }

    [TestMethod]
    public async Task ServeAsync_AnAliasedPublishAndSubscribeUnderFaultRules_GiveThePublishsCanonicalTopicAndTheFilterAsSent()
    {
        var bus = MappedBus();
        var plan = new ScriptedFaultPlan();

        await ServeAsync(bus, "mqtt", plan, ClientPackets.Publish(0x31, "orders/new", "hi"), ClientPackets.Subscribe(1, ("orders/new", 0)));

        CollectionAssert.AreEqual(new[] { null, "shop/orders", "orders/new" }, plan.Points.Select(point => point.Path).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_AnAnonymousCallerGrantedTheCanonicalTopic_PublishesAndSubscribesUnderTheAliasedName()
    {
        var bus = MappedBus(new TopicAccessGrant("shop/orders", TopicAccessGrant.Everyone, TopicAccessRights.PublishAndSubscribe));

        await ServeAsync(bus, "mqtt", ClientPackets.Publish(0x31, "orders/new", "hi"), ClientPackets.Disconnect);
        var written = await SubscribeAsync(bus, "mqtt", "orders/new");

        CollectionAssert.AreEqual(Fetched("orders/new", "hi"), written);
    }

    [TestMethod]
    public async Task ServeAsync_APublishOnTheAliasedNameWithoutPub_IsNotedUnderTheCanonicalTopic()
    {
        var bus = MappedBus(new TopicAccessGrant("shop/orders", TopicAccessGrant.Everyone, TopicAccessRights.Subscribe));
        var log = new RecordingExchangeLog();

        await ServeAsync(bus, "mqtt", plan: null, log, ClientPackets.Publish(0x31, "orders/new", "hi"), ClientPackets.Disconnect);

        CollectionAssert.Contains(log.Notes.ToArray(), "PUBLISH to 'shop/orders' refused by --topic-access for an anonymous caller; dropped.");
    }

    private static MessageBus.MessageBus MappedBus(params TopicAccessGrant[] grants) =>
        new(TimeProvider.System, configuration: new MessageBusConfiguration
        {
            TopicMaps = [new TopicMap("mqtt", "orders/new", "nats", "shop.orders")],
            AccessGrants = grants,
        });

    // CONNACK, SUBACK granting the one filter, the retained PUBLISH under the name, DISCONNECT.
    private static byte[] Fetched(string topicName, string payload) =>
        [.. ConnackAccepted, .. SubackGranted, .. ClientPackets.Publish(0x31, topicName, payload), .. Disconnect];

    private Task<byte[]> SubscribeAsync(MessageBus.MessageBus bus, string scheme, string filter, RecordingExchangeLog? log = null) =>
        ServeAsync(bus, scheme, plan: null, log, ClientPackets.Subscribe(1, (filter, 0)));

    private Task<byte[]> ServeAsync(MessageBus.MessageBus bus, string scheme, params byte[][] packets) =>
        ServeAsync(bus, scheme, plan: null, log: null, packets);

    private Task<byte[]> ServeAsync(MessageBus.MessageBus bus, string scheme, IFaultPlan plan, params byte[][] packets) =>
        ServeAsync(bus, scheme, plan, log: null, packets);

    private async Task<byte[]> ServeAsync(MessageBus.MessageBus bus, string scheme, IFaultPlan? plan, RecordingExchangeLog? log, params byte[][] packets)
    {
        var server = new MqttProtocolServer(bus, new AnonymousAuthenticationPolicy());
        var connection = new InMemoryConnection(RecordedFixture.Whole(ClientPackets.Join([ClientPackets.CurlConnect(), .. packets])));
        var context = MqttTestExchange.Context(TimeProvider.System, TestContext.CancellationToken, log: log, scheme: scheme);

        await server.ServeAsync(connection, plan is null ? context : context with { Faults = plan });
        await connection.DisposeAsync();

        return connection.WrittenBytes;
    }
}
