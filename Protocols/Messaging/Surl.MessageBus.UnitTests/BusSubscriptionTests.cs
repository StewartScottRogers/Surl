namespace Surl.MessageBus;

[TestClass]
public sealed class BusSubscriptionTests
{
    private readonly SettableTimeProvider clock = new();

    [TestMethod]
    public void Dispose_Subscription_ReceivesNothingMore()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var received = new List<BusMessage>();
        var subscription = connection.Subscribe("t", received.Add).Subscription!;
        bus.Publish(new BusMessage("t", "1"u8.ToArray()));

        subscription.Dispose();
        var result = bus.Publish(new BusMessage("t", "2"u8.ToArray()));

        Assert.HasCount(1, received);
        Assert.AreEqual(0, result.DeliveredSubscriptionCount);
    }

    [TestMethod]
    public void Dispose_Twice_DoesNothingMore()
    {
        var bus = new MessageBus(clock, maxSubscriptionsPerConnection: 1);
        using var connection = bus.Connect();
        var subscription = connection.Subscribe("t", _ => { }).Subscription!;

        subscription.Dispose();
        subscription.Dispose();

        Assert.AreEqual(MessageBusOutcome.Succeeded, connection.Subscribe("u", _ => { }).Outcome);
        Assert.AreEqual(MessageBusOutcome.TooManySubscriptions, connection.Subscribe("v", _ => { }).Outcome);
    }

    [TestMethod]
    public void Dispose_DuringADeliveryOfTheSameMessage_SubscriptionNotYetReachedReceivesNothing()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var received = new List<string>();
        BusSubscription? second = null;
        connection.Subscribe("t", _ =>
        {
            received.Add("first");
            second!.Dispose();
        });
        second = connection.Subscribe("t", _ => received.Add("second")).Subscription;

        var result = bus.Publish(new BusMessage("t", "x"u8.ToArray()));

        Assert.AreEqual(2, result.DeliveredSubscriptionCount);
        CollectionAssert.AreEqual(new[] { "first" }, received);
    }

    [TestMethod]
    public void Subscribe_Made_KeepsItsFilterAndGroup()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();

        var subscription = connection.Subscribe("a/+", _ => { }, group: "workers").Subscription!;

        Assert.AreEqual("a/+", subscription.Filter);
        Assert.AreEqual("workers", subscription.Group);
    }

    [TestMethod]
    public void Publish_GroupWithSameFilter_DeliversEachMessageToOneMemberRoundRobin()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var received = new List<string>();
        connection.Subscribe("jobs/#", message => received.Add("A:" + message.Topic), group: "g");
        connection.Subscribe("jobs/#", message => received.Add("B:" + message.Topic), group: "g");
        connection.Subscribe("jobs/#", message => received.Add("solo:" + message.Topic));

        var results = new[] { "jobs/1", "jobs/2", "jobs/3" }.Select(topic => bus.Publish(new BusMessage(topic, "x"u8.ToArray()))).ToList();

        Assert.IsTrue(results.All(result => result.DeliveredSubscriptionCount == 2));
        CollectionAssert.AreEqual(
            new[] { "solo:jobs/1", "A:jobs/1", "solo:jobs/2", "B:jobs/2", "solo:jobs/3", "A:jobs/3" },
            received);
    }

    [TestMethod]
    public void Publish_SameGroupWithDifferentFilters_DeliversToOneMemberOfEachFilter()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var received = new List<string>();
        connection.Subscribe("jobs/+", _ => received.Add("plus"), group: "g");
        connection.Subscribe("jobs/#", _ => received.Add("hash"), group: "g");

        var result = bus.Publish(new BusMessage("jobs/1", "x"u8.ToArray()));

        Assert.AreEqual(2, result.DeliveredSubscriptionCount);
        CollectionAssert.AreEquivalent(new[] { "plus", "hash" }, received);
    }

    [TestMethod]
    public void Publish_GroupMemberLeaves_RemainingMembersShareTheMessages()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        var received = new List<string>();
        connection.Subscribe("t", _ => received.Add("A"), group: "g");
        var second = connection.Subscribe("t", _ => received.Add("B"), group: "g").Subscription!;
        connection.Subscribe("t", _ => received.Add("C"), group: "g");
        bus.Publish(new BusMessage("t", "x"u8.ToArray()));
        bus.Publish(new BusMessage("t", "x"u8.ToArray()));

        second.Dispose();
        bus.Publish(new BusMessage("t", "x"u8.ToArray()));
        bus.Publish(new BusMessage("t", "x"u8.ToArray()));

        CollectionAssert.AreEqual(new[] { "A", "B", "A", "C" }, received);
    }
}
