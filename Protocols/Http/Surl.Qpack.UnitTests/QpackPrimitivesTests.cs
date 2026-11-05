using Surl.Hpack;
using static Surl.Qpack.Rfc9204Hex;

namespace Surl.Qpack;

[TestClass]
public sealed class QpackPrimitivesTests
{
    [TestMethod]
    [DataRow(10L, 5, "0a")]
    [DataRow(1337L, 5, "1f9a0a")]
    [DataRow(42L, 8, "2a")]
    [DataRow(QpackPrimitives.MaximumInteger, 8, "ff 80fe ffff ffff ffff 3f")]
    public void WriteInteger_Value_IsRfc7541Section51Coded(long value, int prefixBits, string expected)
    {
        var output = new List<byte>();

        QpackPrimitives.WriteInteger(output, value, prefixBits, 0x00);

        Assert.AreEqual(Hex(Bytes(expected)), Hex([.. output]));
    }

    [TestMethod]
    [DataRow("0a", 5, 10L)]
    [DataRow("1f9a0a", 5, 1337L)]
    [DataRow("ff 80fe ffff ffff ffff 3f", 8, QpackPrimitives.MaximumInteger)]
    public void ReadInteger_Bytes_GiveTheValue(string bytes, int prefixBits, long expected)
    {
        var position = 0;

        var value = QpackPrimitives.ReadInteger(Bytes(bytes), ref position, prefixBits);

        Assert.AreEqual(expected, value);
        Assert.AreEqual(Bytes(bytes).Length, position);
    }

    [TestMethod]
    [DataRow("ff 81fe ffff ffff ffff 3f")]
    [DataRow("ff 8080 8080 8080 8080 8000")]
    public void ReadInteger_LongerThan62Bits_IsAnOverflow(string bytes)
    {
        var position = 0;

        var exception = Assert.ThrowsExactly<QpackDecodingException>(
            () => QpackPrimitives.ReadInteger(Bytes(bytes), ref position, 8));

        Assert.AreEqual(QpackDecodingError.IntegerOverflow, exception.Error);
        Assert.IsFalse(exception.IsTruncation);
    }

    [TestMethod]
    public void ReadInteger_NoBytes_IsATruncation()
    {
        var position = 0;

        var exception = Assert.ThrowsExactly<QpackDecodingException>(
            () => QpackPrimitives.ReadInteger([], ref position, 8));

        Assert.AreEqual(QpackDecodingError.TruncatedInteger, exception.Error);
        Assert.IsTrue(exception.IsTruncation);
    }

    [TestMethod]
    [DataRow(HpackStringCoding.Raw, "0361 6263")]
    [DataRow(HpackStringCoding.Huffman, "821c 64")]
    [DataRow(HpackStringCoding.WhenShorter, "821c 64")]
    public void WriteString_EightBitPrefix_FollowsTheCoding(HpackStringCoding coding, string expected)
    {
        var output = new List<byte>();

        QpackPrimitives.WriteString(output, "abc", 8, 0x00, coding);

        Assert.AreEqual(Hex(Bytes(expected)), Hex([.. output]));
    }

    [TestMethod]
    public void WriteString_WhenShorterAndHuffmanIsLonger_IsRaw()
    {
        var output = new List<byte>();

        QpackPrimitives.WriteString(output, "ÿ", 8, 0x00, HpackStringCoding.WhenShorter);

        Assert.AreEqual("01ff", Hex([.. output]));
    }

    [TestMethod]
    public void ReadString_SixBitPrefixHuffman_ReadsTheFlagAboveTheLength()
    {
        var bytes = Bytes("62 1c64");
        var position = 0;

        var text = QpackPrimitives.ReadString(bytes, ref position, 6, 100);

        Assert.AreEqual("abc", text);
        Assert.AreEqual(3, position);
    }

    [TestMethod]
    [DataRow("8a 6162", 2L, QpackDecodingError.EntryLargerThanCapacity)]
    [DataRow("03 61", 3L, QpackDecodingError.TruncatedString)]
    [DataRow("", 3L, QpackDecodingError.TruncatedInteger)]
    [DataRow("84 ffff ffff", 10L, QpackDecodingError.HuffmanEndOfStringSymbol)]
    [DataRow("82 1fff", 10L, QpackDecodingError.HuffmanPaddingLongerThanSevenBits)]
    [DataRow("81 18", 10L, QpackDecodingError.HuffmanPaddingNotAllOnes)]
    public void ReadString_Malformed_NamesTheError(string bytes, long maximumDecodedLength, QpackDecodingError error)
    {
        var position = 0;

        var exception = Assert.ThrowsExactly<QpackDecodingException>(
            () => QpackPrimitives.ReadString(Bytes(bytes), ref position, 8, maximumDecodedLength));

        Assert.AreEqual(error, exception.Error);
    }

    [TestMethod]
    public void ReadString_HuffmanWithinFourTimesTheLimit_IsReadAndDecoded()
    {
        var bytes = Bytes("821c 64");
        var position = 0;

        var text = QpackPrimitives.ReadString(bytes, ref position, 8, 1);

        Assert.AreEqual("abc", text);
    }
}
