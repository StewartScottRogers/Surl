using Surl.MessageBus;

namespace Surl.Protocol.Mqtt;

[TestClass]
public sealed class MqttHeldSubscriptionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task TryTake_NothingWaiting_IsFalse()
    {
        await using var subscription = Held(TestMessageBus.New());

        Assert.IsFalse(subscription.TryTake(out _));
    }

    [TestMethod]
    public async Task WaitForPublishAsync_AskedTwiceBeforeAPublish_AnswersOneTaskThatThePublishCompletes()
    {
        await using var subscription = Held(TestMessageBus.New());

        var first = subscription.WaitForPublishAsync();
        var second = subscription.WaitForPublishAsync();
        var completedBeforeThePublish = first.IsCompleted;
        subscription.Offer(new BusMessage("t", "hi"u8.ToArray()));
        await first.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.AreSame(first, second);
        Assert.IsFalse(completedBeforeThePublish);
        Assert.IsTrue(subscription.TryTake(out var publish));
        Assert.AreEqual("t", publish.TopicName);
    }

    [TestMethod]
    public async Task Offer_PayloadsOfExactlyTheByteBound_WaitAndTakingOneMakesRoom()
    {
        await using var subscription = Held(TestMessageBus.New());
        var whole = new BusMessage("t", new byte[MqttHeldSubscription.MaxWaitingPayloadBytes]);

        subscription.Offer(whole);
        var overflowedAtTheBound = subscription.HasOverflowed;
        subscription.TryTake(out _);
        subscription.Offer(whole);

        Assert.IsFalse(overflowedAtTheBound);
        Assert.IsFalse(subscription.HasOverflowed);
        Assert.IsFalse(subscription.Stopped.IsCancellationRequested);
    }

    [TestMethod]
    public async Task Offer_OneByteMoreThanTheByteBound_CancelsStoppedAndQueuesNothingMore()
    {
        await using var subscription = Held(TestMessageBus.New());

        subscription.Offer(new BusMessage("t", new byte[MqttHeldSubscription.MaxWaitingPayloadBytes]));
        subscription.Offer(new BusMessage("t", new byte[1]));
        subscription.Offer(new BusMessage("t", Array.Empty<byte>()));

        Assert.IsTrue(subscription.HasOverflowed);
        Assert.IsTrue(subscription.Stopped.IsCancellationRequested);
        Assert.IsTrue(subscription.TryTake(out _));
        Assert.IsFalse(subscription.TryTake(out _));
    }

    [TestMethod]
    public async Task Offer_AfterDisposal_QueuesNothing()
    {
        var subscription = Held(TestMessageBus.New());
        await subscription.DisposeAsync();

        subscription.Offer(new BusMessage("t", "hi"u8.ToArray()));

        Assert.IsFalse(subscription.TryTake(out _));
    }

    [TestMethod]
    public async Task CountPublishToSend_CountOfTwo_IsTrueAtTheSecondAndNothingIsQueuedAfter()
    {
        await using var subscription = new MqttHeldSubscription(TestMessageBus.New(), null, "mqtt", 2, TestContext.CancellationToken);

        var first = subscription.CountPublishToSend();
        var second = subscription.CountPublishToSend();
        subscription.Offer(new BusMessage("t", "hi"u8.ToArray()));

        Assert.IsFalse(first);
        Assert.IsTrue(second);
        Assert.IsFalse(subscription.TryTake(out _));
    }

    private MqttHeldSubscription Held(MessageBus.MessageBus bus) =>
        new(bus, account: null, "mqtt", publishCount: 0, TestContext.CancellationToken);
}
