using System.Collections.Concurrent;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Mqtt5.UnitTests.Mqtt5BrokerExchange;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// Sessions and the client's DISCONNECT (ADR-0093 decisions 3 and 6; MQTT 5.0 4.1, 3.1.2.4,
/// 3.1.2.11.2 and 3.14).
/// </summary>
public sealed partial class Mqtt5BrokerTests
{
    // CONNACK Success with Session Present 1 and the default properties.
    private static readonly byte[] ConnackSessionPresent =
        [0x20, 0x0E, 0x01, 0x00, 0x0B, 0x21, 0x00, 0x64, 0x27, 0x00, 0x10, 0x00, 0x00, 0x22, 0x00, 0x0A];

    // PUBACK for Packet Identifier 1: Success, its Reason Code left off; and 0x10 No matching subscribers.
    private static readonly byte[] PubackSuccess = [0x40, 0x02, 0x00, 0x01];
    private static readonly byte[] PubackNoMatchingSubscribers = [0x40, 0x03, 0x00, 0x01, 0x10];

    [TestMethod]
    public async Task ServeAsync_CleanStart0WithAKeptSession_ResumesItWithSessionPresentAndDeliversItsQueuedMessages()
    {
        var clock = new ManualTimeProvider();
        var broker = Broker(NewState(clock));
        await KeepSubscribedSessionAsync(broker, clock);
        var publisher = await ConnectClientAsync(broker, "p", clock);
        publisher.Send(Publish("t", "z", 0, 0), Publish("t", "a", 1, 1));
        CollectionAssert.AreEqual(PubackSuccess, await publisher.ReadPacketAsync());

        var resumed = await Mqtt5TestClient.ConnectAsync(broker, Context(clock, TestContext.CancellationToken), Connect("s", properties: [SessionExpiry(60)], cleanStart: false));

        CollectionAssert.AreEqual(ConnackSessionPresent, resumed.ConnectAcknowledgement);
        CollectionAssert.AreEqual(new byte[] { 0x32, 0x07, 0x00, 0x01, 0x74, 0x00, 0x01, 0x00, 0x61 }, await resumed.ReadPacketAsync());
        resumed.Send([0x40, 0x02, 0x00, 0x01]);
        Assert.IsEmpty(await resumed.DisconnectAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_ResumedSessionWithAPublishUnacknowledged_ResendsItWithDup()
    {
        var clock = new ManualTimeProvider();
        var broker = Broker(NewState(clock));
        var subscriber = await Mqtt5TestClient.ConnectAsync(broker, Context(clock, TestContext.CancellationToken), Connect("s", properties: [SessionExpiry(60)]));
        await SubscribeAsync(subscriber, "t", 1);
        var publisher = await ConnectClientAsync(broker, "p", clock);
        publisher.Send(Publish("t", "a", 1, 1));
        _ = await publisher.ReadPacketAsync();
        CollectionAssert.AreEqual(new byte[] { 0x32, 0x07, 0x00, 0x01, 0x74, 0x00, 0x01, 0x00, 0x61 }, await subscriber.ReadPacketAsync());
        subscriber.Abort();
        await subscriber.ServingEndedAsync();

        var resumed = await Mqtt5TestClient.ConnectAsync(broker, Context(clock, TestContext.CancellationToken), Connect("s", properties: [SessionExpiry(60)], cleanStart: false));

        CollectionAssert.AreEqual(new byte[] { 0x3A, 0x07, 0x00, 0x01, 0x74, 0x00, 0x01, 0x00, 0x61 }, await resumed.ReadPacketAsync());
        resumed.Send([0x40, 0x02, 0x00, 0x01]);
        Assert.IsEmpty(await resumed.DisconnectAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_CleanStart1WithAKeptSession_DiscardsIt()
    {
        var clock = new ManualTimeProvider();
        var broker = Broker(NewState(clock));
        await KeepSubscribedSessionAsync(broker, clock);

        var fresh = await Mqtt5TestClient.ConnectAsync(broker, Context(clock, TestContext.CancellationToken), Connect("s", properties: [SessionExpiry(60)]));
        var publisher = await ConnectClientAsync(broker, "p", clock);
        publisher.Send(Publish("t", "a", 1, 1));

        CollectionAssert.AreEqual(ConnackSuccess, fresh.ConnectAcknowledgement);
        CollectionAssert.AreEqual(PubackNoMatchingSubscribers, await publisher.ReadPacketAsync());
        Assert.IsEmpty(await fresh.DisconnectAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_KeptSessionPastItsExpiry_EndsIt()
    {
        var clock = new ManualTimeProvider();
        var state = NewState(clock);
        var broker = Broker(state);
        await KeepSubscribedSessionAsync(broker, clock, expirySeconds: 10);
        Assert.IsTrue(state.HasSession("s"));

        clock.Advance(TimeSpan.FromSeconds(10));
        var publisher = await ConnectClientAsync(broker, "p", clock);
        publisher.Send(Publish("t", "a", 1, 1));

        Assert.IsFalse(state.HasSession("s"));
        CollectionAssert.AreEqual(PubackNoMatchingSubscribers, await publisher.ReadPacketAsync());
        Assert.IsEmpty(await publisher.DisconnectAsync());
    }

    [TestMethod]
    public async Task ServeAsync_KeptSessionLoggedIntoByAnotherAccount_IsNotResumed()
    {
        var clock = new ManualTimeProvider();
        var broker = Broker(NewState(clock), new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted));
        await ServeAsync(broker, Context(clock, TestContext.CancellationToken), Connect("s", properties: [SessionExpiry(60)], userName: "alice", password: [1]), DisconnectNormally);

        var connection = await ServeAsync(broker, Context(clock, TestContext.CancellationToken), Connect("s", properties: [SessionExpiry(60)], userName: "bob", password: [1], cleanStart: false), DisconnectNormally);

        CollectionAssert.AreEqual(ConnackSuccess, connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_NewSessionPastTheKeptSessionBound_AnswersQuotaExceeded()
    {
        var clock = new ManualTimeProvider();
        var state = new Mqtt5BrokerState(new Surl.MessageBus.MessageBus(clock)) { MaxKeptSessions = 1 };
        var broker = Broker(state);
        await ServeAsync(broker, Context(clock, TestContext.CancellationToken), Connect("a", properties: [SessionExpiry(60)]), DisconnectNormally);

        var refused = await ServeAsync(broker, Context(clock, TestContext.CancellationToken), Connect("b", properties: [SessionExpiry(60)]));
        var unkept = await ServeAsync(broker, Context(clock, TestContext.CancellationToken), Connect("b"), DisconnectNormally);

        CollectionAssert.AreEqual(ConnackRefused(0x97), refused.WrittenBytes);
        Assert.IsFalse(state.IsConnected("b"));
        CollectionAssert.AreEqual(ConnackSuccess, unkept.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_DisconnectWithWillMessage_PublishesTheWillAfterItsDelay()
    {
        var clock = new ManualTimeProvider();
        var published = PublishedBy(out var state, clock);
        var connect = Connect(properties: [SessionExpiry(60)], will: Will(WillDelay(5)));

        await ServeAsync(Broker(state), Context(clock, TestContext.CancellationToken), connect, [0xE0, 0x01, 0x04]);
        Assert.IsEmpty(published);
        clock.Advance(TimeSpan.FromSeconds(5));

        Assert.HasCount(1, published);
        Assert.IsTrue(state.HasSession("c1"));
    }

    [TestMethod]
    public async Task ServeAsync_NormalDisconnectWithAKeptSession_DoesNotPublishTheWill()
    {
        var clock = new ManualTimeProvider();
        var published = PublishedBy(out var state, clock);
        var connect = Connect(properties: [SessionExpiry(60)], will: Will(WillDelay(5)));

        await ServeAsync(Broker(state), Context(clock, TestContext.CancellationToken), connect, DisconnectNormally);
        clock.Advance(TimeSpan.FromMinutes(2));

        Assert.IsEmpty(published);
        Assert.IsFalse(state.HasSession("c1"));
    }

    [TestMethod]
    public async Task ServeAsync_DisconnectRaisingASessionExpiryOf0_DisconnectsWithProtocolErrorAndPublishesTheWill()
    {
        var published = PublishedBy(out var state);

        var connection = await ServeAsync(Broker(state), Context(new ManualTimeProvider(), TestContext.CancellationToken), Connect(will: Will()), DisconnectWithSessionExpiry(30));

        CollectionAssert.AreEqual(Disconnect(0x82), AfterConnackSuccess(connection.WrittenBytes));
        Assert.HasCount(1, published);
        Assert.IsFalse(state.HasSession("c1"));
    }

    [TestMethod]
    [DataRow(0u, false, DisplayName = "Lowered to 0: the session ends with the connection")]
    [DataRow(0u, true, DisplayName = "Lowered to 0 from 0: allowed")]
    [DataRow(7200u, false, DisplayName = "Raised past the cap: kept for the cap")]
    public async Task ServeAsync_DisconnectChangingTheSessionExpiry_KeepsTheSessionForTheNewInterval(uint seconds, bool connectWithoutExpiry)
    {
        var clock = new ManualTimeProvider();
        var state = NewState(clock);
        var connect = connectWithoutExpiry ? Connect() : Connect(properties: [SessionExpiry(60)]);

        var connection = await ServeAsync(Broker(state), Context(clock, TestContext.CancellationToken), connect, DisconnectWithSessionExpiry(seconds));
        var keptAtFirst = state.HasSession("c1");
        clock.Advance(TimeSpan.FromSeconds(Mqtt5Broker.MaxSessionExpirySeconds));

        CollectionAssert.AreEqual(ConnackSuccess, connection.WrittenBytes);
        Assert.AreEqual(seconds != 0, keptAtFirst);
        Assert.IsFalse(state.HasSession("c1"));
    }

    // DISCONNECT 0x00 with a Session Expiry Interval (3.14.2.2.2): 0x11 and four bytes.
    private static byte[] DisconnectWithSessionExpiry(uint seconds) =>
        [0xE0, 0x07, 0x00, 0x05, 0x11, (byte)(seconds >> 24), (byte)(seconds >> 16), (byte)(seconds >> 8), (byte)seconds];

    private static ConcurrentQueue<BusMessage> PublishedBy(out Mqtt5BrokerState state, TimeProvider clock)
    {
        state = NewState(clock);
        return PublishedOn(state);
    }

    // Client s subscribes to t at QoS 1 with a Session Expiry Interval, then disconnects normally.
    private async Task KeepSubscribedSessionAsync(Mqtt5Broker broker, TimeProvider clock, uint expirySeconds = 60)
    {
        var subscriber = await Mqtt5TestClient.ConnectAsync(broker, Context(clock, TestContext.CancellationToken), Connect("s", properties: [SessionExpiry(expirySeconds)]));
        await SubscribeAsync(subscriber, "t", 1);
        Assert.IsEmpty(await subscriber.DisconnectAsync());
    }
}
