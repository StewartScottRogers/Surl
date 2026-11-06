using System.Text;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// Records cut at <c>0x1E</c> across the transport messages that carry them (HubProtocol.md
/// "Overview"; ADR-0119, decisions 3 and 7).
/// </summary>
[TestClass]
public sealed class HubRecordReaderTests
{
    [TestMethod]
    public void TryAppend_TwoMessagesInOneBuffer_ReadsThemSeparately()
    {
        var reader = new HubRecordReader(1048576);
        var records = new List<ReadOnlyMemory<byte>>();

        var isWithinLimit = reader.TryAppend(Utf8("{\"type\":6}\u001e{\"type\":7}\u001e"), records);

        Assert.IsTrue(isWithinLimit);
        CollectionAssert.AreEqual(new[] { "{\"type\":6}", "{\"type\":7}" }, Texts(records));
    }

    [TestMethod]
    public void TryAppend_MessageSplitAcrossTwoBuffers_ReassemblesIt()
    {
        var reader = new HubRecordReader(1048576);
        var records = new List<ReadOnlyMemory<byte>>();

        Assert.IsTrue(reader.TryAppend(Utf8("{\"type\":1,\"target\":\"Ec"), records));
        Assert.IsEmpty(records);
        Assert.IsTrue(reader.TryAppend(Utf8("ho\",\"arguments\":[1]}\u001e{\"ty"), records));

        CollectionAssert.AreEqual(new[] { "{\"type\":1,\"target\":\"Echo\",\"arguments\":[1]}" }, Texts(records));
        Assert.IsTrue(reader.TryAppend(Utf8("pe\":6}\u001e"), records));
        Assert.AreEqual("{\"type\":6}", Texts(records)[1]);
    }

    [TestMethod]
    public void TryAppend_RecordOfExactlyTheLimit_IsRead()
    {
        var reader = new HubRecordReader(10);
        var records = new List<ReadOnlyMemory<byte>>();

        Assert.IsTrue(reader.TryAppend(Utf8("{\"type\":6}\u001e"), records));

        Assert.HasCount(1, records);
    }

    [TestMethod]
    public void TryAppend_MessageOverTheLimit_IsRefusedBeforeItsEnd()
    {
        var reader = new HubRecordReader(10);
        var records = new List<ReadOnlyMemory<byte>>();

        Assert.IsTrue(reader.TryAppend(Utf8("{\"type\":"), records));
        var isWithinLimit = reader.TryAppend(Utf8("611"), records);

        Assert.IsFalse(isWithinLimit);
        Assert.IsEmpty(records);
    }

    [TestMethod]
    public void TryAppend_RecordsBeforeOneOverTheLimit_AreKept()
    {
        var reader = new HubRecordReader(10);
        var records = new List<ReadOnlyMemory<byte>>();

        var isWithinLimit = reader.TryAppend(Utf8("{\"type\":6}\u001e{\"type\":71}\u001e"), records);

        Assert.IsFalse(isWithinLimit);
        CollectionAssert.AreEqual(new[] { "{\"type\":6}" }, Texts(records));
    }

    [TestMethod]
    public void TryAppend_LimitZero_HasNoLimit()
    {
        var reader = new HubRecordReader(0);
        var records = new List<ReadOnlyMemory<byte>>();
        var big = new string('x', 100000);

        Assert.IsTrue(reader.TryAppend(Utf8(big + "\u001e"), records));

        Assert.AreEqual(100000, records[0].Length);
    }

    [TestMethod]
    public void TryAppend_EmptyRecord_IsAddedEmpty()
    {
        var reader = new HubRecordReader(10);
        var records = new List<ReadOnlyMemory<byte>>();

        Assert.IsTrue(reader.TryAppend(Utf8("\u001e"), records));

        Assert.IsTrue(records[0].IsEmpty);
    }

    [TestMethod]
    public void TryAppend_NullRecords_Throws()
    {
        var reader = new HubRecordReader(10);

        Assert.ThrowsExactly<ArgumentNullException>(() => reader.TryAppend([], null!));
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static string[] Texts(List<ReadOnlyMemory<byte>> records) =>
        records.Select(record => Encoding.UTF8.GetString(record.Span)).ToArray();
}
