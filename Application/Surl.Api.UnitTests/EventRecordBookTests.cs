using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

[TestClass]
public sealed class EventRecordBookTests
{
    private static readonly IPEndPoint Client = new(IPAddress.Loopback, 50001);

    [TestMethod]
    public void Apply_ExchangeBytes_AreNotKept()
    {
        var book = new EventRecordBook(10);

        book.Apply(new ExchangeBytesEvent(1, ExchangeDirection.Received, "x"u8.ToArray()) { Sequence = 1 });

        Assert.IsEmpty(book.FindEvents(new SurlEventQuery(), new ExchangeRecordBook(10, 10)));
        Assert.AreEqual(0, book.KeptEventTotal);
    }

    [TestMethod]
    public void Apply_MoreEventsThanTheBound_KeepsTheNewestAndCountsTheDropped()
    {
        var book = new EventRecordBook(2);

        for (var sequence = 1; sequence <= 5; sequence++)
        {
            book.Apply(new HookFailedEvent($"h{sequence}", HookFailure.Threw, "boom") { Sequence = sequence });
        }

        var kept = book.FindEvents(new SurlEventQuery(), new ExchangeRecordBook(10, 10));
        CollectionAssert.AreEqual(new long[] { 4, 5 }, kept.Select(surlEvent => surlEvent.Sequence).ToArray());
        Assert.AreEqual(3, book.DroppedEventCount);
        Assert.AreEqual(5, book.KeptEventTotal);
    }

    [TestMethod]
    public void FindEvents_ByScheme_ReadsEachEventsExchangeFromTheExchangeRecords()
    {
        var exchanges = new ExchangeRecordBook(10, 10);
        var events = new EventRecordBook(10);
        SurlEvent[] published =
        [
            new ExchangeStartedEvent(1, "http://127.0.0.1:49731/", "http", Client) { Sequence = 1 },
            new ExchangeStartedEvent(2, "ftp://127.0.0.1:49731/", "ftp", Client) { Sequence = 2 },
            new ExchangeEndedEvent(1, 0, 0) { Sequence = 3 },
            new ExchangeEndedEvent(2, 0, 0) { Sequence = 4 },
        ];
        foreach (var surlEvent in published)
        {
            exchanges.Apply(surlEvent);
            events.Apply(surlEvent);
        }

        var ftp = events.FindEvents(new SurlEventQuery { Scheme = "ftp" }, exchanges);

        CollectionAssert.AreEqual(new long[] { 2, 4 }, ftp.Select(surlEvent => surlEvent.Sequence).ToArray());
    }
}
