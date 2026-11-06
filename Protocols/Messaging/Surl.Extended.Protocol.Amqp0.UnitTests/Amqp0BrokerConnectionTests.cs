using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0ChannelTestBytes;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0TestBytes;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// Channels, exchanges, queues and the basic class (BL-506; ADR-0112 decisions 5, 6 and 7), every
/// client byte and every expected answer written by hand from the AMQP 0-9-1 XML's classes 20, 40,
/// 50 and 60 and section 4.2.6. Each exchange logs in as <c>alice</c>, opens channel 1, and ends
/// with the client's <c>connection.close</c>.
/// </summary>
[TestClass]
public sealed partial class Amqp0BrokerConnectionTests
{
    private static readonly byte[] PlainStartOk = StartOk("PLAIN", PlainResponse("alice", "secret"));
    private static readonly byte[] DefaultTuneOk = TuneOk(0, 0, 0);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Get_PublishedInThreeBodyFrames_IsGetOkWithTheBodyInFrameMaxPieces()
    {
        var body = Encoding.ASCII.GetBytes(new string('a', 3000) + new string('b', 3000) + new string('c', 3000));
        var properties = ShortString("text/plain");

        var written = await ServeTunedAsync(
            NewBroker(NewBus()),
            TuneOk(0, 4096, 0),
            QueueDeclare(1, "orders"),
            Publish(1, string.Empty, "orders"),
            ContentHeader(1, body.Length, 0x8000, properties),
            Body(1, body[..3000]),
            Body(1, body[3000..6000]),
            Body(1, body[6000..]),
            Get(1, "orders"));

        AssertAnswered(
            written,
            QueueDeclareOk(1, "orders"),
            GetOk(1, 1, false, string.Empty, "orders", 0),
            ContentHeader(1, body.Length, 0x8000, properties),
            Body(1, body[..4088]),
            Body(1, body[4088..8176]),
            Body(1, body[8176..]));
    }

    [TestMethod]
    public async Task Get_EmptyQueue_IsGetEmpty()
    {
        var written = await ServeAsync(NewBroker(NewBus()), QueueDeclare(1, "orders"), Get(1, "orders"));

        AssertAnswered(written, QueueDeclareOk(1, "orders"), GetEmpty(1));
    }

    [TestMethod]
    public async Task Publish_MandatoryUnroutable_IsReturnedWith312()
    {
        var written = await ServeAsync(NewBroker(NewBus()), PublishText(1, string.Empty, "nowhere", "hello", bits: 1));

        AssertAnswered(written, Return(1, 312, "NO_ROUTE", string.Empty, "nowhere"), TextContent(1, "hello"));
    }

    [TestMethod]
    public async Task QueueDeclare_PassiveMissing_ClosesTheChannelWith404()
    {
        var written = await ServeAsync(NewBroker(NewBus()), QueueDeclare(1, "missing", bits: 1), ChannelCloseOk(1));

        AssertAnswered(written, BrokerChannelClose(1, 404, "NOT_FOUND - no queue", 50, 10));
    }

    [TestMethod]
    public async Task QueueDeclare_OtherFlags_ClosesTheChannelWith406()
    {
        var written = await ServeAsync(NewBroker(NewBus()), QueueDeclare(1, "orders", bits: 2), QueueDeclare(1, "orders"), ChannelCloseOk(1));

        AssertAnswered(written, QueueDeclareOk(1, "orders"), BrokerChannelClose(1, 406, "PRECONDITION_FAILED - inequivalent declaration", 50, 10));
    }

    [TestMethod]
    public async Task QueueDeclare_ExclusiveQueueOfAnotherConnection_ClosesTheChannelWith405()
    {
        var bus = NewBus();
        using var other = bus.Connect();
        other.DeclareQueue("locked", new BusQueueFlags(Exclusive: true));

        var written = await ServeAsync(NewBroker(bus), QueueDeclare(1, "locked", bits: 4), ChannelCloseOk(1));

        AssertAnswered(written, BrokerChannelClose(1, 405, "RESOURCE_LOCKED - exclusive queue of another connection", 50, 10));
    }

    [TestMethod]
    public async Task Ack_UnknownDeliveryTag_ClosesTheChannelWith406()
    {
        var written = await ServeAsync(NewBroker(NewBus()), Ack(1, 7), ChannelCloseOk(1));

        AssertAnswered(written, BrokerChannelClose(1, 406, "PRECONDITION_FAILED - unknown delivery tag", 60, 80));
    }

    [TestMethod]
    public async Task QueueDeclare_EmptyName_IsAServerNamedQueueTheChannelThenNamesByEmpty()
    {
        var written = await ServeAsync(NewBroker(NewBus()), QueueDeclare(1, string.Empty), PublishText(1, string.Empty, string.Empty, "x"), QueuePurge(1, string.Empty));

        var answers = written[Prefix().Length..];
        var name = Encoding.ASCII.GetString(answers, 12, answers[11]);
        Assert.MatchesRegex(ServerNamedQueue(), name);
        AssertAnswered(written, QueueDeclareOk(1, name), QueuePurgeOk(1, 0));
    }

    [TestMethod]
    public async Task Consume_ThreeMessages_AreDeliveredInPublishOrderAfterConsumeOk()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            QueueDeclare(1, "orders"),
            PublishText(1, string.Empty, "orders", "one"),
            PublishText(1, string.Empty, "orders", "two"),
            PublishText(1, string.Empty, "orders", "three"),
            Consume(1, "orders", "c1"),
            Ack(1, 3, multiple: true),
            QueueDeclare(1, "orders", bits: 1));

        AssertAnswered(
            written,
            QueueDeclareOk(1, "orders"),
            ConsumeOk(1, "c1"),
            Deliver(1, "c1", 1, false, string.Empty, "orders"),
            TextContent(1, "one"),
            Deliver(1, "c1", 2, false, string.Empty, "orders"),
            TextContent(1, "two"),
            Deliver(1, "c1", 3, false, string.Empty, "orders"),
            TextContent(1, "three"),
            QueueDeclareOk(1, "orders", 0, 1));
    }

    [TestMethod]
    public async Task Get_MessagePublishedOnTheBusUnderAnMqttName_ReachesTheQueueBoundOnAmqTopicWithTheMappedKey()
    {
        var clock = new ManualTimeProvider();
        var bus = new MessageBus.MessageBus(clock);
        var broker = NewBroker(bus);
        await ServeAsync(broker, QueueDeclare(1, "readings"), QueueBind(1, "readings", "amq.topic", "sensors.*"));

        bus.Publish(new BusMessage("sensors/kitchen", Encoding.ASCII.GetBytes("21")));
        var written = await ServeAsync(broker, Get(1, "readings"));

        AssertAnswered(
            written,
            GetOk(1, 1, false, "amq.topic", "sensors.kitchen", 0),
            ContentHeader(1, 2, 0x0040, LongLong(clock.GetUtcNow().ToUnixTimeSeconds())),
            Body(1, "21"));
    }

    [TestMethod]
    public async Task Publish_ToAmqTopic_ReachesASubscriberOnTheBusUnderTheMappedTopic()
    {
        var bus = NewBus();
        var received = new List<BusMessage>();
        using var mqtt = bus.Connect();
        mqtt.Subscribe("sensors/+", received.Add);

        var written = await ServeAsync(NewBroker(bus), PublishText(1, "amq.topic", "sensors.hall", "19"));

        AssertAnswered(written);
        Assert.AreEqual("sensors/hall", received.Single().Topic);
        Assert.AreEqual("19", Encoding.ASCII.GetString(received.Single().Payload.Span));
    }

    [TestMethod]
    public async Task Publish_RefusedByTopicAccess_ClosesTheChannelWith403()
    {
        var bus = BusGranting(new TopicAccessGrant("$queue/#", "alice", TopicAccessRights.Subscribe));

        var written = await ServeAsync(NewBroker(bus), QueueDeclare(1, "orders"), PublishText(1, string.Empty, "orders", "x"), ChannelCloseOk(1));

        AssertAnswered(written, QueueDeclareOk(1, "orders"), BrokerChannelClose(1, 403, "ACCESS_REFUSED - access refused", 60, 40));
    }

    [TestMethod]
    public async Task Consume_RefusedByTopicAccess_ClosesTheChannelWith403()
    {
        var bus = BusGranting(new TopicAccessGrant("$queue/#", "alice", TopicAccessRights.Publish));

        var written = await ServeAsync(NewBroker(bus), QueueDeclare(1, "orders"), Consume(1, "orders", "c1"), ChannelCloseOk(1));

        AssertAnswered(written, QueueDeclareOk(1, "orders"), BrokerChannelClose(1, 403, "ACCESS_REFUSED - access refused", 60, 20));
    }

    [GeneratedRegex("^amq\\.gen-[A-Za-z0-9_-]{22}$")]
    private static partial Regex ServerNamedQueue();

    private static MessageBus.MessageBus NewBus() => new(new ManualTimeProvider());

    private static MessageBus.MessageBus BusGranting(params TopicAccessGrant[] grants) =>
        new(new ManualTimeProvider(), configuration: new MessageBusConfiguration { AccessGrants = grants });

    private static Amqp0Broker NewBroker(MessageBus.MessageBus bus) => new(new UnitTestAmqp0AuthenticationPolicy("PLAIN"), bus);

    private static Amqp0Broker NewBroker(MessageBus.MessageBus bus, Amqp0ExchangeTable exchanges) =>
        new(new UnitTestAmqp0AuthenticationPolicy("PLAIN"), bus, null, exchanges);

    private static byte[] Prefix() => Concat(Start("PLAIN AMQPLAIN"), Tune(), OpenOk(), ChannelOpenOk(1));

    private static void AssertAnswered(byte[] written, params byte[][] answers) =>
        CollectionAssert.AreEqual(Concat([Prefix(), .. answers, CloseOk()]), written, Convert.ToHexString(written));

    private Task<byte[]> ServeAsync(Amqp0Broker broker, params byte[][] chunks) => ServeTunedAsync(broker, DefaultTuneOk, chunks);

    private async Task<byte[]> ServeTunedAsync(Amqp0Broker broker, byte[] tuneOk, params byte[][] chunks)
    {
        var (written, _) = await ServeLoggedAsync(broker, tuneOk, chunks);
        return written;
    }

    private async Task<(byte[] Written, RecordingExchangeLog Log)> ServeLoggedAsync(Amqp0Broker broker, byte[] tuneOk, params byte[][] chunks)
    {
        var log = new RecordingExchangeLog();
        byte[][] all = [ProtocolHeader, PlainStartOk, tuneOk, Open("/"), ChannelOpen(1), .. chunks, Close()];
        var connection = new InMemoryConnection(all.Select(chunk => (ReadOnlyMemory<byte>)chunk));

        await broker.ServeAsync(connection, Context(log));

        return (connection.WrittenBytes, log);
    }

    private ExchangeContext Context(IExchangeLog log) => new(
        1,
        new ListenUrl("amqp", "127.0.0.1", 5672).WithBoundPort(5672),
        new IPEndPoint(IPAddress.Loopback, 5672),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        new ManualTimeProvider(),
        TestContext.CancellationToken);
}
