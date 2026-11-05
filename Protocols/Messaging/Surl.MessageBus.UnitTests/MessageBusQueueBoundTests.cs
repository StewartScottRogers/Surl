namespace Surl.MessageBus;

[TestClass]
public sealed class MessageBusQueueBoundTests
{
    private readonly SettableTimeProvider clock = new();

    [TestMethod]
    public void PublishToQueue_PastMaxMessagesPerQueue_ReturnsQueueFull()
    {
        var bus = new MessageBus(clock, maxMessagesPerQueue: 1);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        bus.PublishToQueue("q", Message("1"));

        Assert.AreEqual(MessageBusOutcome.QueueFull, bus.PublishToQueue("q", Message("2")));
        Assert.AreEqual(1, connection.DeclareQueue("q").ReadyMessageCount);
    }

    [TestMethod]
    public void PublishToQueue_UnacknowledgedMessagesCountAgainstMaxMessagesPerQueue()
    {
        var bus = new MessageBus(clock, maxMessagesPerQueue: 1);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");
        connection.Consume("q", _ => { }).Consumer!.Start();
        bus.PublishToQueue("q", Message("1"));

        Assert.AreEqual(MessageBusOutcome.QueueFull, bus.PublishToQueue("q", Message("2")));
    }

    [TestMethod]
    public void PublishToQueue_PastMaxQueuedBytesAcrossQueues_ReturnsQueueFull()
    {
        var bus = new MessageBus(clock, maxQueuedBytes: 3);
        using var connection = bus.Connect();
        connection.DeclareQueue("a");
        connection.DeclareQueue("b");
        bus.PublishToQueue("a", Message("12"));

        Assert.AreEqual(MessageBusOutcome.QueueFull, bus.PublishToQueue("b", Message("34")));
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.PublishToQueue("b", Message("3")));
    }

    [TestMethod]
    public void Publish_ToABoundQueueThatIsFull_StillReachesSubscribersAndOtherQueues()
    {
        var bus = new MessageBus(clock, maxMessagesPerQueue: 1);
        using var connection = bus.Connect();
        connection.DeclareQueue("full");
        connection.DeclareQueue("free");
        connection.BindQueue("full", "t");
        bus.Publish(new BusMessage("t", "1"u8.ToArray()));
        connection.BindQueue("free", "t");
        var received = new List<BusMessage>();
        connection.Subscribe("t", received.Add);

        var result = bus.Publish(new BusMessage("t", "2"u8.ToArray()));

        Assert.AreEqual(new BusPublishResult(MessageBusOutcome.Succeeded, 1) { QueuedCount = 1, FullQueueCount = 1, LogOffset = 1 }, result);
        Assert.HasCount(1, received);
    }

    [TestMethod]
    public void PublishToQueue_PayloadPastMaxMessageBytes_ReturnsMessageTooLarge()
    {
        var bus = new MessageBus(clock, maxMessageBytes: 1);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");

        Assert.AreEqual(MessageBusOutcome.MessageTooLarge, bus.PublishToQueue("q", Message("12")));
    }

    [TestMethod]
    public void PublishToQueue_AlreadyExpired_SucceedsAndQueuesNothing()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        connection.DeclareQueue("q");

        var outcome = bus.PublishToQueue("q", Message("1") with { ExpiresAt = clock.UtcNow });

        Assert.AreEqual(MessageBusOutcome.Succeeded, outcome);
        Assert.AreEqual(0, connection.DeclareQueue("q").ReadyMessageCount);
    }

    [TestMethod]
    [DataRow(0, 1, 1L)]
    [DataRow(1, 0, 1L)]
    [DataRow(1, 1, 0L)]
    public void Constructor_QueueBoundBelowOne_Throws(int maxQueues, int maxMessagesPerQueue, long maxQueuedBytes)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new MessageBus(clock, maxQueues: maxQueues, maxMessagesPerQueue: maxMessagesPerQueue, maxQueuedBytes: maxQueuedBytes));
    }

    [TestMethod]
    public void Constructor_Defaults_AreTheAdrsQueueBounds()
    {
        var bus = new MessageBus(clock);

        Assert.AreEqual(1_000, bus.MaxQueues);
        Assert.AreEqual(100_000, bus.MaxMessagesPerQueue);
        Assert.AreEqual(268_435_456L, bus.MaxQueuedBytes);
    }

    private static BusMessage Message(string payload) => new("t", System.Text.Encoding.UTF8.GetBytes(payload));
}
