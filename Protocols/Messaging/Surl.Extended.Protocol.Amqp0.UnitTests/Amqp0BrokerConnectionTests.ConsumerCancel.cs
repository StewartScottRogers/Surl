using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0ChannelTestBytes;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0TestBytes;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// <c>queue.delete</c> ending the consumers of the queue on every connection, with
/// <c>basic.cancel</c> for a client that announced <c>consumer_cancel_notify</c> (ADR-0112
/// decision 5; matrix row <c>Amqp0:Extension-1</c>).
/// </summary>
public sealed partial class Amqp0BrokerConnectionTests
{
    private static byte[] StartOkOf(bool announced) =>
        StartOkAnnouncingConsumerCancel("PLAIN", PlainResponse("alice", "secret"), announced);

    [TestMethod]
    public async Task QueueDelete_ConsumerOnAnotherConnectionAnnouncedCancelNotify_IsSentBasicCancelWithNoWait()
    {
        var written = await DeleteFromAnotherConnectionAsync(StartOkOf(announced: true));

        CollectionAssert.AreEqual(
            Concat([Prefix(), QueueDeclareOk(1, "q"), ConsumeOk(1, "c1"), Cancel(1, "c1", noWait: true), CloseOk()]), written, Convert.ToHexString(written));
    }

    [TestMethod]
    public async Task QueueDelete_ConsumerOnAnotherConnectionDidNotAnnounceIt_JustStopsReceiving()
    {
        var written = await DeleteFromAnotherConnectionAsync(StartOkOf(announced: false));

        CollectionAssert.AreEqual(Concat([Prefix(), QueueDeclareOk(1, "q"), ConsumeOk(1, "c1"), CloseOk()]), written, Convert.ToHexString(written));
    }

    [TestMethod]
    public async Task QueueDelete_OwnConsumerOfAClientThatAnnouncedIt_IsCancelledBeforeDeleteOk()
    {
        var (written, _) = await ServeWithStartOkAsync(
            NewBroker(NewBus()), StartOkOf(announced: true), QueueDeclare(1, "q"), Consume(1, "q", "c1"), QueueDelete(1, "q"), Get(1, "q"), ChannelCloseOk(1));

        CollectionAssert.AreEqual(
            Concat([
                Prefix(), QueueDeclareOk(1, "q"), ConsumeOk(1, "c1"), Cancel(1, "c1", noWait: true), QueueDeleteOk(1, 0),
                BrokerChannelClose(1, 404, "NOT_FOUND - no queue", 60, 70), CloseOk()]),
            written,
            Convert.ToHexString(written));
    }

    [TestMethod]
    public async Task QueueDelete_OwnConsumerOfAClientThatDidNotAnnounceIt_IsEndedWithoutBasicCancel()
    {
        var (written, _) = await ServeWithStartOkAsync(
            NewBroker(NewBus()), PlainStartOk, QueueDeclare(1, "q"), Consume(1, "q", "c1"), QueueDelete(1, "q"));

        AssertAnswered(written, QueueDeclareOk(1, "q"), ConsumeOk(1, "c1"), QueueDeleteOk(1, 0));
    }

    [TestMethod]
    public async Task QueueDelete_AfterTheConsumerWasCancelled_SendsNoBasicCancel()
    {
        var (written, _) = await ServeWithStartOkAsync(
            NewBroker(NewBus()), StartOkOf(announced: true), QueueDeclare(1, "q"), Consume(1, "q", "c1"), Cancel(1, "c1"), QueueDelete(1, "q"));

        CollectionAssert.AreEqual(
            Concat([Prefix(), QueueDeclareOk(1, "q"), ConsumeOk(1, "c1"), CancelOk(1, "c1"), QueueDeleteOk(1, 0), CloseOk()]), written, Convert.ToHexString(written));
    }

    [TestMethod]
    public async Task QueueDelete_AfterTheConsumersChannelClosed_SendsNoBasicCancel()
    {
        var (written, _) = await ServeWithStartOkAsync(
            NewBroker(NewBus()),
            StartOkOf(announced: true),
            QueueDeclare(1, "q"),
            Consume(1, "q", "c1"),
            ChannelClose(1),
            ChannelOpen(1),
            QueueDelete(1, "q"));

        CollectionAssert.AreEqual(
            Concat([Prefix(), QueueDeclareOk(1, "q"), ConsumeOk(1, "c1"), ChannelCloseOk(1), ChannelOpenOk(1), QueueDeleteOk(1, 0), CloseOk()]),
            written,
            Convert.ToHexString(written));
    }

    [TestMethod]
    public void EndConsumerOfDeletedQueue_ConsumerNotOnTheChannel_DoesNothing()
    {
        var connection = new InMemoryConnection([]);
        var broker = new Amqp0BrokerConnection(
            connection, Context(new RecordingExchangeLog()), new UnitTestAmqp0AuthenticationPolicy("PLAIN"), Amqp0BrokerOptions.Default, NewBus(), new Amqp0ExchangeTable(), new Amqp0ConsumerRegistry());

        broker.EndConsumerOfDeletedQueue(new Amqp0Channel(1), new Amqp0Consumer("c1", noAck: false));

        Assert.IsEmpty(connection.WrittenBytes);
    }

    private async Task<byte[]> DeleteFromAnotherConnectionAsync(byte[] consumerStartOk)
    {
        var broker = NewBroker(NewBus());
        byte[][] before = [ProtocolHeader, consumerStartOk, DefaultTuneOk, Open("/"), ChannelOpen(1), QueueDeclare(1, "q"), Consume(1, "q", "c1")];
        var release = new TaskCompletionSource();
        var consumerConnection = new UnitTestGatedConnection(
            new InMemoryConnection([.. before, Close()]), before.Sum(chunk => chunk.Length), release.Task);
        var consuming = broker.ServeAsync(consumerConnection, Context(new RecordingExchangeLog()));
        var consumeAnswered = Concat([Prefix(), QueueDeclareOk(1, "q"), ConsumeOk(1, "c1")]).Length;
        while (consumerConnection.WrittenBytes.Length < consumeAnswered)
        {
            await Task.Delay(1, TestContext.CancellationToken);
        }

        var deleterWritten = await ServeAsync(broker, QueueDelete(1, "q"));
        release.SetResult();
        await consuming;

        AssertAnswered(deleterWritten, QueueDeleteOk(1, 0));
        return consumerConnection.WrittenBytes;
    }

    private async Task<(byte[] Written, RecordingExchangeLog Log)> ServeWithStartOkAsync(Amqp0Broker broker, byte[] startOk, params byte[][] chunks)
    {
        var log = new RecordingExchangeLog();
        byte[][] all = [ProtocolHeader, startOk, DefaultTuneOk, Open("/"), ChannelOpen(1), .. chunks, Close()];
        var connection = new InMemoryConnection(all.Select(chunk => (ReadOnlyMemory<byte>)chunk));

        await broker.ServeAsync(connection, Context(log));

        return (connection.WrittenBytes, log);
    }
}
