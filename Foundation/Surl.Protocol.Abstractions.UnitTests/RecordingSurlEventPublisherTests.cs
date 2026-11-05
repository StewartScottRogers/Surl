using System.Net;

namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class RecordingSurlEventPublisherTests
{
    [TestMethod]
    public void Events_EveryKindOfEvent_AreKeptInPublishOrder()
    {
        var publisher = new RecordingSurlEventPublisher();
        SurlEvent[] published =
        [
            new ExchangeStartedEvent(1, "smtp://127.0.0.1:2525", "smtp", new IPEndPoint(IPAddress.Loopback, 50000)),
            new ExchangeBytesEvent(1, ExchangeDirection.Received, "HELO"u8.ToArray()),
            new LoginJudgedEvent(1, "smtp", "alice", "PLAIN", true, false),
            new StoreChangedEvent(StoreKind.Mail, StoreChange.Delivered, "bob", 120, 1),
            new FaultAppliedEvent("f1", FaultKind.Delay, FaultStage.Command, 1, "/slow"),
            new HookFailedEvent("h1", HookFailure.Threw, "boom"),
            new ExchangeEndedEvent(1, 4, 0),
        ];

        foreach (var surlEvent in published)
        {
            publisher.Publish(surlEvent);
        }

        CollectionAssert.AreEqual(published, publisher.Events.ToArray());
    }

    [TestMethod]
    public void Publish_AnEvent_KeepsTheSequenceAndTimeItWasPublishedWith()
    {
        var publisher = new RecordingSurlEventPublisher();
        var at = new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

        publisher.Publish(new ExchangeEndedEvent(2, 1, 1) { Sequence = 7, At = at });

        var recorded = publisher.Events[0];
        Assert.AreEqual(7, recorded.Sequence);
        Assert.AreEqual(at, recorded.At);
    }

    [TestMethod]
    public async Task Publish_ConcurrentPublishers_KeepsEveryEventAndEachPublishersOrder()
    {
        const int PublisherCount = 16;
        const int EventsPerPublisher = 500;
        var publisher = new RecordingSurlEventPublisher();

        await Task.WhenAll(Enumerable.Range(0, PublisherCount).Select(exchangeId => Task.Run(() =>
        {
            for (var count = 0; count < EventsPerPublisher; count++)
            {
                publisher.Publish(new ExchangeEndedEvent(exchangeId, count, 0));
            }
        })));

        var events = publisher.Events.Cast<ExchangeEndedEvent>().ToList();
        Assert.HasCount(PublisherCount * EventsPerPublisher, events);
        foreach (var exchange in events.GroupBy(surlEvent => surlEvent.ExchangeId))
        {
            CollectionAssert.AreEqual(
                Enumerable.Range(0, EventsPerPublisher).Select(count => (long)count).ToArray(),
                exchange.Select(surlEvent => surlEvent.ReceivedByteCount).ToArray());
        }
    }

    [TestMethod]
    public void IsPublishingBytes_DefaultsToTrueAndReportsWhatItWasGiven()
    {
        Assert.IsTrue(new RecordingSurlEventPublisher().IsPublishingBytes);
        Assert.IsFalse(new RecordingSurlEventPublisher(isPublishingBytes: false).IsPublishingBytes);
    }

    [TestMethod]
    public void Publish_NullEvent_Throws()
    {
        var publisher = new RecordingSurlEventPublisher();

        Assert.ThrowsExactly<ArgumentNullException>(() => publisher.Publish(null!));
    }
}
