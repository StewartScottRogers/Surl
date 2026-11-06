namespace Surl.Extended.Protocol.Nats.UnitTests;

[TestClass]
public sealed class NatsClientSubscriptionTests
{
    [TestMethod]
    public void TryCountDelivery_AfterItEnded_CountsNothing()
    {
        var subscription = new NatsClientSubscription("1", "foo");

        var ended = subscription.EndAfter(null);

        Assert.IsTrue(ended);
        Assert.IsFalse(subscription.TryCountDelivery());
    }

    [TestMethod]
    public void TryCountDelivery_ReachingItsMax_EndsItAndDisposesTheBusSubscription()
    {
        var bus = UnitTestMessageBus.Create();
        using var connection = bus.Connect();
        var delivered = 0;
        var subscription = new NatsClientSubscription("1", "foo");
        subscription.Attach(connection.Subscribe("foo", _ => delivered++).Subscription!);

        Assert.IsFalse(subscription.EndAfter(1));
        Assert.IsTrue(subscription.TryCountDelivery());

        Assert.IsTrue(subscription.HasEnded);
        Assert.AreEqual(0, bus.Publish(new Surl.MessageBus.BusMessage("foo", "x"u8.ToArray())).DeliveredSubscriptionCount);
        Assert.AreEqual(0, delivered);
    }
}
