using Surl.Content;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0ChannelTestBytes;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0TestBytes;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>Reset, snapshot and restore of the exchanges and bindings (ADR-0159 decisions 2 and 4; BL-1170).</summary>
public sealed partial class Amqp0BrokerConnectionTests
{
    [TestMethod]
    public async Task ResetExchanges_AfterADeclareAndABind_LeavesOnlyTheBuiltInExchanges()
    {
        var bus = NewBus();
        var broker = NewBroker(bus);
        await ServeAsync(
            broker,
            ExchangeDeclare(1, "orders", "direct"),
            QueueDeclare(1, "q"),
            QueueBind(1, "q", "orders", "k"),
            QueueBind(1, "q", "amq.direct", "k"));

        broker.ResetExchanges();
        var missing = await ServeAsync(broker, ExchangeDeclare(1, "orders", "direct", bits: 1), ChannelCloseOk(1));
        var written = await ServeAsync(
            broker,
            PublishText(1, "amq.direct", "k", "dropped"),
            ExchangeDeclare(1, "amq.topic", "topic", bits: 1),
            ExchangeDeclare(1, "orders", "fanout"));

        AssertAnswered(missing, BrokerChannelClose(1, 404, "NOT_FOUND - no exchange", 40, 10));
        AssertAnswered(written, ExchangeDeclareOk(1), ExchangeDeclareOk(1));
        Assert.AreEqual(0, ReadyCount(bus, "q"));
    }

    [TestMethod]
    public async Task ExchangeSnapshot_TakenChangedAndRestored_GivesBackTheSnapshotsExchangesAndBindings()
    {
        var bus = NewBus();
        var broker = NewBroker(bus);
        await ServeAsync(
            broker,
            ExchangeDeclare(1, "kept", "direct"),
            QueueDeclare(1, "q"),
            QueueBind(1, "q", "kept", "k"));
        var snapshot = broker.TakeExchangeSnapshot();
        await ServeAsync(broker, ExchangeDeclare(1, "later", "fanout"), ExchangeDelete(1, "kept"));

        broker.RestoreExchanges(snapshot);
        var written = await ServeAsync(
            broker,
            ExchangeDeclare(1, "kept", "direct", bits: 1),
            PublishText(1, "kept", "k", "routed"),
            ExchangeDeclare(1, "later", "fanout", bits: 1),
            ChannelCloseOk(1));

        AssertAnswered(written, ExchangeDeclareOk(1), BrokerChannelClose(1, 404, "NOT_FOUND - no exchange", 40, 10));
        Assert.AreEqual(1, ReadyCount(bus, "q"));
        Assert.IsGreaterThan(0, snapshot.ByteCount);
    }

    [TestMethod]
    public async Task ExchangeSnapshot_TakenBeforeABindingAndRestoredLater_DropsTheBinding()
    {
        var bus = NewBus();
        var broker = NewBroker(bus);
        await ServeAsync(broker, QueueDeclare(1, "q"), ExchangeDeclare(1, "x", "fanout"));
        var snapshot = broker.TakeExchangeSnapshot();
        await ServeAsync(broker, QueueBind(1, "q", "x", string.Empty));

        broker.RestoreExchanges(snapshot);
        await ServeAsync(broker, PublishText(1, "x", string.Empty, "nobody"));

        Assert.AreEqual(0, ReadyCount(bus, "q"));
    }

    [TestMethod]
    public void RestoreExchanges_ASnapshotFromAnotherBroker_IsRefused()
    {
        var snapshot = NewBroker(NewBus()).TakeExchangeSnapshot();
        var broker = NewBroker(NewBus());

        Assert.ThrowsExactly<InvalidOperationException>(() => broker.RestoreExchanges(snapshot));
        Assert.ThrowsExactly<ArgumentNullException>(() => broker.RestoreExchanges(null!));
    }

    [TestMethod]
    public async Task ExchangesKeptInFiles_ResetSnapshotAndRestore_AreRefused()
    {
        var fileSystem = new InMemoryContentFileSystem(new ManualTimeProvider());
        var broker = NewBroker(NewBus());
        var snapshot = broker.TakeExchangeSnapshot();
        Assert.IsFalse(broker.KeepsExchangesInFiles);
        await broker.LoadExchangesAsync(FilesOn(fileSystem), TestContext.CancellationToken);

        Assert.IsTrue(broker.KeepsExchangesInFiles);
        Assert.ThrowsExactly<InvalidOperationException>(broker.ResetExchanges);
        Assert.ThrowsExactly<InvalidOperationException>(() => broker.TakeExchangeSnapshot());
        Assert.ThrowsExactly<InvalidOperationException>(() => broker.RestoreExchanges(snapshot));
    }
}
