using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.MessageBus;

/// <summary>
/// A queue consumer given a delivery credit (ADR-0146, Amendment 7, item 2): the bus holds at most
/// that many unclaimed deliveries for it, and each is handed out only when it is claimed.
/// </summary>
[TestClass]
public sealed class BusQueueConsumerDeliveryCreditTests
{
    private readonly SettableTimeProvider clock = new();
    private readonly RecordingSurlEventPublisher events = new();

    [TestMethod]
    public void CreditZero_IsAssignedNothing_AndTheOtherConsumerGetsEveryMessage()
    {
        var bus = NewBus(out var connection);
        var none = new List<BusQueueDelivery>();
        var withoutCredit = Consume(connection, none, credit: 0);
        var all = new List<BusQueueDelivery>();
        connection.Consume("q", all.Add).Consumer!.Start();

        Send(bus, "1", "2", "3");

        Assert.IsEmpty(none);
        Assert.IsFalse(withoutCredit.TryClaimDelivery(out _));
        CollectionAssert.AreEqual(new[] { "1", "2", "3" }, Payloads(all));
    }

    [TestMethod]
    public void CreditZero_WithNoOtherConsumer_TheMessagesStayReady()
    {
        var bus = NewBus(out var connection);
        var received = new List<BusQueueDelivery>();
        Consume(connection, received, credit: 0);

        Send(bus, "1", "2");

        Assert.IsEmpty(received);
        Assert.AreEqual(2, connection.InspectQueue("q").ReadyMessageCount);
        Assert.AreEqual("1", Text(connection.GetFromQueue("q").Delivery!));
    }

    [TestMethod]
    public void CreditN_HoldsAtMostN_AndTheRestStayReady()
    {
        var bus = NewBus(out var connection);
        var received = new List<BusQueueDelivery>();
        Consume(connection, received, credit: 2);

        Send(bus, "1", "2", "3", "4");

        CollectionAssert.AreEqual(new[] { "1", "2" }, Payloads(received));
        Assert.AreEqual(2, connection.InspectQueue("q").ReadyMessageCount);
    }

    [TestMethod]
    public void CreditGivenBeforeStart_AssignsTheBacklogOnStart()
    {
        var bus = NewBus(out var connection);
        Send(bus, "1", "2");
        var received = new List<BusQueueDelivery>();
        var consumer = connection.Consume("q", received.Add).Consumer!;
        consumer.SetDeliveryCredit(5);
        var receivedBeforeStart = received.Count;

        consumer.Start();

        Assert.AreEqual(0, receivedBeforeStart);
        CollectionAssert.AreEqual(new[] { "1", "2" }, Payloads(received));
    }

    [TestMethod]
    public void RaisingTheCredit_AssignsTheBacklogBeforeItReturns()
    {
        var bus = NewBus(out var connection);
        var received = new List<BusQueueDelivery>();
        var consumer = Consume(connection, received, credit: 0);
        Send(bus, "1", "2", "3");

        consumer.SetDeliveryCredit(2);

        CollectionAssert.AreEqual(new[] { "1", "2" }, Claimed(consumer));
        Assert.AreEqual(1, connection.InspectQueue("q").ReadyMessageCount);
    }

    [TestMethod]
    public void TwoConsumersWithCredit_TakeTurns()
    {
        var bus = NewBus(out var connection);
        var first = new List<BusQueueDelivery>();
        var second = new List<BusQueueDelivery>();
        Consume(connection, first, credit: 10);
        Consume(connection, second, credit: 10);

        Send(bus, "1", "2", "3", "4");

        CollectionAssert.AreEqual(new[] { "1", "3" }, Payloads(first));
        CollectionAssert.AreEqual(new[] { "2", "4" }, Payloads(second));
    }

    [TestMethod]
    public void MaxUnacknowledged_CountsHeldAndClaimedDeliveries()
    {
        var bus = NewBus(out var connection);
        var received = new List<BusQueueDelivery>();
        var consumer = connection.Consume("q", received.Add, maxUnacknowledged: 2).Consumer!;
        consumer.SetDeliveryCredit(10);
        consumer.Start();

        Send(bus, "1", "2", "3");
        consumer.TryClaimDelivery(out var claimed);
        var receivedWhileFull = received.Count;
        consumer.Acknowledge(claimed!.DeliveryTag);

        Assert.AreEqual(2, receivedWhileFull);
        CollectionAssert.AreEqual(new[] { "1", "2", "3" }, Payloads(received));
    }

    [TestMethod]
    public void TryClaimDelivery_AnswersHeldDeliveriesInTheOrderAssigned_LoweringTheCreditByOneEach()
    {
        var bus = NewBus(out var connection);
        var received = new List<BusQueueDelivery>();
        var consumer = Consume(connection, received, credit: 2);
        Send(bus, "1", "2", "3");

        var firstClaimed = consumer.TryClaimDelivery(out var first);
        var readyAfterFirst = connection.InspectQueue("q").ReadyMessageCount;
        var secondClaimed = consumer.TryClaimDelivery(out var second);
        var thirdClaimed = consumer.TryClaimDelivery(out var third);
        Send(bus, "4");

        Assert.IsTrue(firstClaimed);
        Assert.IsTrue(secondClaimed);
        Assert.IsFalse(thirdClaimed);
        Assert.IsNull(third);
        Assert.AreEqual(received[0], first);
        Assert.AreEqual(received[1], second);
        Assert.AreEqual(1, readyAfterFirst, "a claim frees no room: it lowers the credit as it takes the delivery");
        Assert.HasCount(2, received, "the credit is used up");
        Assert.AreEqual(2, connection.InspectQueue("q").ReadyMessageCount);
    }

    [TestMethod]
    public void TryClaimDelivery_PublishesDeliveredAtTheClaim_NotAtTheAssignment()
    {
        var bus = NewBus(out var connection);
        var consumer = Consume(connection, [], credit: 1);
        var start = events.Events.Count;

        bus.PublishToQueue("q", Message("one"));
        var beforeClaim = EventsSince(start);
        consumer.TryClaimDelivery(out var claimed);
        var outcome = consumer.Acknowledge(claimed!.DeliveryTag);

        CollectionAssert.AreEqual(new[] { Change(StoreChange.Enqueued, 3) }, beforeClaim);
        CollectionAssert.AreEqual(
            new[] { Change(StoreChange.Enqueued, 3), Change(StoreChange.Delivered, 3), Change(StoreChange.Acknowledged, 3) },
            EventsSince(start));
        Assert.AreEqual(MessageBusOutcome.Succeeded, outcome);
        Assert.IsFalse(claimed.Redelivered);
    }

    [TestMethod]
    public void TryClaimDelivery_AfterTheConsumerEnded_AnswersFalse()
    {
        var bus = NewBus(out var connection);
        var consumer = Consume(connection, [], credit: 1);
        Send(bus, "1");

        consumer.Dispose();

        Assert.IsFalse(consumer.TryClaimDelivery(out _));
        Assert.AreEqual(1, connection.InspectQueue("q").ReadyMessageCount);
    }

    [TestMethod]
    public void TryClaimDelivery_AfterTheQueueEnded_AnswersFalse_AndTheBytesAreFree()
    {
        var bus = new MessageBus(clock, maxQueuedBytes: 1);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var consumer = Consume(connection, [], credit: 1);
        Send(bus, "1");

        var deleted = connection.DeleteQueue("q");
        consumer.SetDeliveryCredit(5);
        connection.DeclareQueue("q");

        Assert.AreEqual(MessageBusOutcome.Succeeded, deleted);
        Assert.IsFalse(consumer.TryClaimDelivery(out _));
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.PublishToQueue("q", Message("2")));
        Assert.AreEqual(1, connection.InspectQueue("q").ReadyMessageCount);
    }

    [TestMethod]
    public void AHeldDelivery_CannotBeAcknowledgedOrRejected()
    {
        var bus = NewBus(out var connection);
        var received = new List<BusQueueDelivery>();
        var consumer = Consume(connection, received, credit: 1);
        Send(bus, "1");
        var start = events.Events.Count;
        var tag = received[0].DeliveryTag;

        var acknowledged = consumer.Acknowledge(tag);
        var requeued = consumer.Reject(tag, requeue: true);
        var dropped = consumer.Reject(tag, requeue: false);

        Assert.AreEqual(MessageBusOutcome.UnknownDeliveryTag, acknowledged);
        Assert.AreEqual(MessageBusOutcome.UnknownDeliveryTag, requeued);
        Assert.AreEqual(MessageBusOutcome.UnknownDeliveryTag, dropped);
        Assert.IsEmpty(EventsSince(start));
        Assert.HasCount(1, received);
        Assert.AreEqual(MessageBusOutcome.QueueNotEmpty, connection.DeleteQueue("q", ifUnused: false, ifEmpty: true).Outcome, "held counts as unacknowledged");
        Assert.IsTrue(consumer.TryClaimDelivery(out var claimed));
        Assert.AreEqual(received[0], claimed);
    }

    [TestMethod]
    public void LoweringTheCredit_PutsTheLastAssignedBackAtTheirPlaces_WithNoEvent()
    {
        var bus = NewBus(out var connection);
        var consumer = Consume(connection, [], credit: 3);
        Send(bus, "1", "2", "3", "4");
        var start = events.Events.Count;

        consumer.SetDeliveryCredit(1);

        Assert.IsEmpty(EventsSince(start));
        Assert.AreEqual(3, connection.InspectQueue("q").ReadyMessageCount);
        CollectionAssert.AreEqual(new[] { "1" }, Claimed(consumer));
        var later = new List<BusQueueDelivery>();
        connection.Consume("q", later.Add).Consumer!.Start();
        CollectionAssert.AreEqual(new[] { "2", "3", "4" }, Payloads(later));
        Assert.IsTrue(later.All(delivery => !delivery.Redelivered));
    }

    [TestMethod]
    public void LoweringTheCredit_AnotherConsumerWithRoomReceivesTheReturnedMessages()
    {
        var bus = NewBus(out var connection);
        var first = new List<BusQueueDelivery>();
        var consumer = Consume(connection, first, credit: 3);
        Send(bus, "1", "2", "3");
        var second = new List<BusQueueDelivery>();
        connection.Consume("q", second.Add).Consumer!.Start();

        consumer.SetDeliveryCredit(1);

        CollectionAssert.AreEqual(new[] { "2", "3" }, Payloads(second));
        Assert.IsTrue(second.All(delivery => !delivery.Redelivered));
        CollectionAssert.AreEqual(new[] { "1" }, Claimed(consumer));
    }

    [TestMethod]
    [DataRow("lower the credit")]
    [DataRow("end the consumer")]
    [DataRow("recover")]
    public void AReturnedHeldDelivery_KeepsTheRedeliveredMarkItHadBeforeItWasAssigned(string way)
    {
        var bus = NewBus(out var connection);
        var consumer = HoldOneRejectedAndOneNeverClaimed(bus, connection);

        GiveBack(consumer, way);

        var later = new List<BusQueueDelivery>();
        connection.Consume("q", later.Add).Consumer!.Start();
        // A recover leaves the consumer its credit, so it holds both again.
        var all = way == "recover" ? ClaimedDeliveries(consumer) : later;
        CollectionAssert.AreEqual(new[] { "rejected", "never claimed" }, Payloads(all));
        CollectionAssert.AreEqual(new[] { true, false }, all.Select(delivery => delivery.Redelivered).ToArray());
    }

    [TestMethod]
    public async Task AHeldDelivery_SavedToFiles_LoadsWithTheRedeliveredMarkItHadBeforeItWasAssigned()
    {
        var fixture = new PersistedBusFixture();
        var bus = await fixture.LoadBusAsync();
        using var connection = bus.Connect();
        connection.DeclareQueue("q", new BusQueueFlags(Durable: true));
        HoldOneRejectedAndOneNeverClaimed(bus, connection);

        await bus.SaveChangesAsync();
        var loaded = await fixture.LoadBusAsync();

        using var reader = loaded.Connect();
        var received = new List<BusQueueDelivery>();
        reader.Consume("q", received.Add).Consumer!.Start();
        CollectionAssert.AreEqual(new[] { "rejected", "never claimed" }, Payloads(received));
        CollectionAssert.AreEqual(new[] { true, false }, received.Select(delivery => delivery.Redelivered).ToArray());
    }

    [TestMethod]
    public async Task AClaim_IsSavedToFilesAsRedelivered()
    {
        var fixture = new PersistedBusFixture();
        var bus = await fixture.LoadBusAsync();
        using var connection = bus.Connect();
        connection.DeclareQueue("q", new BusQueueFlags(Durable: true));
        var consumer = Consume(connection, [], credit: 1);
        Send(bus, "1");
        await bus.SaveChangesAsync();

        consumer.TryClaimDelivery(out _);
        await bus.SaveChangesAsync();
        var loaded = await fixture.LoadBusAsync();

        using var reader = loaded.Connect();
        Assert.IsTrue(reader.GetFromQueue("q").Delivery!.Redelivered);
    }

    [TestMethod]
    public void AHeldDelivery_RestoredFromASnapshot_KeepsTheRedeliveredMarkItHadBeforeItWasAssigned()
    {
        var bus = NewBus(out var connection);
        var consumer = HoldOneRejectedAndOneNeverClaimed(bus, connection);
        var snapshot = bus.TakeSnapshot();
        Claimed(consumer);
        consumer.SetDeliveryCredit(0);

        bus.Restore(snapshot);

        Assert.IsFalse(consumer.TryClaimDelivery(out _));
        consumer.SetDeliveryCredit(2);
        var restored = ClaimedDeliveries(consumer);
        CollectionAssert.AreEqual(new[] { "rejected", "never claimed" }, Payloads(restored));
        CollectionAssert.AreEqual(new[] { true, false }, restored.Select(delivery => delivery.Redelivered).ToArray());
    }

    [TestMethod]
    public void AReset_DropsTheHeldDeliveries_AndTheirBytesAreFree()
    {
        var bus = new MessageBus(clock, maxQueuedBytes: 2);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var consumer = Consume(connection, [], credit: 2);
        Send(bus, "1", "2");

        bus.Reset();

        Assert.IsFalse(consumer.TryClaimDelivery(out _));
        Send(bus, "3", "4");
        CollectionAssert.AreEqual(new[] { "3", "4" }, Claimed(consumer));
    }

    [TestMethod]
    public void AHeldMessageThatExpires_IsStillClaimed()
    {
        var bus = NewBus(out var connection);
        var consumer = Consume(connection, [], credit: 1);
        bus.PublishToQueue("q", Message("1") with { ExpiresAt = clock.UtcNow.AddSeconds(1) });

        clock.UtcNow = clock.UtcNow.AddSeconds(2);

        CollectionAssert.AreEqual(new[] { "1" }, Claimed(consumer));
    }

    [TestMethod]
    public void AReturnedHeldMessageThatHasExpired_IsDroppedAsAnyReadyMessageIs()
    {
        var bus = NewBus(out var connection);
        var consumer = Consume(connection, [], credit: 1);
        bus.PublishToQueue("q", Message("one") with { ExpiresAt = clock.UtcNow.AddSeconds(1) });
        clock.UtcNow = clock.UtcNow.AddSeconds(2);
        var start = events.Events.Count;

        consumer.SetDeliveryCredit(0);

        Assert.AreEqual(0, connection.InspectQueue("q").ReadyMessageCount);
        CollectionAssert.AreEqual(new[] { Change(StoreChange.Deleted, 3) }, EventsSince(start));
    }

    [TestMethod]
    public void AStoppedConsumer_KeepsItsHeldDeliveries_UntilItClaimsThemOrEnds()
    {
        var bus = NewBus(out var connection);
        var consumer = Consume(connection, [], credit: 2);
        Send(bus, "1", "2");

        consumer.Stop();

        Assert.IsTrue(consumer.TryClaimDelivery(out var claimed));
        Assert.AreEqual("1", Text(claimed));
        consumer.Dispose();
        Assert.AreEqual(2, connection.InspectQueue("q").ReadyMessageCount);
    }

    [TestMethod]
    public void PublishToQueue_OnlyWhenConsumed_AndTheOnlyConsumerHasNoCreditLeft_IsNoReadyConsumer()
    {
        var bus = NewBus(out var connection);
        var consumer = Consume(connection, [], credit: 1);

        var withCredit = bus.PublishToQueue("q", Message("1"), account: null, onlyWhenConsumed: true);
        var whileHolding = bus.PublishToQueue("q", Message("2"), account: null, onlyWhenConsumed: true);
        consumer.TryClaimDelivery(out _);
        var afterClaim = bus.PublishToQueue("q", Message("3"), account: null, onlyWhenConsumed: true);

        Assert.AreEqual(MessageBusOutcome.Succeeded, withCredit);
        Assert.AreEqual(MessageBusOutcome.NoReadyConsumer, whileHolding);
        Assert.AreEqual(MessageBusOutcome.NoReadyConsumer, afterClaim);
    }

    [TestMethod]
    public void PublishToConsumedQueues_AQueueWhoseOnlyConsumerHasNoCreditLeft_IsCountedUnconsumed()
    {
        var bus = NewBus(out var connection);
        connection.BindQueue("q", "t");
        Consume(connection, [], credit: 1);

        var withCredit = connection.PublishToConsumedQueues(Message("1"));
        var withoutCredit = connection.PublishToConsumedQueues(Message("2"));

        Assert.AreEqual(1, withCredit.QueuedCount);
        Assert.AreEqual(0, withCredit.UnconsumedQueueCount);
        Assert.AreEqual(0, withoutCredit.QueuedCount);
        Assert.AreEqual(1, withoutCredit.UnconsumedQueueCount);
    }

    private MessageBus NewBus(out BusConnection connection)
    {
        var bus = new MessageBus(clock, events: events);
        connection = bus.Connect();
        connection.DeclareQueue("q");
        return bus;
    }

    private SurlEvent[] EventsSince(int count) => events.Events.Skip(count).ToArray();

    private static BusQueueConsumer Consume(BusConnection connection, List<BusQueueDelivery> received, uint credit)
    {
        var consumer = connection.Consume("q", received.Add).Consumer!;
        consumer.SetDeliveryCredit(credit);
        consumer.Start();
        return consumer;
    }

    // Leaves a consumer holding "rejected", once claimed and rejected with requeue, and "never claimed".
    private static BusQueueConsumer HoldOneRejectedAndOneNeverClaimed(MessageBus bus, BusConnection connection)
    {
        var consumer = Consume(connection, [], credit: 1);
        Send(bus, "rejected", "never claimed");
        consumer.TryClaimDelivery(out var claimed);
        consumer.Reject(claimed!.DeliveryTag, requeue: true);
        consumer.SetDeliveryCredit(2);
        return consumer;
    }

    private static void GiveBack(BusQueueConsumer consumer, string way)
    {
        if (way == "lower the credit")
        {
            consumer.SetDeliveryCredit(0);
        }
        else if (way == "end the consumer")
        {
            consumer.Dispose();
        }
        else
        {
            consumer.Recover();
        }
    }

    private static List<BusQueueDelivery> ClaimedDeliveries(BusQueueConsumer consumer)
    {
        var claimed = new List<BusQueueDelivery>();
        while (consumer.TryClaimDelivery(out var delivery))
        {
            claimed.Add(delivery);
        }

        return claimed;
    }

    private static string[] Claimed(BusQueueConsumer consumer) => Payloads(ClaimedDeliveries(consumer));

    private static void Send(MessageBus bus, params string[] payloads)
    {
        foreach (var payload in payloads)
        {
            bus.PublishToQueue("q", Message(payload));
        }
    }

    private static BusMessage Message(string payload) => new("t", Encoding.UTF8.GetBytes(payload));

    private static string Text(BusQueueDelivery delivery) => Encoding.UTF8.GetString(delivery.Message.Payload.Span);

    private static string[] Payloads(List<BusQueueDelivery> deliveries) => deliveries.Select(Text).ToArray();

    private static SurlEvent Change(StoreChange change, long byteCount) =>
        new StoreChangedEvent(StoreKind.Bus, change, "$queue/q", byteCount, null);
}
