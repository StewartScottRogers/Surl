namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// Cutting received bytes into MessagePack hub messages by their VarInt length prefixes
/// (HubProtocol.md "Binary encoding").
/// </summary>
[TestClass]
public sealed class HubMessagePackRecordReaderTests
{
    [TestMethod]
    public void TryAppend_Null_IsRefused()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new HubMessagePackRecordReader(0).TryAppend([1], null!));
    }

    [TestMethod]
    public void TryAppend_TwoMessagesInOneBuffer_AreReadSeparately()
    {
        var reader = new HubMessagePackRecordReader(0);
        var records = new List<ReadOnlyMemory<byte>>();

        Assert.IsTrue(reader.TryAppend(MessagePackHex.Bytes("02 aa bb 01 cc"), records));

        Assert.HasCount(2, records);
        CollectionAssert.AreEqual(MessagePackHex.Bytes("aa bb"), records[0].ToArray());
        CollectionAssert.AreEqual(MessagePackHex.Bytes("cc"), records[1].ToArray());
    }

    [TestMethod]
    public void TryAppend_MessageSplitAfterItsPrefixAndWithinIt_IsReassembled()
    {
        var reader = new HubMessagePackRecordReader(0);
        var records = new List<ReadOnlyMemory<byte>>();

        Assert.IsTrue(reader.TryAppend(MessagePackHex.Bytes("03"), records));
        Assert.IsTrue(reader.TryAppend(MessagePackHex.Bytes("aa"), records));
        Assert.IsEmpty(records);
        Assert.IsTrue(reader.TryAppend(MessagePackHex.Bytes("bb cc 01"), records));

        Assert.HasCount(1, records);
        CollectionAssert.AreEqual(MessagePackHex.Bytes("aa bb cc"), records[0].ToArray());
        Assert.IsTrue(reader.TryAppend(MessagePackHex.Bytes("dd"), records));
        CollectionAssert.AreEqual(MessagePackHex.Bytes("dd"), records[1].ToArray());
    }

    [TestMethod]
    public void TryAppend_TwoByteVarIntArrivingOneByteAtATime_IsReadWhenComplete()
    {
        var reader = new HubMessagePackRecordReader(0);
        var records = new List<ReadOnlyMemory<byte>>();
        var message = new byte[] { 0xac, 0x02 }.Concat(new byte[300]).ToArray();

        foreach (var value in message)
        {
            Assert.IsTrue(reader.TryAppend([value], records));
        }

        Assert.HasCount(1, records);
        Assert.AreEqual(300, records[0].Length);
    }

    [TestMethod]
    public void TryAppend_MessageWithinTheLimit_IsRead()
    {
        var reader = new HubMessagePackRecordReader(2);
        var records = new List<ReadOnlyMemory<byte>>();

        Assert.IsTrue(reader.TryAppend(MessagePackHex.Bytes("02 aa bb"), records));
        Assert.HasCount(1, records);
    }

    [TestMethod]
    public void TryAppend_MessagePastTheLimit_IsRefusedBeforeItsBytesArrive()
    {
        var reader = new HubMessagePackRecordReader(2);
        var records = new List<ReadOnlyMemory<byte>>();

        Assert.IsFalse(reader.TryAppend(MessagePackHex.Bytes("01 aa 03"), records));

        Assert.HasCount(1, records);
    }

    [TestMethod]
    public void TryAppend_PrefixOfSixBytes_IsRefused()
    {
        var reader = new HubMessagePackRecordReader(0);

        Assert.IsFalse(reader.TryAppend(MessagePackHex.Bytes("80 80 80 80 80 01"), []));
    }

    [TestMethod]
    public void TryAppend_FiveByteVarInt_IsAPrefixAndWaitsForItsMessage()
    {
        var reader = new HubMessagePackRecordReader(0);
        var records = new List<ReadOnlyMemory<byte>>();

        Assert.IsTrue(reader.TryAppend(MessagePackHex.Bytes("80 80 80 80 01"), records));
        Assert.IsEmpty(records);
    }
}
