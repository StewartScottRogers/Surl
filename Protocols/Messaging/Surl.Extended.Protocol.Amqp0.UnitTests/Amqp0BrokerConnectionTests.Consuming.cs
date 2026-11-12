using System.Text;
using System.Text.RegularExpressions;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0ChannelTestBytes;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0TestBytes;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary><c>basic.qos</c>, <c>consume</c>, <c>cancel</c>, <c>get</c> and the settling methods (ADR-0112 decision 6).</summary>
public sealed partial class Amqp0BrokerConnectionTests
{
    [TestMethod]
    public async Task Consume_EmptyTagOnTheLastDeclaredQueue_GetsAServerMadeTag()
    {
        var written = await ServeAsync(NewBroker(NewBus()), QueueDeclare(1, "q"), Consume(1, string.Empty, string.Empty));

        var consumeOk = written[(Prefix().Length + QueueDeclareOk(1, "q").Length)..];
        var tag = Encoding.ASCII.GetString(consumeOk, 12, consumeOk[11]);
        Assert.MatchesRegex(ServerMadeConsumerTag(), tag);
        AssertAnswered(written, QueueDeclareOk(1, "q"), ConsumeOk(1, tag));
    }

    [TestMethod]
    public async Task Consume_TagAlreadyUsedOnTheChannel_IsClosedWith530()
    {
        var written = await ServeAsync(NewBroker(NewBus()), QueueDeclare(1, "q"), Consume(1, "q", "c1"), Consume(1, "q", "c1"));

        AssertAnswered(written, QueueDeclareOk(1, "q"), ConsumeOk(1, "c1"), BrokerClose(530, "NOT_ALLOWED - consumer tag in use", 60, 20));
    }

    [TestMethod]
    public async Task Consume_ExclusiveOnAQueueWithAConsumer_ClosesTheChannelWith403()
    {
        var written = await ServeAsync(NewBroker(NewBus()), QueueDeclare(1, "q"), Consume(1, "q", "c1"), Consume(1, "q", "c2", bits: 4), ChannelCloseOk(1));

        AssertAnswered(written, QueueDeclareOk(1, "q"), ConsumeOk(1, "c1"), BrokerChannelClose(1, 403, "ACCESS_REFUSED - queue consumed exclusively", 60, 20));
    }

    [TestMethod]
    public async Task Consume_MissingQueue_ClosesTheChannelWith404()
    {
        var written = await ServeAsync(NewBroker(NewBus()), Consume(1, "missing", "c1"), ChannelCloseOk(1));

        AssertAnswered(written, BrokerChannelClose(1, 404, "NOT_FOUND - no queue", 60, 20));
    }

    [TestMethod]
    public async Task Consume_PastTheBusSubscriptionBound_IsClosedWith506()
    {
        var bus = new MessageBus.MessageBus(new ManualTimeProvider(), maxSubscriptionsPerConnection: 1);

        var written = await ServeAsync(NewBroker(bus), QueueDeclare(1, "q"), Consume(1, "q", "c1", bits: 8), Consume(1, "q", "c2"));

        AssertAnswered(written, QueueDeclareOk(1, "q"), BrokerClose(506, "RESOURCE_ERROR - broker bound reached", 60, 20));
    }

    [TestMethod]
    public async Task Consume_NoAck_AcknowledgesEachDeliveryAsItIsWritten()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            QueueDeclare(1, "q"),
            Consume(1, "q", "c1", bits: 2),
            PublishText(1, string.Empty, "q", "x"),
            ChannelClose(1),
            ChannelOpen(1),
            Get(1, "q"));

        AssertAnswered(
            written,
            QueueDeclareOk(1, "q"),
            ConsumeOk(1, "c1"),
            Deliver(1, "c1", 1, false, string.Empty, "q"),
            TextContent(1, "x"),
            ChannelCloseOk(1),
            ChannelOpenOk(1),
            GetEmpty(1));
    }

    [TestMethod]
    public async Task Qos_PrefetchCountOne_HoldsTheNextDeliveryUntilTheFirstIsAcknowledged()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            Qos(1, 1),
            QueueDeclare(1, "q"),
            PublishText(1, string.Empty, "q", "1"),
            PublishText(1, string.Empty, "q", "2"),
            Consume(1, "q", "c1"),
            Ack(1, 1));

        AssertAnswered(
            written,
            QosOk(1),
            QueueDeclareOk(1, "q"),
            ConsumeOk(1, "c1"),
            Deliver(1, "c1", 1, false, string.Empty, "q"),
            TextContent(1, "1"),
            Deliver(1, "c1", 2, false, string.Empty, "q"),
            TextContent(1, "2"));
    }

    [TestMethod]
    public async Task Qos_PrefetchCountOneWithTwoConsumers_HoldsOneDeliveryBetweenThem()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            Qos(1, 1),
            QueueDeclare(1, "q"),
            PublishText(1, string.Empty, "q", "1"),
            PublishText(1, string.Empty, "q", "2"),
            Consume(1, "q", "c1"),
            Consume(1, "q", "c2"),
            QueueDeclare(1, "r"),
            Ack(1, 1));

        AssertAnswered(
            written,
            QosOk(1),
            QueueDeclareOk(1, "q"),
            ConsumeOk(1, "c1"),
            Deliver(1, "c1", 1, false, string.Empty, "q"),
            TextContent(1, "1"),
            ConsumeOk(1, "c2"),
            QueueDeclareOk(1, "r"),
            Deliver(1, "c2", 2, false, string.Empty, "q"),
            TextContent(1, "2"));
    }

    [TestMethod]
    public async Task Qos_GlobalPrefetchCountOne_BoundsTwoChannelsTogether()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            ChannelOpen(2),
            Qos(1, 1, global: true),
            Qos(1, 1),
            QueueDeclare(1, "q"),
            PublishText(1, string.Empty, "q", "1"),
            PublishText(1, string.Empty, "q", "2"),
            PublishText(1, string.Empty, "q", "3"),
            PublishText(1, string.Empty, "q", "4"),
            Consume(1, "q", "c1"),
            Consume(2, "q", "c2"),
            QueueDeclare(1, "r"),
            Ack(1, 1),
            Ack(2, 1));

        AssertAnswered(
            written,
            ChannelOpenOk(2),
            QosOk(1),
            QosOk(1),
            QueueDeclareOk(1, "q"),
            ConsumeOk(1, "c1"),
            Deliver(1, "c1", 1, false, string.Empty, "q"),
            TextContent(1, "1"),
            ConsumeOk(2, "c2"),
            QueueDeclareOk(1, "r"),
            Deliver(2, "c2", 1, false, string.Empty, "q"),
            TextContent(2, "2"),
            Deliver(2, "c2", 2, false, string.Empty, "q"),
            TextContent(2, "3"));
    }

    [TestMethod]
    public async Task Qos_PrefetchSize_HoldsASecondDeliveryPastItButNeverTheFirst()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            Qos(1, 0, prefetchSize: 3),
            QueueDeclare(1, "q"),
            PublishText(1, string.Empty, "q", "abcd"),
            PublishText(1, string.Empty, "q", "ef"),
            Consume(1, "q", "c1"),
            QueueDeclare(1, "r"),
            Ack(1, 1));

        AssertAnswered(
            written,
            QosOk(1),
            QueueDeclareOk(1, "q"),
            ConsumeOk(1, "c1"),
            Deliver(1, "c1", 1, false, string.Empty, "q"),
            TextContent(1, "abcd"),
            QueueDeclareOk(1, "r"),
            Deliver(1, "c1", 2, false, string.Empty, "q"),
            TextContent(1, "ef"));
    }

    [TestMethod]
    public async Task Cancel_ConsumerWithAWaitingDelivery_PutsItBackForTheOthers()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            Qos(1, 1),
            QueueDeclare(1, "q"),
            PublishText(1, string.Empty, "q", "1"),
            PublishText(1, string.Empty, "q", "2"),
            Consume(1, "q", "c1"),
            Consume(1, "q", "c2"),
            Cancel(1, "c2"),
            Ack(1, 1));

        AssertAnswered(
            written,
            QosOk(1),
            QueueDeclareOk(1, "q"),
            ConsumeOk(1, "c1"),
            Deliver(1, "c1", 1, false, string.Empty, "q"),
            TextContent(1, "1"),
            ConsumeOk(1, "c2"),
            CancelOk(1, "c2"),
            Deliver(1, "c1", 2, true, string.Empty, "q"),
            TextContent(1, "2"));
    }

    [TestMethod]
    public async Task ChannelClose_WithAWaitingDelivery_PutsItBackOnTheQueue()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            ChannelOpen(2),
            Qos(1, 1),
            QueueDeclare(1, "q"),
            PublishText(1, string.Empty, "q", "1"),
            PublishText(1, string.Empty, "q", "2"),
            Consume(1, "q", "c1"),
            Consume(1, "q", "c2"),
            ChannelClose(1),
            Get(2, "q", noAck: true));

        AssertAnswered(
            written,
            ChannelOpenOk(2),
            QosOk(1),
            QueueDeclareOk(1, "q"),
            ConsumeOk(1, "c1"),
            Deliver(1, "c1", 1, false, string.Empty, "q"),
            TextContent(1, "1"),
            ConsumeOk(1, "c2"),
            ChannelCloseOk(1),
            GetOk(2, 1, true, string.Empty, "q", 1),
            TextContent(2, "1"));
    }

    [TestMethod]
    public async Task Recover_WithoutRequeue_RedeliversToTheSameConsumerUnderANewTagAndRequeuesAGet()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            QueueDeclare(1, "q"),
            PublishText(1, string.Empty, "q", "x"),
            Get(1, "q"),
            PublishText(1, string.Empty, "q", "y"),
            Consume(1, "q", "c1"),
            Recover(1, requeue: false));

        AssertAnswered(
            written,
            QueueDeclareOk(1, "q"),
            GetOk(1, 1, false, string.Empty, "q", 0),
            TextContent(1, "x"),
            ConsumeOk(1, "c1"),
            Deliver(1, "c1", 2, false, string.Empty, "q"),
            TextContent(1, "y"),
            Deliver(1, "c1", 3, true, string.Empty, "q"),
            TextContent(1, "y"),
            Deliver(1, "c1", 4, true, string.Empty, "q"),
            TextContent(1, "x"),
            RecoverOk(1));
    }

    [TestMethod]
    public async Task Recover_WithoutRequeueAfterCancel_PutsTheCancelledConsumersDeliveryBack()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            QueueDeclare(1, "q"),
            PublishText(1, string.Empty, "q", "x"),
            Consume(1, "q", "c1"),
            Cancel(1, "c1"),
            Recover(1, requeue: false),
            Get(1, "q", noAck: true));

        AssertAnswered(
            written,
            QueueDeclareOk(1, "q"),
            ConsumeOk(1, "c1"),
            Deliver(1, "c1", 1, false, string.Empty, "q"),
            TextContent(1, "x"),
            CancelOk(1, "c1"),
            RecoverOk(1),
            GetOk(1, 2, true, string.Empty, "q", 0),
            TextContent(1, "x"));
    }

    [TestMethod]
    public async Task Cancel_KnownUnknownAndNoWait_EndsDeliveriesAndKeepsTheOutstandingOnes()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            QueueDeclare(1, "q"),
            PublishText(1, string.Empty, "q", "1"),
            Consume(1, "q", "c1"),
            Cancel(1, "c1"),
            Cancel(1, "unknown"),
            Cancel(1, "unknown", noWait: true),
            PublishText(1, string.Empty, "q", "2"),
            Ack(1, 1),
            Get(1, "q", noAck: true));

        AssertAnswered(
            written,
            QueueDeclareOk(1, "q"),
            ConsumeOk(1, "c1"),
            Deliver(1, "c1", 1, false, string.Empty, "q"),
            TextContent(1, "1"),
            CancelOk(1, "c1"),
            CancelOk(1, "unknown"),
            GetOk(1, 2, false, string.Empty, "q", 0),
            TextContent(1, "2"));
    }

    [TestMethod]
    public async Task Reject_WithAndWithoutRequeue_PutsBackOrDrops()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            QueueDeclare(1, "q"),
            PublishText(1, string.Empty, "q", "x"),
            Get(1, "q"),
            Reject(1, 1, requeue: true),
            Get(1, "q"),
            Reject(1, 2, requeue: false),
            Get(1, "q"));

        AssertAnswered(
            written,
            QueueDeclareOk(1, "q"),
            GetOk(1, 1, false, string.Empty, "q", 0),
            TextContent(1, "x"),
            GetOk(1, 2, true, string.Empty, "q", 0),
            TextContent(1, "x"),
            GetEmpty(1));
    }

    [TestMethod]
    public async Task NackAndAckMultiple_SettleEveryTagUpToTheOneGiven()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            QueueDeclare(1, "q"),
            PublishText(1, string.Empty, "q", "1"),
            PublishText(1, string.Empty, "q", "2"),
            Get(1, "q"),
            Get(1, "q"),
            Nack(1, 2, multiple: true, requeue: true),
            Get(1, "q"),
            Get(1, "q"),
            Ack(1, 0, multiple: true),
            Get(1, "q"));

        AssertAnswered(
            written,
            QueueDeclareOk(1, "q"),
            GetOk(1, 1, false, string.Empty, "q", 1),
            TextContent(1, "1"),
            GetOk(1, 2, false, string.Empty, "q", 0),
            TextContent(1, "2"),
            GetOk(1, 3, true, string.Empty, "q", 1),
            TextContent(1, "1"),
            GetOk(1, 4, true, string.Empty, "q", 0),
            TextContent(1, "2"),
            GetEmpty(1));
    }

    [TestMethod]
    public async Task Recover_PutsEveryOutstandingDeliveryBackAndRecoverAsyncIsNotAnswered()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            QueueDeclare(1, "q"),
            PublishText(1, string.Empty, "q", "x"),
            Get(1, "q"),
            Recover(1, requeue: true),
            Get(1, "q"),
            RecoverAsync(1, requeue: true),
            Get(1, "q", noAck: true));

        AssertAnswered(
            written,
            QueueDeclareOk(1, "q"),
            GetOk(1, 1, false, string.Empty, "q", 0),
            TextContent(1, "x"),
            RecoverOk(1),
            GetOk(1, 2, true, string.Empty, "q", 0),
            TextContent(1, "x"),
            GetOk(1, 3, true, string.Empty, "q", 0),
            TextContent(1, "x"));
    }

    [TestMethod]
    public async Task Reject_ConsumerDelivery_RedeliversItMarkedRedelivered()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            QueueDeclare(1, "q"),
            PublishText(1, string.Empty, "q", "x"),
            Consume(1, "q", "c1"),
            Reject(1, 1, requeue: true));

        AssertAnswered(
            written,
            QueueDeclareOk(1, "q"),
            ConsumeOk(1, "c1"),
            Deliver(1, "c1", 1, false, string.Empty, "q"),
            TextContent(1, "x"),
            Deliver(1, "c1", 2, true, string.Empty, "q"),
            TextContent(1, "x"));
    }

    [TestMethod]
    public async Task Get_PriorityFiveToNine_ComesBeforeZeroToFourEachInPublishOrder()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            QueueDeclare(1, "q"),
            Publish(1, string.Empty, "q"), ContentHeader(1, 2, 0x0800, [4]), Body(1, "l1"),
            Publish(1, string.Empty, "q"), ContentHeader(1, 2, 0x0800, [5]), Body(1, "h1"),
            PublishText(1, string.Empty, "q", "l2"),
            Publish(1, string.Empty, "q"), ContentHeader(1, 2, 0x0800, [9]), Body(1, "h2"),
            Get(1, "q", noAck: true),
            Get(1, "q", noAck: true),
            Get(1, "q", noAck: true),
            Get(1, "q", noAck: true));

        AssertAnswered(
            written,
            QueueDeclareOk(1, "q"),
            GetOk(1, 1, false, string.Empty, "q", 3), ContentHeader(1, 2, 0x0800, [5]), Body(1, "h1"),
            GetOk(1, 2, false, string.Empty, "q", 2), ContentHeader(1, 2, 0x0800, [9]), Body(1, "h2"),
            GetOk(1, 3, false, string.Empty, "q", 1), ContentHeader(1, 2, 0x0800, [4]), Body(1, "l1"),
            GetOk(1, 4, false, string.Empty, "q", 0), TextContent(1, "l2"));
    }

    [TestMethod]
    public async Task Get_MissingQueue_ClosesTheChannelWith404()
    {
        var written = await ServeAsync(NewBroker(NewBus()), Get(1, "missing"), ChannelCloseOk(1));

        AssertAnswered(written, BrokerChannelClose(1, 404, "NOT_FOUND - no queue", 60, 70));
    }

    [GeneratedRegex("^amq\\.ctag-[A-Za-z0-9_-]{22}$")]
    private static partial Regex ServerMadeConsumerTag();
}
