namespace Surl.MessageBus;

[TestClass]
public sealed class BusSubscriptionNoLocalTests
{
    private readonly SettableTimeProvider clock = new();

    [TestMethod]
    public void Publish_FromTheSubscribingConnection_IsKeptFromItsNoLocalSubscriptionOnly()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var received = new List<string>();
        var noLocal = connection.Subscribe("t", _ => received.Add("no local"), noLocal: true).Subscription!;
        connection.Subscribe("t", _ => received.Add("local"));

        var result = connection.Publish(new BusMessage("t", "1"u8.ToArray()));

        Assert.IsTrue(noLocal.NoLocal);
        CollectionAssert.AreEqual(new[] { "local" }, received);
        Assert.AreEqual(1, result.DeliveredSubscriptionCount);
    }

    [TestMethod]
    public void Publish_FromAnotherConnectionOrTheBus_ReachesANoLocalSubscription()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        using var other = bus.Connect("alice");
        var received = new List<string>();
        connection.Subscribe("t", message => received.Add(message.Topic + "=" + message.Payload.Length), noLocal: true);

        other.Publish(new BusMessage("t", "1"u8.ToArray()));
        bus.Publish(new BusMessage("t", "22"u8.ToArray()));

        CollectionAssert.AreEqual(new[] { "t=1", "t=2" }, received);
    }

    [TestMethod]
    public void Publish_ThroughAConnection_IsCheckedForItsAccount()
    {
        var bus = new MessageBus(clock, configuration: new MessageBusConfiguration { AccessGrants = [new TopicAccessGrant("t", "alice", TopicAccessRights.Publish)] });
        using var alice = bus.Connect("alice");
        using var bob = bus.Connect("bob");

        Assert.AreEqual(MessageBusOutcome.Succeeded, alice.Publish(new BusMessage("t", "1"u8.ToArray())).Outcome);
        Assert.AreEqual(MessageBusOutcome.AccessDenied, bob.Publish(new BusMessage("t", "1"u8.ToArray())).Outcome);
    }

    [TestMethod]
    public void AfterMessageDelivered_IsCalledOnceAConnectionsMatchingSubscriptionsHaveAllReceivedIt()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        using var other = bus.Connect();
        var events = new List<string>();
        connection.AfterMessageDelivered = message => events.Add("delivered " + message.Topic);
        connection.Subscribe("t", _ => events.Add("t"));
        connection.Subscribe("#", _ => events.Add("#"));
        other.Subscribe("t", _ => events.Add("other"));

        bus.Publish(new BusMessage("t", "1"u8.ToArray()));
        bus.Publish(new BusMessage("u", "1"u8.ToArray()));

        CollectionAssert.AreEqual(new[] { "t", "#", "other", "delivered t", "#", "delivered u" }, events);
    }
}
