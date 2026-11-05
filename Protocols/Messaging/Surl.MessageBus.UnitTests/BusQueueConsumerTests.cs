namespace Surl.MessageBus;

[TestClass]
public sealed class BusQueueConsumerTests
{
    private readonly SettableTimeProvider clock = new();

    [TestMethod]
    public void Publish_OnABoundTopic_IsQueuedUntilAConsumerStarts()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        connection.BindQueue("q", "t");
        var received = new List<BusQueueDelivery>();

        var result = bus.Publish(new BusMessage("t", "hi"u8.ToArray()));
        var consumer = connection.Consume("q", received.Add).Consumer!;
        var receivedBeforeStart = received.Count;
        consumer.Start();
        consumer.Start();

        Assert.AreEqual(new BusPublishResult(MessageBusOutcome.Succeeded, 0) { QueuedCount = 1 }, result);
        Assert.AreEqual(0, receivedBeforeStart);
        Assert.HasCount(1, received);
        Assert.AreEqual("q", received[0].QueueName);
        Assert.AreEqual("q", consumer.QueueName);
        Assert.AreEqual(0, consumer.MaxUnacknowledged);
        CollectionAssert.AreEqual("hi"u8.ToArray(), received[0].Message.Payload.ToArray());
        Assert.IsFalse(received[0].Redelivered);
    }

    [TestMethod]
    public void TwoConsumers_EachMessageGoesToExactlyOne_RoundRobin()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var first = new List<BusQueueDelivery>();
        var second = new List<BusQueueDelivery>();
        connection.Consume("q", first.Add).Consumer!.Start();
        connection.Consume("q", second.Add).Consumer!.Start();

        foreach (var payload in new[] { "1", "2", "3", "4" })
        {
            bus.PublishToQueue("q", Message(payload));
        }

        CollectionAssert.AreEqual(new[] { "1", "3" }, Payloads(first));
        CollectionAssert.AreEqual(new[] { "2", "4" }, Payloads(second));
    }

    [TestMethod]
    public void ConsumerNotStarted_IsPassedOverForAStartedOne()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var idle = new List<BusQueueDelivery>();
        connection.Consume("q", idle.Add);
        var started = new List<BusQueueDelivery>();
        connection.Consume("q", started.Add).Consumer!.Start();

        bus.PublishToQueue("q", Message("1"));
        bus.PublishToQueue("q", Message("2"));

        Assert.IsEmpty(idle);
        CollectionAssert.AreEqual(new[] { "1", "2" }, Payloads(started));
    }

    [TestMethod]
    public void MaxUnacknowledged_FullConsumer_IsPassedOverUntilItAcknowledges()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var received = new List<BusQueueDelivery>();
        var consumer = connection.Consume("q", received.Add, maxUnacknowledged: 1).Consumer!;
        consumer.Start();
        bus.PublishToQueue("q", Message("1"));
        bus.PublishToQueue("q", Message("2"));
        var receivedBeforeAcknowledging = received.Count;

        var outcome = consumer.Acknowledge(received[0].DeliveryTag);

        Assert.AreEqual(1, receivedBeforeAcknowledging);
        Assert.AreEqual(MessageBusOutcome.Succeeded, outcome);
        CollectionAssert.AreEqual(new[] { "1", "2" }, Payloads(received));
        Assert.AreNotEqual(received[0].DeliveryTag, received[1].DeliveryTag);
    }

    [TestMethod]
    public void Acknowledge_MessageIsGone()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var received = new List<BusQueueDelivery>();
        var consumer = connection.Consume("q", received.Add).Consumer!;
        consumer.Start();
        bus.PublishToQueue("q", Message("1"));

        consumer.Acknowledge(received[0].DeliveryTag);
        consumer.Dispose();
        var later = new List<BusQueueDelivery>();
        connection.Consume("q", later.Add).Consumer!.Start();

        Assert.IsEmpty(later);
        Assert.AreEqual(MessageBusOutcome.UnknownDeliveryTag, consumer.Acknowledge(received[0].DeliveryTag));
    }

    [TestMethod]
    public void ConsumerDisposed_UnacknowledgedMessageIsRedeliveredMarkedToAnotherConsumer()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var first = new List<BusQueueDelivery>();
        var consumer = connection.Consume("q", first.Add).Consumer!;
        consumer.Start();
        bus.PublishToQueue("q", Message("1"));
        var second = new List<BusQueueDelivery>();
        connection.Consume("q", second.Add).Consumer!.Start();

        consumer.Dispose();
        consumer.Dispose();

        Assert.HasCount(1, second);
        Assert.AreEqual("1", Payloads(second).Single());
        Assert.IsTrue(second[0].Redelivered);
        Assert.AreNotEqual(first[0].DeliveryTag, second[0].DeliveryTag);
    }

    [TestMethod]
    public void ConnectionDisposed_ItsConsumersUnacknowledgedMessagesAreRedelivered()
    {
        var bus = new MessageBus(clock);
        using var stays = bus.Connect();
        stays.DeclareQueue("q");
        var goes = bus.Connect();
        goes.Consume("q", _ => { }).Consumer!.Start();
        bus.PublishToQueue("q", Message("1"));
        var received = new List<BusQueueDelivery>();
        stays.Consume("q", received.Add).Consumer!.Start();

        goes.Dispose();

        Assert.HasCount(1, received);
        Assert.IsTrue(received[0].Redelivered);
    }

    [TestMethod]
    public void Redelivered_KeepsItsPlaceAheadOfLaterMessages()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var first = new List<BusQueueDelivery>();
        var consumer = connection.Consume("q", first.Add, maxUnacknowledged: 1).Consumer!;
        consumer.Start();
        bus.PublishToQueue("q", Message("1"));
        bus.PublishToQueue("q", Message("2"));

        consumer.Dispose();
        var later = new List<BusQueueDelivery>();
        connection.Consume("q", later.Add).Consumer!.Start();

        CollectionAssert.AreEqual(new[] { "1", "2" }, Payloads(later));
        CollectionAssert.AreEqual(new[] { true, false }, later.Select(delivery => delivery.Redelivered).ToArray());
    }

    [TestMethod]
    public void Reject_WithRequeue_IsRedeliveredMarked()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var received = new List<BusQueueDelivery>();
        var consumer = connection.Consume("q", received.Add).Consumer!;
        consumer.Start();
        bus.PublishToQueue("q", Message("1"));

        var outcome = consumer.Reject(received[0].DeliveryTag, requeue: true);

        Assert.AreEqual(MessageBusOutcome.Succeeded, outcome);
        Assert.HasCount(2, received);
        Assert.IsTrue(received[1].Redelivered);
    }

    [TestMethod]
    public void Reject_WithoutRequeue_IsDropped()
    {
        var bus = new MessageBus(clock, maxQueuedBytes: 1);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var received = new List<BusQueueDelivery>();
        var consumer = connection.Consume("q", received.Add).Consumer!;
        consumer.Start();
        bus.PublishToQueue("q", Message("1"));

        var outcome = consumer.Reject(received[0].DeliveryTag, requeue: false);
        consumer.Dispose();
        var later = new List<BusQueueDelivery>();
        connection.Consume("q", later.Add).Consumer!.Start();

        Assert.AreEqual(MessageBusOutcome.Succeeded, outcome);
        Assert.IsEmpty(later);
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.PublishToQueue("q", Message("2")), "the dropped message's bytes are free again");
        Assert.AreEqual(MessageBusOutcome.UnknownDeliveryTag, consumer.Reject(received[0].DeliveryTag, requeue: true));
    }

    [TestMethod]
    public void Recover_RedeliversEveryUnacknowledgedMessageMarked()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var received = new List<BusQueueDelivery>();
        var consumer = connection.Consume("q", received.Add).Consumer!;
        consumer.Start();
        bus.PublishToQueue("q", Message("1"));
        bus.PublishToQueue("q", Message("2"));

        consumer.Recover();

        CollectionAssert.AreEqual(new[] { "1", "2", "1", "2" }, Payloads(received));
        CollectionAssert.AreEqual(new[] { false, false, true, true }, received.Select(delivery => delivery.Redelivered).ToArray());
    }

    [TestMethod]
    public void AutoDeleteQueue_LastConsumerEnds_IsDeleted()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var flags = new BusQueueFlags(AutoDelete: true);
        connection.DeclareQueue("q", flags);
        var first = connection.Consume("q", _ => { }).Consumer!;
        var second = connection.Consume("q", _ => { }).Consumer!;

        first.Dispose();
        var afterFirst = connection.DeclareQueue("q", flags);
        second.Dispose();

        Assert.AreEqual(new BusQueueDeclareResult(MessageBusOutcome.Succeeded, 0, 1), afterFirst);
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, bus.PublishToQueue("q", Message("x")));
    }

    [TestMethod]
    public void ExpiredMessage_IsDroppedOnReadAndNeverDelivered()
    {
        var bus = new MessageBus(clock, maxQueuedBytes: 1);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        bus.PublishToQueue("q", Message("1") with { ExpiresAt = clock.UtcNow.AddSeconds(1) });
        bus.PublishToQueue("q", Message("2") with { ExpiresAt = clock.UtcNow });
        clock.UtcNow = clock.UtcNow.AddSeconds(2);
        var received = new List<BusQueueDelivery>();

        connection.Consume("q", received.Add).Consumer!.Start();

        Assert.IsEmpty(received);
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.PublishToQueue("q", Message("3")), "the expired message's bytes are free again");
        Assert.HasCount(1, received);
    }

    [TestMethod]
    public void Delivery_ToAConsumerEndedBeforeItsTurn_IsSkippedAndTheMessageRedelivered()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var skipped = new List<BusQueueDelivery>();
        var consumer = connection.Consume("q", skipped.Add).Consumer!;
        consumer.Start();
        connection.Subscribe("trigger", _ =>
        {
            bus.PublishToQueue("q", Message("1"));
            consumer.Dispose();
        });

        bus.Publish(new BusMessage("trigger", "x"u8.ToArray()));
        var later = new List<BusQueueDelivery>();
        connection.Consume("q", later.Add).Consumer!.Start();

        Assert.IsEmpty(skipped);
        Assert.HasCount(1, later);
    }

    [TestMethod]
    public void Consume_UnknownQueueOrPastTheSubscriptionBound_IsRefused()
    {
        var bus = new MessageBus(clock, maxSubscriptionsPerConnection: 1);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var consumer = connection.Consume("q", _ => { }).Consumer!;

        Assert.AreEqual(new BusConsumeResult(MessageBusOutcome.UnknownQueue, null), connection.Consume("none", _ => { }));
        Assert.AreEqual(new BusConsumeResult(MessageBusOutcome.TooManySubscriptions, null), connection.Consume("q", _ => { }));
        consumer.Dispose();
        Assert.AreEqual(MessageBusOutcome.Succeeded, connection.Consume("q", _ => { }).Outcome);
    }

    private static BusMessage Message(string payload) => new("t", System.Text.Encoding.UTF8.GetBytes(payload));

    private static string[] Payloads(List<BusQueueDelivery> deliveries) =>
        deliveries.Select(delivery => System.Text.Encoding.UTF8.GetString(delivery.Message.Payload.Span)).ToArray();
}
