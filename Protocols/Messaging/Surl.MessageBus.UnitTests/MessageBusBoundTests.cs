namespace Surl.MessageBus;

[TestClass]
public sealed class MessageBusBoundTests
{
    private readonly SettableTimeProvider clock = new();

    [TestMethod]
    public void Publish_PayloadPastMaxMessageBytes_ReturnsMessageTooLargeAndStoresAndDeliversNothing()
    {
        var bus = new MessageBus(clock, maxMessageBytes: 3);
        using var connection = bus.Connect();
        var received = new List<BusMessage>();
        connection.Subscribe("#", received.Add);

        var result = bus.Publish(new BusMessage("t", "abcd"u8.ToArray()) { Retain = true });

        Assert.AreEqual(new BusPublishResult(MessageBusOutcome.MessageTooLarge, 0), result);
        Assert.IsEmpty(received);
        Assert.IsEmpty(RetainedOn(bus));
    }

    [TestMethod]
    public void Publish_PayloadAtMaxMessageBytes_IsTaken()
    {
        var bus = new MessageBus(clock, maxMessageBytes: 3);

        var result = bus.Publish(new BusMessage("t", "abc"u8.ToArray()));

        Assert.AreEqual(MessageBusOutcome.Succeeded, result.Outcome);
    }

    [TestMethod]
    public void Publish_RetainedOnANewTopicPastMaxTopics_ReturnsTooManyTopicsAndStoresAndDeliversNothing()
    {
        var bus = new MessageBus(clock, maxTopics: 1);
        bus.Publish(Retained("a", "1"));
        using var connection = bus.Connect();
        var received = new List<BusMessage>();
        connection.Subscribe("#", received.Add);

        var result = bus.Publish(Retained("b", "2"));

        Assert.AreEqual(new BusPublishResult(MessageBusOutcome.TooManyTopics, 0), result);
        Assert.IsEmpty(received);
        CollectionAssert.AreEqual(new[] { "a" }, RetainedOn(bus).Select(message => message.Topic).ToArray());
    }

    [TestMethod]
    public void Publish_RetainedReplacingATopicAtMaxTopics_IsTaken()
    {
        var bus = new MessageBus(clock, maxTopics: 1);
        bus.Publish(Retained("a", "1"));

        var result = bus.Publish(Retained("a", "2"));

        Assert.AreEqual(MessageBusOutcome.Succeeded, result.Outcome);
        CollectionAssert.AreEqual("2"u8.ToArray(), RetainedOn(bus).Single().Payload.ToArray());
    }

    [TestMethod]
    public void Publish_NotRetainedAtMaxTopics_IsDelivered()
    {
        var bus = new MessageBus(clock, maxTopics: 1);
        bus.Publish(Retained("a", "1"));
        using var connection = bus.Connect();
        var received = new List<BusMessage>();
        connection.Subscribe("#", received.Add);

        var result = bus.Publish(new BusMessage("b", "2"u8.ToArray()));

        Assert.AreEqual(new BusPublishResult(MessageBusOutcome.Succeeded, 1), result);
        Assert.HasCount(1, received);
    }

    [TestMethod]
    public void Publish_RetainedPastMaxRetainedPayloadBytes_ReturnsStoreFullAndStoresAndDeliversNothing()
    {
        var bus = new MessageBus(clock, maxRetainedPayloadBytes: 4);
        bus.Publish(Retained("a", "abc"));
        using var connection = bus.Connect();
        var received = new List<BusMessage>();
        connection.Subscribe("#", received.Add);

        var result = bus.Publish(Retained("b", "de"));

        Assert.AreEqual(new BusPublishResult(MessageBusOutcome.StoreFull, 0), result);
        Assert.IsEmpty(received);
        CollectionAssert.AreEqual(new[] { "a" }, RetainedOn(bus).Select(message => message.Topic).ToArray());
    }

    [TestMethod]
    public void Publish_RetainedReplacementCountsOnlyTheNewPayload_IsTakenUpToMaxRetainedPayloadBytes()
    {
        var bus = new MessageBus(clock, maxRetainedPayloadBytes: 4);
        bus.Publish(Retained("a", "abc"));

        var result = bus.Publish(Retained("a", "defg"));

        Assert.AreEqual(MessageBusOutcome.Succeeded, result.Outcome);
        Assert.AreEqual(MessageBusOutcome.StoreFull, bus.Publish(Retained("b", "h")).Outcome);
    }

    [TestMethod]
    public void Subscribe_PastMaxSubscriptionsPerConnection_ReturnsTooManySubscriptionsAndMakesNone()
    {
        var bus = new MessageBus(clock, maxSubscriptionsPerConnection: 2);
        using var connection = bus.Connect();
        using var other = bus.Connect();
        var received = new List<BusMessage>();
        connection.Subscribe("a", received.Add);
        connection.Subscribe("b", received.Add);

        var refused = connection.Subscribe("c", received.Add, includeRetained: true);
        bus.Publish(new BusMessage("c", "x"u8.ToArray()));

        Assert.AreEqual(MessageBusOutcome.TooManySubscriptions, refused.Outcome);
        Assert.IsNull(refused.Subscription);
        Assert.IsEmpty(refused.RetainedMessages);
        Assert.IsEmpty(received);
        Assert.AreEqual(MessageBusOutcome.Succeeded, other.Subscribe("c", _ => { }).Outcome);
    }

    [TestMethod]
    public void Subscribe_AfterDisposingOneAtMaxSubscriptionsPerConnection_IsMade()
    {
        var bus = new MessageBus(clock, maxSubscriptionsPerConnection: 1);
        using var connection = bus.Connect();
        connection.Subscribe("a", _ => { }).Subscription!.Dispose();

        var result = connection.Subscribe("b", _ => { });

        Assert.AreEqual(MessageBusOutcome.Succeeded, result.Outcome);
    }

    private static BusMessage Retained(string topic, string payload) =>
        new(topic, System.Text.Encoding.UTF8.GetBytes(payload)) { Retain = true };

    private static IReadOnlyList<BusMessage> RetainedOn(MessageBus bus)
    {
        using var connection = bus.Connect();
        return connection.Subscribe("#", _ => { }, includeRetained: true).RetainedMessages;
    }
}
