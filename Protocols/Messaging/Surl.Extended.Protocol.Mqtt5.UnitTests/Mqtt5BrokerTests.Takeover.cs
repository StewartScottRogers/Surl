using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Mqtt5.UnitTests.Mqtt5BrokerExchange;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// Session takeover: a <c>CONNECT</c> for a connected client identifier (ADR-0093 decision 1;
/// MQTT 5.0 <c>[MQTT-3.1.4-3]</c> and 3.1.2.5).
/// </summary>
public sealed partial class Mqtt5BrokerTests
{
    // DISCONNECT 0x8E Session taken over, no properties.
    private static readonly byte[] DisconnectSessionTakenOver = [0xE0, 0x01, 0x8E];

    [TestMethod]
    public async Task ServeAsync_ConnectForAConnectedIdentifier_DisconnectsTheOlderWithSessionTakenOverAndAcceptsTheNewer()
    {
        var clock = new ManualTimeProvider();
        var state = NewState(clock);
        var broker = Broker(state);
        var older = await Mqtt5TestClient.ConnectAsync(broker, Context(clock, TestContext.CancellationToken), Connect("s"));

        var newer = await Mqtt5TestClient.ConnectAsync(broker, Context(clock, TestContext.CancellationToken), Connect("s"));

        CollectionAssert.AreEqual(DisconnectSessionTakenOver, await older.ServingEndedAsync());
        CollectionAssert.AreEqual(ConnackSuccess, newer.ConnectAcknowledgement);
        Assert.IsTrue(state.IsConnected("s"));
        Assert.IsEmpty(await newer.DisconnectAsync());
        Assert.IsFalse(state.IsConnected("s"));
    }

    [TestMethod]
    public async Task ServeAsync_TakeoverWithCleanStart0_ResumesTheOlderConnectionsSession()
    {
        var clock = new ManualTimeProvider();
        var broker = Broker(NewState(clock));
        var older = await Mqtt5TestClient.ConnectAsync(broker, Context(clock, TestContext.CancellationToken), Connect("s", properties: [SessionExpiry(60)]));
        await SubscribeAsync(older, "t", 1);

        var newer = await Mqtt5TestClient.ConnectAsync(broker, Context(clock, TestContext.CancellationToken), Connect("s", properties: [SessionExpiry(60)], cleanStart: false));
        var publisher = await ConnectClientAsync(broker, "p", clock);
        publisher.Send(Publish("t", "a", 1, 1));

        CollectionAssert.AreEqual(DisconnectSessionTakenOver, await older.ServingEndedAsync());
        CollectionAssert.AreEqual(ConnackSessionPresent, newer.ConnectAcknowledgement);
        CollectionAssert.AreEqual(PubackSuccess, await publisher.ReadPacketAsync());
        CollectionAssert.AreEqual(new byte[] { 0x32, 0x07, 0x00, 0x01, 0x74, 0x00, 0x01, 0x00, 0x61 }, await newer.ReadPacketAsync());
        newer.Send([0x40, 0x02, 0x00, 0x01]);
        Assert.IsEmpty(await newer.DisconnectAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
    }

    [TestMethod]
    [DataRow(true, DisplayName = "New Clean Start 1")]
    [DataRow(false, DisplayName = "New Clean Start 0")]
    public async Task ServeAsync_TakeoverOfAConnectionWithWillDelay0_PublishesItsWillAtOnce(bool cleanStart)
    {
        var clock = new ManualTimeProvider();
        var published = PublishedBy(out var state, clock);
        var broker = Broker(state);
        var older = await Mqtt5TestClient.ConnectAsync(broker, Context(clock, TestContext.CancellationToken), Connect("s", properties: [SessionExpiry(60)], will: Will()));

        var newer = await Mqtt5TestClient.ConnectAsync(broker, Context(clock, TestContext.CancellationToken), Connect("s", properties: [SessionExpiry(60)], cleanStart: cleanStart));

        CollectionAssert.AreEqual(DisconnectSessionTakenOver, await older.ServingEndedAsync());
        Assert.HasCount(1, published);
        Assert.AreEqual("w", published.Single().Topic);
        Assert.IsEmpty(await newer.DisconnectAsync());
    }

    [TestMethod]
    [DataRow(true, 1, DisplayName = "Clean Start 1 ends the older session: its Will is published")]
    [DataRow(false, 0, DisplayName = "Clean Start 0 continues it before the Will Delay passes: its Will is not published")]
    public async Task ServeAsync_TakeoverOfAConnectionWithAWillDelay_SettlesItsWillByCleanStart(bool cleanStart, int expectedPublished)
    {
        var clock = new ManualTimeProvider();
        var published = PublishedBy(out var state, clock);
        var broker = Broker(state);
        var older = await Mqtt5TestClient.ConnectAsync(broker, Context(clock, TestContext.CancellationToken), Connect("s", properties: [SessionExpiry(60)], will: Will(WillDelay(30))));

        var newer = await Mqtt5TestClient.ConnectAsync(broker, Context(clock, TestContext.CancellationToken), Connect("s", properties: [SessionExpiry(60)], cleanStart: cleanStart));
        CollectionAssert.AreEqual(DisconnectSessionTakenOver, await older.ServingEndedAsync());
        Assert.HasCount(expectedPublished, published);
        Assert.IsEmpty(await newer.DisconnectAsync());
        clock.Advance(TimeSpan.FromMinutes(5));

        Assert.HasCount(expectedPublished, published);
    }

    [TestMethod]
    public async Task ServeAsync_OlderConnectionNotEndingWithinTheHeadTimeout_ClosesTheNewerWithNoReply()
    {
        var clock = new ManualTimeProvider();
        var state = NewState(clock);
        var broker = Broker(state);
        using var olderCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var olderServing = broker.ServeAsync(new Mqtt5StalledWriteConnection(Connect("s")), Context(clock, olderCancellation.Token));
        Assert.IsTrue(state.IsConnected("s"));
        var log = new RecordingExchangeLog();
        var newer = ConnectionSending(staysOpen: true, Connect("s"));
        var newerServing = broker.ServeAsync(newer, Context(clock, TestContext.CancellationToken, log: log));
        Assert.IsFalse(newerServing.IsCompleted);

        clock.Advance(ExchangeLimits.Default.HeadTimeout);
        await newerServing;
        await olderCancellation.CancelAsync();

        Assert.IsEmpty(newer.WrittenBytes);
        Assert.Contains("The connection whose session this CONNECT takes over did not end within the head timeout; closed with no reply.", log.Notes);
        await Assert.ThrowsAsync<OperationCanceledException>(() => olderServing);
        Assert.IsFalse(state.IsConnected("s"));
        Assert.IsFalse(state.HasSession("s"));
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledWhileTheOlderConnectionEnds_Throws()
    {
        var clock = new ManualTimeProvider();
        var state = NewState(clock);
        var broker = Broker(state);
        using var olderCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var olderServing = broker.ServeAsync(new Mqtt5StalledWriteConnection(Connect("s")), Context(clock, olderCancellation.Token));
        using var newerCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var newerServing = broker.ServeAsync(ConnectionSending(staysOpen: true, Connect("s")), Context(clock, newerCancellation.Token));

        await newerCancellation.CancelAsync();
        await olderCancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => newerServing);
        await Assert.ThrowsAsync<OperationCanceledException>(() => olderServing);
        Assert.IsFalse(state.IsConnected("s"));
    }
}
