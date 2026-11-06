using Surl.Protocol.Abstractions;

namespace Surl.Control;

[TestClass]
public sealed class ControlEventLogTests
{
    [TestMethod]
    public void Add_PastTheEventBound_DropsTheOldestAndCountsIt()
    {
        var log = new ControlEventLog(maxEvents: 2, maxBytes: 100);

        log.Add(Started(1, 1));
        log.Add(Ended(2, 1));
        log.Add(Ended(3, 1));

        var view = log.View();
        CollectionAssert.AreEqual(new long[] { 2, 3 }, view.Events.Select(surlEvent => surlEvent.Sequence).ToArray());
        Assert.AreEqual(1, view.DroppedEventCount);
        Assert.AreEqual(1, view.DroppedExchangeCount);
    }

    [TestMethod]
    public void Add_PastTheByteBound_DropsTheOldestButKeepsTheNewest()
    {
        var log = new ControlEventLog(maxEvents: 10, maxBytes: 4);

        log.Add(Bytes(1, 3));
        log.Add(Bytes(2, 3));
        log.Add(Bytes(3, 9));

        var view = log.View();
        CollectionAssert.AreEqual(new long[] { 3 }, view.Events.Select(surlEvent => surlEvent.Sequence).ToArray());
        Assert.AreEqual(2, view.DroppedEventCount);
        Assert.AreEqual(0, view.DroppedExchangeCount);
    }

    [TestMethod]
    public void Add_SequenceGap_CountsTheSkippedEventsAndForgetsTheGapOnceItsEventsAreGone()
    {
        var log = new ControlEventLog(maxEvents: 2, maxBytes: 100);
        log.Add(Ended(5, 1));
        log.Add(Ended(8, 1));

        var withGap = log.View();
        log.Add(Ended(9, 1));
        var afterDrop = log.View();

        Assert.AreEqual(2, withGap.DroppedEventCount);
        Assert.IsTrue(withGap.HasGapBetween(5, 8));
        Assert.IsFalse(withGap.HasGapBetween(1, 5));
        Assert.IsEmpty(afterDrop.Gaps);
        Assert.AreEqual(3, afterDrop.DroppedEventCount);
    }

    [TestMethod]
    public void Add_OutOfOrderSequence_RecordsNoGap()
    {
        var log = new ControlEventLog();

        log.Add(Ended(5, 1));
        log.Add(Ended(4, 1));
        log.Add(Ended(6, 1));

        Assert.AreEqual(0, log.View().DroppedEventCount);
    }

    [TestMethod]
    public void View_EventAdded_CompletesTheEarlierViewsSignalOnly()
    {
        var log = new ControlEventLog();
        var before = log.View();

        log.Add(Ended(1, 1));

        Assert.IsTrue(before.NextEventAdded.IsCompleted);
        Assert.IsFalse(log.View().NextEventAdded.IsCompleted);
    }

    private static ExchangeStartedEvent Started(long sequence, long exchangeId) =>
        new ExchangeStartedEvent(exchangeId, "http://127.0.0.1:80", "http", ControlHarness.LoopbackPeer) { Sequence = sequence };

    private static ExchangeEndedEvent Ended(long sequence, long exchangeId) =>
        new ExchangeEndedEvent(exchangeId, 0, 0) { Sequence = sequence };

    private static ExchangeBytesEvent Bytes(long sequence, int count) =>
        new ExchangeBytesEvent(1, ExchangeDirection.Received, new byte[count]) { Sequence = sequence };
}
