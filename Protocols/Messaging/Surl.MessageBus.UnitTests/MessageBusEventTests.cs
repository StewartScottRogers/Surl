using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.MessageBus;

/// <summary>
/// The changes the bus publishes on its event stream (ADR-0124 decision 1, BL-790): each one a
/// <see cref="StoreChangedEvent"/> of <see cref="StoreKind.Bus"/>, named by the canonical topic or
/// by <see cref="MessageBus.QueueChangeName"/>, in the order the changes happened.
/// </summary>
[TestClass]
public sealed class MessageBusEventTests
{
    private readonly SettableTimeProvider clock = new();
    private readonly RecordingSurlEventPublisher events = new();

    [TestMethod]
    public void Publish_ToATopicWithTwoSubscriptions_PublishesPublishedThenOneDeliveredEach()
    {
        var bus = new MessageBus(clock, events: events);
        using var connection = bus.Connect();
        connection.Subscribe("a/+", _ => { });
        connection.Subscribe("a/#", _ => { });

        bus.Publish(Message("a/b", "hi"));

        CollectionAssert.AreEqual(
            new[] { Change(StoreChange.Published, "a/b", 2), Change(StoreChange.Delivered, "a/b", 2), Change(StoreChange.Delivered, "a/b", 2) },
            events.Events.ToArray());
    }

    [TestMethod]
    public void Publish_RetainedThenCleared_PublishesWrittenAndDeletedBeforeEachPublished()
    {
        var bus = new MessageBus(clock, events: events);

        bus.Publish(Message("a/b", "abc") with { Retain = true });
        bus.Publish(Message("a/b", string.Empty) with { Retain = true });

        CollectionAssert.AreEqual(
            new[]
            {
                Change(StoreChange.Written, "a/b", 3),
                Change(StoreChange.Published, "a/b", 3),
                Change(StoreChange.Deleted, "a/b", 3),
                Change(StoreChange.Published, "a/b", 0),
            },
            events.Events.ToArray());
    }

    [TestMethod]
    public void Subscribe_IncludingRetained_AnExpiredRetainedMessage_PublishesItsDeletion()
    {
        var bus = new MessageBus(clock, events: events);
        bus.Publish(Message("a/b", "abc") with { Retain = true, ExpiresAt = clock.UtcNow.AddSeconds(1) });
        clock.UtcNow = clock.UtcNow.AddSeconds(2);
        using var connection = bus.Connect();

        connection.Subscribe("a/b", _ => { }, includeRetained: true);

        Assert.AreEqual(Change(StoreChange.Deleted, "a/b", 3), events.Events[^1]);
    }

    [TestMethod]
    public void Publish_ASubscriptionEndedByAnEarlierDelivery_PublishesNoDeliveryForIt()
    {
        var bus = new MessageBus(clock, events: events);
        using var connection = bus.Connect();
        BusSubscription? second = null;
        connection.Subscribe("a/b", _ => second!.Dispose());
        second = connection.Subscribe("a/b", _ => { }).Subscription;

        bus.Publish(Message("a/b", "x"));

        CollectionAssert.AreEqual(
            new[] { Change(StoreChange.Published, "a/b", 1), Change(StoreChange.Delivered, "a/b", 1) },
            events.Events.ToArray());
    }

    [TestMethod]
    public void QueueLifecycle_DeclareSendConsumeSettleAndDelete_PublishesEachChangeUnderTheQueuesName()
    {
        var bus = new MessageBus(clock, events: events);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var received = new List<BusQueueDelivery>();
        var consumer = connection.Consume("q", received.Add).Consumer!;
        consumer.Start();

        bus.PublishToQueue("q", Message("t", "one"));
        consumer.Acknowledge(received[0].DeliveryTag);
        bus.PublishToQueue("q", Message("t", "four"));
        consumer.Reject(received[1].DeliveryTag, requeue: false);
        consumer.Dispose();
        connection.DeleteQueue("q", ifUnused: false, ifEmpty: false);

        CollectionAssert.AreEqual(
            new[]
            {
                Change(StoreChange.Written, "$queue/q", null),
                Change(StoreChange.Enqueued, "$queue/q", 3),
                Change(StoreChange.Delivered, "$queue/q", 3),
                Change(StoreChange.Acknowledged, "$queue/q", 3),
                Change(StoreChange.Enqueued, "$queue/q", 4),
                Change(StoreChange.Delivered, "$queue/q", 4),
                Change(StoreChange.Deleted, "$queue/q", 4),
                Change(StoreChange.Deleted, "$queue/q", null),
            },
            events.Events.ToArray());
    }

    [TestMethod]
    public void GetFromQueue_AndPurgeQueue_PublishDeliveredAndDeletedOnlyWhenAMessageMoved()
    {
        var bus = new MessageBus(clock, events: events);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        bus.PublishToQueue("q", Message("t", "ab"));
        bus.PublishToQueue("q", Message("t", "cde"));
        var mark = events.Events.Count;

        connection.GetFromQueue("q");
        connection.PurgeQueue("q");
        connection.GetFromQueue("q");
        connection.PurgeQueue("q");

        CollectionAssert.AreEqual(
            new[] { Change(StoreChange.Delivered, "$queue/q", 2), Change(StoreChange.Deleted, "$queue/q", 3) },
            events.Events.Skip(mark).ToArray());
    }

    [TestMethod]
    public void Publish_ToATopicABoundQueueTakes_PublishesPublishedThenEnqueued()
    {
        var bus = new MessageBus(clock, events: events);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        connection.BindQueue("q", "a/#");
        var mark = events.Events.Count;

        bus.Publish(Message("a/b", "xy"));

        CollectionAssert.AreEqual(
            new[] { Change(StoreChange.Published, "a/b", 2), Change(StoreChange.Enqueued, "$queue/q", 2) },
            events.Events.Skip(mark).ToArray());
    }

    [TestMethod]
    public void Publish_AQueueConsumerEndedByAnEarlierDelivery_PublishesNoDeliveryForIt()
    {
        var bus = new MessageBus(clock, events: events);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        connection.BindQueue("q", "a/b");
        var consumer = connection.Consume("q", _ => { }).Consumer!;
        consumer.Start();
        connection.Subscribe("a/b", _ => consumer.Dispose());
        var mark = events.Events.Count;

        bus.Publish(Message("a/b", "x"));

        Assert.IsFalse(events.Events.Skip(mark).Contains(Change(StoreChange.Delivered, "$queue/q", 1)));
        Assert.Contains(Change(StoreChange.Delivered, "a/b", 1), events.Events.Skip(mark).ToList());
    }

    [TestMethod]
    public void CreateLog_ANewTopic_PublishesWrittenOnce()
    {
        var bus = new MessageBus(clock, events: events);

        bus.CreateLog("k");
        bus.CreateLog("k");

        CollectionAssert.AreEqual(new[] { Change(StoreChange.Written, "k", null) }, events.Events.ToArray());
    }

    [TestMethod]
    public void QueueChangeName_IsTheQueueFilterTheAccessGrantsUse()
    {
        Assert.AreEqual("$queue/orders", MessageBus.QueueChangeName("orders"));
        Assert.ThrowsExactly<ArgumentNullException>(() => MessageBus.QueueChangeName(null!));
    }

    private static StoreChangedEvent Change(StoreChange change, string name, long? byteCount) =>
        new(StoreKind.Bus, change, name, byteCount, ExchangeId: null);

    private static BusMessage Message(string topic, string payload) => new(topic, Encoding.UTF8.GetBytes(payload));
}
