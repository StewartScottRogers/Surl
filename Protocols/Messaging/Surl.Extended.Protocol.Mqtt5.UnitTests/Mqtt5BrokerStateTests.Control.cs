using Surl.MessageBus;
using static Surl.Extended.Protocol.Mqtt5.UnitTests.Mqtt5BrokerExchange;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>Reset, snapshot and restore of the sessions and pending Wills (ADR-0159 decision 2; BL-1171).</summary>
public sealed partial class Mqtt5BrokerStateTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Reset_SessionWithNoConnection_EndsItWithItsExpiryTimer()
    {
        var clock = new ManualTimeProvider();
        var state = NewState(clock);
        var session = state.OpenSession("c", null, cleanStart: true, TimeSpan.FromSeconds(60), "mqtt5", out _)!;
        session.Offer(Queued("t"), 1, retain: false, []);
        state.CloseSession(session, clock);

        state.Reset();

        Assert.IsFalse(state.HasSession("c"));
        Assert.AreEqual(0, clock.PendingTimerCount);
    }

    [TestMethod]
    public async Task Reset_ConnectedSession_KeepsItButDropsQueuedDeliveriesAndUnacknowledgedPublishes()
    {
        var state = NewState();
        var session = state.OpenSession("c", null, cleanStart: true, TimeSpan.FromSeconds(60), "mqtt5", out _)!;
        session.Offer(Queued("old"), 1, retain: false, []);
        session.InFlight[1] = new(1, Mqtt5PacketType.PublishAcknowledgement, new(Queued("sent"), 1, Retain: false, []));

        state.Reset();
        session.Offer(Queued("new"), 1, retain: false, []);

        Assert.IsTrue(state.HasSession("c"));
        Assert.IsTrue(session.IsConnected);
        Assert.IsEmpty(session.InFlight);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        Assert.AreEqual("new", (await session.TakeAsync(timeout.Token)).Message.Topic);
    }

    [TestMethod]
    public void Reset_PendingWill_IsDiscardedWithoutBeingPublished()
    {
        var clock = new ManualTimeProvider();
        var state = NewState(clock);
        var published = PublishedOn(state);
        state.ScheduleWill("c", First, null, TimeSpan.FromSeconds(5), clock);

        state.Reset();
        clock.Advance(TimeSpan.FromSeconds(10));

        Assert.IsEmpty(published);
        Assert.AreEqual(0, clock.PendingTimerCount);
    }

    [TestMethod]
    public void Reset_ConnectedClient_StaysRegistered()
    {
        var state = NewState();
        state.RegisterClient("c");

        state.Reset();

        Assert.IsTrue(state.IsConnected("c"));
    }

    [TestMethod]
    public async Task Restore_AfterAResetAndAChange_GivesBackTheOfflineSessionsWithSubscriptionsAndNoQueuedDeliveries()
    {
        var clock = new ManualTimeProvider();
        var state = NewState(clock);
        var kept = OfflineSession(state, clock, "c", "mqtt5");
        kept.Subscriptions.Subscribe(new("t", 1, false, false, 0), 5, []);
        kept.Subscriptions.Subscribe(new("u", 0, false, false, 0), null, []);
        kept.Offer(Queued("t"), 1, retain: false, []);
        var snapshot = state.TakeSnapshot();
        state.Reset();
        OfflineSession(state, clock, "other", "mqtt5");

        state.Restore(snapshot);
        var resumed = state.OpenSession("c", null, cleanStart: false, TimeSpan.FromSeconds(60), "mqtt5", out var wasResumed)!;
        state.Publish(First with { Topic = "t", QualityOfService = 1 }, null, clock.GetUtcNow());

        Assert.IsGreaterThan(0, snapshot.ByteCount);
        Assert.IsTrue(wasResumed);
        Assert.IsFalse(state.HasSession("other"));
        Assert.AreEqual(1, resumed.Subscriptions.Copy().Count(subscription => subscription.Options.TopicFilter == "t" && subscription.Identifier == 5));
        Assert.AreEqual(1, resumed.Subscriptions.Copy().Count(subscription => subscription.Options.TopicFilter == "u" && subscription.Identifier is null));
        var delivery = await TryTakeAsync(resumed);
        Assert.AreEqual(5u, delivery!.SubscriptionIdentifiers.Single());
        Assert.IsNull(await TryTakeAsync(resumed));
    }

    [TestMethod]
    public void Restore_RestoredSession_ExpiresAfterItsFullExpiryInterval()
    {
        var clock = new ManualTimeProvider();
        var state = NewState(clock);
        OfflineSession(state, clock, "c", "mqtt5");
        var snapshot = state.TakeSnapshot();
        state.Reset();

        state.Restore(snapshot);
        clock.Advance(TimeSpan.FromSeconds(59));
        var keptJustBefore = state.HasSession("c");
        clock.Advance(TimeSpan.FromSeconds(2));

        Assert.IsTrue(keptJustBefore);
        Assert.IsFalse(state.HasSession("c"));
    }

    [TestMethod]
    public void TakeSnapshot_OnlyAConnectedSession_HoldsNothing()
    {
        var state = NewState();
        _ = state.OpenSession("c", null, cleanStart: true, TimeSpan.FromSeconds(60), "mqtt5", out _);

        var snapshot = state.TakeSnapshot();

        Assert.AreEqual(0, snapshot.ByteCount);
    }

    [TestMethod]
    public async Task Restore_ClientConnectedUnderASnapshotSessionsIdentifier_KeepsItsOwnSession()
    {
        var clock = new ManualTimeProvider();
        var state = NewState(clock);
        var offline = OfflineSession(state, clock, "c", "mqtt5");
        offline.Subscriptions.Subscribe(new("t", 1, false, false, 0), null, []);
        var snapshot = state.TakeSnapshot();
        state.Reset();
        var connected = state.OpenSession("c", null, cleanStart: true, TimeSpan.FromSeconds(60), "mqtt5", out _)!;

        state.Restore(snapshot);
        state.Publish(First with { Topic = "t", QualityOfService = 1 }, null, clock.GetUtcNow());

        Assert.IsTrue(connected.IsConnected);
        Assert.IsNull(await TryTakeAsync(connected));
    }

    [TestMethod]
    public void TakeSnapshot_SessionOfAnAccount_CountsTheAccountNameInItsBytes()
    {
        var clock = new ManualTimeProvider();
        var state = NewState(clock);
        var anonymous = state.TakeSnapshot().ByteCount;
        var session = state.OpenSession("c", "bob", cleanStart: true, TimeSpan.FromSeconds(60), "mqtt5", out _)!;
        state.CloseSession(session, clock);

        var snapshot = state.TakeSnapshot();

        Assert.AreEqual(anonymous + "c".Length + "bob".Length, snapshot.ByteCount);
        Assert.AreEqual("bob", session.Account);
    }

    [TestMethod]
    public void Restore_ASnapshotAnotherStateTook_Throws()
    {
        var snapshot = NewState().TakeSnapshot();

        Assert.ThrowsExactly<InvalidOperationException>(() => NewState().Restore(snapshot));
    }

    // The next waiting delivery, or null when none arrives within a moment.
    private async Task<Mqtt5Delivery?> TryTakeAsync(Mqtt5Session session)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(100));
        try
        {
            return await session.TakeAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!TestContext.CancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static Mqtt5Session OfflineSession(Mqtt5BrokerState state, TimeProvider clock, string clientIdentifier, string scheme)
    {
        var session = state.OpenSession(clientIdentifier, null, cleanStart: true, TimeSpan.FromSeconds(60), scheme, out _)!;
        state.CloseSession(session, clock);
        return session;
    }

    private static BusMessage Queued(string topic) => new(topic, new byte[1]) { QualityOfService = BusQualityOfService.AtLeastOnce };
}
