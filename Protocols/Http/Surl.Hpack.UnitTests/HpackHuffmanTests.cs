using System.Text;
using static Surl.Hpack.Rfc7541Hex;

namespace Surl.Hpack;

[TestClass]
public sealed class HpackHuffmanTests
{
    [TestMethod]
    [DataRow(0, 0x1ff8u, 13)]
    [DataRow(' ', 0x14u, 6)]
    [DataRow('0', 0x0u, 5)]
    [DataRow('A', 0x21u, 6)]
    [DataRow('a', 0x3u, 5)]
    [DataRow('~', 0x1ffdu, 13)]
    [DataRow(255, 0x3ffffeeu, 26)]
    [DataRow(HpackHuffman.EndOfStringSymbol, 0x3fffffffu, 30)]
    public void GetCode_Symbol_IsTheAppendixBCode(int symbol, uint code, int length)
    {
        var actual = HpackHuffman.GetCode(symbol);

        Assert.AreEqual((code, length), actual);
    }

    [TestMethod]
    public void Encode_WwwExampleCom_IsTheAppendixC4Bytes()
    {
        var encoded = HpackHuffman.Encode(Encoding.ASCII.GetBytes("www.example.com"));

        Assert.AreEqual("f1e3c2e5f23a6ba0ab90f4ff", Hex(encoded));
    }

    [TestMethod]
    public void Encode_WholeBytes_NeedsNoPadding()
    {
        var encoded = HpackHuffman.Encode(Encoding.ASCII.GetBytes("00000000"));

        Assert.AreEqual("0000000000", Hex(encoded));
    }

    [TestMethod]
    public void TryDecode_EveryByteValue_RoundTrips()
    {
        var everyByte = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();

        var isDecoded = HpackHuffman.TryDecode(HpackHuffman.Encode(everyByte), out var decoded, out _);

        Assert.IsTrue(isDecoded);
        CollectionAssert.AreEqual(everyByte, decoded);
    }

    [TestMethod]
    public void TryDecode_Empty_GivesEmpty()
    {
        var isDecoded = HpackHuffman.TryDecode([], out var decoded, out _);

        Assert.IsTrue(isDecoded);
        Assert.IsEmpty(decoded);
    }

    [TestMethod]
    [DataRow("ffffffff", HpackDecodingError.HuffmanEndOfStringSymbol)]
    [DataRow("1fff", HpackDecodingError.HuffmanPaddingLongerThanSevenBits)]
    [DataRow("18", HpackDecodingError.HuffmanPaddingNotAllOnes)]
    public void TryDecode_MalformedString_NamesTheError(string coded, HpackDecodingError error)
    {
        var isDecoded = HpackHuffman.TryDecode(Bytes(coded), out var decoded, out var actual);

        Assert.IsFalse(isDecoded);
        Assert.IsEmpty(decoded);
        Assert.AreEqual(error, actual);
    }

    [TestMethod]
    public void GetEncodedLength_WwwExampleCom_IsTwelve()
    {
        var length = HpackHuffman.GetEncodedLength(Encoding.ASCII.GetBytes("www.example.com"));

        Assert.AreEqual(12, length);
    }
}
