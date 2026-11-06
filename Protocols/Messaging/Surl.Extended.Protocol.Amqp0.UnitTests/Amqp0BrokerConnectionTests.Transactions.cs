using Surl.MessageBus;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0ChannelTestBytes;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0TestBytes;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// Transactions (XML class <c>tx</c>, 90) and RabbitMQ's publisher confirms (<c>confirm</c>, 85)
/// (BL-731; ADR-0112 decision 6).
/// </summary>
public sealed partial class Amqp0BrokerConnectionTests
{
    [TestMethod]
    public async Task Commit_PublishesOnATransactedChannel_AreInvisibleUntilCommitted()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            QueueDeclare(1, "q"),
            TxSelect(1),
            TxSelect(1),
            PublishText(1, string.Empty, "q", "a"),
            PublishText(1, string.Empty, "nowhere", "b", bits: 1),
            Get(1, "q"),
            TxCommit(1),
            Get(1, "q", noAck: true));

        AssertAnswered(
            written,
            QueueDeclareOk(1, "q"),
            TxSelectOk(1),
            TxSelectOk(1),
            GetEmpty(1),
            Return(1, 312, "NO_ROUTE", string.Empty, "nowhere"),
            TextContent(1, "b"),
            TxCommitOk(1),
            GetOk(1, 1, false, string.Empty, "q", 0),
            TextContent(1, "a"));
    }

    [TestMethod]
    public async Task Rollback_PublishesOnATransactedChannel_AreGone()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            QueueDeclare(1, "q"),
            TxSelect(1),
            PublishText(1, string.Empty, "q", "a"),
            TxRollback(1),
            TxCommit(1),
            Get(1, "q"));

        AssertAnswered(written, QueueDeclareOk(1, "q"), TxSelectOk(1), TxRollbackOk(1), TxCommitOk(1), GetEmpty(1));
    }

    [TestMethod]
    public async Task Commit_AnAcknowledgementOnATransactedChannel_TakesEffectOnlyThen()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            QueueDeclare(1, "q"),
            PublishText(1, string.Empty, "q", "a"),
            TxSelect(1),
            Get(1, "q"),
            Ack(1, 1),
            TxRollback(1),
            Ack(1, 1),
            TxCommit(1),
            ChannelClose(1),
            ChannelOpen(1),
            Get(1, "q"));

        AssertAnswered(
            written,
            QueueDeclareOk(1, "q"),
            TxSelectOk(1),
            GetOk(1, 1, false, string.Empty, "q", 0),
            TextContent(1, "a"),
            TxRollbackOk(1),
            TxCommitOk(1),
            ChannelCloseOk(1),
            ChannelOpenOk(1),
            GetEmpty(1));
    }

    [TestMethod]
    public async Task ChannelClose_TransactedWithAnUncommittedSettlement_PutsTheMessageBack()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            QueueDeclare(1, "q"),
            PublishText(1, string.Empty, "q", "a"),
            TxSelect(1),
            Get(1, "q"),
            Reject(1, 1, requeue: false),
            ChannelClose(1),
            ChannelOpen(1),
            Get(1, "q", noAck: true));

        AssertAnswered(
            written,
            QueueDeclareOk(1, "q"),
            TxSelectOk(1),
            GetOk(1, 1, false, string.Empty, "q", 0),
            TextContent(1, "a"),
            ChannelCloseOk(1),
            ChannelOpenOk(1),
            GetOk(1, 1, true, string.Empty, "q", 0),
            TextContent(1, "a"));
    }

    [TestMethod]
    public async Task Commit_NackedDeliveryOnATransactedChannel_RequeuesItThen()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            QueueDeclare(1, "q"),
            PublishText(1, string.Empty, "q", "a"),
            TxSelect(1),
            Get(1, "q"),
            Nack(1, 0, multiple: true, requeue: true),
            Get(1, "q"),
            TxCommit(1),
            Get(1, "q", noAck: true));

        AssertAnswered(
            written,
            QueueDeclareOk(1, "q"),
            TxSelectOk(1),
            GetOk(1, 1, false, string.Empty, "q", 0),
            TextContent(1, "a"),
            GetEmpty(1),
            TxCommitOk(1),
            GetOk(1, 2, true, string.Empty, "q", 0),
            TextContent(1, "a"));
    }

    [TestMethod]
    public async Task Ack_UnknownTagOnATransactedChannel_ClosesTheChannelWith406AtOnce()
    {
        var written = await ServeAsync(NewBroker(NewBus()), TxSelect(1), Ack(1, 5), ChannelCloseOk(1));

        AssertAnswered(written, TxSelectOk(1), BrokerChannelClose(1, 406, "PRECONDITION_FAILED - unknown delivery tag", 60, 80));
    }

    [TestMethod]
    [DataRow(20, DisplayName = "tx.commit")]
    [DataRow(30, DisplayName = "tx.rollback")]
    public async Task Commit_OnAChannelNotTransacted_ClosesTheChannelWith406(int methodId)
    {
        var written = await ServeAsync(NewBroker(NewBus()), Method(1, 90, methodId), ChannelCloseOk(1));

        AssertAnswered(written, BrokerChannelClose(1, 406, "PRECONDITION_FAILED - channel not transacted", 90, methodId));
    }

    [TestMethod]
    public async Task Publish_TransactionBufferAndContentInProgressAboveTheLimit_IsClosedWith506()
    {
        var bus = new MessageBus.MessageBus(new ManualTimeProvider(), maxMessageBytes: 20);

        var written = await ServeAsync(
            NewBroker(bus),
            QueueDeclare(1, "q"),
            TxSelect(1),
            PublishText(1, string.Empty, "q", "123456789012"),
            TxCommit(1),
            PublishText(1, string.Empty, "q", "123456789012"),
            Publish(1, string.Empty, "q"),
            ContentHeader(1, 12));

        AssertAnswered(
            written,
            QueueDeclareOk(1, "q"),
            TxSelectOk(1),
            TxCommitOk(1),
            BrokerClose(506, "RESOURCE_ERROR - content in progress above the limit", 60, 40));
    }

    [TestMethod]
    public async Task Publish_InConfirmMode_IsAcknowledgedInOrderEachWithMultipleClear()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            QueueDeclare(1, "q"),
            ConfirmSelect(1),
            ConfirmSelect(1, noWait: true),
            PublishText(1, string.Empty, "q", "a"),
            PublishText(1, string.Empty, "nowhere", "b", bits: 1),
            PublishText(1, string.Empty, "q", "c"));

        AssertAnswered(
            written,
            QueueDeclareOk(1, "q"),
            ConfirmSelectOk(1),
            Confirm(1, 1),
            Return(1, 312, "NO_ROUTE", string.Empty, "nowhere"),
            TextContent(1, "b"),
            Confirm(1, 2),
            Confirm(1, 3));
    }

    [TestMethod]
    public async Task Publish_InConfirmModeTheBusRefuses_IsNackedAndTheChannelStaysOpen()
    {
        var bus = new MessageBus.MessageBus(new ManualTimeProvider(), maxMessagesPerQueue: 1);

        var (written, log) = await ServeLoggedAsync(
            NewBroker(bus),
            DefaultTuneOk,
            QueueDeclare(1, "q"),
            ConfirmSelect(1),
            PublishText(1, string.Empty, "q", "1"),
            Publish(1, string.Empty, "q"),
            PersistentHeader,
            Body(1, "2"),
            PublishText(1, string.Empty, "q", "3"));

        AssertAnswered(written, QueueDeclareOk(1, "q"), ConfirmSelectOk(1), Confirm(1, 1), ConfirmRefused(1, 2), Confirm(1, 3));
        Assert.Contains("Publish 2 on channel 1 was nacked: RESOURCE_ERROR - queue full.", log.Notes);
    }

    [TestMethod]
    public async Task ConfirmSelect_OnATransactedChannel_ClosesTheChannelWith406()
    {
        var written = await ServeAsync(NewBroker(NewBus()), TxSelect(1), ConfirmSelect(1), ChannelCloseOk(1));

        AssertAnswered(written, TxSelectOk(1), BrokerChannelClose(1, 406, "PRECONDITION_FAILED - channel transacted", 85, 10));
    }

    [TestMethod]
    public async Task TxSelect_OnAChannelInConfirmMode_ClosesTheChannelWith406()
    {
        var written = await ServeAsync(NewBroker(NewBus()), ConfirmSelect(1), TxSelect(1), ChannelCloseOk(1));

        AssertAnswered(written, ConfirmSelectOk(1), BrokerChannelClose(1, 406, "PRECONDITION_FAILED - channel in confirm mode", 90, 10));
    }

    [TestMethod]
    public async Task ConnectionStart_Capabilities_AnnouncePublisherConfirms()
    {
        byte[] publisherConfirms = [.. ShortString("publisher_confirms"), (byte)'t', 1];

        var written = await ServeAsync(NewBroker(NewBus()));

        var start = Start("PLAIN AMQPLAIN");
        CollectionAssert.AreEqual(start, written[..start.Length]);
        Assert.IsTrue(start.AsSpan().IndexOf(publisherConfirms) > 0);
    }
}
