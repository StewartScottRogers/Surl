namespace Surl.MessageBus;

[TestClass]
public sealed class MessageBusRetainedTests
{
    private readonly SettableTimeProvider clock = new();

    [TestMethod]
    public void Subscribe_IncludeRetained_OffersMatchingRetainedMessagesInOrdinalOrderOfTopic()
    {
        var bus = new MessageBus(clock);
        bus.Publish(Retained("a/z", "1"));
        bus.Publish(Retained("a/B", "2"));
        bus.Publish(Retained("b/c", "3"));
        bus.Publish(new BusMessage("a/live", "4"u8.ToArray()));
        using var connection = bus.Connect();

        var result = connection.Subscribe("a/+", _ => { }, includeRetained: true);

        Assert.AreEqual(MessageBusOutcome.Succeeded, result.Outcome);
        CollectionAssert.AreEqual(new[] { "a/B", "a/z" }, result.RetainedMessages.Select(message => message.Topic).ToArray());
        Assert.IsTrue(result.RetainedMessages.All(message => message.Retain));
    }

    [TestMethod]
    public void Subscribe_WithoutIncludeRetained_OffersNoRetainedMessage()
    {
        var bus = new MessageBus(clock);
        bus.Publish(Retained("t", "1"));
        using var connection = bus.Connect();

        var result = connection.Subscribe("t", _ => { });

        Assert.IsEmpty(result.RetainedMessages);
    }

    [TestMethod]
    public void Publish_RetainedOnATopicHoldingOne_ReplacesIt()
    {
        var bus = new MessageBus(clock);
        bus.Publish(Retained("t", "old"));

        bus.Publish(Retained("t", "new"));

        var retained = RetainedOn(bus, "t");
        Assert.HasCount(1, retained);
        CollectionAssert.AreEqual("new"u8.ToArray(), retained[0].Payload.ToArray());
    }

    [TestMethod]
    public void Publish_RetainedWithEmptyPayload_RemovesTheTopicsRetainedMessageAndIsStillDelivered()
    {
        var bus = new MessageBus(clock, maxRetainedPayloadBytes: 3);
        bus.Publish(Retained("t", "old"));
        using var connection = bus.Connect();
        var received = new List<BusMessage>();
        connection.Subscribe("t", received.Add);

        var result = bus.Publish(Retained("t", ""));

        Assert.AreEqual(new BusPublishResult(MessageBusOutcome.Succeeded, 1) { LogOffset = 1 }, result);
        Assert.IsTrue(received.Single().Payload.IsEmpty);
        Assert.IsEmpty(RetainedOn(bus, "#"));
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.Publish(Retained("u", "abc")).Outcome);
    }

    [TestMethod]
    public void Publish_RetainedWithEmptyPayloadOnATopicHoldingNone_ChangesNothing()
    {
        var bus = new MessageBus(clock);
        bus.Publish(Retained("other", "x"));

        var result = bus.Publish(Retained("t", ""));

        Assert.AreEqual(MessageBusOutcome.Succeeded, result.Outcome);
        Assert.HasCount(1, RetainedOn(bus, "#"));
    }

    [TestMethod]
    public void Subscribe_RetainedMessageExpired_IsDroppedAndNotOffered()
    {
        var bus = new MessageBus(clock, maxRetainedPayloadBytes: 1);
        bus.Publish(Retained("t", "x") with { ExpiresAt = clock.UtcNow.AddSeconds(10) });
        clock.UtcNow = clock.UtcNow.AddSeconds(10);

        var offered = RetainedOn(bus, "#");

        Assert.IsEmpty(offered);
        Assert.AreEqual(MessageBusOutcome.Succeeded, bus.Publish(Retained("u", "y")).Outcome);
    }

    [TestMethod]
    public void Subscribe_RetainedMessageNotYetExpired_IsOffered()
    {
        var bus = new MessageBus(clock);
        bus.Publish(Retained("t", "x") with { ExpiresAt = clock.UtcNow.AddSeconds(10) });
        clock.UtcNow = clock.UtcNow.AddSeconds(9);

        Assert.HasCount(1, RetainedOn(bus, "t"));
    }

    [TestMethod]
    public void Subscribe_RetainedTopicBeginningWithDollar_IsNotOfferedToAWildcardFilter()
    {
        var bus = new MessageBus(clock);
        bus.Publish(Retained("$SYS/uptime", "1"));

        Assert.IsEmpty(RetainedOn(bus, "#"));
        Assert.HasCount(1, RetainedOn(bus, "$SYS/#"));
    }

    private static BusMessage Retained(string topic, string payload) =>
        new(topic, System.Text.Encoding.UTF8.GetBytes(payload)) { Retain = true };

    private static IReadOnlyList<BusMessage> RetainedOn(MessageBus bus, string filter)
    {
        using var connection = bus.Connect();
        return connection.Subscribe(filter, _ => { }, includeRetained: true).RetainedMessages;
    }
}
