using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

[TestClass]
public sealed class ExchangeRecordBookTests
{
    private const string HttpListenUrl = "http://127.0.0.1:49731/";
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 9, 30, 0, TimeSpan.Zero);
    private static readonly IPEndPoint Alice = new(IPAddress.Loopback, 50001);
    private static readonly IPEndPoint Bob = new(IPAddress.IPv6Loopback, 50002);

    [TestMethod]
    public void Apply_OneExchange_KeepsItsListenUrlSchemeClientStartAndBytesInTheOrderTheyCrossed()
    {
        var book = new ExchangeRecordBook(10, 100);

        book.Apply(Started(7, Alice, "ftp://[::1]:2121/", "ftp"));
        book.Apply(Received(7, "abc"));
        book.Apply(Sent(7, "def"));
        book.Apply(Received(7, "g"));
        var record = book.Snapshot().Single();

        Assert.AreEqual(7, record.ExchangeId);
        Assert.AreEqual(new Uri("ftp://[::1]:2121/"), record.ListenUrl);
        Assert.AreEqual("ftp", record.Scheme);
        Assert.AreEqual(Alice, record.Client);
        Assert.AreEqual(Now, record.StartedAt);
        Assert.IsFalse(record.IsTruncated);
        CollectionAssert.AreEqual(
            new[] { SurlExchangeDirection.Received, SurlExchangeDirection.Sent, SurlExchangeDirection.Received },
            record.Chunks.Select(chunk => chunk.Direction).ToArray());
        CollectionAssert.AreEqual(
            new[] { "abc", "def", "g" },
            record.Chunks.Select(chunk => System.Text.Encoding.ASCII.GetString(chunk.Bytes.Span)).ToArray());
        CollectionAssert.AreEqual("abcg"u8.ToArray(), record.Received);
        CollectionAssert.AreEqual("def"u8.ToArray(), record.Sent);
    }

    [TestMethod]
    public void Apply_TwoExchangesInterleaved_KeepsThemApartWithTheirIdsAndClientsOldestFirst()
    {
        var book = new ExchangeRecordBook(10, 100);

        book.Apply(Started(1, Alice));
        book.Apply(Started(2, Bob));
        book.Apply(Received(2, "to bob"));
        book.Apply(Received(1, "to alice"));
        var records = book.Snapshot();

        Assert.HasCount(2, records);
        Assert.AreEqual(1, records[0].ExchangeId);
        Assert.AreEqual(Alice, records[0].Client);
        CollectionAssert.AreEqual("to alice"u8.ToArray(), records[0].Received);
        Assert.AreEqual(2, records[1].ExchangeId);
        Assert.AreEqual(Bob, records[1].Client);
        CollectionAssert.AreEqual("to bob"u8.ToArray(), records[1].Received);
    }

    [TestMethod]
    public void Apply_TftpDatagramExchange_KeepsEachDatagramAsOneChunk()
    {
        var book = new ExchangeRecordBook(10, 1000);
        byte[] readRequest = [0, 1, .. "file.txt"u8, 0, .. "octet"u8, 0];
        byte[] firstData = [0, 3, 0, 1, .. "hello"u8];
        byte[] acknowledgement = [0, 4, 0, 1];

        book.Apply(Started(3, Alice, "tftp://127.0.0.1:6969/", "tftp"));
        book.Apply(new ExchangeBytesEvent(3, ExchangeDirection.Received, readRequest));
        book.Apply(new ExchangeBytesEvent(3, ExchangeDirection.Sent, firstData));
        book.Apply(new ExchangeBytesEvent(3, ExchangeDirection.Received, acknowledgement));
        var record = book.Snapshot().Single();

        Assert.HasCount(3, record.Chunks);
        CollectionAssert.AreEqual(readRequest, record.Chunks[0].Bytes.ToArray());
        CollectionAssert.AreEqual(firstData, record.Chunks[1].Bytes.ToArray());
        CollectionAssert.AreEqual(acknowledgement, record.Chunks[2].Bytes.ToArray());
        CollectionAssert.AreEqual(firstData, record.Sent);
    }

    [TestMethod]
    public void Apply_BytesPastTheCapBothDirectionsTogether_CutsThemAndMarksTheRecordTruncated()
    {
        var book = new ExchangeRecordBook(10, 5);

        book.Apply(Started(1, Alice));
        book.Apply(Received(1, "abc"));
        book.Apply(Sent(1, "defg"));
        book.Apply(Received(1, "h"));
        var record = book.Snapshot().Single();

        Assert.IsTrue(record.IsTruncated);
        Assert.HasCount(2, record.Chunks);
        CollectionAssert.AreEqual("abc"u8.ToArray(), record.Received);
        CollectionAssert.AreEqual("de"u8.ToArray(), record.Sent);
    }

    [TestMethod]
    public void Apply_BytesExactlyAtTheCap_KeepsThemAllAndIsNotTruncated()
    {
        var book = new ExchangeRecordBook(10, 5);

        book.Apply(Started(1, Alice));
        book.Apply(Received(1, "abcde"));
        var record = book.Snapshot().Single();

        Assert.IsFalse(record.IsTruncated);
        CollectionAssert.AreEqual("abcde"u8.ToArray(), record.Received);
    }

    [TestMethod]
    public void Apply_MoreExchangesThanTheCap_DropsTheOldestAndCountsItAndIgnoresItsLaterBytes()
    {
        var book = new ExchangeRecordBook(2, 100);

        book.Apply(Started(1, Alice));
        book.Apply(Started(2, Alice));
        book.Apply(Started(3, Bob));
        book.Apply(Received(1, "late"));
        var records = book.Snapshot();

        CollectionAssert.AreEqual(new long[] { 2, 3 }, records.Select(record => record.ExchangeId).ToArray());
        Assert.IsTrue(records.All(record => record.Chunks.Count == 0));
        Assert.AreEqual(1, book.DroppedExchangeCount);
    }

    [TestMethod]
    public void Apply_BytesAfterTheExchangeEndedOrForAnExchangeNeverStarted_AreIgnored()
    {
        var book = new ExchangeRecordBook(10, 100);

        book.Apply(Started(1, Alice));
        book.Apply(Received(1, "a"));
        book.Apply(new ExchangeEndedEvent(1, 1, 0));
        book.Apply(Received(1, "after the end"));
        book.Apply(Received(9, "never started"));
        book.Apply(new ExchangeEndedEvent(9, 0, 0));
        var record = book.Snapshot().Single();

        CollectionAssert.AreEqual("a"u8.ToArray(), record.Received);
    }

    [TestMethod]
    public void Apply_AnEventThatIsNoExchangesPart_ChangesNothing()
    {
        var book = new ExchangeRecordBook(10, 100);

        book.Apply(new LoginJudgedEvent(1, "http", "alice", "Basic", true, false));

        Assert.IsEmpty(book.Snapshot());
        Assert.AreEqual(0, book.DroppedExchangeCount);
    }

    [TestMethod]
    public void MarkOpenExchangesTruncated_MarksOnlyTheExchangesNotYetEnded()
    {
        var book = new ExchangeRecordBook(10, 100);
        book.Apply(Started(1, Alice));
        book.Apply(new ExchangeEndedEvent(1, 0, 0));
        book.Apply(Started(2, Bob));

        book.MarkOpenExchangesTruncated();
        var records = book.Snapshot();

        Assert.IsFalse(records[0].IsTruncated);
        Assert.IsTrue(records[1].IsTruncated);
    }

    [TestMethod]
    public void Snapshot_EventsAppliedAfterIt_LeaveItUnchanged()
    {
        var book = new ExchangeRecordBook(10, 100);
        book.Apply(Started(1, Alice));
        book.Apply(Received(1, "a"));

        var snapshot = book.Snapshot();
        book.Apply(Received(1, "b"));
        book.Apply(Started(2, Bob));

        Assert.HasCount(1, snapshot);
        CollectionAssert.AreEqual("a"u8.ToArray(), snapshot[0].Received);
    }

    [TestMethod]
    public void FindExchanges_ByAccountAndStore_ReturnsExactlyTheExchangesWhoseEventsSaySo()
    {
        var book = new ExchangeRecordBook(10, 100);
        book.Apply(Started(1, Alice, "smtp://127.0.0.1:49731/", "smtp"));
        book.Apply(Started(2, Bob, "smtp://127.0.0.1:49731/", "smtp"));
        book.Apply(new LoginJudgedEvent(2, "smtp", "bob", "PLAIN", true, false));
        book.Apply(new StoreChangedEvent(StoreKind.Mail, StoreChange.Delivered, "bob/INBOX/;UID=1", 10, 2));
        book.Apply(new ExchangeEndedEvent(2, 0, 0));
        book.Apply(new LoginJudgedEvent(99, "smtp", "bob", "PLAIN", true, false));
        book.Apply(new LoginJudgedEvent(null, "smtp", "bob", "PLAIN", true, false));

        var byAccount = book.FindExchanges(new SurlEventQuery { Account = "bob" });
        var byStore = book.FindExchanges(new SurlEventQuery { Store = StoreKind.Mail });
        var ended = book.FindExchanges(new SurlEventQuery { Kind = SurlEventKind.ExchangeEnded });

        Assert.AreEqual(2, byAccount.Single().ExchangeId);
        Assert.AreEqual(2, byStore.Single().ExchangeId);
        Assert.AreEqual(2, ended.Single().ExchangeId);
        Assert.HasCount(2, book.FindExchanges(new SurlEventQuery()));
    }

    [TestMethod]
    public void FactsOf_ADroppedOrUnknownExchange_IsNull()
    {
        var book = new ExchangeRecordBook(1, 100);
        book.Apply(Started(1, Alice));
        book.Apply(Started(2, Bob));

        Assert.IsNull(book.FactsOf(1));
        Assert.IsNull(book.FactsOf(null));
        Assert.AreEqual(2, book.FactsOf(2)!.ExchangeId);
    }

    private static ExchangeStartedEvent Started(long exchangeId, EndPoint client, string listenUrl = HttpListenUrl, string scheme = "http") =>
        new ExchangeStartedEvent(exchangeId, listenUrl, scheme, client) { Sequence = exchangeId, At = Now };

    private static ExchangeBytesEvent Received(long exchangeId, string text) =>
        new(exchangeId, ExchangeDirection.Received, System.Text.Encoding.ASCII.GetBytes(text));

    private static ExchangeBytesEvent Sent(long exchangeId, string text) =>
        new(exchangeId, ExchangeDirection.Sent, System.Text.Encoding.ASCII.GetBytes(text));
}
