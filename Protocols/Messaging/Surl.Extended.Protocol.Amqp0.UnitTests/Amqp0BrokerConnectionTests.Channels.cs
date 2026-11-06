using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0ChannelTestBytes;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0TestBytes;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>Channels and exceptions (XML class <c>channel</c>; ADR-0112 decision 7).</summary>
public sealed partial class Amqp0BrokerConnectionTests
{
    [TestMethod]
    public async Task ChannelOpen_AboveTheNegotiatedChannelMax_IsClosedWith504()
    {
        var written = await ServeTunedAsync(NewBroker(NewBus()), TuneOk(2, 0, 0), ChannelOpen(3));

        AssertAnswered(written, BrokerClose(504, "CHANNEL_ERROR - channel above channel-max", 20, 10));
    }

    [TestMethod]
    public async Task ChannelOpen_OnAnOpenChannel_IsClosedWith504()
    {
        var written = await ServeAsync(NewBroker(NewBus()), ChannelOpen(1));

        AssertAnswered(written, BrokerClose(504, "CHANNEL_ERROR - channel already open", 20, 10));
    }

    [TestMethod]
    public async Task ChannelClose_FromTheClient_IsAnsweredCloseOkAndTheChannelCanOpenAgain()
    {
        var written = await ServeAsync(NewBroker(NewBus()), ChannelClose(1), ChannelOpen(1), Get(1, "missing"), ChannelCloseOk(1), ChannelOpen(1));

        AssertAnswered(
            written,
            ChannelCloseOk(1),
            ChannelOpenOk(1),
            BrokerChannelClose(1, 404, "NOT_FOUND - no queue", 60, 70),
            ChannelOpenOk(1));
    }

    [TestMethod]
    public async Task ClosingChannel_DiscardsEveryFrameButCloseAndAnswersCloseWithCloseOk()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            Get(1, "missing"),
            Get(1, "missing"),
            Body(1, "x"),
            Frame(1, 1, [0x00]),
            QueueDeclare(1, "q"),
            Flow(1, true),
            ChannelClose(1),
            ChannelOpen(1));

        AssertAnswered(written, BrokerChannelClose(1, 404, "NOT_FOUND - no queue", 60, 70), ChannelCloseOk(1), ChannelOpenOk(1));
    }

    [TestMethod]
    public async Task MethodFrame_TooShortForItsIds_IsClosedWith502()
    {
        var written = await ServeAsync(NewBroker(NewBus()), Frame(1, 1, [0x00]));

        AssertAnswered(written, BrokerClose(502, "SYNTAX_ERROR - malformed method", 0, 0));
    }

    [TestMethod]
    public async Task Method_ArgumentsThatDoNotDecode_IsClosedWith502NamingIt()
    {
        var written = await ServeAsync(NewBroker(NewBus()), Method(1, 50, 10));

        AssertAnswered(written, BrokerClose(502, "SYNTAX_ERROR - malformed method", 50, 10));
    }

    [TestMethod]
    [DataRow(90, 10, DisplayName = "tx.select")]
    [DataRow(85, 10, DisplayName = "confirm.select")]
    [DataRow(40, 30, DisplayName = "exchange.bind")]
    public async Task Method_NotServed_IsClosedWith540(int classId, int methodId)
    {
        var written = await ServeAsync(NewBroker(NewBus()), Method(1, classId, methodId));

        AssertAnswered(written, BrokerClose(540, "NOT_IMPLEMENTED - method not served", classId, methodId));
    }

    [TestMethod]
    [DataRow(60, 60, DisplayName = "basic.deliver")]
    [DataRow(20, 41, DisplayName = "channel.close-ok unasked")]
    [DataRow(10, 50, DisplayName = "connection.close off channel 0")]
    public async Task Method_OnlyAServerSends_IsClosedWith503(int classId, int methodId)
    {
        var written = await ServeAsync(NewBroker(NewBus()), Method(1, classId, methodId));

        AssertAnswered(written, BrokerClose(503, "COMMAND_INVALID - method not valid from a client", classId, methodId));
    }

    [TestMethod]
    public async Task Flow_Stopped_HoldsDeliveriesUntilResumed()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            Flow(1, false),
            QueueDeclare(1, "orders"),
            Consume(1, "orders", "c1"),
            PublishText(1, string.Empty, "orders", "held"),
            Flow(1, true));

        AssertAnswered(
            written,
            FlowOk(1, false),
            QueueDeclareOk(1, "orders"),
            ConsumeOk(1, "c1"),
            Deliver(1, "c1", 1, false, string.Empty, "orders"),
            TextContent(1, "held"),
            FlowOk(1, true));
    }

    [TestMethod]
    public async Task ChannelClose_WithUnsettledDeliveries_PutsThemBackMarkedRedelivered()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            QueueDeclare(1, "orders"),
            PublishText(1, string.Empty, "orders", "one"),
            PublishText(1, string.Empty, "orders", "two"),
            Get(1, "orders"),
            Consume(1, "orders", "c1"),
            Cancel(1, "c1"),
            ChannelClose(1),
            ChannelOpen(1),
            Get(1, "orders", noAck: true),
            Get(1, "orders", noAck: true));

        AssertAnswered(
            written,
            QueueDeclareOk(1, "orders"),
            GetOk(1, 1, false, string.Empty, "orders", 1),
            TextContent(1, "one"),
            ConsumeOk(1, "c1"),
            Deliver(1, "c1", 2, false, string.Empty, "orders"),
            TextContent(1, "two"),
            CancelOk(1, "c1"),
            ChannelCloseOk(1),
            ChannelOpenOk(1),
            GetOk(1, 1, true, string.Empty, "orders", 1),
            TextContent(1, "one"),
            GetOk(1, 2, true, string.Empty, "orders", 0),
            TextContent(1, "two"));
    }
}
