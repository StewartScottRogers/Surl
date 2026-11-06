using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.MessageBus;

/// <summary>
/// Seeding, resetting, snapshotting and restoring the bus (ADR-0124 decision 3, BL-801): a seed
/// takes the publish or enqueue path a broker uses but no access grant or fault rule refuses it; a
/// reset leaves the configured topics and queues empty; a restore gives back retained messages,
/// queue contents and log offsets; live subscriptions stay and unacknowledged deliveries are
/// dropped; a bus on disk refuses all but seeding.
/// </summary>
[TestClass]
public sealed class MessageBusSeedResetSnapshotTests
{
    private readonly SettableTimeProvider clock = new();
    private readonly RecordingSurlEventPublisher events = new();

    [TestMethod]
    public void SeedPublish_ARetainedMessage_IsDeliveredToALaterSubscriberAndPublishesSeeded()
    {
        var bus = new MessageBus(clock, events: events);

        var result = bus.SeedPublish(Message("a/b", "hi") with { Retain = true });
        using var connection = bus.Connect();
        var subscribed = connection.Subscribe("a/#", _ => { }, includeRetained: true);

        Assert.AreEqual(MessageBusOutcome.Succeeded, result.Outcome);
        Assert.AreEqual("hi", Text(subscribed.RetainedMessages.Single()));
        CollectionAssert.AreEqual(
            new[] { Change(StoreChange.Written, "a/b", 2), Change(StoreChange.Published, "a/b", 2), Change(StoreChange.Seeded, "a/b", 2) },
            events.Events.ToArray());
    }

    [TestMethod]
    public void SeedPublish_ReachesALiveSubscriptionAndABoundQueuesConsumer()
    {
        var bus = new MessageBus(clock, events: events);
        using var connection = bus.Connect();
        var live = new List<BusMessage>();
        connection.Subscribe("a/+", live.Add);
        connection.DeclareQueue("q");
        connection.BindQueue("q", "a/#");
        var consumed = new List<BusQueueDelivery>();
        connection.Consume("q", consumed.Add).Consumer!.Start();

        var result = bus.SeedPublish(Message("a/b", "x"));

        Assert.AreEqual(1, result.DeliveredSubscriptionCount);
        Assert.AreEqual(1, result.QueuedCount);
        Assert.AreEqual("x", Text(live.Single()));
        Assert.AreEqual("x", Text(consumed.Single().Message));
        Assert.Contains(Change(StoreChange.Seeded, "a/b", 1), events.Events.ToList());
    }

    [TestMethod]
    public void SeedPublish_IsNeitherRefusedByAccessGrantsNorByAnOutOfSpaceRule()
    {
        var configuration = new MessageBusConfiguration { AccessGrants = [new TopicAccessGrant("other", "alice", TopicAccessRights.Publish)] };
        var plan = new SpendingFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace));
        var bus = new MessageBus(clock, configuration: configuration, events: events, faults: plan);
        Assert.AreEqual(MessageBusOutcome.AccessDenied, bus.Publish(Message("a", "x")).Outcome);

        var result = bus.SeedPublish(Message("a", "x"));

        Assert.AreEqual(MessageBusOutcome.Succeeded, result.Outcome);
        Assert.AreEqual(1, bus.GetLogOffsets("a").HighWatermark);
        Assert.IsFalse(events.Events.OfType<FaultAppliedEvent>().Any());
    }

    [TestMethod]
    public void SeedPublish_Refused_PublishesNoSeeded()
    {
        var bus = new MessageBus(clock, events: events);

        Assert.AreEqual(MessageBusOutcome.InvalidTopic, bus.SeedPublish(Message("a/+", "x")).Outcome);
        Assert.ThrowsExactly<ArgumentNullException>(() => bus.SeedPublish(null!));
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public void SeedEnqueue_ReachesTheQueuesConsumerAndPublishesSeededUnderTheQueuesName()
    {
        var configuration = new MessageBusConfiguration { Queues = [new DeclaredQueue("q")] };
        var plan = new SpendingFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace));
        var bus = new MessageBus(clock, configuration: configuration, events: events, faults: plan);
        using var connection = bus.Connect();
        var consumed = new List<BusQueueDelivery>();
        connection.Consume("q", consumed.Add).Consumer!.Start();

        var outcome = bus.SeedEnqueue("q", Message("t", "abc"));

        Assert.AreEqual(MessageBusOutcome.Succeeded, outcome);
        Assert.AreEqual("abc", Text(consumed.Single().Message));
        CollectionAssert.AreEqual(
            new[] { Change(StoreChange.Enqueued, "$queue/q", 3), Change(StoreChange.Seeded, "$queue/q", 3), Change(StoreChange.Delivered, "$queue/q", 3) },
            events.Events.ToArray());
    }

    [TestMethod]
    public void SeedEnqueue_Refusals_AnswerTheirOutcomesAndPublishNoSeeded()
    {
        var configuration = new MessageBusConfiguration { Queues = [new DeclaredQueue("q")] };
        var bus = new MessageBus(clock, maxMessageBytes: 3, maxMessagesPerQueue: 1, configuration: configuration, events: events);

        Assert.AreEqual(MessageBusOutcome.MessageTooLarge, bus.SeedEnqueue("q", Message("t", "four")));
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, bus.SeedEnqueue("none", Message("t", "x")));
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.SeedEnqueue("q", Message("t", "x") with { ExpiresAt = clock.UtcNow }));
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.SeedEnqueue("q", Message("t", "y")));
        Assert.AreEqual(MessageBusOutcome.QueueFull, bus.SeedEnqueue("q", Message("t", "z")));
        Assert.ThrowsExactly<ArgumentNullException>(() => bus.SeedEnqueue(null!, Message("t", "x")));
        Assert.ThrowsExactly<ArgumentNullException>(() => bus.SeedEnqueue("q", null!));

        Assert.HasCount(1, events.Events.OfType<StoreChangedEvent>().Where(change => change.Change == StoreChange.Seeded));
    }

    [TestMethod]
    public void Reset_LeavesTheConfiguredTopicsAndQueuesEmptyAndDropsEverythingElseUnused()
    {
        var configuration = new MessageBusConfiguration { Topics = [new DeclaredTopic("declared")], Queues = [new DeclaredQueue("q")] };
        var bus = new MessageBus(clock, configuration: configuration, events: events);
        using (var client = bus.Connect())
        {
            client.DeclareQueue("abandoned", new BusQueueFlags(Durable: true));
        }

        bus.Publish(Message("declared", "x") with { Retain = true });
        bus.Publish(Message("runtime", "y"));
        bus.PublishToQueue("q", Message("t", "z"));
        bus.PublishToQueue("abandoned", Message("t", "w"));
        var mark = events.Events.Count;

        bus.Reset();

        CollectionAssert.AreEqual(new[] { "declared" }, bus.GetLogTopics().ToArray());
        Assert.AreEqual(new BusLogOffsets(MessageBusOutcome.Succeeded, 0, 0), bus.GetLogOffsets("declared"));
        using var connection = bus.Connect();
        Assert.IsEmpty(connection.Subscribe("#", _ => { }, includeRetained: true).RetainedMessages);
        Assert.AreEqual(new BusQueueInspectResult(MessageBusOutcome.Succeeded, 0, 0), connection.InspectQueue("q"));
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, connection.InspectQueue("abandoned").Outcome);
        CollectionAssert.AreEqual(new[] { Change(StoreChange.Reset, MessageBus.WholeBusChangeName, null) }, events.Events.Skip(mark).ToArray());
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.Publish(Message("declared", "after")).Outcome);
    }

    [TestMethod]
    public void Reset_KeepsLiveSubscriptionsAndQueuesInUse_AndDropsUnacknowledgedDeliveries()
    {
        var bus = new MessageBus(clock, events: events);
        using var connection = bus.Connect();
        var live = new List<BusMessage>();
        connection.Subscribe("a", live.Add);
        connection.DeclareQueue("consumed");
        connection.DeclareQueue("exclusive", new BusQueueFlags(Exclusive: true));
        bus.PublishToQueue("exclusive", Message("t", "e"));
        var consumed = new List<BusQueueDelivery>();
        var consumer = connection.Consume("consumed", consumed.Add).Consumer!;
        consumer.Start();
        bus.PublishToQueue("consumed", Message("t", "before"));

        bus.Reset();

        Assert.AreEqual(MessageBusOutcome.UnknownDeliveryTag, consumer.Acknowledge(consumed[0].DeliveryTag));
        Assert.AreEqual(new BusQueueInspectResult(MessageBusOutcome.Succeeded, 0, 0), connection.InspectQueue("exclusive"));
        bus.PublishToQueue("consumed", Message("t", "after"));
        bus.Publish(Message("a", "live"));
        CollectionAssert.AreEqual(new[] { "before", "after" }, consumed.Select(delivery => Text(delivery.Message)).ToArray());
        Assert.AreEqual("live", Text(live.Single()));
        Assert.AreEqual(MessageBusOutcome.Succeeded, consumer.Acknowledge(consumed[1].DeliveryTag));
    }

    [TestMethod]
    public void SnapshotChangeRestore_GivesBackRetainedMessagesQueueContentsAndLogOffsets()
    {
        var bus = new MessageBus(clock, maxLogBytesPerTopic: MessageBus.LogRecordOverheadBytes + 1, events: events);
        bus.Publish(Message("r", "kept") with { Retain = true });
        bus.Publish(Message("log", "1"));
        bus.Publish(Message("log", "2"));
        using (var setup = bus.Connect())
        {
            setup.DeclareQueue("q", new BusQueueFlags(Durable: true));
            setup.BindQueue("q", "bound/#");
        }

        bus.PublishToQueue("q", Message("t", "one"));
        bus.PublishToQueue("q", Message("t", "two"));
        var snapshot = bus.TakeSnapshot();

        bus.Publish(Message("r", "changed") with { Retain = true });
        bus.Publish(Message("log", "3"));
        bus.Publish(Message("new", "n"));
        using (var change = bus.Connect())
        {
            change.DeleteQueue("q");
        }

        var mark = events.Events.Count;
        bus.Restore(snapshot);

        CollectionAssert.AreEqual(new[] { Change(StoreChange.Restored, MessageBus.WholeBusChangeName, null) }, events.Events.Skip(mark).ToArray());
        using var connection = bus.Connect();
        Assert.AreEqual("kept", Text(connection.Subscribe("r", _ => { }, includeRetained: true).RetainedMessages.Single()));
        Assert.AreEqual(new BusLogOffsets(MessageBusOutcome.Succeeded, 1, 2), bus.GetLogOffsets("log"));
        Assert.AreEqual("2", Text(bus.ReadLog("log", 1, 100).Records.Single().Message));
        CollectionAssert.DoesNotContain(bus.GetLogTopics().ToArray(), "new");
        var received = new List<BusQueueDelivery>();
        connection.Consume("q", received.Add).Consumer!.Start();
        bus.Publish(Message("bound/x", "three"));
        CollectionAssert.AreEqual(new[] { "one", "two", "three" }, received.Select(delivery => Text(delivery.Message)).ToArray());
    }

    [TestMethod]
    public void Restore_AnUnacknowledgedMessage_ComesBackRedeliveredToTheLiveConsumer()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        connection.DeclareQueue("later");
        var received = new List<BusQueueDelivery>();
        var consumer = connection.Consume("q", received.Add).Consumer!;
        consumer.Start();
        bus.PublishToQueue("q", Message("t", "held"));
        var snapshot = bus.TakeSnapshot();
        consumer.Acknowledge(received[0].DeliveryTag);
        connection.DeclareQueue("after");
        var laterReceived = new List<BusQueueDelivery>();
        connection.Consume("later", laterReceived.Add).Consumer!.Start();

        bus.Restore(snapshot);

        Assert.HasCount(2, received);
        Assert.IsTrue(received[1].Redelivered);
        Assert.AreEqual("held", Text(received[1].Message));
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, connection.InspectQueue("after").Outcome);
        Assert.AreEqual(MessageBusOutcome.Succeeded, connection.InspectQueue("later").Outcome);
    }

    [TestMethod]
    public void TakeSnapshot_HoldsTheBytesTheBoundsCount_AndRefusesPastMaxBytes()
    {
        var bus = new MessageBus(clock);
        bus.Publish(Message("a", "abc") with { Retain = true });
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        bus.PublishToQueue("q", Message("t", "de"));
        var expected = 3 + MessageBus.LogRecordOverheadBytes + 3 + 2;

        Assert.AreEqual(expected, bus.TakeSnapshot(expected).ByteCount);
        Assert.ThrowsExactly<InvalidOperationException>(() => bus.TakeSnapshot(expected - 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => bus.TakeSnapshot(-1));
    }

    [TestMethod]
    public void Restore_ANullSnapshotOrOneFromAnotherBus_IsRefused()
    {
        var bus = new MessageBus(clock);
        var other = new MessageBus(clock).TakeSnapshot();

        Assert.ThrowsExactly<ArgumentNullException>(() => bus.Restore(null!));
        Assert.ThrowsExactly<ArgumentException>(() => bus.Restore(other));
    }

    [TestMethod]
    public async Task APersistedBus_RefusesResetSnapshotAndRestore_ButIsSeededAndSaved()
    {
        var fixture = new PersistedBusFixture();
        var snapshot = fixture.NewBus().TakeSnapshot();
        var bus = await fixture.LoadBusAsync();

        Assert.ThrowsExactly<InvalidOperationException>(bus.Reset);
        Assert.ThrowsExactly<InvalidOperationException>(() => bus.TakeSnapshot());
        Assert.ThrowsExactly<ArgumentException>(() => bus.Restore(snapshot));
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.SeedPublish(PersistedBusFixture.Message("a", "seeded") with { Retain = true }).Outcome);
        Assert.AreEqual(BusSaveResult.Saved, await bus.SaveChangesAsync());

        var reloaded = await fixture.LoadBusAsync();
        using var connection = reloaded.Connect();
        Assert.AreEqual("seeded", Text(connection.Subscribe("a", _ => { }, includeRetained: true).RetainedMessages.Single()));
    }

    [TestMethod]
    public async Task APersistedBus_RefusesToRestoreItsOwnSnapshotOnceLoaded()
    {
        var fixture = new PersistedBusFixture();
        var bus = fixture.NewBus();
        var snapshot = bus.TakeSnapshot();
        Assert.AreEqual(BusSaveResult.Saved, await bus.LoadAsync(fixture.Files));

        Assert.ThrowsExactly<InvalidOperationException>(() => bus.Restore(snapshot));
    }

    private static StoreChangedEvent Change(StoreChange change, string name, long? byteCount) =>
        new(StoreKind.Bus, change, name, byteCount, ExchangeId: null);

    private static BusMessage Message(string topic, string payload) => new(topic, Encoding.UTF8.GetBytes(payload));

    private static string Text(BusMessage message) => Encoding.UTF8.GetString(message.Payload.Span);
}
