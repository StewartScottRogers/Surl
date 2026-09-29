namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class RecordingExchangeLogTests
{
    [TestMethod]
    public void Entries_EveryKindOfCall_AreRecordedInOrder()
    {
        var log = new RecordingExchangeLog();

        log.BytesReceived("GET"u8);
        log.Note("mapped / to index.html");
        log.BytesSent("HTTP"u8);

        var entries = log.Entries;
        Assert.HasCount(3, entries);
        Assert.AreEqual(ExchangeLogEntryKind.BytesReceived, entries[0].Kind);
        CollectionAssert.AreEqual("GET"u8.ToArray(), entries[0].Bytes);
        Assert.AreEqual(string.Empty, entries[0].Text);
        Assert.AreEqual(ExchangeLogEntryKind.Note, entries[1].Kind);
        Assert.IsEmpty(entries[1].Bytes);
        Assert.AreEqual("mapped / to index.html", entries[1].Text);
        Assert.AreEqual(ExchangeLogEntryKind.BytesSent, entries[2].Kind);
        CollectionAssert.AreEqual("HTTP"u8.ToArray(), entries[2].Bytes);
    }

    [TestMethod]
    public void BytesReceived_CallerReusesItsBuffer_RecordedBytesAreACopy()
    {
        var log = new RecordingExchangeLog();
        var buffer = "abc"u8.ToArray();

        log.BytesReceived(buffer);
        buffer[0] = (byte)'z';

        CollectionAssert.AreEqual("abc"u8.ToArray(), log.Entries[0].Bytes);
    }

    [TestMethod]
    public void Notes_MixedCalls_ReturnsOnlyTheNotesInOrder()
    {
        var log = new RecordingExchangeLog();

        log.Note("first");
        log.BytesSent("x"u8);
        log.Note("second");

        CollectionAssert.AreEqual(new[] { "first", "second" }, log.Notes.ToArray());
    }

    [TestMethod]
    public void Note_NullText_Throws()
    {
        var log = new RecordingExchangeLog();

        Assert.ThrowsExactly<ArgumentNullException>(() => log.Note(null!));
    }
}
