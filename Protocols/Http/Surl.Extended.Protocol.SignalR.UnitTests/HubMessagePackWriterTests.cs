namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The MessagePack hub protocol's writing: the VarInt length prefix of HubProtocol.md "Binary
/// encoding" and the shortest MessagePack form of each value.
/// </summary>
[TestClass]
public sealed class HubMessagePackWriterTests
{
    [TestMethod]
    public void Write_Null_IsRefused()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => HubMessagePackWriter.Write(null!));
    }

    [TestMethod]
    public void Write_CompletionWithAResult_IsItsArray()
    {
        var message = new HubCompletion("1", new HubJsonValue(MessagePackHex.Bytes("a2 68 69")), null);

        CollectionAssert.AreEqual(MessagePackHex.Prefixed("95 03 80 a1 31 03 a2 68 69"), HubMessagePackWriter.Write(message));
    }

    [TestMethod]
    public void Write_ErrorClose_IsThreeElements()
    {
        var message = new HubClose("e", AllowReconnect: true);

        CollectionAssert.AreEqual(MessagePackHex.Prefixed("93 07 a1 65 c3"), HubMessagePackWriter.Write(message));
    }

    [TestMethod]
    [DataRow(127, new byte[] { 0x7f })]
    [DataRow(128, new byte[] { 0x80, 0x01 })]
    [DataRow(300, new byte[] { 0xac, 0x02 })]
    [DataRow(16384, new byte[] { 0x80, 0x80, 0x01 })]
    public void Write_LongMessage_StartsWithItsLengthAsAVarInt(int bodyLength, byte[] expectedPrefix)
    {
        // A StreamItem is 5 bytes (array header, type, headers, id) before its item; the item is a byte array.
        var item = new byte[bodyLength - 5];
        item[0] = 0xc6;
        item[1] = 0;
        item[2] = 0;
        item[3] = (byte)((item.Length - 5) >> 8);
        item[4] = (byte)(item.Length - 5);
        var message = new HubStreamItem("1", new HubJsonValue(item));

        var written = HubMessagePackWriter.Write(message);

        CollectionAssert.AreEqual(expectedPrefix, written[..expectedPrefix.Length]);
        Assert.AreEqual(expectedPrefix.Length + bodyLength, written.Length);
    }

    [TestMethod]
    [DataRow(0, "00")]
    [DataRow(127, "7f")]
    [DataRow(128, "cc 80")]
    [DataRow(255, "cc ff")]
    [DataRow(256, "cd 01 00")]
    [DataRow(65535, "cd ff ff")]
    [DataRow(65536, "ce 00 01 00 00")]
    [DataRow(int.MaxValue, "ce 7f ff ff ff")]
    public void WriteUnsigned_Number_IsItsShortestForm(int number, string expected)
    {
        var writer = new MessagePackWriter();

        writer.WriteUnsigned(number);

        CollectionAssert.AreEqual(MessagePackHex.Bytes(expected), writer.Written.ToArray());
    }

    [TestMethod]
    [DataRow(0, "90")]
    [DataRow(15, "9f")]
    [DataRow(16, "dc 00 10")]
    [DataRow(65535, "dc ff ff")]
    [DataRow(65536, "dd 00 01 00 00")]
    public void WriteArrayHeader_Count_IsItsShortestForm(int count, string expected)
    {
        var writer = new MessagePackWriter();

        writer.WriteArrayHeader(count);

        CollectionAssert.AreEqual(MessagePackHex.Bytes(expected), writer.Written.ToArray());
    }

    [TestMethod]
    [DataRow(0, "a0")]
    [DataRow(31, "bf")]
    [DataRow(32, "d9 20")]
    [DataRow(255, "d9 ff")]
    [DataRow(256, "da 01 00")]
    [DataRow(65535, "da ff ff")]
    [DataRow(65536, "db 00 01 00 00")]
    public void WriteStringOrNil_String_IsItsShortestFormFollowedByItsBytes(int length, string expectedHeader)
    {
        var writer = new MessagePackWriter();

        writer.WriteStringOrNil(new string('a', length));

        var expected = MessagePackHex.Bytes(expectedHeader).Concat(Enumerable.Repeat((byte)'a', length)).ToArray();
        CollectionAssert.AreEqual(expected, writer.Written.ToArray());
    }

    [TestMethod]
    public void WriteStringOrNil_NonAscii_IsItsUtf8ByteCount()
    {
        var writer = new MessagePackWriter();

        writer.WriteStringOrNil("€");

        CollectionAssert.AreEqual(MessagePackHex.Bytes("a3 e2 82 ac"), writer.Written.ToArray());
    }

    [TestMethod]
    public void WriteBoolAndNilAndEmptyMap_AreTheirSingleBytes()
    {
        var writer = new MessagePackWriter();

        writer.WriteBool(false);
        writer.WriteBool(true);
        writer.WriteNil();
        writer.WriteStringOrNil(null);
        writer.WriteEmptyMap();

        CollectionAssert.AreEqual(MessagePackHex.Bytes("c2 c3 c0 c0 80"), writer.Written.ToArray());
    }
}
