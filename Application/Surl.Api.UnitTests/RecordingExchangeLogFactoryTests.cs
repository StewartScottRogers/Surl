using System.Net;

namespace Surl.Api;

[TestClass]
public sealed class RecordingExchangeLogFactoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 9, 30, 0, TimeSpan.Zero);
    private static readonly IPEndPoint Alice = new(IPAddress.Loopback, 50001);
    private static readonly IPEndPoint Bob = new(IPAddress.IPv6Loopback, 50002);

    [TestMethod]
    public void Create_OneExchange_KeepsReceivedAndSentBytesInTheOrderTheyCrossed()
    {
        var factory = new RecordingExchangeLogFactory(10, 100, new FixedTimeProvider(Now));

        var log = factory.Create(7, Alice);
        log.BytesReceived("abc"u8);
        log.BytesSent("def"u8);
        log.BytesReceived("g"u8);
        var record = factory.Snapshot().Single();

        Assert.AreEqual(7, record.ExchangeId);
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
    public void Create_TwoExchanges_KeepsThemApartWithTheirIdsAndClientsOldestFirst()
    {
        var factory = new RecordingExchangeLogFactory(10, 100, new FixedTimeProvider(Now));

        var first = factory.Create(1, Alice);
        var second = factory.Create(2, Bob);
        second.BytesReceived("to bob"u8);
        first.BytesReceived("to alice"u8);
        var records = factory.Snapshot();

        Assert.HasCount(2, records);
        Assert.AreEqual(1, records[0].ExchangeId);
        Assert.AreEqual(Alice, records[0].Client);
        CollectionAssert.AreEqual("to alice"u8.ToArray(), records[0].Received);
        Assert.AreEqual(2, records[1].ExchangeId);
        Assert.AreEqual(Bob, records[1].Client);
        CollectionAssert.AreEqual("to bob"u8.ToArray(), records[1].Received);
    }

    [TestMethod]
    public void Create_TftpDatagramExchange_KeepsEachDatagramAsOneChunk()
    {
        var factory = new RecordingExchangeLogFactory(10, 1000, new FixedTimeProvider(Now));
        byte[] readRequest = [0, 1, .. "file.txt"u8, 0, .. "octet"u8, 0];
        byte[] firstData = [0, 3, 0, 1, .. "hello"u8];
        byte[] acknowledgement = [0, 4, 0, 1];

        var log = factory.Create(3, Alice);
        log.BytesReceived(readRequest);
        log.BytesSent(firstData);
        log.BytesReceived(acknowledgement);
        var record = factory.Snapshot().Single();

        Assert.HasCount(3, record.Chunks);
        CollectionAssert.AreEqual(readRequest, record.Chunks[0].Bytes.ToArray());
        CollectionAssert.AreEqual(firstData, record.Chunks[1].Bytes.ToArray());
        CollectionAssert.AreEqual(acknowledgement, record.Chunks[2].Bytes.ToArray());
        CollectionAssert.AreEqual(firstData, record.Sent);
    }

    [TestMethod]
    public void Create_BytesPastTheCapBothDirectionsTogether_CutsThemAndMarksTheRecordTruncated()
    {
        var factory = new RecordingExchangeLogFactory(10, 5, new FixedTimeProvider(Now));

        var log = factory.Create(1, Alice);
        log.BytesReceived("abc"u8);
        log.BytesSent("defg"u8);
        log.BytesReceived("h"u8);
        var record = factory.Snapshot().Single();

        Assert.IsTrue(record.IsTruncated);
        Assert.HasCount(2, record.Chunks);
        CollectionAssert.AreEqual("abc"u8.ToArray(), record.Received);
        CollectionAssert.AreEqual("de"u8.ToArray(), record.Sent);
    }

    [TestMethod]
    public void Create_BytesExactlyAtTheCap_KeepsThemAllAndIsNotTruncated()
    {
        var factory = new RecordingExchangeLogFactory(10, 5, new FixedTimeProvider(Now));

        var log = factory.Create(1, Alice);
        log.BytesReceived("abcde"u8);
        var record = factory.Snapshot().Single();

        Assert.IsFalse(record.IsTruncated);
        CollectionAssert.AreEqual("abcde"u8.ToArray(), record.Received);
    }

    [TestMethod]
    public void Create_MoreExchangesThanTheCap_DropsTheOldestAndCountsIt()
    {
        var factory = new RecordingExchangeLogFactory(2, 100, new FixedTimeProvider(Now));

        factory.Create(1, Alice);
        factory.Create(2, Alice);
        factory.Create(3, Bob);
        var records = factory.Snapshot();

        CollectionAssert.AreEqual(new long[] { 2, 3 }, records.Select(record => record.ExchangeId).ToArray());
        Assert.AreEqual(1, factory.DroppedExchangeCount);
    }

    [TestMethod]
    public void Snapshot_BytesRecordedAfterIt_LeaveItUnchanged()
    {
        var factory = new RecordingExchangeLogFactory(10, 100, new FixedTimeProvider(Now));
        var log = factory.Create(1, Alice);
        log.BytesReceived("a"u8);

        var snapshot = factory.Snapshot();
        log.BytesReceived("b"u8);
        factory.Create(2, Bob);

        Assert.HasCount(1, snapshot);
        CollectionAssert.AreEqual("a"u8.ToArray(), snapshot[0].Received);
    }

    [TestMethod]
    public void Note_AndNoteOutsideExchange_RecordNothing()
    {
        var factory = new RecordingExchangeLogFactory(10, 100, new FixedTimeProvider(Now));

        var log = factory.Create(1, Alice);
        log.Note("refused");
        factory.NoteOutsideExchange("connection refused past the limit");
        var record = factory.Snapshot().Single();

        Assert.IsEmpty(record.Chunks);
        Assert.AreEqual(0, factory.DroppedExchangeCount);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
