namespace Surl.MessageBus;

[TestClass]
public sealed class MessageBusQueueDeclareTests
{
    private readonly SettableTimeProvider clock = new();

    [TestMethod]
    public void DeclareQueue_NewName_MakesAnEmptyQueue()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();

        var result = connection.DeclareQueue("q", new BusQueueFlags(Durable: true));

        Assert.AreEqual(new BusQueueDeclareResult(MessageBusOutcome.Succeeded, 0, 0), result);
    }

    [TestMethod]
    public void DeclareQueue_AgainWithTheSameFlags_SucceedsAndCountsReadyMessagesAndConsumers()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        bus.PublishToQueue("q", Message("1"));
        bus.PublishToQueue("q", Message("2"));
        var consumer = connection.Consume("q", _ => { }, maxUnacknowledged: 1).Consumer!;
        consumer.Start();

        var result = connection.DeclareQueue("q");

        Assert.AreEqual(new BusQueueDeclareResult(MessageBusOutcome.Succeeded, 1, 1), result);
    }

    [TestMethod]
    [DataRow(true, false, false)]
    [DataRow(false, false, true)]
    public void DeclareQueue_AgainWithDifferentFlags_ReturnsQueueFlagsDifferAndLeavesTheQueue(bool durable, bool exclusive, bool autoDelete)
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        bus.PublishToQueue("q", Message("1"));

        var result = connection.DeclareQueue("q", new BusQueueFlags(durable, exclusive, autoDelete));

        Assert.AreEqual(new BusQueueDeclareResult(MessageBusOutcome.QueueFlagsDiffer, 0, 0), result);
        Assert.AreEqual(new BusQueueDeclareResult(MessageBusOutcome.Succeeded, 1, 0), connection.DeclareQueue("q"));
    }

    [TestMethod]
    public void DeclareQueue_EmptyName_ReturnsInvalidQueueName()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();

        Assert.AreEqual(MessageBusOutcome.InvalidQueueName, connection.DeclareQueue("").Outcome);
    }

    [TestMethod]
    public void DeclareQueue_PastMaxQueues_ReturnsTooManyQueuesAndMakesNoQueue()
    {
        var bus = new MessageBus(clock, maxQueues: 1);
        using var connection = bus.Connect();
        connection.DeclareQueue("a");

        var result = connection.DeclareQueue("b");

        Assert.AreEqual(new BusQueueDeclareResult(MessageBusOutcome.TooManyQueues, 0, 0), result);
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, bus.PublishToQueue("b", Message("x")));
        Assert.AreEqual(MessageBusOutcome.Succeeded, connection.DeclareQueue("a").Outcome);
    }

    [TestMethod]
    public void ExclusiveQueue_FromAnotherConnection_IsQueueLockedForEveryOperationButASend()
    {
        var bus = new MessageBus(clock);
        using var owner = bus.Connect();
        using var other = bus.Connect();
        var flags = new BusQueueFlags(Exclusive: true);
        owner.DeclareQueue("q", flags);

        Assert.AreEqual(new BusQueueDeclareResult(MessageBusOutcome.QueueLocked, 0, 0), other.DeclareQueue("q", flags));
        Assert.AreEqual(MessageBusOutcome.QueueLocked, other.BindQueue("q", "t"));
        Assert.AreEqual(MessageBusOutcome.QueueLocked, other.UnbindQueue("q", "t"));
        Assert.AreEqual(new BusConsumeResult(MessageBusOutcome.QueueLocked, null), other.Consume("q", _ => { }));
        Assert.AreEqual(MessageBusOutcome.QueueLocked, other.DeleteQueue("q"));
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.PublishToQueue("q", Message("x")));
        Assert.AreEqual(MessageBusOutcome.Succeeded, owner.DeclareQueue("q", flags).Outcome);
    }

    [TestMethod]
    public void ExclusiveQueue_OwnerDisposed_IsDeleted()
    {
        var bus = new MessageBus(clock);
        var owner = bus.Connect();
        owner.DeclareQueue("q", new BusQueueFlags(Exclusive: true));
        owner.Consume("q", _ => { }).Consumer!.Start();
        bus.PublishToQueue("q", Message("x"));

        owner.Dispose();

        Assert.AreEqual(MessageBusOutcome.UnknownQueue, bus.PublishToQueue("q", Message("y")));
    }

    [TestMethod]
    public void ConnectionDisposed_KeepsTheSharedQueuesItDeclared()
    {
        var bus = new MessageBus(clock);
        var declarer = bus.Connect();
        declarer.DeclareQueue("q");

        declarer.Dispose();

        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.PublishToQueue("q", Message("x")));
    }

    [TestMethod]
    public void BindQueue_PublishOnAMatchingTopic_IsQueuedOnceHoweverManyFiltersMatch()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        connection.BindQueue("q", "a/+");
        connection.BindQueue("q", "a/#");
        connection.BindQueue("q", "a/#");

        var matching = bus.Publish(new BusMessage("a/b", "1"u8.ToArray()));
        var other = bus.Publish(new BusMessage("c", "2"u8.ToArray()));

        Assert.AreEqual(1, matching.QueuedCount);
        Assert.AreEqual(0, other.QueuedCount);
        Assert.AreEqual(1, connection.DeclareQueue("q").ReadyMessageCount);
    }

    [TestMethod]
    public void UnbindQueue_LaterPublishesAreNotQueued()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        connection.BindQueue("q", "t");

        var outcome = connection.UnbindQueue("q", "t");
        var result = bus.Publish(new BusMessage("t", "1"u8.ToArray()));

        Assert.AreEqual(MessageBusOutcome.Succeeded, outcome);
        Assert.AreEqual(0, result.QueuedCount);
        Assert.AreEqual(MessageBusOutcome.Succeeded, connection.UnbindQueue("q", "never-bound"));
    }

    [TestMethod]
    public void BindQueue_InvalidFilterOrUnknownQueue_IsRefused()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");

        Assert.AreEqual(MessageBusOutcome.InvalidFilter, connection.BindQueue("q", "a/#b"));
        Assert.AreEqual(MessageBusOutcome.InvalidFilter, connection.UnbindQueue("q", ""));
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, connection.BindQueue("none", "t"));
    }

    [TestMethod]
    public void DeleteQueue_DropsItsMessagesAndEndsItsConsumers()
    {
        var bus = new MessageBus(clock, maxQueuedBytes: 2);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        var received = new List<BusQueueDelivery>();
        var consumer = connection.Consume("q", received.Add, maxUnacknowledged: 1).Consumer!;
        consumer.Start();
        bus.PublishToQueue("q", Message("1"));
        bus.PublishToQueue("q", Message("2"));

        var outcome = connection.DeleteQueue("q");

        Assert.AreEqual(MessageBusOutcome.Succeeded, outcome);
        Assert.AreEqual(MessageBusOutcome.UnknownDeliveryTag, consumer.Acknowledge(received[0].DeliveryTag));
        Assert.AreEqual(MessageBusOutcome.UnknownQueue, connection.DeleteQueue("q"));
        connection.DeclareQueue("q");
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.PublishToQueue("q", Message("ab")), "the deleted messages' bytes are free again");
        consumer.Dispose();
    }

    [TestMethod]
    public void DeleteQueue_FreesItsConsumersFromTheSubscriptionBound()
    {
        var bus = new MessageBus(clock, maxSubscriptionsPerConnection: 1);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        connection.Consume("q", _ => { });

        connection.DeleteQueue("q");

        Assert.AreEqual(MessageBusOutcome.Succeeded, connection.Subscribe("t", _ => { }).Outcome);
    }

    [TestMethod]
    public void QueueOperations_OnADisposedConnection_Throw()
    {
        var bus = new MessageBus(clock);
        var connection = bus.Connect();
        connection.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => connection.DeclareQueue("q"));
        Assert.ThrowsExactly<ObjectDisposedException>(() => connection.BindQueue("q", "t"));
        Assert.ThrowsExactly<ObjectDisposedException>(() => connection.DeleteQueue("q"));
        Assert.ThrowsExactly<ObjectDisposedException>(() => connection.Consume("q", _ => { }));
    }

    [TestMethod]
    public void QueueOperations_NullArguments_Throw()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();

        Assert.ThrowsExactly<ArgumentNullException>(() => connection.DeclareQueue(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => connection.BindQueue(null!, "t"));
        Assert.ThrowsExactly<ArgumentNullException>(() => connection.BindQueue("q", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => connection.UnbindQueue(null!, "t"));
        Assert.ThrowsExactly<ArgumentNullException>(() => connection.UnbindQueue("q", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => connection.DeleteQueue(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => connection.Consume(null!, _ => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => connection.Consume("q", null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => connection.Consume("q", _ => { }, -1));
        Assert.ThrowsExactly<ArgumentNullException>(() => bus.PublishToQueue(null!, Message("x")));
        Assert.ThrowsExactly<ArgumentNullException>(() => bus.PublishToQueue("q", null!));
    }

    [TestMethod]
    public void DeclareQueue_WithASubscriptionDescription_InspectQueueReturnsIt()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();

        var declared = connection.DeclareQueue("q", subscriptionDescription: "/topic/a|filters");
        var inspected = connection.InspectQueue("q");

        Assert.AreEqual(new BusQueueDeclareResult(MessageBusOutcome.Succeeded, 0, 0), declared);
        Assert.AreEqual(new BusQueueInspectResult(MessageBusOutcome.Succeeded, 0, 0, "/topic/a|filters"), inspected);
    }

    [TestMethod]
    [DataRow("/topic/b")]
    [DataRow("")]
    public void DeclareQueue_AgainWithAnotherDescription_ReturnsQueueDescriptionDiffersAndChangesNothing(string otherDescription)
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q", subscriptionDescription: "/topic/a");
        bus.PublishToQueue("q", Message("1"));

        var result = connection.DeclareQueue("q", subscriptionDescription: otherDescription);

        Assert.AreEqual(new BusQueueDeclareResult(MessageBusOutcome.QueueDescriptionDiffers, 0, 0), result);
        Assert.AreEqual(new BusQueueInspectResult(MessageBusOutcome.Succeeded, 1, 0, "/topic/a"), connection.InspectQueue("q"));
    }

    [TestMethod]
    public void DeclareQueue_ADescriptionOnAQueueWithNone_ReturnsQueueDescriptionDiffers()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");

        var result = connection.DeclareQueue("q", subscriptionDescription: "/topic/a");

        Assert.AreEqual(MessageBusOutcome.QueueDescriptionDiffers, result.Outcome);
        Assert.IsNull(connection.InspectQueue("q").SubscriptionDescription);
    }

    [TestMethod]
    [DataRow("/topic/a")]
    [DataRow(null)]
    public void DeclareQueue_AgainWithTheSameDescriptionOrNone_Succeeds(string? description)
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q", subscriptionDescription: "/topic/a");
        bus.PublishToQueue("q", Message("1"));

        var result = connection.DeclareQueue("q", subscriptionDescription: description);

        Assert.AreEqual(new BusQueueDeclareResult(MessageBusOutcome.Succeeded, 1, 0), result);
        Assert.AreEqual("/topic/a", connection.InspectQueue("q").SubscriptionDescription);
    }

    private static BusMessage Message(string payload) => new("t", System.Text.Encoding.UTF8.GetBytes(payload));
}
