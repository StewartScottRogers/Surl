using static Surl.Extended.Protocol.Mqtt5.UnitTests.Mqtt5BrokerExchange;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>A session's queued messages and a Will under reset, snapshot and restore (ADR-0159 decision 2; BL-1171).</summary>
public sealed partial class Mqtt5BrokerTests
{
    // CONNACK Success with Session Present 0 and the default properties.
    private static readonly byte[] ConnackNoSessionPresent =
        [0x20, 0x0E, 0x00, 0x00, 0x0B, 0x21, 0x00, 0x64, 0x27, 0x00, 0x10, 0x00, 0x00, 0x22, 0x00, 0x0A];

    [TestMethod]
    public async Task ResetSessions_OfflineSessionWithAQueuedMessage_IsGoneWhenTheClientReconnects()
    {
        var clock = new ManualTimeProvider();
        var broker = Broker(NewState(clock));
        await KeepSubscribedSessionAsync(broker, clock);
        var publisher = await ConnectClientAsync(broker, "p", clock);
        publisher.Send(Publish("t", "a", 1, 1));
        CollectionAssert.AreEqual(PubackSuccess, await publisher.ReadPacketAsync());

        broker.ResetSessions();
        var reconnected = await Mqtt5TestClient.ConnectAsync(broker, Context(clock, TestContext.CancellationToken), Connect("s", properties: [SessionExpiry(60)], cleanStart: false));

        CollectionAssert.AreEqual(ConnackNoSessionPresent, reconnected.ConnectAcknowledgement);
        Assert.IsEmpty(await reconnected.DisconnectAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
    }

    [TestMethod]
    public async Task SessionSnapshot_TakenThenReset_RestoresTheSessionWithItsSubscriptionAndNoQueuedMessage()
    {
        var clock = new ManualTimeProvider();
        var broker = Broker(NewState(clock));
        await KeepSubscribedSessionAsync(broker, clock);
        var snapshot = broker.TakeSessionSnapshot();
        broker.ResetSessions();

        broker.RestoreSessions(snapshot);
        var resumed = await Mqtt5TestClient.ConnectAsync(broker, Context(clock, TestContext.CancellationToken), Connect("s", properties: [SessionExpiry(60)], cleanStart: false));
        var publisher = await ConnectClientAsync(broker, "p", clock);
        publisher.Send(Publish("t", "a", 1, 1));

        CollectionAssert.AreEqual(ConnackSessionPresent, resumed.ConnectAcknowledgement);
        CollectionAssert.AreEqual(PubackSuccess, await publisher.ReadPacketAsync());
        CollectionAssert.AreEqual(new byte[] { 0x32, 0x07, 0x00, 0x01, 0x74, 0x00, 0x01, 0x00, 0x61 }, await resumed.ReadPacketAsync());
        resumed.Send([0x40, 0x02, 0x00, 0x01]);
        Assert.IsEmpty(await resumed.DisconnectAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
    }

    [TestMethod]
    public void RestoreSessions_NullSnapshot_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Broker().RestoreSessions(null!));
    }
}
