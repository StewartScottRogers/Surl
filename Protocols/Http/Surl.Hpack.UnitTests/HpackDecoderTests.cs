using static Surl.Hpack.Rfc7541Hex;

namespace Surl.Hpack;

[TestClass]
public sealed class HpackDecoderTests
{
    private const int AnyHeaderListSize = 1 << 20;

    [TestMethod]
    [DataRow("ff ff ff ff ff ff 0f", DisplayName = "index past 31 bits")]
    [DataRow("ff 80 80 80 80 80 00", DisplayName = "more continuation bytes than 31 bits need")]
    public void Decode_IntegerOverflow_ReturnsIntegerOverflow(string block)
    {
        AssertFails(block, HpackDecodingError.IntegerOverflow);
    }

    [TestMethod]
    [DataRow("ff")]
    [DataRow("ff 80")]
    [DataRow("7f")]
    public void Decode_BlockEndsInsideAnInteger_ReturnsTruncatedInteger(string block)
    {
        AssertFails(block, HpackDecodingError.TruncatedInteger);
    }

    [TestMethod]
    [DataRow("80", DisplayName = "index 0")]
    [DataRow("be", DisplayName = "index 62, empty dynamic table")]
    [DataRow("7f 08", DisplayName = "name index 71")]
    public void Decode_IndexOutsideBothTables_ReturnsIndexOutsideBothTables(string block)
    {
        AssertFails(block, HpackDecodingError.IndexOutsideBothTables);
    }

    [TestMethod]
    [DataRow("40 0a 63", DisplayName = "length past the block")]
    [DataRow("41", DisplayName = "no value at all")]
    public void Decode_TruncatedString_ReturnsTruncatedString(string block)
    {
        AssertFails(block, HpackDecodingError.TruncatedString);
    }

    [TestMethod]
    public void Decode_HuffmanStringWithEos_ReturnsHuffmanEndOfStringSymbol()
    {
        AssertFails("41 84 ff ff ff ff", HpackDecodingError.HuffmanEndOfStringSymbol);
    }

    [TestMethod]
    [DataRow("41 82 1f ff", DisplayName = "'a' then eleven one bits")]
    [DataRow("41 81 ff", DisplayName = "eight one bits")]
    public void Decode_HuffmanPaddingLongerThanSevenBits_ReturnsHuffmanPaddingLongerThanSevenBits(string block)
    {
        AssertFails(block, HpackDecodingError.HuffmanPaddingLongerThanSevenBits);
    }

    [TestMethod]
    public void Decode_HuffmanPaddingNotAllOnes_ReturnsHuffmanPaddingNotAllOnes()
    {
        AssertFails("41 81 18", HpackDecodingError.HuffmanPaddingNotAllOnes);
    }

    [TestMethod]
    public void Decode_TableSizeUpdateAboveTheAdvertisedSize_ReturnsTableSizeUpdateAboveLimit()
    {
        AssertFails("3f e2 1f", HpackDecodingError.TableSizeUpdateAboveLimit);
    }

    [TestMethod]
    public void Decode_TableSizeUpdateAfterAField_ReturnsTableSizeUpdateAfterField()
    {
        AssertFails("82 20", HpackDecodingError.TableSizeUpdateAfterField);
    }

    [TestMethod]
    public void Decode_HeaderListOverTheBound_ReturnsHeaderListTooLarge()
    {
        var decoder = new HpackDecoder(HpackDecoder.DefaultMaximumTableSize, maximumHeaderListSize: 83);

        var result = decoder.Decode(Bytes("82 82"));

        Assert.AreEqual(HpackDecodingError.HeaderListTooLarge, result.Error);
        Assert.IsEmpty(result.Fields);
    }

    [TestMethod]
    public void Decode_HeaderListExactlyAtTheBound_Decodes()
    {
        var decoder = new HpackDecoder(HpackDecoder.DefaultMaximumTableSize, maximumHeaderListSize: 84);

        var result = decoder.Decode(Bytes("82 82"));

        Assert.IsTrue(result.IsDecoded);
        Assert.HasCount(2, result.Fields);
    }

    [TestMethod]
    public void Decode_TableSizeUpdatesAtTheAdvertisedSizeAndBelow_ResizeTheTable()
    {
        var decoder = new HpackDecoder(HpackDecoder.DefaultMaximumTableSize, AnyHeaderListSize);
        decoder.Decode(Bytes("400a 6375 7374 6f6d 2d6b 6579 0d63 7573 746f 6d2d 6865 6164 6572"));

        var result = decoder.Decode(Bytes("20 3f e1 1f 82"));

        Assert.IsTrue(result.IsDecoded);
        Assert.AreEqual(HpackDecoder.DefaultMaximumTableSize, decoder.MaximumTableSize);
        Assert.AreEqual(0, decoder.TableSize);
        Assert.HasCount(1, result.Fields);
    }

    [TestMethod]
    public void Decode_EmptyBlock_GivesAnEmptyHeaderList()
    {
        var decoder = new HpackDecoder(HpackDecoder.DefaultMaximumTableSize, AnyHeaderListSize);

        var result = decoder.Decode([]);

        Assert.IsTrue(result.IsDecoded);
        Assert.IsNull(result.Error);
        Assert.IsEmpty(result.Fields);
    }

    [TestMethod]
    public void Decode_LiteralWithANameFromTheDynamicTable_TakesTheName()
    {
        var decoder = new HpackDecoder(HpackDecoder.DefaultMaximumTableSize, AnyHeaderListSize);
        decoder.Decode(Bytes("400a 6375 7374 6f6d 2d6b 6579 0d63 7573 746f 6d2d 6865 6164 6572"));

        var result = decoder.Decode(Bytes("0f 2f 01 78"));

        CollectionAssert.AreEqual(
            new[] { new HpackHeaderField("custom-key", "x", HpackFieldIndexing.WithoutIndexing) },
            result.Fields.ToArray());
    }

    [TestMethod]
    [DataRow(-1, 0)]
    [DataRow(0, -1)]
    public void Constructor_NegativeLimit_Throws(int tableSize, int headerListSize)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new HpackDecoder(tableSize, headerListSize));
    }

    private static void AssertFails(string block, HpackDecodingError error)
    {
        var decoder = new HpackDecoder(HpackDecoder.DefaultMaximumTableSize, AnyHeaderListSize);

        var result = decoder.Decode(Bytes(block));

        Assert.IsFalse(result.IsDecoded);
        Assert.AreEqual(error, result.Error);
        Assert.IsEmpty(result.Fields);
    }
}
