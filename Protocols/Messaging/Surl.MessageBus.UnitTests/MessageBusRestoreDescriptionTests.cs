using System.Text;

namespace Surl.MessageBus;

/// <summary>
/// A restore that changes a queue's subscription description ends the queue's consumers and get
/// holders before it puts the snapshot's messages back, and one that leaves the description as it
/// was keeps them (BL-2051; ADR-0146, Amendment 8).
/// </summary>
[TestClass]
public sealed class MessageBusRestoreDescriptionTests
{
    private const string Queue = "subscription-queue";
    private const string Filter = "sport/+";

    private readonly SettableTimeProvider clock = new();

    [TestMethod]
    [DataRow(null, "described", DisplayName = "none to a description")]
    [DataRow("older", "described", DisplayName = "one description to another")]
    [DataRow("older", null, DisplayName = "a description to none")]
    [DataRow("Described", "described", DisplayName = "a description differing only in case")]
    public void Restore_ChangingAQueuesDescription_ItsConsumerReceivesNothingRestoredAndTheMessagesStayReady(string? live, string? restored)
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var snapshot = SnapshotThenRedeclare(bus, connection, restored, live);
        var received = new List<string>();
        var consumer = Consume(connection, received);
        consumer.Start();

        bus.Restore(snapshot);
        bus.Publish(new BusMessage("sport/chess", "later"u8.ToArray()));

        var inspected = connection.InspectQueue(Queue);
        Assert.IsEmpty(received);
        Assert.AreEqual(new BusQueueInspectResult(MessageBusOutcome.Succeeded, 2, 0, restored), inspected);
    }

    [TestMethod]
    public void Restore_ChangingAQueuesDescription_ALaterConsumerReceivesTheRestoredMessages()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var snapshot = SnapshotThenRedeclare(bus, connection, restored: "described", live: null);
        Consume(connection, []).Start();
        bus.Restore(snapshot);
        var received = new List<string>();

        connection.ConsumeSubscriptionQueue(Queue, "described", Filter, delivery => received.Add(Text(delivery))).Consumer!.Start();

        CollectionAssert.AreEqual(new[] { "kept" }, received);
    }

    [TestMethod]
    public void Restore_ChangingAQueuesDescription_AConsumerNotYetStartedStaysEndedWhenStarted()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var snapshot = SnapshotThenRedeclare(bus, connection, restored: "described", live: null);
        var received = new List<string>();
        var consumer = Consume(connection, received);

        bus.Restore(snapshot);
        consumer.Start();
        consumer.SetDeliveryCredit(10);

        Assert.IsEmpty(received);
        Assert.IsFalse(consumer.TryClaimDelivery(out _));
        Assert.AreEqual(1, connection.InspectQueue(Queue).ReadyMessageCount);
    }

    [TestMethod]
    public void Restore_ChangingAQueuesDescription_ItsConsumerNoLongerCountsAgainstTheConnectionsSubscriptions()
    {
        var bus = new MessageBus(clock, maxSubscriptionsPerConnection: 1);
        using var connection = bus.Connect();
        var snapshot = SnapshotThenRedeclare(bus, connection, restored: "described", live: null);
        var ended = Consume(connection, []);

        bus.Restore(snapshot);
        var first = connection.Consume(Queue, _ => { });
        ended.Dispose();
        var second = connection.Consume(Queue, _ => { });

        Assert.AreEqual(MessageBusOutcome.Succeeded, first.Outcome);
        Assert.AreEqual(MessageBusOutcome.TooManySubscriptions, second.Outcome);
    }

    [TestMethod]
    public void Restore_ChangingAQueuesDescription_ItsGetHolderIsEndedAndALaterGetTakesARestoredMessage()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var snapshot = SnapshotThenRedeclare(bus, connection, restored: "described", live: null);
        bus.PublishToQueue(Queue, new BusMessage("t", "dropped"u8.ToArray()));
        var held = connection.GetFromQueue(Queue).Delivery!;

        bus.Restore(snapshot);
        var settled = connection.AcknowledgeGet(Queue, held.DeliveryTag);
        var got = connection.GetFromQueue(Queue);

        Assert.AreEqual(MessageBusOutcome.UnknownDeliveryTag, settled);
        Assert.AreEqual("kept", Text(got.Delivery!));
    }

    [TestMethod]
    public void Restore_ChangingAnAutoDeleteQueuesDescription_KeepsTheQueueWithNoConsumer()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var flags = new BusQueueFlags(AutoDelete: true);
        connection.DeclareQueue(Queue, flags, "described");
        connection.BindQueue(Queue, Filter);
        bus.Publish(new BusMessage("sport/chess", "kept"u8.ToArray()));
        var snapshot = bus.TakeSnapshot();
        connection.DeleteQueue(Queue);
        connection.DeclareQueue(Queue, flags);
        Consume(connection, []).Start();

        bus.Restore(snapshot);

        Assert.AreEqual(new BusQueueInspectResult(MessageBusOutcome.Succeeded, 1, 0, "described"), connection.InspectQueue(Queue));
    }

    // The live queue is unbound and the restored one bound, so the bindings change here too.
    [TestMethod]
    [DataRow(null, DisplayName = "no description")]
    [DataRow("described", DisplayName = "the same description")]
    public void Restore_LeavingAQueuesDescriptionUnchanged_KeepsItsConsumersWhichReceiveTheRestoredMessages(string? description)
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var snapshot = SnapshotThenRedeclare(bus, connection, restored: description, live: description);
        var received = new List<string>();
        var consumer = Consume(connection, received);
        consumer.Start();

        bus.Restore(snapshot);

        CollectionAssert.AreEqual(new[] { "kept" }, received);
        Assert.AreEqual(1, connection.InspectQueue(Queue).ConsumerCount);
    }

    // Takes a snapshot of the queue carrying the restored description, bound to the filter and holding
    // one message, then leaves under its name an empty, unbound queue carrying the live description.
    private static MessageBusSnapshot SnapshotThenRedeclare(MessageBus bus, BusConnection connection, string? restored, string? live)
    {
        connection.DeclareQueue(Queue, subscriptionDescription: restored);
        connection.BindQueue(Queue, Filter);
        bus.Publish(new BusMessage("sport/chess", "kept"u8.ToArray()));
        var snapshot = bus.TakeSnapshot();
        connection.DeleteQueue(Queue);
        connection.DeclareQueue(Queue, subscriptionDescription: live);
        return snapshot;
    }

    private static BusQueueConsumer Consume(BusConnection connection, List<string> received) =>
        connection.Consume(Queue, delivery => received.Add(Text(delivery))).Consumer!;

    private static string Text(BusQueueDelivery delivery) => Encoding.UTF8.GetString(delivery.Message.Payload.Span);
}
