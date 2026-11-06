using System.Net;
using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

[TestClass]
public sealed partial class ExchangeRecordSubscriberTests
{
    private const string HttpListenUrl = "http://127.0.0.1:49731/";
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 9, 30, 0, TimeSpan.Zero);
    private static readonly IPEndPoint Alice = new(IPAddress.Loopback, 50001);

    [TestMethod]
    public async Task Snapshot_AnExchangePublishedOnTheStream_HoldsItsListenUrlSchemeStartAndBytes()
    {
        var stream = new SurlEventStream(new FixedTimeProvider(Now));
        await using var subscriber = new ExchangeRecordSubscriber(stream, TimeProvider.System, Bounds(10, 100));

        stream.Publish(new ExchangeStartedEvent(1, HttpListenUrl, "http", Alice));
        stream.Publish(new ExchangeBytesEvent(1, ExchangeDirection.Received, "GET"u8.ToArray()));
        stream.Publish(new ExchangeBytesEvent(1, ExchangeDirection.Sent, "200"u8.ToArray()));
        stream.Publish(new ExchangeEndedEvent(1, 3, 3));
        var record = subscriber.Snapshot().Single();

        Assert.AreEqual(new Uri(HttpListenUrl), record.ListenUrl);
        Assert.AreEqual("http", record.Scheme);
        Assert.AreEqual(Alice, record.Client);
        Assert.AreEqual(Now, record.StartedAt);
        CollectionAssert.AreEqual("GET"u8.ToArray(), record.Received);
        CollectionAssert.AreEqual("200"u8.ToArray(), record.Sent);
        Assert.IsFalse(record.IsTruncated);
        Assert.AreEqual(0, subscriber.DroppedExchangeCount);
    }

    [TestMethod]
    public async Task Snapshot_EventsPublishedWhileThePumpIsHeldOff_WaitsUntilThePumpHasAppliedThem()
    {
        var stream = new SurlEventStream(new FixedTimeProvider(Now));
        await using var subscriber = new ExchangeRecordSubscriber(stream, TimeProvider.System, Bounds(10, 100));
        IReadOnlyList<SurlExchangeRecord> records;

        // Holding the gate keeps the pump from applying; the snapshot's wait releases it.
        lock (subscriber.Gate)
        {
            stream.Publish(new ExchangeStartedEvent(1, HttpListenUrl, "http", Alice));
            stream.Publish(new ExchangeBytesEvent(1, ExchangeDirection.Received, "abc"u8.ToArray()));
            records = subscriber.Snapshot();
        }

        CollectionAssert.AreEqual("abc"u8.ToArray(), records.Single().Received);
    }

    [TestMethod]
    public async Task Snapshot_TheSubscriptionDroppedEventsOfAnOpenExchange_MarksItTruncated()
    {
        var stream = new SurlEventStream(new FixedTimeProvider(Now));
        await using var subscriber = new ExchangeRecordSubscriber(stream, TimeProvider.System, Bounds(10, 100), capacity: 1);
        stream.Publish(new ExchangeStartedEvent(1, HttpListenUrl, "http", Alice));
        Assert.IsFalse(subscriber.Snapshot().Single().IsTruncated);
        IReadOnlyList<SurlExchangeRecord> records;

        // With the pump held off it takes at most one event, so the one-event buffer drops another.
        lock (subscriber.Gate)
        {
            stream.Publish(new ExchangeBytesEvent(1, ExchangeDirection.Received, "a"u8.ToArray()));
            stream.Publish(new ExchangeBytesEvent(1, ExchangeDirection.Received, "b"u8.ToArray()));
            stream.Publish(new ExchangeBytesEvent(1, ExchangeDirection.Received, "c"u8.ToArray()));
            records = subscriber.Snapshot();
        }

        Assert.IsTrue(records.Single().IsTruncated);
    }

    [TestMethod]
    public async Task Snapshot_MoreExchangesThanTheCap_DropsTheOldestAndCountsIt()
    {
        var stream = new SurlEventStream(new FixedTimeProvider(Now));
        await using var subscriber = new ExchangeRecordSubscriber(stream, TimeProvider.System, Bounds(1, 100));

        stream.Publish(new ExchangeStartedEvent(1, HttpListenUrl, "http", Alice));
        stream.Publish(new ExchangeStartedEvent(2, HttpListenUrl, "http", Alice));

        Assert.AreEqual(2, subscriber.Snapshot().Single().ExchangeId);
        Assert.AreEqual(1, subscriber.DroppedExchangeCount);
    }

    [TestMethod]
    public async Task Snapshot_AfterDisposing_KeepsWhatWasRecordedAndDoesNotWaitForLaterEvents()
    {
        var stream = new SurlEventStream(new FixedTimeProvider(Now));
        var subscriber = new ExchangeRecordSubscriber(stream, TimeProvider.System, Bounds(10, 100));
        stream.Publish(new ExchangeStartedEvent(1, HttpListenUrl, "http", Alice));

        await subscriber.DisposeAsync();
        stream.Publish(new ExchangeStartedEvent(2, HttpListenUrl, "http", Alice));

        Assert.AreEqual(1, subscriber.Snapshot().Single().ExchangeId);
        Assert.AreEqual(0, subscriber.DroppedExchangeCount);
    }

    [TestMethod]
    public async Task Snapshot_EventsPublishedBeforeSubscribing_AreNotRecordedAndNotWaitedFor()
    {
        var stream = new SurlEventStream(new FixedTimeProvider(Now));
        stream.Publish(new ExchangeStartedEvent(1, HttpListenUrl, "http", Alice));

        await using var subscriber = new ExchangeRecordSubscriber(stream, TimeProvider.System, Bounds(10, 100));

        Assert.IsEmpty(subscriber.Snapshot());
    }

    private static ExchangeRecordBounds Bounds(int maxExchanges, int maxBytesPerExchange) =>
        new(maxExchanges, maxBytesPerExchange, 10000);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
