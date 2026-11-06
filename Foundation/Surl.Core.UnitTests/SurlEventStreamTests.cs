using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

[TestClass]
public sealed class SurlEventStreamTests
{
    private static readonly EndPoint Client = new IPEndPoint(IPAddress.Loopback, 51234);

    [TestMethod]
    public void Constructor_NoTimeProvider_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SurlEventStream(null!));
    }

    [TestMethod]
    public void Subscribe_NoOptions_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SurlEventStream(new ManualTimeProvider()).Subscribe(null!));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(SurlEventSubscriptionOptions.MaxCapacity + 1)]
    public void Subscribe_CapacityOutsideItsRange_Throws(int capacity)
    {
        var stream = new SurlEventStream(new ManualTimeProvider());

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => stream.Subscribe(new SurlEventSubscriptionOptions { Capacity = capacity }));
    }

    [TestMethod]
    public void Options_Defaults_AreTheAdrs()
    {
        var options = new SurlEventSubscriptionOptions();

        Assert.AreEqual(65_536, options.Capacity);
        Assert.IsFalse(options.IncludeBytes);
        Assert.IsNull(options.Filter);
        Assert.AreEqual(1, SurlEventSubscriptionOptions.MinCapacity);
        Assert.AreEqual(1_048_576, SurlEventSubscriptionOptions.MaxCapacity);
    }

    [TestMethod]
    public void Publish_NoEvent_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SurlEventStream(new ManualTimeProvider()).Publish(null!));
    }

    [TestMethod]
    public async Task Publish_DeliversInPublishOrder_WithIncreasingSequenceNumbers_AndTheStreamsTime()
    {
        var time = new ManualTimeProvider();
        var stream = new SurlEventStream(time);
        await using var subscription = stream.Subscribe(new SurlEventSubscriptionOptions());
        var startedAt = time.GetUtcNow();

        stream.Publish(Started(1));
        time.Advance(TimeSpan.FromSeconds(2));
        stream.Publish(Ended(1));
        stream.Publish(Started(2));

        var events = Drain(subscription);
        CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, events.Select(e => e.Sequence).ToArray());
        CollectionAssert.AreEqual(
            new[] { startedAt, startedAt.AddSeconds(2), startedAt.AddSeconds(2) }, events.Select(e => e.At).ToArray());
        Assert.AreEqual(Started(1) with { Sequence = 1, At = startedAt }, events[0]);
        Assert.IsInstanceOfType<ExchangeEndedEvent>(events[1]);
        Assert.AreEqual(3, stream.LastSequence);
        Assert.AreEqual(0, subscription.DroppedEventCount);
    }

    [TestMethod]
    public async Task Publish_TwoSubscribers_EachSeeEveryEvent()
    {
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var first = stream.Subscribe(new SurlEventSubscriptionOptions());
        await using var second = stream.Subscribe(new SurlEventSubscriptionOptions());

        stream.Publish(Started(1));
        stream.Publish(Ended(1));

        var firstEvents = Drain(first);
        CollectionAssert.AreEqual(firstEvents, Drain(second));
        Assert.HasCount(2, firstEvents);
    }

    [TestMethod]
    public async Task Publish_SlowSubscriberPastItsCapacity_LosesTheOldestEvents_AndCountsThem_WithoutBlocking()
    {
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var slow = stream.Subscribe(new SurlEventSubscriptionOptions { Capacity = 2 });
        await using var roomy = stream.Subscribe(new SurlEventSubscriptionOptions());

        for (var exchangeId = 1; exchangeId <= 5; exchangeId++)
        {
            stream.Publish(Started(exchangeId));
        }

        CollectionAssert.AreEqual(new long[] { 4, 5 }, Drain(slow).Select(e => e.Sequence).ToArray());
        Assert.AreEqual(3, slow.DroppedEventCount);
        Assert.HasCount(5, Drain(roomy));
        Assert.AreEqual(0, roomy.DroppedEventCount);
    }

    [TestMethod]
    public async Task Subscribe_SeesOnlyEventsPublishedAfterIt()
    {
        var stream = new SurlEventStream(new ManualTimeProvider());
        stream.Publish(Started(1));

        await using var subscription = stream.Subscribe(new SurlEventSubscriptionOptions());
        stream.Publish(Ended(1));

        CollectionAssert.AreEqual(new long[] { 2 }, Drain(subscription).Select(e => e.Sequence).ToArray());
    }

    [TestMethod]
    public async Task DisposeAsync_SubscriptionReceivesNothingMore_AndItsReadEndsOnceDrained()
    {
        var stream = new SurlEventStream(new ManualTimeProvider());
        var subscription = stream.Subscribe(new SurlEventSubscriptionOptions());
        stream.Publish(Started(1));

        await subscription.DisposeAsync();
        stream.Publish(Ended(1));

        var read = new List<SurlEvent>();
        await foreach (var surlEvent in subscription.ReadAllAsync(CancellationToken.None).WithCancellation(CancellationToken.None))
        {
            read.Add(surlEvent);
        }

        CollectionAssert.AreEqual(new long[] { 1 }, read.Select(e => e.Sequence).ToArray());
    }

    [TestMethod]
    public async Task DisposeAsync_Twice_LeavesTheOtherSubscriptionsAndTheBytesCountAlone()
    {
        var stream = new SurlEventStream(new ManualTimeProvider());
        var once = stream.Subscribe(new SurlEventSubscriptionOptions { IncludeBytes = true });
        await using var kept = stream.Subscribe(new SurlEventSubscriptionOptions { IncludeBytes = true });

        await once.DisposeAsync();
        await once.DisposeAsync();

        Assert.IsTrue(stream.IsPublishingBytes);
        stream.Publish(Started(1));
        Assert.HasCount(1, Drain(kept));
    }

    [TestMethod]
    public async Task IsPublishingBytes_IsTrueOnlyWhileASubscriptionAskedForBytes()
    {
        var stream = new SurlEventStream(new ManualTimeProvider());
        Assert.IsFalse(stream.IsPublishingBytes);

        await using var withoutBytes = stream.Subscribe(new SurlEventSubscriptionOptions());
        Assert.IsFalse(stream.IsPublishingBytes);

        var withBytes = stream.Subscribe(new SurlEventSubscriptionOptions { IncludeBytes = true });
        Assert.IsTrue(stream.IsPublishingBytes);
        Assert.IsTrue(withBytes.IncludesBytes);

        await withBytes.DisposeAsync();
        Assert.IsFalse(stream.IsPublishingBytes);
    }

    [TestMethod]
    public async Task Publish_BytesEvent_ReachesOnlyTheSubscriptionsThatAskedForBytes()
    {
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var withoutBytes = stream.Subscribe(new SurlEventSubscriptionOptions());
        await using var withBytes = stream.Subscribe(new SurlEventSubscriptionOptions { IncludeBytes = true });

        stream.Publish(new ExchangeBytesEvent(1, ExchangeDirection.Received, "ping"u8.ToArray()));
        stream.Publish(Ended(1));

        Assert.IsInstanceOfType<ExchangeEndedEvent>(Drain(withoutBytes).Single());
        Assert.IsInstanceOfType<ExchangeBytesEvent>(Drain(withBytes)[0]);
    }

    [TestMethod]
    public async Task Publish_Filter_PicksTheEvents_AndAFilterThatThrowsRefusesWithoutThrowingIntoThePublisher()
    {
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var startsOnly = stream.Subscribe(
            new SurlEventSubscriptionOptions { Filter = surlEvent => surlEvent is ExchangeStartedEvent });
        await using var throwing = stream.Subscribe(
            new SurlEventSubscriptionOptions { Filter = _ => throw new InvalidOperationException("filter broke") });

        stream.Publish(Started(1));
        stream.Publish(Ended(1));

        Assert.IsInstanceOfType<ExchangeStartedEvent>(Drain(startsOnly).Single());
        Assert.IsEmpty(Drain(throwing));
    }

    [TestMethod]
    public async Task Publish_TwoStreams_NeverSeeEachOthersEvents()
    {
        var first = new SurlEventStream(new ManualTimeProvider());
        var second = new SurlEventStream(new ManualTimeProvider());
        await using var firstSubscription = first.Subscribe(new SurlEventSubscriptionOptions());
        await using var secondSubscription = second.Subscribe(new SurlEventSubscriptionOptions());

        first.Publish(Started(1));

        Assert.HasCount(1, Drain(firstSubscription));
        Assert.IsEmpty(Drain(secondSubscription));
        Assert.AreEqual(0, second.LastSequence);
    }

    private static ExchangeStartedEvent Started(long exchangeId) =>
        new(exchangeId, "http://127.0.0.1:8080/", "http", Client);

    private static ExchangeEndedEvent Ended(long exchangeId) => new(exchangeId, 4, 4);

    private static List<SurlEvent> Drain(SurlEventSubscription subscription)
    {
        var events = new List<SurlEvent>();

        while (subscription.TryRead(out var surlEvent))
        {
            events.Add(surlEvent!);
        }

        return events;
    }
}
