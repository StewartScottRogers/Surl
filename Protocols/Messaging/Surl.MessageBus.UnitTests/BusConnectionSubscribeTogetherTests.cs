namespace Surl.MessageBus;

[TestClass]
public sealed class BusConnectionSubscribeTogetherTests
{
    private readonly SettableTimeProvider clock = new();

    [TestMethod]
    public void SubscribeTogether_SeveralFilters_AnswersEachInOrderWithItsRetainedMessagesAndARefusalOnlyItsOwn()
    {
        var bus = new MessageBus(clock, maxSubscriptionsPerConnection: 2);
        bus.Publish(new BusMessage("a/b", "kept"u8.ToArray()) { Retain = true });
        using var connection = bus.Connect();
        var received = new List<string>();

        var results = connection.SubscribeTogether(
            [new("a/#"), new("a/b#"), new("c"), new("d")],
            message => received.Add(message.Topic));
        bus.Publish(new BusMessage("c", "live"u8.ToArray()));

        CollectionAssert.AreEqual(
            new[] { MessageBusOutcome.Succeeded, MessageBusOutcome.InvalidFilter, MessageBusOutcome.Succeeded, MessageBusOutcome.TooManySubscriptions },
            results.Select(result => result.Outcome).ToArray());
        Assert.AreEqual("a/b", results[0].RetainedMessages.Single().Topic);
        Assert.IsEmpty(results[2].RetainedMessages);
        CollectionAssert.AreEqual(new[] { "c" }, received);
    }

    [TestMethod]
    public void SubscribeTogether_APublishMadeBetweenTwoFilters_WaitsAndReachesBothLiveAndNeitherAsRetained()
    {
        var hookedClock = new HookedClock();
        var bus = new MessageBus(hookedClock);
        using var connection = bus.Connect();
        var received = new List<string>();
        var publisher = new Thread(() => bus.Publish(new BusMessage("a/b", "hi"u8.ToArray()) { Retain = true })) { IsBackground = true };
        var waitedForTheSubscribe = false;
        // The second read of the clock is the second filter's.
        hookedClock.RunAtRead(2, () =>
        {
            publisher.Start();
            SpinWait.SpinUntil(() => !publisher.IsAlive || (publisher.ThreadState & ThreadState.WaitSleepJoin) != 0, TimeSpan.FromSeconds(30));
            waitedForTheSubscribe = publisher.IsAlive;
        });

        var results = connection.SubscribeTogether([new("a/#"), new("a/b")], message => received.Add(message.Topic));
        Assert.IsTrue(publisher.Join(TimeSpan.FromSeconds(30)));

        Assert.IsTrue(waitedForTheSubscribe);
        Assert.IsEmpty(results.SelectMany(result => result.RetainedMessages));
        CollectionAssert.AreEqual(new[] { "a/b", "a/b" }, received);
    }

    [TestMethod]
    public void SubscribeTogether_AHeldSubscription_IsKeptAndItsRetainedMessagesAreReadAgain()
    {
        var bus = new MessageBus(clock, maxSubscriptionsPerConnection: 1);
        using var connection = bus.Connect();
        var held = connection.Subscribe("t", _ => { }).Subscription!;
        bus.Publish(new BusMessage("t", "kept"u8.ToArray()) { Retain = true });

        var result = connection.SubscribeTogether([new("t", held)], _ => { }).Single();

        Assert.AreEqual(MessageBusOutcome.Succeeded, result.Outcome);
        Assert.AreSame(held, result.Subscription);
        Assert.AreEqual("t", result.RetainedMessages.Single().Topic);
        Assert.AreEqual(1, bus.Publish(new BusMessage("t", "live"u8.ToArray())).DeliveredSubscriptionCount);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "another connection's")]
    [DataRow(true, DisplayName = "one already disposed")]
    public void SubscribeTogether_AHeldSubscriptionThatIsNotLiveOnThisConnection_ThrowsAndSubscribesNothing(bool disposed)
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();
        using var other = bus.Connect();
        var held = (disposed ? connection : other).Subscribe("t", _ => { }).Subscription!;
        if (disposed)
        {
            held.Dispose();
        }

        var thrown = Assert.ThrowsExactly<ArgumentException>(
            () => connection.SubscribeTogether([new("u"), new("t", held)], _ => { }));

        Assert.AreEqual("requests", thrown.ParamName);
        Assert.AreEqual(0, bus.Publish(new BusMessage("u", "x"u8.ToArray())).DeliveredSubscriptionCount);
    }

    [TestMethod]
    public void SubscribeTogether_ADisposedConnection_Throws()
    {
        var bus = new MessageBus(clock);
        var connection = bus.Connect();
        connection.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => connection.SubscribeTogether([], _ => { }));
    }

    [TestMethod]
    public void SubscribeTogether_ANullArgument_Throws()
    {
        var bus = new MessageBus(clock);
        using var connection = bus.Connect();

        Assert.ThrowsExactly<ArgumentNullException>(() => connection.SubscribeTogether(null!, _ => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => connection.SubscribeTogether([], null!));
    }

    // Stands still and runs an action once, at a chosen read of the time.
    private sealed class HookedClock : TimeProvider
    {
        private Action? action;
        private int readsLeft;

        public void RunAtRead(int read, Action hook)
        {
            readsLeft = read;
            action = hook;
        }

        public override DateTimeOffset GetUtcNow()
        {
            if (action is { } hook && --readsLeft == 0)
            {
                action = null;
                hook();
            }

            return DateTimeOffset.UnixEpoch;
        }
    }
}
