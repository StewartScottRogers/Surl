using Surl.MessageBus;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0ChannelTestBytes;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0TestBytes;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>Exchanges and queues (XML classes <c>exchange</c> and <c>queue</c>; ADR-0112 decision 5).</summary>
public sealed partial class Amqp0BrokerConnectionTests
{
    [TestMethod]
    public async Task ExchangeDeclare_NewAndRedeclaredAlike_IsDeclareOkUnlessNoWait()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            ExchangeDeclare(1, "orders-x_1.y", "direct"),
            ExchangeDeclare(1, "orders-x_1.y", "direct"),
            ExchangeDeclare(1, "fan:1", "fanout", bits: 16),
            ExchangeDeclare(1, "amq.direct", "direct", bits: 2),
            ExchangeDeclare(1, "orders-x_1.y", "direct", bits: 1),
            ExchangeDeclare(1, string.Empty, "direct", bits: 1));

        AssertAnswered(written, ExchangeDeclareOk(1), ExchangeDeclareOk(1), ExchangeDeclareOk(1), ExchangeDeclareOk(1), ExchangeDeclareOk(1));
    }

    [TestMethod]
    [DataRow("missing", "direct", 1, 404, "NOT_FOUND - no exchange", DisplayName = "passive and missing")]
    [DataRow("", "direct", 0, 403, "ACCESS_REFUSED - default exchange", DisplayName = "the default exchange")]
    [DataRow("bad name", "direct", 0, 406, "PRECONDITION_FAILED - invalid name", DisplayName = "a name outside the syntax")]
    [DataRow("amq.custom", "direct", 0, 403, "ACCESS_REFUSED - reserved name", DisplayName = "an absent amq. name")]
    [DataRow("amq.direct", "direct", 0, 406, "PRECONDITION_FAILED - inequivalent declaration", DisplayName = "another durable")]
    public async Task ExchangeDeclare_Refused_ClosesTheChannel(string name, string type, int bits, int replyCode, string replyText)
    {
        var written = await ServeAsync(NewBroker(NewBus()), ExchangeDeclare(1, name, type, (byte)bits), ChannelCloseOk(1));

        AssertAnswered(written, BrokerChannelClose(1, replyCode, replyText, 40, 10));
    }

    [TestMethod]
    public async Task ExchangeDeclare_OtherArguments_ClosesTheChannelWith406()
    {
        byte[] table = [0, 0, 0, 4, .. ShortString("k"), (byte)'t', 1];

        var written = await ServeAsync(NewBroker(NewBus()), ExchangeDeclare(1, "x", "fanout"), ExchangeDeclare(1, "x", "fanout", table: table), ChannelCloseOk(1));

        AssertAnswered(written, ExchangeDeclareOk(1), BrokerChannelClose(1, 406, "PRECONDITION_FAILED - inequivalent declaration", 40, 10));
    }

    [TestMethod]
    public async Task ExchangeDeclare_UnknownType_IsClosedWith503()
    {
        var written = await ServeAsync(NewBroker(NewBus()), ExchangeDeclare(1, "x", "x-custom"));

        AssertAnswered(written, BrokerClose(503, "COMMAND_INVALID - unknown exchange type", 40, 10));
    }

    [TestMethod]
    public async Task ExchangeDeclare_AnotherType_IsClosedWith530()
    {
        var written = await ServeAsync(NewBroker(NewBus()), ExchangeDeclare(1, "amq.fanout", "topic"));

        AssertAnswered(written, BrokerClose(530, "NOT_ALLOWED - exchange type differs", 40, 10));
    }

    [TestMethod]
    public async Task ExchangeDeclare_PastTheBound_IsClosedWith506()
    {
        var written = await ServeAsync(NewBroker(NewBus(), new Amqp0ExchangeTable(maxDeclaredExchanges: 1)), ExchangeDeclare(1, "a", "direct"), ExchangeDeclare(1, "b", "direct"));

        AssertAnswered(written, ExchangeDeclareOk(1), BrokerClose(506, "RESOURCE_ERROR - broker bound reached", 40, 10));
    }

    [TestMethod]
    public async Task ExchangeDelete_Declared_IsDeleteOkAndItIsGone()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            ExchangeDeclare(1, "x", "direct"),
            ExchangeDeclare(1, "y", "direct"),
            QueueDeclare(1, "orders"),
            QueueBind(1, "orders", "x", "k"),
            ExchangeDelete(1, "x"),
            ExchangeDelete(1, "y", bits: 3),
            ExchangeDeclare(1, "x", "direct", bits: 1),
            ChannelCloseOk(1));

        AssertAnswered(
            written,
            ExchangeDeclareOk(1),
            ExchangeDeclareOk(1),
            QueueDeclareOk(1, "orders"),
            QueueBindOk(1),
            ExchangeDeleteOk(1),
            BrokerChannelClose(1, 404, "NOT_FOUND - no exchange", 40, 10));
    }

    [TestMethod]
    [DataRow("", 0, 403, "ACCESS_REFUSED - default exchange", DisplayName = "the default exchange")]
    [DataRow("missing", 0, 404, "NOT_FOUND - no exchange", DisplayName = "missing")]
    [DataRow("amq.topic", 0, 403, "ACCESS_REFUSED - reserved name", DisplayName = "pre-declared")]
    [DataRow("x", 1, 406, "PRECONDITION_FAILED - exchange in use", DisplayName = "if-unused with a binding")]
    public async Task ExchangeDelete_Refused_ClosesTheChannel(string name, int bits, int replyCode, string replyText)
    {
        var written = await ServeAsync(
            NewBroker(NewBus()), ExchangeDeclare(1, "x", "direct"), QueueDeclare(1, "q"), QueueBind(1, "q", "x", "k"), ExchangeDelete(1, name, (byte)bits), ChannelCloseOk(1));

        AssertAnswered(written, ExchangeDeclareOk(1), QueueDeclareOk(1, "q"), QueueBindOk(1), BrokerChannelClose(1, replyCode, replyText, 40, 20));
    }

    [TestMethod]
    public async Task Publish_ThroughDirectFanoutAndTopicExchanges_ReachesTheBoundQueues()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            QueueDeclare(1, "a"),
            QueueDeclare(1, "b"),
            ExchangeDeclare(1, "logs.t", "topic"),
            QueueBind(1, "a", "amq.direct", "k"),
            QueueBind(1, "a", "amq.direct", "k"),
            QueueBind(1, "b", "amq.fanout", "ignored", noWait: true),
            QueueBind(1, "b", "logs.t", "app.*.error"),
            QueueUnbind(1, "a", "amq.direct", "gone"),
            PublishText(1, "amq.direct", "k", "direct"),
            PublishText(1, "amq.fanout", "any", "fanout"),
            PublishText(1, "logs.t", "app.web.error", "topic"),
            PublishText(1, "logs.t", "app.web.info", "dropped"),
            QueuePurge(1, "a"),
            QueuePurge(1, "b"));

        AssertAnswered(
            written,
            QueueDeclareOk(1, "a"),
            QueueDeclareOk(1, "b"),
            ExchangeDeclareOk(1),
            QueueBindOk(1),
            QueueBindOk(1),
            QueueBindOk(1),
            QueueUnbindOk(1),
            QueuePurgeOk(1, 1),
            QueuePurgeOk(1, 2));
    }

    [TestMethod]
    public async Task QueueUnbind_FromAnExchange_StopsTheRouting()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            QueueDeclare(1, "a"),
            QueueBind(1, "a", "amq.direct", "k"),
            QueueUnbind(1, "a", "amq.direct", "k"),
            QueueBind(1, "a", "amq.topic", "t.#"),
            QueueUnbind(1, "a", "amq.topic", "t.#"),
            PublishText(1, "amq.direct", "k", "x"),
            PublishText(1, "amq.topic", "t.u", "x"),
            QueuePurge(1, "a", noWait: true),
            QueueDeclare(1, "a", bits: 1));

        AssertAnswered(written, QueueDeclareOk(1, "a"), QueueBindOk(1), QueueUnbindOk(1), QueueBindOk(1), QueueUnbindOk(1), QueueDeclareOk(1, "a"));
    }

    [TestMethod]
    public async Task QueueBind_KeyWithNoCanonicalForm_BindsNothingAndAnswersBindOk()
    {
        var (written, log) = await ServeLoggedAsync(NewBroker(NewBus()), DefaultTuneOk, QueueDeclare(1, "a"), QueueBind(1, "a", "amq.topic", "a/b"));

        AssertAnswered(written, QueueDeclareOk(1, "a"), QueueBindOk(1));
        CollectionAssert.Contains(log.Notes.ToList(), "A binding key on amq.topic has no canonical form; nothing was bound.");
    }

    [TestMethod]
    [DataRow("q", "", 403, "ACCESS_REFUSED - default exchange", DisplayName = "the default exchange")]
    [DataRow("q", "missing", 404, "NOT_FOUND - no exchange", DisplayName = "a missing exchange")]
    [DataRow("missing", "amq.direct", 404, "NOT_FOUND - no queue", DisplayName = "a missing queue")]
    [DataRow("", "amq.direct", 404, "NOT_FOUND - no queue declared on the channel", DisplayName = "no queue declared")]
    public async Task QueueBind_Refused_ClosesTheChannel(string queue, string exchange, int replyCode, string replyText)
    {
        var written = await ServeAsync(NewBroker(NewBus()), QueueBind(1, queue, exchange, "k"), ChannelCloseOk(1));

        AssertAnswered(written, BrokerChannelClose(1, replyCode, replyText, 50, 20));
    }

    [TestMethod]
    public async Task QueueBind_OnAmqTopicRefusedByTopicAccess_ClosesTheChannelWith403()
    {
        var bus = BusGranting(new TopicAccessGrant("allowed/#", "alice", TopicAccessRights.Subscribe));

        var written = await ServeAsync(NewBroker(bus), QueueDeclare(1, "q"), QueueBind(1, "q", "amq.topic", "other.#"), ChannelCloseOk(1));

        AssertAnswered(written, QueueDeclareOk(1, "q"), BrokerChannelClose(1, 403, "ACCESS_REFUSED - access refused", 50, 20));
    }

    [TestMethod]
    public async Task QueueBind_PastTheBound_IsClosedWith506()
    {
        var written = await ServeAsync(
            NewBroker(NewBus(), new Amqp0ExchangeTable(maxBindings: 1)), QueueDeclare(1, "q"), QueueBind(1, "q", "amq.direct", "a"), QueueBind(1, "q", "amq.direct", "b"));

        AssertAnswered(written, QueueDeclareOk(1, "q"), QueueBindOk(1), BrokerClose(506, "RESOURCE_ERROR - broker bound reached", 50, 20));
    }

    [TestMethod]
    public async Task QueueDeclare_NoWaitPassiveAndExisting_AnswerAsAsked()
    {
        var bus = NewBus();
        using var stomp = bus.Connect();
        stomp.DeclareQueue("orders/new");

        var written = await ServeAsync(
            NewBroker(bus),
            QueueDeclare(1, "orders/new"),
            QueueDeclare(1, "quiet", bits: 16),
            QueueDeclare(1, string.Empty, bits: 1),
            QueueDeclare(1, "amq.gen-x", bits: 0),
            ChannelCloseOk(1));

        AssertAnswered(
            written,
            QueueDeclareOk(1, "orders/new"),
            QueueDeclareOk(1, "quiet"),
            BrokerChannelClose(1, 403, "ACCESS_REFUSED - reserved name", 50, 10));
    }

    [TestMethod]
    public async Task QueueDeclare_NameOutsideTheSyntax_ClosesTheChannelWith406()
    {
        var written = await ServeAsync(NewBroker(NewBus()), QueueDeclare(1, "bad@name"), ChannelCloseOk(1));

        AssertAnswered(written, BrokerChannelClose(1, 406, "PRECONDITION_FAILED - invalid name", 50, 10));
    }

    [TestMethod]
    public async Task QueueDeclare_PastTheBusQueueBound_IsClosedWith506()
    {
        var bus = new MessageBus.MessageBus(new ManualTimeProvider(), maxQueues: 1);

        var written = await ServeAsync(NewBroker(bus), QueueDeclare(1, "a"), QueueDeclare(1, "b"));

        AssertAnswered(written, QueueDeclareOk(1, "a"), BrokerClose(506, "RESOURCE_ERROR - broker bound reached", 50, 10));
    }

    [TestMethod]
    public async Task QueueDelete_CountsTheMessagesAndUnbindsTheQueue()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            QueueDeclare(1, "a"),
            QueueBind(1, "a", "amq.fanout", string.Empty),
            PublishText(1, "amq.fanout", string.Empty, "x"),
            QueueDelete(1, "a"),
            QueueDeclare(1, "b"),
            QueueDelete(1, "b", bits: 4),
            QueueDeclare(1, "a"),
            PublishText(1, "amq.fanout", string.Empty, "y"),
            QueuePurge(1, "a"));

        AssertAnswered(written, QueueDeclareOk(1, "a"), QueueBindOk(1), QueueDeleteOk(1, 1), QueueDeclareOk(1, "b"), QueueDeclareOk(1, "a"), QueuePurgeOk(1, 0));
    }

    [TestMethod]
    public async Task QueueDelete_IfEmptyWithAMessage_ClosesTheChannelWith406()
    {
        var written = await ServeAsync(NewBroker(NewBus()), QueueDeclare(1, "a"), PublishText(1, string.Empty, "a", "x"), QueueDelete(1, "a", bits: 2), ChannelCloseOk(1));

        AssertAnswered(written, QueueDeclareOk(1, "a"), BrokerChannelClose(1, 406, "PRECONDITION_FAILED - queue not empty", 50, 40));
    }

    [TestMethod]
    public async Task QueueDelete_OfADeclaredQueue_ClosesTheChannelWith403()
    {
        var bus = new MessageBus.MessageBus(new ManualTimeProvider(), configuration: new MessageBusConfiguration { Queues = [new DeclaredQueue("fixed")] });

        var written = await ServeAsync(NewBroker(bus), QueueDelete(1, "fixed"), ChannelCloseOk(1));

        AssertAnswered(written, BrokerChannelClose(1, 403, "ACCESS_REFUSED - access refused", 50, 40));
    }

    [TestMethod]
    public async Task QueuePurge_Missing_ClosesTheChannelWith404()
    {
        var written = await ServeAsync(NewBroker(NewBus()), QueuePurge(1, "missing"), ChannelCloseOk(1));

        AssertAnswered(written, BrokerChannelClose(1, 404, "NOT_FOUND - no queue", 50, 30));
    }
}
