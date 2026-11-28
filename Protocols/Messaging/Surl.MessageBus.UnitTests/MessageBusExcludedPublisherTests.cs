using Surl.Protocol.Abstractions;
using static Surl.MessageBus.PersistedBusFixture;

namespace Surl.MessageBus;

/// <summary>A subscription queue's excluded publisher (ADR-0146, Amendment 3).</summary>
[TestClass]
public sealed class MessageBusExcludedPublisherTests
{
    private const string Excluded = "amqp1:me";

    private readonly SettableTimeProvider clock = new();

    [TestMethod]
    public void PublisherName_OfANewConnection_IsNull()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();

        Assert.IsNull(connection.PublisherName);
    }

    [TestMethod]
    public void Publish_ThroughAConnectionNamedAsTheExcludedPublisher_IsNeverDeliveredAndCountedNowhere()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new SpendingFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Path = MessageBus.QueueChangeName("q") });
        var bus = new MessageBus(clock, events: events, faults: plan);
        using var subscriber = bus.Connect();
        var received = new List<BusQueueDelivery>();
        DeclareExcluding(subscriber, "q", Excluded);
        subscriber.Consume("q", received.Add).Consumer!.Start();
        using var publisher = NamedConnection(bus, Excluded);
        var mark = events.Events.Count;

        var result = publisher.Publish(Message("t", "x"));

        Assert.AreEqual(MessageBusOutcome.Succeeded, result.Outcome);
        Assert.AreEqual((0, 0, 0), (result.QueuedCount, result.FullQueueCount, result.UnconsumedQueueCount));
        Assert.IsEmpty(received);
        Assert.AreEqual(0, subscriber.InspectQueue("q").ReadyMessageCount);
        CollectionAssert.AreEqual(
            new SurlEvent[] { new StoreChangedEvent(StoreKind.Bus, StoreChange.Published, "t", 1, ExchangeId: null) },
            events.Events.Skip(mark).ToArray());
        CollectionAssert.AreEqual(new[] { "t" }, plan.Points.Select(point => point.Path).ToArray());
    }

    [TestMethod]
    public void PublishToConsumedQueues_ThroughTheExcludedPublisher_CountsTheQueueNeitherQueuedNorUnconsumed()
    {
        var bus = new MessageBus(clock);
        using var subscriber = NamedConnection(bus, Excluded);
        DeclareExcluding(subscriber, "q", Excluded);

        var result = subscriber.PublishToConsumedQueues(Message("t", "x"));

        Assert.AreEqual((0, 0, 0), (result.QueuedCount, result.FullQueueCount, result.UnconsumedQueueCount));
    }

    [TestMethod]
    public void Publish_ThroughTheExcludedPublisher_StillReachesTheLogTheLiveSubscriptionsAndOtherQueues()
    {
        var bus = new MessageBus(clock);
        using var subscriber = bus.Connect();
        var live = new List<BusMessage>();
        subscriber.Subscribe("t", live.Add);
        DeclareExcluding(subscriber, "excluding", Excluded);
        DeclareExcluding(subscriber, "other", "amqp1:someone");
        using var publisher = NamedConnection(bus, Excluded);

        var result = publisher.Publish(Message("t", "x") with { Retain = true });

        Assert.AreEqual((1, 1), (result.QueuedCount, result.DeliveredSubscriptionCount));
        Assert.HasCount(1, live);
        Assert.AreEqual(0L, result.LogOffset);
        Assert.AreEqual(1, subscriber.InspectQueue("other").ReadyMessageCount);
        Assert.HasCount(1, subscriber.Subscribe("t", live.Add, includeRetained: true).RetainedMessages);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "No publisher name")]
    [DataRow("amqp1:you", DisplayName = "Another name")]
    [DataRow("amqp1:ME", DisplayName = "The name in another case")]
    [DataRow("mqtt:me", DisplayName = "Another scheme family")]
    public void Publish_ThroughAConnectionNotNamedAsTheExcludedPublisher_IsDelivered(string? publisherName)
    {
        var bus = new MessageBus(clock);
        using var subscriber = bus.Connect();
        DeclareExcluding(subscriber, "q", Excluded);
        using var publisher = NamedConnection(bus, publisherName);

        var single = publisher.Publish(Message("t", "1"));
        var batch = publisher.PublishBatch([BusBatchEntry.ToTopic(Message("t", "2"))]);

        Assert.AreEqual(1, single.QueuedCount);
        Assert.AreEqual(1, batch.Results.Single().QueuedCount);
        Assert.AreEqual(2, subscriber.InspectQueue("q").ReadyMessageCount);
    }

    [TestMethod]
    public void Publish_WithNoConnection_IsDeliveredFromTheTestHostAndFromASeed()
    {
        var bus = new MessageBus(clock);
        using var subscriber = bus.Connect();
        DeclareExcluding(subscriber, "q", Excluded);

        bus.Publish(Message("t", "1"));
        bus.SeedPublish(Message("t", "2"));
        bus.SeedEnqueue("q", Message("t", "3"));

        Assert.AreEqual(3, subscriber.InspectQueue("q").ReadyMessageCount);
    }

    [TestMethod]
    public void PublishBatch_ThroughTheExcludedPublisher_QueuesNoCopyAndRaisesNoQueueChange()
    {
        var events = new RecordingSurlEventPublisher();
        var bus = new MessageBus(clock, events: events);
        using var publisher = NamedConnection(bus, Excluded);
        var received = new List<BusQueueDelivery>();
        DeclareExcluding(publisher, "q", Excluded);
        publisher.Consume("q", received.Add).Consumer!.Start();
        var mark = events.Events.Count;

        var result = publisher.PublishBatch([BusBatchEntry.ToTopic(Message("t", "1")), BusBatchEntry.ToTopic(Message("t", "2"))]);

        Assert.AreEqual(MessageBusOutcome.Succeeded, result.Outcome);
        foreach (var entry in result.Results)
        {
            Assert.AreEqual((0, 0, 0), (entry.QueuedCount, entry.FullQueueCount, entry.UnconsumedQueueCount));
        }

        Assert.IsEmpty(received);
        Assert.AreEqual(0, publisher.InspectQueue("q").ReadyMessageCount);
        CollectionAssert.AreEqual(new[] { "t", "t" }, events.Events.Skip(mark).Cast<StoreChangedEvent>().Select(change => change.Name).ToArray());
    }

    [TestMethod]
    public void PublishBatch_AWithheldCopy_CountsAgainstNoQueueBound()
    {
        var bus = new MessageBus(clock, maxQueuedBytes: 2);
        using var publisher = NamedConnection(bus, Excluded);
        DeclareExcluding(publisher, "excluding", Excluded);
        publisher.DeclareQueue("direct");

        var result = publisher.PublishBatch([BusBatchEntry.ToTopic(Message("t", "ab")), BusBatchEntry.ToQueue("direct", Message("t", "cd"))]);

        Assert.AreEqual(MessageBusOutcome.Succeeded, result.Outcome);
        Assert.AreEqual(0, result.Results[0].FullQueueCount);
        Assert.AreEqual(1, publisher.InspectQueue("direct").ReadyMessageCount);
    }

    [TestMethod]
    public void Publish_ToAQueueExcludingNoPublisher_DeliversItsOwnConnectionsPublish()
    {
        var bus = new MessageBus(clock);
        using var connection = NamedConnection(bus, Excluded);
        connection.DeclareQueue("q", default, "d", excludedPublisherName: null);
        connection.BindQueue("q", "t");

        var result = connection.Publish(Message("t", "x"));

        Assert.AreEqual(1, result.QueuedCount);
    }

    [TestMethod]
    public void DeclareQueue_RedeclaredNamingAnExcludedPublisher_SetsItAndKeepsWhatIsQueued()
    {
        var bus = new MessageBus(clock);
        using var connection = NamedConnection(bus, Excluded);
        connection.DeclareQueue("q", default, "d");
        connection.BindQueue("q", "t");
        connection.Publish(Message("t", "before"));

        var redeclared = connection.DeclareQueue("q", default, "d", Excluded);
        var after = connection.Publish(Message("t", "after"));

        Assert.AreEqual(MessageBusOutcome.Succeeded, redeclared.Outcome);
        Assert.AreEqual(0, after.QueuedCount);
        Assert.AreEqual(1, connection.InspectQueue("q").ReadyMessageCount);
    }

    [TestMethod]
    public void DeclareQueue_RedeclaredNamingAnotherExcludedPublisher_ReplacesTheOneItHas()
    {
        var bus = new MessageBus(clock);
        using var first = NamedConnection(bus, Excluded);
        using var second = NamedConnection(bus, "amqp1:you");
        DeclareExcluding(first, "q", Excluded);

        first.DeclareQueue("q", default, "d", Excluded);
        var whileUnchanged = first.Publish(Message("t", "1")).QueuedCount;
        first.DeclareQueue("q", default, "d", "amqp1:you");

        Assert.AreEqual(0, whileUnchanged);
        Assert.AreEqual(1, first.Publish(Message("t", "2")).QueuedCount);
        Assert.AreEqual(0, second.Publish(Message("t", "3")).QueuedCount);
    }

    [TestMethod]
    public void DeclareQueue_RedeclaredNamingNoExcludedPublisher_KeepsTheOneItHas()
    {
        var bus = new MessageBus(clock);
        using var connection = NamedConnection(bus, Excluded);
        DeclareExcluding(connection, "q", Excluded);

        connection.DeclareQueue("q", default, "d", excludedPublisherName: null);
        connection.DeclareQueue("q");

        Assert.AreEqual(0, connection.Publish(Message("t", "x")).QueuedCount);
    }

    [TestMethod]
    public void DeclareQueue_RedeclareRefusedForItsFlagsOrDescription_SetsNoExcludedPublisher()
    {
        var bus = new MessageBus(clock);
        using var connection = NamedConnection(bus, Excluded);
        connection.DeclareQueue("q", default, "d");
        connection.BindQueue("q", "t");

        var flags = connection.DeclareQueue("q", new BusQueueFlags(Durable: true), "d", Excluded);
        var description = connection.DeclareQueue("q", default, "other", Excluded);

        Assert.AreEqual(MessageBusOutcome.QueueFlagsDiffer, flags.Outcome);
        Assert.AreEqual(MessageBusOutcome.QueueDescriptionDiffers, description.Outcome);
        Assert.AreEqual(1, connection.Publish(Message("t", "x")).QueuedCount);
    }

    [TestMethod]
    public void DeclareQueue_AnExcludedPublisherWithNoSubscriptionDescription_ThrowsAndMakesNoQueue()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();

        var exception = Assert.ThrowsExactly<ArgumentException>(() => connection.DeclareQueue("q", default, subscriptionDescription: null, Excluded));

        Assert.AreEqual("excludedPublisherName", exception.ParamName);
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, connection.InspectQueue("q").Outcome);
    }

    [TestMethod]
    public void DeclareQueue_ANullNameNamingAnExcludedPublisher_Throws()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();

        Assert.ThrowsExactly<ArgumentNullException>(() => connection.DeclareQueue(null!, default, "d", Excluded));
    }

    [TestMethod]
    public async Task SaveChangesAsync_AQueueExcludingAPublisher_WritesTheThirdIndexFormAndLoadsItBack()
    {
        var fixture = new PersistedBusFixture();
        var bus = await fixture.LoadBusAsync();
        var connection = bus.Connect();
        connection.DeclareQueue("a", new BusQueueFlags(Durable: true), "d", "c");
        connection.DeclareQueue("b", new BusQueueFlags(Durable: true));
        connection.BindQueue("a", "t");

        await bus.SaveChangesAsync();
        var loaded = await fixture.LoadBusAsync();
        using var publisher = NamedConnection(loaded, "c");

        CollectionAssert.AreEqual(
            (byte[])[
                .. "SURL-BUS-QUEUES-3\n"u8,
                .. Bytes("00000002"),
                .. Bytes("00000001 61 01 00000001 64 01 00000001 63 00000001 00000001 74 00000000"),
                .. Bytes("00000001 62 00 00 00000000 00000000"),
            ],
            fixture.Read(fixture.Files.QueueIndexPath));
        Assert.AreEqual("d", publisher.InspectQueue("a").SubscriptionDescription);
        Assert.AreEqual(0, publisher.Publish(Message("t", "x")).QueuedCount);
        Assert.AreEqual(1, loaded.Publish(Message("t", "x")).QueuedCount);
    }

    [TestMethod]
    public async Task SaveChangesAsync_ARedeclareSettingTheExcludedPublisherOfALoadedQueue_IsSaved()
    {
        var fixture = new PersistedBusFixture();
        var first = await fixture.LoadBusAsync();
        var setup = first.Connect();
        setup.DeclareQueue("q", new BusQueueFlags(Durable: true), "d");
        setup.BindQueue("q", "t");
        await first.SaveChangesAsync();
        var second = await fixture.LoadBusAsync();
        second.Connect().DeclareQueue("q", new BusQueueFlags(Durable: true), "d", "c");

        await second.SaveChangesAsync();
        var third = await fixture.LoadBusAsync();
        using var publisher = NamedConnection(third, "c");

        Assert.AreEqual(0, publisher.Publish(Message("t", "x")).QueuedCount);
        Assert.AreEqual(1, third.Publish(Message("t", "x")).QueuedCount);
    }

    [TestMethod]
    [DataRow("00000001 00000001 71 01 00000001 64 02 00000000 00000000", DisplayName = "A publisher byte that is neither 0 nor 1")]
    [DataRow("00000001 00000001 71 01 00000001 64 01", DisplayName = "A publisher name cut short")]
    [DataRow("00000001 00000001 71 00 01 00000001 63 00000000 00000000", DisplayName = "A publisher name with no description")]
    public async Task LoadAsync_AMalformedThirdFormQueueIndex_ThrowsWithItsPathAndNotAMessageBusFile(string hex)
    {
        var fixture = new PersistedBusFixture();
        fixture.Write(fixture.Files.QueueIndexPath, [.. "SURL-BUS-QUEUES-3\n"u8, .. Bytes(hex)]);

        var exception = await fixture.LoadFailureAsync();

        Assert.AreEqual(fixture.Files.QueueIndexPath, exception.FilePath);
        Assert.AreEqual(MessageBusFiles.MalformedFileReason, exception.Message);
    }

    [TestMethod]
    public void Restore_ASnapshotOfAQueueExcludingAPublisher_BringsTheExclusionBack()
    {
        var bus = new MessageBus(clock);
        using var connection = NamedConnection(bus, Excluded);
        DeclareExcluding(connection, "q", Excluded);
        var snapshot = bus.TakeSnapshot();
        connection.DeleteQueue("q");
        connection.DeclareQueue("q", default, "d");

        bus.Restore(snapshot);

        Assert.AreEqual(0, connection.Publish(Message("t", "x")).QueuedCount);
        Assert.AreEqual(1, bus.Publish(Message("t", "x")).QueuedCount);
    }

    [TestMethod]
    public void Restore_ASnapshotTakenBeforeThePublisherWasExcluded_TakesTheExclusionAway()
    {
        var bus = new MessageBus(clock);
        using var connection = NamedConnection(bus, Excluded);
        connection.DeclareQueue("q", default, "d");
        connection.BindQueue("q", "t");
        var snapshot = bus.TakeSnapshot();
        connection.DeclareQueue("q", default, "d", Excluded);

        bus.Restore(snapshot);

        Assert.AreEqual(1, connection.Publish(Message("t", "x")).QueuedCount);
    }

    [TestMethod]
    public void Reset_AQueueItKeeps_KeepsItsExcludedPublisherAndOneItRemovesTakesItAway()
    {
        var bus = new MessageBus(clock);
        using var connection = NamedConnection(bus, Excluded);
        DeclareExcluding(connection, "kept", Excluded);
        DeclareExcluding(connection, "removed", Excluded);
        connection.Consume("kept", _ => { });

        bus.Reset();
        connection.DeclareQueue("removed", default, "d");
        connection.BindQueue("removed", "t");
        var result = connection.Publish(Message("t", "x"));

        Assert.AreEqual(1, result.QueuedCount);
        Assert.AreEqual(0, connection.InspectQueue("kept").ReadyMessageCount);
        Assert.AreEqual(1, connection.InspectQueue("removed").ReadyMessageCount);
    }

    private static BusConnection NamedConnection(MessageBus bus, string? publisherName)
    {
        var connection = bus.Connect();
        connection.PublisherName = publisherName;
        return connection;
    }

    private static void DeclareExcluding(BusConnection connection, string queueName, string excludedPublisherName)
    {
        connection.DeclareQueue(queueName, default, "d", excludedPublisherName);
        connection.BindQueue(queueName, "t");
    }
}
