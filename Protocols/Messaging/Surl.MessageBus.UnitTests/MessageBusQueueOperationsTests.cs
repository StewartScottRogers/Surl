using System.Text;

namespace Surl.MessageBus;

/// <summary>
/// The queue operations AMQP 0-9-1 needs (BL-730, ADR-0112 decisions 5 and 6): inspection,
/// purge, single get, conditional delete, exclusive consumers, a publish to consumed queues
/// only, a consumer stop that keeps its deliveries, and two priority levels.
/// </summary>
[TestClass]
public sealed class MessageBusQueueOperationsTests
{
    private readonly SettableTimeProvider clock = new();

    [TestMethod]
    public void InspectQueue_AMissingQueue_IsUnknownAndDoesNotCreateIt()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();

        var inspected = connection.InspectQueue("q");

        Assert.AreEqual(new BusQueueInspectResult(MessageBusOutcome.UnknownQueue, 0, 0), inspected);
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, bus.PublishToQueue("q", Message("1")));
    }

    [TestMethod]
    public void InspectQueue_AnExistingQueue_CountsReadyMessagesAndConsumersTakingDeliveries()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        bus.PublishToQueue("q", Message("1"));
        bus.PublishToQueue("q", Message("2"));
        connection.GetFromQueue("q");
        connection.Consume("q", _ => { });
        connection.Consume("q", _ => { }).Consumer!.Stop();

        var inspected = connection.InspectQueue("q");

        Assert.AreEqual(new BusQueueInspectResult(MessageBusOutcome.Succeeded, 1, 1), inspected);
        Assert.AreEqual(new BusQueueDeclareResult(MessageBusOutcome.Succeeded, 1, 1), connection.DeclareQueue("q"));
    }

    [TestMethod]
    public void QueueOperations_AnotherConnectionsExclusiveQueue_AreLocked()
    {
        var bus = new MessageBus(clock);
        using var owner = bus.Connect();
        using var other = bus.Connect();
        owner.DeclareQueue("q", new BusQueueFlags(Exclusive: true));

        Assert.AreEqual(new BusQueueInspectResult(MessageBusOutcome.QueueLocked, 0, 0), other.InspectQueue("q"));
        Assert.AreEqual(new BusQueueRemovalResult(MessageBusOutcome.QueueLocked, 0), other.PurgeQueue("q"));
        Assert.AreEqual(new BusGetResult(MessageBusOutcome.QueueLocked, null, 0), other.GetFromQueue("q"));
        Assert.AreEqual(new BusQueueRemovalResult(MessageBusOutcome.QueueLocked, 0), other.DeleteQueue("q", ifUnused: true, ifEmpty: true));
    }

    [TestMethod]
    public void PurgeQueue_RemovesTheReadyMessagesOnly_AndCountsThem()
    {
        var bus = new MessageBus(clock, maxMessagesPerQueue: 3);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var received = new List<BusQueueDelivery>();
        var consumer = connection.Consume("q", received.Add, maxUnacknowledged: 1).Consumer!;
        consumer.Start();
        bus.PublishToQueue("q", Message("1"));
        bus.PublishToQueue("q", Message("2"));
        bus.PublishToQueue("q", Message("3"));

        var purged = connection.PurgeQueue("q");
        var full = bus.PublishToQueue("q", Message("4"));
        var fifth = bus.PublishToQueue("q", Message("5"));
        var sixth = bus.PublishToQueue("q", Message("6"));
        consumer.Reject(received[0].DeliveryTag, requeue: true);

        Assert.AreEqual(new BusQueueRemovalResult(MessageBusOutcome.Succeeded, 2), purged);
        Assert.AreEqual(MessageBusOutcome.Succeeded, full, "the purged messages no longer count");
        Assert.AreEqual(MessageBusOutcome.Succeeded, fifth);
        Assert.AreEqual(MessageBusOutcome.QueueFull, sixth, "the unacknowledged message still counts");
        CollectionAssert.AreEqual(new[] { "1", "1" }, Payloads(received), "the unacknowledged message was kept");
        Assert.AreEqual(new BusQueueRemovalResult(MessageBusOutcome.UnknownQueue, 0), connection.PurgeQueue("missing"));
    }

    [TestMethod]
    public void GetFromQueue_TakesOneMessage_HeldUnacknowledged_AndRequeuedWhenTheConnectionEnds()
    {
        var bus = new MessageBus(clock);
        using var other = bus.Connect();
        other.DeclareQueue("q");
        bus.PublishToQueue("q", Message("1"));
        bus.PublishToQueue("q", Message("2"));
        var getter = bus.Connect();

        var got = getter.GetFromQueue("q");
        var heldCount = other.InspectQueue("q").ReadyMessageCount;
        getter.Dispose();
        var received = new List<BusQueueDelivery>();
        other.Consume("q", received.Add).Consumer!.Start();

        Assert.AreEqual(MessageBusOutcome.Succeeded, got.Outcome);
        Assert.AreEqual("1", Text(got.Delivery!.Message));
        Assert.AreEqual("q", got.Delivery.QueueName);
        Assert.IsFalse(got.Delivery.Redelivered);
        Assert.AreEqual(1, got.ReadyMessageCount);
        Assert.AreEqual(1, heldCount);
        CollectionAssert.AreEqual(new[] { "1", "2" }, Payloads(received));
        Assert.IsTrue(received[0].Redelivered);
        Assert.IsFalse(received[1].Redelivered);
    }

    [TestMethod]
    public void GetFromQueue_AnEmptyQueue_ReturnsNoDelivery_AndDropsExpiredMessages()
    {
        var bus = new MessageBus(clock, maxMessagesPerQueue: 1);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        bus.PublishToQueue("q", Message("1") with { ExpiresAt = clock.UtcNow.AddSeconds(1) });
        clock.UtcNow = clock.UtcNow.AddSeconds(1);

        var got = connection.GetFromQueue("q");

        Assert.AreEqual(new BusGetResult(MessageBusOutcome.Succeeded, null, 0), got);
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.PublishToQueue("q", Message("2")), "the expired message no longer counts");
        Assert.AreEqual(new BusGetResult(MessageBusOutcome.UnknownQueue, null, 0), connection.GetFromQueue("missing"));
    }

    [TestMethod]
    public void AcknowledgeGet_RemovesTheMessage_AndRejectGetRequeuesOrDropsIt()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        bus.PublishToQueue("q", Message("1"));
        bus.PublishToQueue("q", Message("2"));
        bus.PublishToQueue("q", Message("3"));

        var first = connection.GetFromQueue("q").Delivery!;
        var acknowledged = connection.AcknowledgeGet("q", first.DeliveryTag);
        var acknowledgedAgain = connection.AcknowledgeGet("q", first.DeliveryTag);
        var second = connection.GetFromQueue("q").Delivery!;
        var requeued = connection.RejectGet("q", second.DeliveryTag, requeue: true);
        var secondAgain = connection.GetFromQueue("q").Delivery!;
        var dropped = connection.RejectGet("q", secondAgain.DeliveryTag, requeue: false);
        var third = connection.GetFromQueue("q");

        Assert.AreEqual(MessageBusOutcome.Succeeded, acknowledged);
        Assert.AreEqual(MessageBusOutcome.UnknownDeliveryTag, acknowledgedAgain);
        Assert.AreEqual(MessageBusOutcome.Succeeded, requeued);
        Assert.AreEqual("2", Text(secondAgain.Message));
        Assert.IsTrue(secondAgain.Redelivered);
        Assert.AreEqual(MessageBusOutcome.Succeeded, dropped);
        Assert.AreEqual("3", Text(third.Delivery!.Message));
        Assert.AreEqual(0, third.ReadyMessageCount);
        Assert.AreEqual(MessageBusOutcome.UnknownDeliveryTag, connection.AcknowledgeGet("missing", 1));
        Assert.AreEqual(MessageBusOutcome.UnknownDeliveryTag, bus.Connect().RejectGet("q", third.Delivery.DeliveryTag, requeue: true), "another connection holds no get");
    }

    [TestMethod]
    public void GetFromQueue_WithoutSubOnTheQueue_IsAccessDenied()
    {
        var bus = new MessageBus(clock, configuration: new MessageBusConfiguration { AccessGrants = [new TopicAccessGrant("t", "alice", TopicAccessRights.Publish)] });
        using var connection = bus.Connect("alice");

        Assert.AreEqual(new BusGetResult(MessageBusOutcome.AccessDenied, null, 0), connection.GetFromQueue("q"));
    }

    [TestMethod]
    public void GetAndSettle_OnAClosedConnection_Throw()
    {
        var bus = new MessageBus(clock);
        var connection = bus.Connect();
        connection.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => connection.GetFromQueue("q"));
        Assert.ThrowsExactly<ObjectDisposedException>(() => connection.AcknowledgeGet("q", 1));
        Assert.ThrowsExactly<ArgumentNullException>(() => connection.InspectQueue(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => connection.PurgeQueue(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => connection.GetFromQueue(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => connection.AcknowledgeGet(null!, 1));
        Assert.ThrowsExactly<ArgumentNullException>(() => connection.RejectGet(null!, 1, requeue: false));
        Assert.ThrowsExactly<ArgumentNullException>(() => connection.DeleteQueue(null!, ifUnused: false, ifEmpty: false));
    }

    [TestMethod]
    public void DeleteQueue_IfUnused_WithAConsumer_IsRefusedAndChangesNothing()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var consumer = connection.Consume("q", _ => { }).Consumer!;

        var refused = connection.DeleteQueue("q", ifUnused: true, ifEmpty: false);
        consumer.Stop();
        var deleted = connection.DeleteQueue("q", ifUnused: true, ifEmpty: false);

        Assert.AreEqual(new BusQueueRemovalResult(MessageBusOutcome.QueueInUse, 0), refused);
        Assert.AreEqual(new BusQueueRemovalResult(MessageBusOutcome.Succeeded, 0), deleted, "a stopped consumer no longer uses the queue");
    }

    [TestMethod]
    public void DeleteQueue_IfEmpty_WithAMessage_IsRefused_ReadyOrUnacknowledged()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        bus.PublishToQueue("q", Message("1"));

        var refusedReady = connection.DeleteQueue("q", ifUnused: false, ifEmpty: true);
        var got = connection.GetFromQueue("q").Delivery!;
        var refusedUnacknowledged = connection.DeleteQueue("q", ifUnused: false, ifEmpty: true);
        connection.AcknowledgeGet("q", got.DeliveryTag);
        var deleted = connection.DeleteQueue("q", ifUnused: false, ifEmpty: true);

        Assert.AreEqual(new BusQueueRemovalResult(MessageBusOutcome.QueueNotEmpty, 0), refusedReady);
        Assert.AreEqual(new BusQueueRemovalResult(MessageBusOutcome.QueueNotEmpty, 0), refusedUnacknowledged);
        Assert.AreEqual(new BusQueueRemovalResult(MessageBusOutcome.Succeeded, 0), deleted);
        Assert.AreEqual(new BusQueueInspectResult(MessageBusOutcome.UnknownQueue, 0, 0), connection.InspectQueue("q"));
    }

    [TestMethod]
    public void DeleteQueue_WithoutConditions_CountsEveryMessageDeleted_AndFreesTheirBytes()
    {
        var bus = new MessageBus(clock, maxQueuedBytes: 3);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        connection.Consume("q", _ => { }, maxUnacknowledged: 1).Consumer!.Start();
        bus.PublishToQueue("q", Message("1"));
        bus.PublishToQueue("q", Message("2"));
        connection.GetFromQueue("q");

        var deleted = connection.DeleteQueue("q", ifUnused: false, ifEmpty: false);
        connection.DeclareQueue("q");

        Assert.AreEqual(new BusQueueRemovalResult(MessageBusOutcome.Succeeded, 2), deleted);
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.PublishToQueue("q", Message("abc")), "every deleted message's bytes are free again");
        Assert.AreEqual(new BusQueueRemovalResult(MessageBusOutcome.UnknownQueue, 0), connection.DeleteQueue("missing", ifUnused: true, ifEmpty: true));
    }

    [TestMethod]
    public void DeleteQueue_ADeclaredQueue_IsAccessDeniedWhateverTheConditions()
    {
        var bus = new MessageBus(clock, configuration: new MessageBusConfiguration { Queues = [new DeclaredQueue("orders")] });
        using var connection = bus.Connect();

        Assert.AreEqual(new BusQueueRemovalResult(MessageBusOutcome.AccessDenied, 0), connection.DeleteQueue("orders", ifUnused: true, ifEmpty: true));
    }

    [TestMethod]
    public void Consume_Exclusive_OnAQueueWithAConsumer_IsRefused()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var first = connection.Consume("q", _ => { }).Consumer!;

        var refused = connection.Consume("q", _ => { }, exclusive: true);
        first.Stop();
        var made = connection.Consume("q", _ => { }, exclusive: true);

        Assert.AreEqual(new BusConsumeResult(MessageBusOutcome.ExclusiveConsumeRefused, null), refused);
        Assert.AreEqual(MessageBusOutcome.Succeeded, made.Outcome);
    }

    [TestMethod]
    public void Consume_OnAQueueConsumedExclusively_IsRefusedUntilTheExclusiveConsumerEnds()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        using var other = bus.Connect();
        connection.DeclareQueue("q");
        var exclusive = connection.Consume("q", _ => { }, exclusive: true).Consumer!;

        var refused = other.Consume("q", _ => { });
        var refusedExclusive = other.Consume("q", _ => { }, exclusive: true);
        exclusive.Dispose();
        var made = other.Consume("q", _ => { });

        Assert.AreEqual(new BusConsumeResult(MessageBusOutcome.ExclusiveConsumeRefused, null), refused);
        Assert.AreEqual(new BusConsumeResult(MessageBusOutcome.ExclusiveConsumeRefused, null), refusedExclusive);
        Assert.AreEqual(MessageBusOutcome.Succeeded, made.Outcome);
    }

    [TestMethod]
    public void PublishToConsumedQueues_CopiesOnlyIntoBoundQueuesWithAConsumerAbleToTakeIt()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        foreach (var name in new[] { "consumed", "idle", "full", "unconsumed" })
        {
            connection.DeclareQueue(name);
            connection.BindQueue(name, "t");
        }

        var consumed = new List<BusQueueDelivery>();
        connection.Consume("consumed", consumed.Add).Consumer!.Start();
        connection.Consume("idle", _ => { });
        connection.Consume("full", _ => { }, maxUnacknowledged: 1).Consumer!.Start();
        bus.PublishToQueue("full", Message("0"));
        var live = new List<BusMessage>();
        connection.Subscribe("t", live.Add);

        var result = connection.PublishToConsumedQueues(Message("1"));

        Assert.AreEqual(new BusPublishResult(MessageBusOutcome.Succeeded, 1) { QueuedCount = 1, UnconsumedQueueCount = 3, LogOffset = 0 }, result);
        CollectionAssert.AreEqual(new[] { "1" }, Payloads(consumed));
        Assert.HasCount(1, live);
        Assert.AreEqual(0, connection.InspectQueue("idle").ReadyMessageCount);
        Assert.AreEqual(0, connection.InspectQueue("unconsumed").ReadyMessageCount);
    }

    [TestMethod]
    public void PublishToQueue_OnlyWhenConsumed_IsNoReadyConsumerUnlessAConsumerCanTakeIt()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var received = new List<BusQueueDelivery>();

        var refused = bus.PublishToQueue("q", Message("1"), account: null, onlyWhenConsumed: true);
        var consumer = connection.Consume("q", received.Add).Consumer!;
        consumer.Start();
        var taken = bus.PublishToQueue("q", Message("2"), account: null, onlyWhenConsumed: true);
        consumer.Stop();
        var refusedAfterStop = bus.PublishToQueue("q", Message("3"), account: null, onlyWhenConsumed: true);

        Assert.AreEqual(MessageBusOutcome.NoReadyConsumer, refused);
        Assert.AreEqual(MessageBusOutcome.Succeeded, taken);
        Assert.AreEqual(MessageBusOutcome.NoReadyConsumer, refusedAfterStop);
        CollectionAssert.AreEqual(new[] { "2" }, Payloads(received));
        Assert.AreEqual(0, connection.InspectQueue("q").ReadyMessageCount);
    }

    [TestMethod]
    public void Stop_TakesNoMoreDeliveries_AndKeepsItsOutstandingOnesUntilSettled()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var stopped = new List<BusQueueDelivery>();
        var consumer = connection.Consume("q", stopped.Add).Consumer!;
        consumer.Start();
        bus.PublishToQueue("q", Message("1"));
        bus.PublishToQueue("q", Message("2"));

        consumer.Stop();
        consumer.Stop();
        bus.PublishToQueue("q", Message("3"));
        var other = new List<BusQueueDelivery>();
        connection.Consume("q", other.Add).Consumer!.Start();
        var acknowledged = consumer.Acknowledge(stopped[0].DeliveryTag);
        var rejected = consumer.Reject(stopped[1].DeliveryTag, requeue: true);

        CollectionAssert.AreEqual(new[] { "1", "2" }, Payloads(stopped));
        CollectionAssert.AreEqual(new[] { "3", "2" }, Payloads(other), "the outstanding deliveries stayed with the stopped consumer until settled");
        Assert.AreEqual(MessageBusOutcome.Succeeded, acknowledged);
        Assert.AreEqual(MessageBusOutcome.Succeeded, rejected);
        Assert.IsTrue(other[1].Redelivered);
    }

    [TestMethod]
    public void Stop_ThenDispose_PutsTheOutstandingDeliveriesBack()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var consumer = connection.Consume("q", _ => { }).Consumer!;
        consumer.Start();
        bus.PublishToQueue("q", Message("1"));
        consumer.Stop();

        consumer.Dispose();

        Assert.AreEqual(1, connection.InspectQueue("q").ReadyMessageCount);
    }

    [TestMethod]
    public void AutoDeleteQueue_HeldOnlyByAGet_IsDeletedWhenItsLastConsumerEnds()
    {
        var bus = new MessageBus(clock, maxSubscriptionsPerConnection: 1);
        using var connection = bus.Connect();
        connection.DeclareQueue("q", new BusQueueFlags(AutoDelete: true));
        bus.PublishToQueue("q", Message("1"));
        connection.GetFromQueue("q");
        var consumer = connection.Consume("q", _ => { }).Consumer!;

        consumer.Dispose();

        Assert.AreEqual(MessageBusOutcome.UnknownQueue, connection.InspectQueue("q").Outcome);
        Assert.AreEqual(MessageBusOutcome.Succeeded, connection.Subscribe("t", _ => { }).Outcome, "a get never counts against the connection's subscriptions");
    }

    [TestMethod]
    public void HighPriorityMessages_AreDeliveredBeforeNormalOnes_EachLevelInOrder()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        connection.BindQueue("q", "t");
        bus.PublishToQueue("q", Message("n1"));
        bus.PublishToQueue("q", Message("h1") with { HighPriority = true });
        bus.Publish(Message("n2"));
        bus.Publish(Message("h2") with { HighPriority = true });
        bus.PublishToQueue("q", Message("expired") with { HighPriority = true, ExpiresAt = clock.UtcNow.AddSeconds(1) });
        clock.UtcNow = clock.UtcNow.AddSeconds(1);
        var got = connection.GetFromQueue("q").Delivery!;
        var received = new List<BusQueueDelivery>();

        var consumer = connection.Consume("q", received.Add).Consumer!;
        consumer.Start();
        consumer.Reject(received[0].DeliveryTag, requeue: true);
        connection.RejectGet("q", got.DeliveryTag, requeue: true);

        Assert.AreEqual("h1", Text(got.Message));
        CollectionAssert.AreEqual(new[] { "h2", "n1", "n2", "h2", "h1" }, Payloads(received));
    }

    [TestMethod]
    public async Task HighPriorityMessages_AfterASaveAndLoad_AreStillDeliveredFirst()
    {
        var fixture = new PersistedBusFixture();
        var bus = await fixture.LoadBusAsync();
        var connection = bus.Connect();
        connection.DeclareQueue("q", new BusQueueFlags(Durable: true));
        bus.PublishToQueue("q", Message("n1"));
        bus.PublishToQueue("q", Message("h1") with { HighPriority = true });
        bus.PublishToQueue("q", Message("n2"));
        bus.PublishToQueue("q", Message("h2") with { HighPriority = true });
        connection.GetFromQueue("q");
        await bus.SaveChangesAsync();

        var index = fixture.Read(fixture.Files.QueueIndexPath);
        var loaded = await fixture.LoadBusAsync();
        var loadedConnection = loaded.Connect();
        loadedConnection.DeclareQueue("q", new BusQueueFlags(Durable: true));
        var received = new List<BusQueueDelivery>();
        loadedConnection.Consume("q", received.Add).Consumer!.Start();

        CollectionAssert.AreEqual(
            (byte[])[.. "SURL-BUS-QUEUES-1\n"u8, .. PersistedBusFixture.Bytes("00000001 00000001 71 00000000 00000004 0000000000000000 00 0000000000000001 03 0000000000000002 00 0000000000000003 02")],
            index);
        CollectionAssert.AreEqual(new[] { "h1", "h2", "n1", "n2" }, Payloads(received));
        CollectionAssert.AreEqual(new[] { true, false, false, false }, received.Select(delivery => delivery.Redelivered).ToArray());
        Assert.IsTrue(received[0].Message.HighPriority);
        Assert.IsFalse(received[2].Message.HighPriority);
    }

    private static BusMessage Message(string payload) => new("t", Encoding.UTF8.GetBytes(payload));

    private static string Text(BusMessage message) => Encoding.UTF8.GetString(message.Payload.Span);

    private static string[] Payloads(List<BusQueueDelivery> deliveries) => [.. deliveries.Select(delivery => Text(delivery.Message))];
}
