using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.MessageBus;

/// <summary>
/// A queue a restore puts back takes the snapshot's flags and has no owning connection, whatever
/// live queue held its name; one whose flags change ends the live queue's consumers and get holders
/// (BL-2095; ADR-0146, Amendment 9).
/// </summary>
[TestClass]
public sealed class MessageBusRestoreFlagsTests
{
    private const string Queue = "flagged-queue";

    private static readonly BusQueueFlags Exclusive = new(Exclusive: true);

    private readonly SettableTimeProvider clock = new();

    [TestMethod]
    public void Restore_OverAnExclusiveQueue_AnotherConnectionInspectsAndConsumesIt()
    {
        var bus = new MessageBus(clock);
        using var owner = bus.Connect();
        using var other = bus.Connect();
        var snapshot = SnapshotThenRedeclare(bus, owner, Exclusive);
        var received = new List<string>();

        bus.Restore(snapshot);
        var inspected = other.InspectQueue(Queue);
        var consumed = other.Consume(Queue, delivery => received.Add(Text(delivery)));
        consumed.Consumer?.Start();

        Assert.AreEqual(new BusQueueInspectResult(MessageBusOutcome.Succeeded, 1, 0), inspected);
        Assert.AreEqual(MessageBusOutcome.Succeeded, consumed.Outcome);
        CollectionAssert.AreEqual(new[] { "kept" }, received);
    }

    [TestMethod]
    public void Restore_OverAnExclusiveQueue_TheFormerOwnersEndLeavesItAndItsMessages()
    {
        var events = new RecordingSurlEventPublisher();
        var bus = new MessageBus(clock, events: events);
        using var owner = bus.Connect();
        using var other = bus.Connect();
        var snapshot = SnapshotThenRedeclare(bus, owner, Exclusive);
        var mark = events.Events.Count;

        bus.Restore(snapshot);
        owner.Dispose();
        var inspected = other.InspectQueue(Queue);

        Assert.AreEqual(new BusQueueInspectResult(MessageBusOutcome.Succeeded, 1, 0), inspected);
        CollectionAssert.AreEqual(
            new[] { StoreChange.Restored },
            events.Events.Skip(mark).OfType<StoreChangedEvent>().Select(change => change.Change).ToArray());
    }

    [TestMethod]
    public void Restore_OverAnExclusiveQueue_EndsTheOwnersConsumer()
    {
        var bus = new MessageBus(clock, maxSubscriptionsPerConnection: 1);
        using var owner = bus.Connect();
        var snapshot = SnapshotThenRedeclare(bus, owner, Exclusive);
        var received = new List<BusQueueDelivery>();
        var consumer = owner.Consume(Queue, received.Add, maxUnacknowledged: 1).Consumer!;
        consumer.Start();
        bus.PublishToQueue(Queue, Message("live"));

        bus.Restore(snapshot);
        var settled = consumer.Acknowledge(received[0].DeliveryTag);
        var inspected = owner.InspectQueue(Queue);
        var again = owner.Consume(Queue, _ => { });

        CollectionAssert.AreEqual(new[] { "live" }, received.Select(Text).ToArray());
        Assert.AreEqual(MessageBusOutcome.UnknownDeliveryTag, settled);
        Assert.AreEqual(new BusQueueInspectResult(MessageBusOutcome.Succeeded, 1, 0), inspected);
        Assert.AreEqual(MessageBusOutcome.Succeeded, again.Outcome);
    }

    [TestMethod]
    [DataRow(true, false, false, DisplayName = "over a durable queue")]
    [DataRow(false, true, false, DisplayName = "over an exclusive queue")]
    [DataRow(false, false, true, DisplayName = "over an auto-delete queue")]
    public void Restore_OverAQueueWithOtherFlags_DeclaringWithTheSnapshotsFlagsSucceeds(bool durable, bool exclusive, bool autoDelete)
    {
        var bus = new MessageBus(clock);
        using var owner = bus.Connect();
        var liveFlags = new BusQueueFlags(durable, exclusive, autoDelete);
        var snapshot = SnapshotThenRedeclare(bus, owner, liveFlags);

        bus.Restore(snapshot);
        var withSnapshotFlags = owner.DeclareQueue(Queue);
        var withLiveFlags = owner.DeclareQueue(Queue, liveFlags);

        Assert.AreEqual(MessageBusOutcome.Succeeded, withSnapshotFlags.Outcome);
        Assert.AreEqual(MessageBusOutcome.QueueFlagsDiffer, withLiveFlags.Outcome);
    }

    [TestMethod]
    public void Restore_ADurableQueueOverAPlainOne_TakesTheSnapshotsFlags()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var durable = new BusQueueFlags(Durable: true);
        connection.DeclareQueue(Queue, durable);
        var snapshot = bus.TakeSnapshot();
        connection.DeleteQueue(Queue);
        connection.DeclareQueue(Queue);

        bus.Restore(snapshot);
        var withSnapshotFlags = connection.DeclareQueue(Queue, durable);
        var withLiveFlags = connection.DeclareQueue(Queue);

        Assert.AreEqual(MessageBusOutcome.Succeeded, withSnapshotFlags.Outcome);
        Assert.AreEqual(MessageBusOutcome.QueueFlagsDiffer, withLiveFlags.Outcome);
    }

    [TestMethod]
    public void Restore_OverAnAutoDeleteQueue_KeepsTheQueueWhenALaterConsumerEnds()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var snapshot = SnapshotThenRedeclare(bus, connection, new BusQueueFlags(AutoDelete: true));
        var received = new List<string>();
        var old = connection.Consume(Queue, delivery => received.Add(Text(delivery))).Consumer!;
        old.Start();

        bus.Restore(snapshot);
        old.Dispose();
        connection.Consume(Queue, _ => { }).Consumer?.Dispose();
        var inspected = connection.InspectQueue(Queue);

        Assert.IsEmpty(received);
        Assert.AreEqual(new BusQueueInspectResult(MessageBusOutcome.Succeeded, 1, 0), inspected);
    }

    [TestMethod]
    public void Restore_OverAQueueWithOtherFlags_AStaleGetTagSettlesNoLaterGet()
    {
        var bus = new MessageBus(clock);
        using var owner = bus.Connect();
        var snapshot = SnapshotThenRedeclare(bus, owner, Exclusive);
        bus.PublishToQueue(Queue, Message("live"));
        var stale = owner.GetFromQueue(Queue).Delivery!;

        bus.Restore(snapshot);
        var later = owner.GetFromQueue(Queue).Delivery!;
        var staleSettled = owner.AcknowledgeGet(Queue, stale.DeliveryTag);
        var laterSettled = owner.AcknowledgeGet(Queue, later.DeliveryTag);

        Assert.IsGreaterThan(stale.DeliveryTag, later.DeliveryTag);
        Assert.AreEqual(MessageBusOutcome.UnknownDeliveryTag, staleSettled);
        Assert.AreEqual(MessageBusOutcome.Succeeded, laterSettled);
    }

    [TestMethod]
    public void Restore_NotHoldingAnExclusiveQueuesName_LeavesItLockedToItsOwner()
    {
        var bus = new MessageBus(clock);
        using var owner = bus.Connect();
        using var other = bus.Connect();
        var snapshot = bus.TakeSnapshot();
        owner.DeclareQueue(Queue, Exclusive);

        bus.Restore(snapshot);
        var inspected = other.InspectQueue(Queue);

        Assert.AreEqual(MessageBusOutcome.QueueLocked, inspected.Outcome);
    }

    [TestMethod]
    public void Reset_AnUndeclaredExclusiveQueue_LeavesItLockedToItsOwner()
    {
        var bus = new MessageBus(clock);
        using var owner = bus.Connect();
        using var other = bus.Connect();
        owner.DeclareQueue(Queue, Exclusive);

        bus.Reset();
        var inspected = other.InspectQueue(Queue);

        Assert.AreEqual(MessageBusOutcome.QueueLocked, inspected.Outcome);
    }

    // Takes a snapshot of the queue declared with no flags and holding one message, then leaves under
    // its name an empty queue the owner declared with the live flags.
    private static MessageBusSnapshot SnapshotThenRedeclare(MessageBus bus, BusConnection owner, BusQueueFlags liveFlags)
    {
        owner.DeclareQueue(Queue);
        bus.PublishToQueue(Queue, Message("kept"));
        var snapshot = bus.TakeSnapshot();
        owner.DeleteQueue(Queue);
        owner.DeclareQueue(Queue, liveFlags);
        return snapshot;
    }

    private static BusMessage Message(string payload) => new("t", Encoding.UTF8.GetBytes(payload));

    private static string Text(BusQueueDelivery delivery) => Encoding.UTF8.GetString(delivery.Message.Payload.Span);
}
