using System.Text;
using Surl.MessageBus;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0ChannelTestBytes;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0TestBytes;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// <c>--topic-map</c> aliases on <c>amq.topic</c>'s routing and binding keys and on
/// <c>reply-to</c>, both ways (ADR-0102 decision 3.3 and its BL-1703 and BL-1704 amendments).
/// </summary>
public sealed partial class Amqp0BrokerConnectionTests
{
    [TestMethod]
    [DataRow("amqp", DisplayName = "a map naming amqp")]
    [DataRow("amqps", DisplayName = "a map naming amqps, which amqp shares")]
    public async Task Publish_MappedRoutingKeyToAmqTopic_LandsOnTheMapsTargetTopic(string mapScheme)
    {
        var bus = BusMapping(new TopicMap(mapScheme, "orders", "mqtt", "shop/orders"));
        var received = new List<BusMessage>();
        using var mqtt = bus.Connect();
        mqtt.Subscribe("#", received.Add);

        var written = await ServeAsync(NewBroker(bus), PublishText(1, "amq.topic", "orders", "19"));

        AssertAnswered(written);
        Assert.AreEqual("shop/orders", received.Single().Topic);
    }

    [TestMethod]
    public async Task Publish_MappedRoutingKeyToTheDefaultExchange_QueuesTheMessageUnderTheMapsTargetTopic()
    {
        var bus = OrdersMappedBus();
        var received = new List<BusQueueDelivery>();
        using var other = bus.Connect();
        other.DeclareQueue("orders", new BusQueueFlags());
        other.Consume("orders", received.Add).Consumer!.Start();

        await ServeAsync(NewBroker(bus), PublishText(1, string.Empty, "orders", "19"));

        Assert.AreEqual("shop/orders", received.Single().Message.Topic);
    }

    [TestMethod]
    public async Task Get_QueueBoundOnAmqTopicWithAMappedKey_ReceivesTheTargetTopicUnderTheMappedKey()
    {
        var clock = new ManualTimeProvider();
        var bus = OrdersMappedBus(clock);
        var broker = NewBroker(bus);
        await ServeAsync(broker, QueueDeclare(1, "q"), QueueBind(1, "q", "amq.topic", "orders"));

        bus.Publish(new BusMessage("shop/orders", Encoding.ASCII.GetBytes("21")));
        var written = await ServeAsync(broker, Get(1, "q"));

        AssertAnswered(
            written,
            GetOk(1, 1, false, "amq.topic", "orders", 0),
            ContentHeader(1, 2, 0x0040, LongLong(clock.GetUtcNow().ToUnixTimeSeconds())),
            Body(1, "21"));
    }

    [TestMethod]
    public async Task Get_QueueBoundToHash_DoesNotReceiveTheHiddenNameAsARoutingKey()
    {
        var clock = new ManualTimeProvider();
        var bus = OrdersMappedBus(clock);
        var broker = NewBroker(bus);
        await ServeAsync(broker, QueueDeclare(1, "everything"), QueueBind(1, "everything", "amq.topic", "#"));

        bus.Publish(new BusMessage("orders", Encoding.ASCII.GetBytes("21")));
        var written = await ServeAsync(broker, Get(1, "everything"));

        // The topic has no AMQP name, so it is delivered as any such topic is (ADR-0112 decision 6).
        AssertAnswered(
            written,
            GetOk(1, 1, false, string.Empty, "everything", 0),
            ContentHeader(1, 2, 0x0040, LongLong(clock.GetUtcNow().ToUnixTimeSeconds())),
            Body(1, "21"));
    }

    [TestMethod]
    public async Task QueueBind_WildcardKeyBesideAMapsName_IsMappedByTheDefaultRulesAlone()
    {
        var clock = new ManualTimeProvider();
        var bus = OrdersMappedBus(clock);
        var broker = NewBroker(bus);
        await ServeAsync(broker, QueueDeclare(1, "q"), QueueBind(1, "q", "amq.topic", "orders.*"));

        bus.Publish(new BusMessage("shop/orders/new", Encoding.ASCII.GetBytes("no")));
        bus.Publish(new BusMessage("orders/new", Encoding.ASCII.GetBytes("21")));
        var written = await ServeAsync(broker, Get(1, "q"));

        AssertAnswered(
            written,
            GetOk(1, 1, false, "amq.topic", "orders.new", 0),
            ContentHeader(1, 2, 0x0040, LongLong(clock.GetUtcNow().ToUnixTimeSeconds())),
            Body(1, "21"));
    }

    [TestMethod]
    public async Task QueueBind_WildcardKeyThatIsAlsoAMapsName_IsMappedByTheDefaultRulesAlone()
    {
        var clock = new ManualTimeProvider();
        var bus = BusMapping(new TopicMap("amqp", "orders.*", "mqtt", "shop/orders"), clock);
        var broker = NewBroker(bus);
        await ServeAsync(broker, QueueDeclare(1, "q"), QueueBind(1, "q", "amq.topic", "orders.*"));

        bus.Publish(new BusMessage("shop/orders", Encoding.ASCII.GetBytes("no")));
        bus.Publish(new BusMessage("orders/new", Encoding.ASCII.GetBytes("21")));
        var written = await ServeAsync(broker, Get(1, "q"));

        AssertAnswered(
            written,
            GetOk(1, 1, false, "amq.topic", "orders.new", 0),
            ContentHeader(1, 2, 0x0040, LongLong(clock.GetUtcNow().ToUnixTimeSeconds())),
            Body(1, "21"));
    }

    [TestMethod]
    public async Task Get_MapNamingAKeyPast255Bytes_DeliversItsTargetUnderTheQueuesNameWithNoReplyTo()
    {
        var clock = new ManualTimeProvider();
        var bus = BusMapping(new TopicMap("amqp", new string('k', 256), "mqtt", "shop/orders"), clock);
        var broker = NewBroker(bus);
        await ServeAsync(broker, QueueDeclare(1, "q"), QueueBind(1, "q", "amq.topic", "#"));

        bus.Publish(new BusMessage("shop/orders", Encoding.ASCII.GetBytes("21")) { ResponseTopic = "shop/orders" });
        var written = await ServeAsync(broker, Get(1, "q"));

        AssertAnswered(
            written,
            GetOk(1, 1, false, string.Empty, "q", 0),
            ContentHeader(1, 2, 0x0040, LongLong(clock.GetUtcNow().ToUnixTimeSeconds())),
            Body(1, "21"));
    }

    [TestMethod]
    public async Task QueueUnbind_MappedKey_RemovesTheBindingToTheMapsTargetTopic()
    {
        var bus = OrdersMappedBus();
        var broker = NewBroker(bus);
        await ServeAsync(broker, QueueDeclare(1, "q"), QueueBind(1, "q", "amq.topic", "orders"), QueueUnbind(1, "q", "amq.topic", "orders"));

        bus.Publish(new BusMessage("shop/orders", Encoding.ASCII.GetBytes("21")));
        var written = await ServeAsync(broker, Get(1, "q"));

        AssertAnswered(written, GetEmpty(1));
    }

    [TestMethod]
    public async Task Publish_ReplyToNamingAMappedKey_IsTheMapsTargetResponseTopic()
    {
        var bus = OrdersMappedBus();
        var received = new List<BusMessage>();
        using var mqtt = bus.Connect();
        mqtt.Subscribe("#", received.Add);

        await ServeAsync(NewBroker(bus), Publish(1, "amq.topic", "t"), ContentHeader(1, 0, 0x0200, ShortString("orders")));

        Assert.AreEqual("shop/orders", received.Single().ResponseTopic);
    }

    [TestMethod]
    public async Task Get_ResponseTopicAMapTargets_IsTheMappedKeyAsReplyTo()
    {
        var clock = new ManualTimeProvider();
        var bus = OrdersMappedBus(clock);
        var broker = NewBroker(bus);
        await ServeAsync(broker, QueueDeclare(1, "q"), QueueBind(1, "q", "amq.topic", "t"));

        bus.Publish(new BusMessage("t", Encoding.ASCII.GetBytes("21")) { ResponseTopic = "shop/orders" });
        var written = await ServeAsync(broker, Get(1, "q"));

        AssertAnswered(
            written,
            GetOk(1, 1, false, "amq.topic", "t", 0),
            ContentHeader(1, 2, 0x0240, Concat(ShortString("orders"), LongLong(clock.GetUtcNow().ToUnixTimeSeconds()))),
            Body(1, "21"));
    }

    private static MessageBus.MessageBus BusMapping(TopicMap map, TimeProvider? clock = null) =>
        new(clock ?? new ManualTimeProvider(), configuration: new MessageBusConfiguration { TopicMaps = [map] });

    // ADR-0102 decision 3.3's own example.
    private static MessageBus.MessageBus OrdersMappedBus(TimeProvider? clock = null) =>
        BusMapping(new TopicMap("amqp", "orders", "mqtt", "shop/orders"), clock);
}
