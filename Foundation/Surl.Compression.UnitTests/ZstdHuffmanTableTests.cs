namespace Surl.Compression;

/// <summary>
/// The Huffman decoding table against RFC 8878, section 4.2.1's example: literals 0 to 4 with
/// weights 4, 3, 2, 0 and 1, the implied weight of literal 5 being 1, have the prefix codes 1, 01,
/// 001, none, 0000 and 0001.
/// </summary>
[TestClass]
public sealed class ZstdHuffmanTableTests
{
    [TestMethod]
    [DataRow(0, "1")]
    [DataRow(1, "01")]
    [DataRow(2, "001")]
    [DataRow(4, "0000")]
    [DataRow(5, "0001")]
    public void FromWeights_RfcExample_DecodesEachPrefixCode(int literal, string code)
    {
        ZstdHuffmanTable table = ZstdHuffmanTable.FromWeights([4, 3, 2, 0, 1]);
        ZstdBackwardBitReader reader = new(new ZstdBackwardBitStreamWriter().Add(code.Length, Convert.ToInt32(code, 2)).ToArray());

        byte decoded = table.DecodeSymbol(reader);

        Assert.AreEqual(4, table.TableLog);
        Assert.AreEqual(literal, decoded);
        Assert.AreEqual(0, reader.RemainingBits);
    }

    [TestMethod]
    public void Codes_RfcExample_AreTheExamplesPrefixCodes()
    {
        (int Code, int Length)[] codes = ZstdHuffmanLiteralsWriter.Codes([4, 3, 2, 0, 1]);

        CollectionAssert.AreEqual(
            new[] { "1", "01", "001", "0000", "0001" },
            new[] { 0, 1, 2, 4, 5 }.Select(literal => Convert.ToString(codes[literal].Code, 2).PadLeft(codes[literal].Length, '0')).ToArray());
    }
}
