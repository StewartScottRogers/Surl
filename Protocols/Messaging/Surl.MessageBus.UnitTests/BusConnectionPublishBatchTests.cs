using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.MessageBus;

/// <summary>
/// <see cref="BusConnection.PublishBatch"/> publishes all or none (BL-961, ADR-0146 decision 1.6):
/// a batch that fits is published entry by entry exactly as the single publishes would be, and a
/// batch any entry of which would be refused publishes nothing and raises no store change.
/// </summary>
[TestClass]
public sealed class BusConnectionPublishBatchTests
{
    private readonly SettableTimeProvider clock = new();

    [TestMethod]
    public void PublishBatch_ThatFits_PublishesEveryEntryInOrderAsTheSinglePublishesWould()
    {
        var batchEvents = new RecordingSurlEventPublisher();
        var singleEvents = new RecordingSurlEventPublisher();
        var (batchBus, batchConnection, batchReceived) = Watched(batchEvents);
        var (singleBus, singleConnection, singleReceived) = Watched(singleEvents);
        BusBatchEntry[] entries =
        [
            BusBatchEntry.ToTopic(Message("t1", "1")),
            BusBatchEntry.ToQueue("q", Message("ignored", "2")),
            BusBatchEntry.ToTopic(Message("t2", "3") with { Retain = true }),
            BusBatchEntry.ToTopic(Message("t1", "4")),
        ];

        var result = batchConnection.PublishBatch(entries);
        BusPublishResult[] singleResults =
        [
            singleConnection.Publish(entries[0].Message),
            new(singleBus.PublishToQueue("q", entries[1].Message), 0) { QueuedCount = 1 },
            singleConnection.Publish(entries[2].Message),
            singleConnection.Publish(entries[3].Message),
        ];

        Assert.AreEqual(MessageBusOutcome.Succeeded, result.Outcome);
        Assert.IsNull(result.RefusedIndex);
        CollectionAssert.AreEqual(singleResults, result.Results.ToArray());
        CollectionAssert.AreEqual(new[] { "1", "3", "4" }, batchReceived);
        CollectionAssert.AreEqual(singleReceived, batchReceived);
        // Deliveries run after the batch's one hold of the lock, so its Delivered events follow
        // every other change of the batch; each kind keeps the single publishes' order.
        CollectionAssert.AreEqual(Changes(singleEvents, delivered: false), Changes(batchEvents, delivered: false));
        CollectionAssert.AreEqual(Changes(singleEvents, delivered: true), Changes(batchEvents, delivered: true));
        CollectionAssert.AreEqual(new[] { "1", "4" }, Drain(batchConnection, "b"));
        CollectionAssert.AreEqual(new[] { "2" }, Drain(batchConnection, "q"));
        CollectionAssert.AreEqual(new[] { "1", "4" }, LogPayloads(batchBus, "t1"));
    }

    [TestMethod]
    public void PublishBatch_ThirdEntryStoreFull_PublishesNothingAndRaisesNoStoreChange()
    {
        // Each one-byte record counts 17 bytes, so two fit in 40 and the third does not.
        var events = new RecordingSurlEventPublisher();
        var (bus, connection, received) = Watched(events, maxLogBytes: 40);
        var setUpEventCount = events.Events.Count;

        var result = connection.PublishBatch(
        [
            BusBatchEntry.ToTopic(Message("t2", "1") with { Retain = true }),
            BusBatchEntry.ToTopic(Message("t1", "2")),
            BusBatchEntry.ToTopic(Message("t1", "3")),
            BusBatchEntry.ToQueue("q", Message("ignored", "4")),
        ]);

        AssertRefused(MessageBusOutcome.StoreFull, 2, result);
        AssertNothingPublished(bus, connection, received, events.Events.Skip(setUpEventCount));
    }

    [TestMethod]
    public void PublishBatch_OneTopicsListPassingMaxLogBytesOnItsLast_IsRefusedWholeAndLeavesTheHighWatermark()
    {
        // Each one-byte record counts 17 bytes, so two fit in 40 and the third does not.
        var events = new RecordingSurlEventPublisher();
        var (bus, connection, received) = Watched(events, maxLogBytes: 40);
        bus.CreateLog("t3");

        var result = connection.PublishBatch(
        [
            BusBatchEntry.ToTopic(Message("t3", "1")),
            BusBatchEntry.ToTopic(Message("t3", "2")),
            BusBatchEntry.ToTopic(Message("t3", "3")),
        ]);

        AssertRefused(MessageBusOutcome.StoreFull, 2, result);
        Assert.AreEqual(0, bus.GetLogOffsets("t3").HighWatermark);
        Assert.IsEmpty(received);
    }

    [TestMethod]
    public void PublishBatch_ThirdEntryQueueFullOnADirectQueue_PublishesNothing()
    {
        var events = new RecordingSurlEventPublisher();
        var (bus, connection, received) = Watched(events, maxMessagesPerQueue: 2);
        var setUpEventCount = events.Events.Count;

        var result = connection.PublishBatch(
        [
            BusBatchEntry.ToTopic(Message("t1", "1")),
            BusBatchEntry.ToQueue("q", Message("ignored", "2")),
            BusBatchEntry.ToQueue("q", Message("ignored", "3")),
            BusBatchEntry.ToQueue("q", Message("ignored", "4")),
        ]);

        AssertRefused(MessageBusOutcome.QueueFull, 3, result);
        AssertNothingPublished(bus, connection, received, events.Events.Skip(setUpEventCount));
    }

    [TestMethod]
    public void PublishBatch_DirectQueuePastMaxQueuedBytesWithTheBatchsOwnCopies_IsQueueFull()
    {
        var bus = new MessageBus(clock, maxQueuedBytes: 3);
        using var connection = bus.Connect();
        connection.DeclareQueue("b");
        connection.DeclareQueue("q");
        connection.BindQueue("b", "t");

        var result = connection.PublishBatch([BusBatchEntry.ToTopic(Message("t", "12")), BusBatchEntry.ToQueue("q", Message("t", "34"))]);

        AssertRefused(MessageBusOutcome.QueueFull, 1, result);
        Assert.AreEqual(0, connection.InspectQueue("b").ReadyMessageCount);
    }

    [TestMethod]
    public void PublishBatch_ABoundQueueTooFullForACopy_StillPublishesAndCountsTheFullQueue()
    {
        var bus = new MessageBus(clock, maxMessagesPerQueue: 1);
        using var connection = bus.Connect();
        connection.DeclareQueue("b");
        connection.BindQueue("b", "t");

        var result = connection.PublishBatch([BusBatchEntry.ToTopic(Message("t", "1")), BusBatchEntry.ToTopic(Message("t", "2"))]);

        Assert.AreEqual(MessageBusOutcome.Succeeded, result.Outcome);
        Assert.AreEqual(new BusPublishResult(MessageBusOutcome.Succeeded, 0) { QueuedCount = 1, LogOffset = 0 }, result.Results[0]);
        Assert.AreEqual(new BusPublishResult(MessageBusOutcome.Succeeded, 0) { FullQueueCount = 1, LogOffset = 1 }, result.Results[1]);
        CollectionAssert.AreEqual(new[] { "1" }, Drain(connection, "b"));
    }

    [TestMethod]
    public void PublishBatch_NewTopicsPastMaxTopics_IsTooManyTopics()
    {
        var bus = new MessageBus(clock, maxTopics: 2);
        using var connection = bus.Connect();
        bus.Publish(Message("a", "1"));

        var result = connection.PublishBatch(
        [
            BusBatchEntry.ToTopic(Message("a", "2")),
            BusBatchEntry.ToTopic(Message("b", "3")),
            BusBatchEntry.ToTopic(Message("b", "4")),
            BusBatchEntry.ToTopic(Message("c", "5")),
        ]);

        AssertRefused(MessageBusOutcome.TooManyTopics, 3, result);
        CollectionAssert.AreEqual(new[] { "a" }, bus.GetLogTopics().ToArray());
    }

    [TestMethod]
    public void PublishBatch_RetainedPayloadsPastTheirBound_AreStoreFullCountingReplacementsAndRemovals()
    {
        var bus = new MessageBus(clock, maxRetainedPayloadBytes: 4);
        using var connection = bus.Connect();
        bus.Publish(Message("a", "12") with { Retain = true });

        var fits = connection.PublishBatch(
        [
            BusBatchEntry.ToTopic(Message("a", "123") with { Retain = true }),
            BusBatchEntry.ToTopic(Message("a", string.Empty) with { Retain = true }),
            BusBatchEntry.ToTopic(Message("b", "1234") with { Retain = true }),
        ]);
        var refused = connection.PublishBatch(
        [
            BusBatchEntry.ToTopic(Message("b", "1") with { Retain = true }),
            BusBatchEntry.ToTopic(Message("c", "1234") with { Retain = true }),
        ]);

        Assert.AreEqual(MessageBusOutcome.Succeeded, fits.Outcome);
        AssertRefused(MessageBusOutcome.StoreFull, 1, refused);
        var retained = connection.Subscribe("#", _ => { }, includeRetained: true).RetainedMessages;
        CollectionAssert.AreEqual(new[] { "b:1234" }, retained.Select(message => message.Topic + ":" + Payload(message)).ToArray());
    }

    [TestMethod]
    public void PublishBatch_PastMaxLogBytesPerTopic_DropsTheOldestRecordsAsTheSinglePublishesWould()
    {
        // Each one-byte record counts 17 bytes: a topic keeps two, and every log together three.
        var batchBus = new MessageBus(clock, maxLogBytesPerTopic: 34, maxLogBytes: 51);
        var singleBus = new MessageBus(clock, maxLogBytesPerTopic: 34, maxLogBytes: 51);
        using var connection = batchBus.Connect();
        batchBus.Publish(Message("t", "0"));
        batchBus.Publish(Message("t", "1"));
        singleBus.Publish(Message("t", "0"));
        singleBus.Publish(Message("t", "1"));
        string[] payloads = ["2", "3", "4", "5"];

        var result = connection.PublishBatch([.. payloads.Select(payload => BusBatchEntry.ToTopic(Message("t", payload)))]);
        foreach (var payload in payloads)
        {
            singleBus.Publish(Message("t", payload));
        }

        Assert.AreEqual(MessageBusOutcome.Succeeded, result.Outcome);
        Assert.AreEqual(singleBus.GetLogOffsets("t"), batchBus.GetLogOffsets("t"));
        CollectionAssert.AreEqual(new[] { "4", "5" }, LogPayloads(batchBus, "t"));
        AssertRefused(MessageBusOutcome.StoreFull, 1, connection.PublishBatch([BusBatchEntry.ToTopic(Message("u", "1")), BusBatchEntry.ToTopic(Message("v", "1"))]));
    }

    [TestMethod]
    public void PublishBatch_AnEntryItsOwnPublishWouldRefuseUnlocked_IsRefusedWithItsOwnOutcome()
    {
        var plan = new SpendingFaultPlan(
            new FaultRule("topic", FaultKind.OutOfSpace) { Path = "full" },
            new FaultRule("queue", FaultKind.OutOfSpace) { Path = MessageBus.QueueChangeName("fullq") });
        var configuration = new MessageBusConfiguration
        {
            AccessGrants =
            [
                new TopicAccessGrant("#", "alice", TopicAccessRights.Publish),
                new TopicAccessGrant("$queue/q", "alice", TopicAccessRights.Publish),
                new TopicAccessGrant("$queue/fullq", "alice", TopicAccessRights.Publish),
                new TopicAccessGrant("$queue/none", "alice", TopicAccessRights.Publish),
            ],
        };
        var bus = new MessageBus(clock, maxMessageBytes: 2, configuration: configuration, faults: plan);
        using var connection = bus.Connect("alice");
        connection.DeclareQueue("q");
        connection.DeclareQueue("fullq");
        var ok = BusBatchEntry.ToTopic(Message("t", "1"));

        AssertRefused(MessageBusOutcome.InvalidTopic, 1, connection.PublishBatch([ok, BusBatchEntry.ToTopic(Message("a/+", "1"))]));
        AssertRefused(MessageBusOutcome.MessageTooLarge, 1, connection.PublishBatch([ok, BusBatchEntry.ToTopic(Message("t", "123"))]));
        AssertRefused(MessageBusOutcome.MessageTooLarge, 1, connection.PublishBatch([ok, BusBatchEntry.ToQueue("q", Message("t", "123"))]));
        AssertRefused(MessageBusOutcome.AccessDenied, 1, connection.PublishBatch([ok, BusBatchEntry.ToTopic(Message("$sys", "1"))]));
        AssertRefused(MessageBusOutcome.AccessDenied, 1, connection.PublishBatch([ok, BusBatchEntry.ToQueue("other", Message("t", "1"))]));
        AssertRefused(MessageBusOutcome.UnknownQueue, 1, connection.PublishBatch([ok, BusBatchEntry.ToQueue("none", Message("t", "1"))]));
        AssertRefused(MessageBusOutcome.StoreFull, 1, connection.PublishBatch([ok, BusBatchEntry.ToTopic(Message("full", "1"))]));
        AssertRefused(MessageBusOutcome.QueueFull, 1, connection.PublishBatch([ok, BusBatchEntry.ToQueue("fullq", Message("t", "1"))]));
        Assert.AreEqual(0, bus.GetLogTopics().Count);
        Assert.AreEqual(0, connection.InspectQueue("q").ReadyMessageCount);
    }

    [TestMethod]
    public void PublishBatch_AnEarlierEntryTheLockedCheckRefuses_IsTheOneAnswered()
    {
        var bus = new MessageBus(clock, maxMessagesPerQueue: 1);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");

        var result = connection.PublishBatch(
        [
            BusBatchEntry.ToQueue("q", Message("t", "1")),
            BusBatchEntry.ToQueue("q", Message("t", "2")),
            BusBatchEntry.ToTopic(Message("a/#", "3")),
        ]);

        AssertRefused(MessageBusOutcome.QueueFull, 1, result);
    }

    [TestMethod]
    public void PublishBatch_ExpiredEntries_SucceedAndAreStoredNowhereButAnUnknownQueueIsStillRefused()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var expiresAt = clock.UtcNow;

        var result = connection.PublishBatch(
        [
            BusBatchEntry.ToTopic(Message("t", "1") with { ExpiresAt = expiresAt }),
            BusBatchEntry.ToQueue("q", Message("t", "2") with { ExpiresAt = expiresAt }),
        ]);

        Assert.AreEqual(MessageBusOutcome.Succeeded, result.Outcome);
        CollectionAssert.AreEqual(new[] { new BusPublishResult(MessageBusOutcome.Succeeded, 0), new BusPublishResult(MessageBusOutcome.Succeeded, 0) }, result.Results.ToArray());
        Assert.AreEqual(0, bus.GetLogTopics().Count);
        Assert.AreEqual(0, connection.InspectQueue("q").ReadyMessageCount);
        AssertRefused(MessageBusOutcome.UnknownQueue, 0, connection.PublishBatch([BusBatchEntry.ToQueue("none", Message("t", "2") with { ExpiresAt = expiresAt })]));
    }

    [TestMethod]
    public void PublishBatch_IsKeptFromThePublishingConnectionsNoLocalSubscriptions()
    {
        var bus = new MessageBus(clock);
        using var publisher = bus.Connect();
        using var other = bus.Connect();
        var ownReceived = new List<BusMessage>();
        var otherReceived = new List<BusMessage>();
        publisher.Subscribe("t", ownReceived.Add, noLocal: true);
        other.Subscribe("t", otherReceived.Add, noLocal: true);

        publisher.PublishBatch([BusBatchEntry.ToTopic(Message("t", "1"))]);

        Assert.IsEmpty(ownReceived);
        Assert.HasCount(1, otherReceived);
    }

    [TestMethod]
    public async Task PublishBatch_RacingBatches_NeverInterleave()
    {
        var bus = new MessageBus(clock, maxMessagesPerQueue: 2);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var start = new ManualResetEventSlim();
        var racers = Enumerable.Range(0, 8).Select(racer => Task.Run(() =>
        {
            start.Wait(TestContext.CancellationToken);
            return connection.PublishBatch([BusBatchEntry.ToQueue("q", Message("t", $"{racer}a")), BusBatchEntry.ToQueue("q", Message("t", $"{racer}b"))]);
        })).ToArray();

        start.Set();
        var results = await Task.WhenAll(racers);

        Assert.AreEqual(1, results.Count(result => result.Outcome is MessageBusOutcome.Succeeded));
        var held = Drain(connection, "q");
        Assert.AreEqual(held[0][0], held[1][0]);
    }

    [TestMethod]
    public void PublishBatch_NullEntriesOrANullEntry_Throws()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();

        Assert.ThrowsExactly<ArgumentNullException>(() => connection.PublishBatch(null!));
        Assert.ThrowsExactly<ArgumentException>(() => connection.PublishBatch([BusBatchEntry.ToTopic(Message("t", "1")), null!]));
        Assert.ThrowsExactly<ArgumentNullException>(() => BusBatchEntry.ToTopic(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => BusBatchEntry.ToQueue(null!, Message("t", "1")));
        Assert.ThrowsExactly<ArgumentNullException>(() => BusBatchEntry.ToQueue("q", null!));
    }

    [TestMethod]
    public void BusBatchEntry_ToTopicAndToQueue_HoldTheirQueueAndMessage()
    {
        var message = Message("t", "1");

        Assert.IsNull(BusBatchEntry.ToTopic(message).QueueName);
        Assert.AreEqual("q", BusBatchEntry.ToQueue("q", message).QueueName);
        Assert.AreSame(message, BusBatchEntry.ToQueue("q", message).Message);
    }

    public TestContext TestContext { get; set; } = null!;

    private (MessageBus Bus, BusConnection Connection, List<string> Received) Watched(
        RecordingSurlEventPublisher events, long maxLogBytes = MessageBus.DefaultMaxLogBytes, int maxMessagesPerQueue = MessageBus.DefaultMaxMessagesPerQueue)
    {
        var bus = new MessageBus(clock, maxMessagesPerQueue: maxMessagesPerQueue, maxLogBytes: maxLogBytes, events: events);
        var connection = bus.Connect();
        var received = new List<string>();
        bus.Connect().Subscribe("#", message => received.Add(Payload(message)));
        connection.DeclareQueue("b");
        connection.BindQueue("b", "t1");
        connection.DeclareQueue("q");
        return (bus, connection, received);
    }

    private static void AssertRefused(MessageBusOutcome outcome, int index, BusBatchPublishResult result)
    {
        Assert.AreEqual(outcome, result.Outcome);
        Assert.AreEqual(index, result.RefusedIndex);
        Assert.IsEmpty(result.Results);
    }

    private static void AssertNothingPublished(MessageBus bus, BusConnection connection, List<string> received, IEnumerable<SurlEvent> eventsSinceSetUp)
    {
        Assert.IsEmpty(received);
        Assert.IsFalse(eventsSinceSetUp.OfType<StoreChangedEvent>().Any());
        Assert.AreEqual(0, bus.GetLogTopics().Count);
        Assert.AreEqual(0, connection.InspectQueue("b").ReadyMessageCount);
        Assert.AreEqual(0, connection.InspectQueue("q").ReadyMessageCount);
        Assert.IsEmpty(connection.Subscribe("#", _ => { }, includeRetained: true).RetainedMessages);
    }

    private static StoreChangedEvent[] Changes(RecordingSurlEventPublisher events, bool delivered) =>
        [.. events.Events.OfType<StoreChangedEvent>().Where(change => (change.Change is StoreChange.Delivered) == delivered)];

    private static List<string> Drain(BusConnection connection, string queueName)
    {
        var payloads = new List<string>();
        while (connection.GetFromQueue(queueName).Delivery is { } delivery)
        {
            payloads.Add(Payload(delivery.Message));
        }

        return payloads;
    }

    private static string[] LogPayloads(MessageBus bus, string topic) =>
        [.. bus.ReadLog(topic, bus.GetLogOffsets(topic).LogStartOffset, long.MaxValue).Records.Select(record => Payload(record.Message))];

    private static BusMessage Message(string topic, string payload) => new(topic, Encoding.UTF8.GetBytes(payload));

    private static string Payload(BusMessage message) => Encoding.UTF8.GetString(message.Payload.Span);
}
