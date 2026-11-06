namespace Surl.MessageBus;

[TestClass]
public sealed class BusConnectionTests
{
    private readonly SettableTimeProvider clock = new();

    [TestMethod]
    public void Dispose_Connection_EndsEveryOneOfItsSubscriptionsAndNoOtherConnections()
    {
        var bus = new MessageBus(clock);
        var connection = bus.Connect();
        using var other = bus.Connect();
        var received = new List<string>();
        connection.Subscribe("a", _ => received.Add("mine a"));
        connection.Subscribe("#", _ => received.Add("mine #"));
        other.Subscribe("a", _ => received.Add("other a"));

        connection.Dispose();
        var result = bus.Publish(new BusMessage("a", "x"u8.ToArray()));

        Assert.AreEqual(1, result.DeliveredSubscriptionCount);
        CollectionAssert.AreEqual(new[] { "other a" }, received);
    }

    [TestMethod]
    public void Dispose_Twice_DoesNothingMore()
    {
        var bus = new MessageBus(clock);
        var connection = bus.Connect();

        connection.Dispose();
        connection.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => connection.Subscribe("a", _ => { }));
    }

    [TestMethod]
    public void Dispose_SubscriptionOfADisposedConnection_DoesNothing()
    {
        var bus = new MessageBus(clock);
        var connection = bus.Connect();
        var subscription = connection.Subscribe("a", _ => { }).Subscription!;
        connection.Dispose();

        subscription.Dispose();

        Assert.AreEqual(0, bus.Publish(new BusMessage("a", "x"u8.ToArray())).DeliveredSubscriptionCount);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("a/b+")]
    [DataRow("a/#b")]
    public void Subscribe_FilterNotCanonical_ReturnsInvalidFilterAndMakesNone(string filter)
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();

        var result = connection.Subscribe(filter, _ => { }, includeRetained: true);

        Assert.AreEqual(MessageBusOutcome.InvalidFilter, result.Outcome);
        Assert.IsNull(result.Subscription);
        Assert.IsEmpty(result.RetainedMessages);
    }

    [TestMethod]
    public void Subscribe_NullDelivery_Throws()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();

        Assert.ThrowsExactly<ArgumentNullException>(() => connection.Subscribe("a", null!));
    }
}
