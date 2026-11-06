using System.Text;
using Surl.MessageBus;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0ChannelTestBytes;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0TestBytes;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary><c>basic.publish</c>, its content, routing and <c>basic.return</c> (ADR-0112 decision 6).</summary>
public sealed partial class Amqp0BrokerConnectionTests
{
    // delivery-mode 2: flag bit 12 and one octet.
    private static readonly byte[] PersistentHeader = ContentHeader(1, 1, 0x1000, [2]);

    [TestMethod]
    public async Task Publish_ToAMissingExchange_ClosesTheChannelWith404AndDiscardsTheContent()
    {
        var written = await ServeAsync(NewBroker(NewBus()), PublishText(1, "missing", "k", "x"), ChannelCloseOk(1));

        AssertAnswered(written, BrokerChannelClose(1, 404, "NOT_FOUND - no exchange", 60, 40));
    }

    [TestMethod]
    [DataRow(1, DisplayName = "a content header no publish announced")]
    [DataRow(2, DisplayName = "a body frame before the header")]
    [DataRow(3, DisplayName = "a second content header")]
    [DataRow(4, DisplayName = "a content header of another class")]
    [DataRow(5, DisplayName = "body frames past the body size")]
    public async Task Content_OutOfSequence_IsClosedWith505NamingPublish(int fault)
    {
        var (chunks, replyText) = fault switch
        {
            1 => (new[] { ContentHeader(1, 1) }, "UNEXPECTED_FRAME - content without basic.publish"),
            2 => (new[] { Publish(1, string.Empty, "q"), Body(1, "x") }, "UNEXPECTED_FRAME - body frame out of sequence"),
            3 => (new[] { Publish(1, string.Empty, "q"), ContentHeader(1, 5), ContentHeader(1, 5) }, "UNEXPECTED_FRAME - second content header"),
            4 => (new[] { Publish(1, string.Empty, "q"), ContentHeader(1, 5, classId: 50) }, "UNEXPECTED_FRAME - content header of another class"),
            _ => (new[] { Publish(1, string.Empty, "q"), ContentHeader(1, 1), Body(1, "xy") }, "UNEXPECTED_FRAME - body frame out of sequence"),
        };

        var written = await ServeAsync(NewBroker(NewBus()), chunks);

        AssertAnswered(written, BrokerClose(505, replyText, 60, 40));
    }

    [TestMethod]
    public async Task Method_BeforeTheContentIsComplete_IsClosedWith505NamingIt()
    {
        var written = await ServeAsync(NewBroker(NewBus()), Publish(1, string.Empty, "q"), Get(1, "q"));

        AssertAnswered(written, BrokerClose(505, "UNEXPECTED_FRAME - method before the content was complete", 60, 70));
    }

    [TestMethod]
    public async Task Publish_BodyAboveTheLimit_ClosesTheChannelWith311AndDiscardsTheBody()
    {
        var bus = new MessageBus.MessageBus(new ManualTimeProvider(), maxMessageBytes: 4);

        var written = await ServeAsync(NewBroker(bus), PublishText(1, string.Empty, "q", "hello"), ChannelCloseOk(1));

        AssertAnswered(written, BrokerChannelClose(1, 311, "CONTENT_TOO_LARGE - body above the limit", 60, 40));
    }

    [TestMethod]
    public async Task Publish_ContentInProgressOnTwoChannelsAboveTheLimit_IsClosedWith506()
    {
        var bus = new MessageBus.MessageBus(new ManualTimeProvider(), maxMessageBytes: 10);

        var written = await ServeAsync(
            NewBroker(bus), ChannelOpen(2), Publish(1, string.Empty, "q"), ContentHeader(1, 6), Publish(2, string.Empty, "q"), ContentHeader(2, 6));

        AssertAnswered(written, ChannelOpenOk(2), BrokerClose(506, "RESOURCE_ERROR - content in progress above the limit", 60, 40));
    }

    [TestMethod]
    public async Task Publish_BodyAndNativeFormAboveTheBusLimit_ClosesTheChannelWith311()
    {
        var bus = new MessageBus.MessageBus(new ManualTimeProvider(), maxMessageBytes: 12);

        var written = await ServeAsync(NewBroker(bus), QueueDeclare(1, "q"), PublishText(1, string.Empty, "q", "0123456789"), ChannelCloseOk(1));

        AssertAnswered(written, QueueDeclareOk(1, "q"), BrokerChannelClose(1, 311, "CONTENT_TOO_LARGE - body above the limit", 60, 40));
    }

    [TestMethod]
    [DataRow(new byte[] { 0x00 }, DisplayName = "a content header too short")]
    public async Task Publish_ContentHeaderThatDoesNotDecode_IsClosedWith502(byte[] payload)
    {
        var written = await ServeAsync(NewBroker(NewBus()), Publish(1, string.Empty, "q"), Frame(2, 1, payload));

        AssertAnswered(written, BrokerClose(502, "SYNTAX_ERROR - malformed content header", 60, 40));
    }

    [TestMethod]
    public async Task Publish_PropertyListShorterThanItsFlags_IsClosedWith502()
    {
        var written = await ServeAsync(NewBroker(NewBus()), Publish(1, string.Empty, "q"), ContentHeader(1, 0, 0x8000, []));

        AssertAnswered(written, BrokerClose(502, "SYNTAX_ERROR - malformed content header", 60, 40));
    }

    [TestMethod]
    public async Task Publish_ExpirationNotACount_ClosesTheChannelWith406()
    {
        var written = await ServeAsync(NewBroker(NewBus()), Publish(1, string.Empty, "q"), ContentHeader(1, 0, 0x0100, ShortString("soon")), ChannelCloseOk(1));

        AssertAnswered(written, BrokerChannelClose(1, 406, "PRECONDITION_FAILED - invalid expiration", 60, 40));
    }

    [TestMethod]
    public async Task Publish_UnroutableWithoutMandatory_IsDroppedAndNoted()
    {
        var (written, log) = await ServeLoggedAsync(NewBroker(NewBus()), DefaultTuneOk, PublishText(1, string.Empty, "nowhere", "x"), PublishText(1, "amq.topic", "a/b", "y"));

        AssertAnswered(written);
        Assert.AreEqual(2, log.Notes.Count(note => note == "An unroutable message was dropped."));
    }

    [TestMethod]
    public async Task Publish_Immediate_IsReturnedWith313WhenNoConsumerTakesIt()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            QueueDeclare(1, "idle"),
            QueueBind(1, "idle", "amq.topic", "idle.*"),
            PublishText(1, string.Empty, "idle", "a", bits: 2),
            PublishText(1, "amq.topic", "idle.x", "b", bits: 2),
            PublishText(1, string.Empty, "nowhere", "c", bits: 2),
            PublishText(1, "amq.topic", "a/b", "d", bits: 1));

        AssertAnswered(
            written,
            QueueDeclareOk(1, "idle"),
            QueueBindOk(1),
            Return(1, 313, "NO_CONSUMERS", string.Empty, "idle"),
            TextContent(1, "a"),
            Return(1, 313, "NO_CONSUMERS", "amq.topic", "idle.x"),
            TextContent(1, "b"),
            Return(1, 312, "NO_ROUTE", string.Empty, "nowhere"),
            TextContent(1, "c"),
            Return(1, 312, "NO_ROUTE", "amq.topic", "a/b"),
            TextContent(1, "d"));
    }

    [TestMethod]
    public async Task Publish_ImmediateWithAConsumer_IsDelivered()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            QueueDeclare(1, "busy"),
            QueueBind(1, "busy", "amq.topic", "busy"),
            Consume(1, "busy", "c1", bits: 2),
            PublishText(1, string.Empty, "busy", "a", bits: 2),
            PublishText(1, "amq.topic", "busy", "b", bits: 2));

        AssertAnswered(
            written,
            QueueDeclareOk(1, "busy"),
            QueueBindOk(1),
            ConsumeOk(1, "c1"),
            Deliver(1, "c1", 1, false, string.Empty, "busy"),
            TextContent(1, "a"),
            Deliver(1, "c1", 2, false, "amq.topic", "busy"),
            TextContent(1, "b"));
    }

    [TestMethod]
    public async Task Publish_TransientToAFullQueue_IsNotedAndTheRestStands()
    {
        var bus = new MessageBus.MessageBus(new ManualTimeProvider(), maxMessagesPerQueue: 1);

        var (written, log) = await ServeLoggedAsync(
            NewBroker(bus),
            DefaultTuneOk,
            QueueDeclare(1, "q"),
            QueueBind(1, "q", "amq.topic", "t"),
            PublishText(1, string.Empty, "q", "1"),
            PublishText(1, string.Empty, "q", "2"),
            PublishText(1, "amq.topic", "t", "3"));

        AssertAnswered(written, QueueDeclareOk(1, "q"), QueueBindOk(1));
        Assert.AreEqual(2, log.Notes.Count(note => note == "A full queue did not take a transient message."));
    }

    [TestMethod]
    [DataRow("", "q", DisplayName = "the default exchange")]
    [DataRow("amq.topic", "t", DisplayName = "amq.topic")]
    public async Task Publish_PersistentToAFullQueue_IsClosedWith506(string exchange, string routingKey)
    {
        var bus = new MessageBus.MessageBus(new ManualTimeProvider(), maxMessagesPerQueue: 1);

        var written = await ServeAsync(
            NewBroker(bus),
            QueueDeclare(1, "q"),
            QueueBind(1, "q", "amq.topic", "t"),
            PublishText(1, string.Empty, "q", "1"),
            Publish(1, exchange, routingKey),
            PersistentHeader,
            Body(1, "2"));

        AssertAnswered(written, QueueDeclareOk(1, "q"), QueueBindOk(1), BrokerClose(506, "RESOURCE_ERROR - queue full", 60, 40));
    }

    [TestMethod]
    public async Task Publish_OnANewTopicPastTheBusTopicBound_IsClosedWith506()
    {
        var bus = new MessageBus.MessageBus(new ManualTimeProvider(), maxTopics: 1);

        var written = await ServeAsync(NewBroker(bus), PublishText(1, "amq.topic", "a", "1"), PublishText(1, "amq.topic", "b", "2"));

        AssertAnswered(written, BrokerClose(506, "RESOURCE_ERROR - broker bound reached", 60, 40));
    }

    [TestMethod]
    public async Task Publish_OnAmqTopicRefusedByTopicAccess_ClosesTheChannelWith403()
    {
        var bus = BusGranting(new TopicAccessGrant("allowed/#", "alice", TopicAccessRights.Publish));

        var written = await ServeAsync(NewBroker(bus), PublishText(1, "amq.topic", "other", "x"), ChannelCloseOk(1));

        AssertAnswered(written, BrokerChannelClose(1, 403, "ACCESS_REFUSED - access refused", 60, 40));
    }

    [TestMethod]
    public async Task Get_EveryProperty_ComesBackByteForByte()
    {
        var properties = new Amqp0BasicProperties
        {
            ContentType = "text/plain",
            ContentEncoding = "gzip",
            Headers = new([new("h", Amqp0FieldValue.LongString("v")), new("n", Amqp0FieldValue.Int32(1))]),
            DeliveryMode = 1,
            Priority = 9,
            CorrelationId = "c-1",
            ReplyTo = "replies.alice",
            Expiration = "60000",
            MessageId = "m-1",
            Timestamp = 1_700_000_000,
            Type = "order",
            UserId = "alice",
            AppId = "shop",
            ClusterId = "c",
        };
        var (flags, list) = properties.Encode();

        var written = await ServeAsync(
            NewBroker(NewBus()), QueueDeclare(1, "q"), Publish(1, string.Empty, "q"), ContentHeader(1, 2, flags, list), Body(1, "hi"), Get(1, "q", noAck: true));

        AssertAnswered(written, QueueDeclareOk(1, "q"), GetOk(1, 1, false, string.Empty, "q", 0), ContentHeader(1, 2, flags, list), Body(1, Encoding.ASCII.GetBytes("hi")));
    }
}
