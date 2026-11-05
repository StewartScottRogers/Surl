using static Surl.Qpack.Rfc9204Hex;

namespace Surl.Qpack;

[TestClass]
public sealed class QpackDecoderTests
{
    /// <summary>The maximum table capacity Surl's HTTP/3 server advertises (ADR-0082 decision 8).</summary>
    private const int ServerTableCapacity = 0;

    private const int AppendixTableCapacity = 220;

    private const int AnyFieldSectionSize = 1 << 20;

    /// <summary>RFC 9204 Appendix B.2's encoder stream: capacity 220, then two inserts.</summary>
    private const string TwoInserts =
        "3fbd01 c00f 7777 772e 6578 616d 706c 652e 636f 6d c10c 2f73 616d 706c 652f 7061 7468";

    [TestMethod]
    public void Constructor_NegativeTableCapacity_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QpackDecoder(-1, AnyFieldSectionSize));
    }

    [TestMethod]
    public void Constructor_NegativeFieldSectionSize_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QpackDecoder(0, -1));
    }

    [TestMethod]
    public void DecodeFieldSection_ServerCapacityAndDynamicReference_FailsWithDecompressionFailed()
    {
        var decoder = new QpackDecoder(ServerTableCapacity, AnyFieldSectionSize);

        var result = decoder.DecodeFieldSection(0, Bytes("0000 80"));

        Assert.IsFalse(result.IsDecoded);
        Assert.IsEmpty(result.FieldLines);
        Assert.AreEqual(QpackDecodingError.DynamicReferenceOutsideTable, result.Error);
        Assert.AreEqual(QpackErrorCode.DecompressionFailed, result.ErrorCode);
    }

    [TestMethod]
    public void DecodeFieldSection_ServerCapacityAndNonZeroRequiredInsertCount_FailsWithDecompressionFailed()
    {
        var decoder = new QpackDecoder(ServerTableCapacity, AnyFieldSectionSize);

        var result = decoder.DecodeFieldSection(0, Bytes("0100 10"));

        Assert.AreEqual(QpackDecodingError.InvalidRequiredInsertCount, result.Error);
        Assert.AreEqual(QpackErrorCode.DecompressionFailed, result.ErrorCode);
    }

    [TestMethod]
    public void ReceiveEncoderStream_ServerCapacityAndInsert_FailsWithEncoderStreamError()
    {
        var decoder = new QpackDecoder(ServerTableCapacity, AnyFieldSectionSize);

        var result = decoder.ReceiveEncoderStream(Bytes("c00f 7777 772e 6578 616d 706c 652e 636f 6d"));

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(QpackDecodingError.EntryLargerThanCapacity, result.Error);
        Assert.AreEqual(QpackErrorCode.EncoderStreamError, result.ErrorCode);
    }

    [TestMethod]
    public void ReceiveEncoderStream_ServerCapacityAndCapacityAboveZero_FailsWithEncoderStreamError()
    {
        var decoder = new QpackDecoder(ServerTableCapacity, AnyFieldSectionSize);

        var result = decoder.ReceiveEncoderStream(Bytes("21"));

        Assert.AreEqual(QpackDecodingError.CapacityAboveMaximum, result.Error);
        Assert.AreEqual(QpackErrorCode.EncoderStreamError, result.ErrorCode);
    }

    [TestMethod]
    public void ReceiveEncoderStream_ServerCapacityAndCapacityZero_IsAccepted()
    {
        var decoder = new QpackDecoder(ServerTableCapacity, AnyFieldSectionSize);

        var result = decoder.ReceiveEncoderStream(Bytes("20"));

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.ErrorCode);
    }

    [TestMethod]
    public void ReceiveEncoderStream_ServerCapacityAndLiteralNameInsert_FailsBeforeTheNameArrives()
    {
        var decoder = new QpackDecoder(ServerTableCapacity, AnyFieldSectionSize);

        var result = decoder.ReceiveEncoderStream(Bytes("4a"));

        Assert.AreEqual(QpackDecodingError.EntryLargerThanCapacity, result.Error);
    }

    [TestMethod]
    public void ReceiveEncoderStream_DuplicateOfEmptyTable_FailsWithEncoderStreamError()
    {
        var decoder = new QpackDecoder(AppendixTableCapacity, AnyFieldSectionSize);

        var result = decoder.ReceiveEncoderStream(Bytes("00"));

        Assert.AreEqual(QpackDecodingError.DynamicReferenceOutsideTable, result.Error);
        Assert.AreEqual(QpackErrorCode.EncoderStreamError, result.ErrorCode);
    }

    [TestMethod]
    public void ReceiveEncoderStream_StaticNameIndexOutsideTable_FailsWithEncoderStreamError()
    {
        var decoder = new QpackDecoder(AppendixTableCapacity, AnyFieldSectionSize);
        decoder.ReceiveEncoderStream(Bytes("3fbd01"));

        var result = decoder.ReceiveEncoderStream(Bytes("ff24 00"));

        Assert.AreEqual(QpackDecodingError.StaticIndexOutsideTable, result.Error);
    }

    [TestMethod]
    public void ReceiveEncoderStream_ValueLongerThanTheCapacityLeaves_FailsBeforeTheValueArrives()
    {
        var decoder = new QpackDecoder(AppendixTableCapacity, AnyFieldSectionSize);
        decoder.ReceiveEncoderStream(Bytes("3fbd01"));

        // :authority (10 bytes) + 32 leaves 178 of 220 for the value; 179 is refused.
        var result = decoder.ReceiveEncoderStream(Bytes("c0 7f34"));

        Assert.AreEqual(QpackDecodingError.EntryLargerThanCapacity, result.Error);
    }

    [TestMethod]
    public void ReceiveEncoderStream_HuffmanCodedValue_InsertsTheDecodedValue()
    {
        var decoder = new QpackDecoder(AppendixTableCapacity, AnyFieldSectionSize);
        decoder.ReceiveEncoderStream(Bytes("3fbd01"));

        var result = decoder.ReceiveEncoderStream(Bytes("c0 8c f1e3 c2e5 f23a 6ba0 ab90 f4ff"));

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { new QpackFieldLine(":authority", "www.example.com") }, decoder.DynamicTableEntries.ToArray());
    }

    [TestMethod]
    public void ReceiveEncoderStream_HuffmanValueWithEndOfString_FailsWithEncoderStreamError()
    {
        var decoder = new QpackDecoder(AppendixTableCapacity, AnyFieldSectionSize);
        decoder.ReceiveEncoderStream(Bytes("3fbd01"));

        var result = decoder.ReceiveEncoderStream(Bytes("c0 84 ffff ffff"));

        Assert.AreEqual(QpackDecodingError.HuffmanEndOfStringSymbol, result.Error);
        Assert.AreEqual(QpackErrorCode.EncoderStreamError, result.ErrorCode);
    }

    [TestMethod]
    public void ReceiveEncoderStream_PartialInstruction_WaitsForTheRest()
    {
        var decoder = new QpackDecoder(AppendixTableCapacity, AnyFieldSectionSize);

        var first = decoder.ReceiveEncoderStream(Bytes("3fbd"));
        var tableCapacityBetween = decoder.TableCapacity;
        var second = decoder.ReceiveEncoderStream(Bytes("01"));

        Assert.IsTrue(first.IsAccepted);
        Assert.AreEqual(0, tableCapacityBetween);
        Assert.IsTrue(second.IsAccepted);
        Assert.AreEqual(220, decoder.TableCapacity);
    }

    [TestMethod]
    public void ReceiveEncoderStream_LowerCapacity_EvictsEntries()
    {
        var decoder = new QpackDecoder(AppendixTableCapacity, AnyFieldSectionSize);
        decoder.ReceiveEncoderStream(Bytes(TwoInserts));

        decoder.ReceiveEncoderStream(Bytes("20"));

        Assert.IsEmpty(decoder.DynamicTableEntries);
        Assert.AreEqual(2, decoder.FirstAbsoluteIndex);
        Assert.AreEqual(0, decoder.TableSize);
    }

    [TestMethod]
    public void DecodeFieldSection_RequiredInsertCountNotYetReceived_FailsAsTheDecoderHoldsNoBlockedStreams()
    {
        var decoder = new QpackDecoder(AppendixTableCapacity, AnyFieldSectionSize);

        var result = decoder.DecodeFieldSection(4, Bytes("0200 80"));

        Assert.AreEqual(QpackDecodingError.RequiredInsertCountNotReached, result.Error);
        Assert.AreEqual(QpackErrorCode.DecompressionFailed, result.ErrorCode);
    }

    [TestMethod]
    public void DecodeFieldSection_RequiredInsertCountAboveItsReferences_Fails()
    {
        var decoder = new QpackDecoder(AppendixTableCapacity, AnyFieldSectionSize);
        decoder.ReceiveEncoderStream(Bytes(TwoInserts));

        var result = decoder.DecodeFieldSection(4, Bytes("0300 d1"));

        Assert.AreEqual(QpackDecodingError.RequiredInsertCountAboveReferences, result.Error);
    }

    [TestMethod]
    public void DecodeFieldSection_ReferenceAtTheRequiredInsertCount_Fails()
    {
        var decoder = new QpackDecoder(AppendixTableCapacity, AnyFieldSectionSize);
        decoder.ReceiveEncoderStream(Bytes(TwoInserts));

        var result = decoder.DecodeFieldSection(4, Bytes("0200 10 11"));

        Assert.AreEqual(QpackDecodingError.DynamicReferenceOutsideTable, result.Error);
    }

    [TestMethod]
    public void DecodeFieldSection_ReferenceToAnEvictedEntry_Fails()
    {
        var decoder = new QpackDecoder(AppendixTableCapacity, AnyFieldSectionSize);
        decoder.ReceiveEncoderStream(Bytes(TwoInserts));
        decoder.ReceiveEncoderStream(Bytes("20 3fbd01"));

        var result = decoder.DecodeFieldSection(4, Bytes("0381 10"));

        Assert.AreEqual(QpackDecodingError.DynamicReferenceOutsideTable, result.Error);
    }

    [TestMethod]
    public void DecodeFieldSection_NegativeBase_Fails()
    {
        var decoder = new QpackDecoder(AppendixTableCapacity, AnyFieldSectionSize);
        decoder.ReceiveEncoderStream(Bytes(TwoInserts));

        var result = decoder.DecodeFieldSection(4, Bytes("0382 10"));

        Assert.AreEqual(QpackDecodingError.NegativeBase, result.Error);
    }

    [TestMethod]
    public void DecodeFieldSection_PositiveDeltaBase_ResolvesRelativeIndexesFromTheBase()
    {
        var decoder = new QpackDecoder(AppendixTableCapacity, AnyFieldSectionSize);
        decoder.ReceiveEncoderStream(Bytes(TwoInserts));

        // Required Insert Count 1, Base 1 + 1 = 2: relative index 1 is absolute index 0.
        var result = decoder.DecodeFieldSection(4, Bytes("0201 81"));

        CollectionAssert.AreEqual(
            new[] { new QpackFieldLine(":authority", "www.example.com") }, result.FieldLines.ToArray());
    }

    [TestMethod]
    public void DecodeFieldSection_LiteralsWithDynamicNameReferences_TakeTheNamesAndKeepTheNBit()
    {
        var decoder = new QpackDecoder(AppendixTableCapacity, AnyFieldSectionSize);
        decoder.ReceiveEncoderStream(Bytes(TwoInserts));

        // Base 1: 0x40 names relative index 0 (absolute 0); 0x08 with the N bit names post-Base index 0
        // (absolute 1).
        var result = decoder.DecodeFieldSection(4, Bytes("0380 40 0161 08 0162"));

        CollectionAssert.AreEqual(
            new[] { new QpackFieldLine(":authority", "a"), new QpackFieldLine(":path", "b", IsNeverIndexed: true) },
            result.FieldLines.ToArray());
    }

    [TestMethod]
    public void DecodeFieldSection_LiteralNameAndNeverIndexedStaticName_KeepTheNBit()
    {
        var decoder = new QpackDecoder(ServerTableCapacity, AnyFieldSectionSize);

        var result = decoder.DecodeFieldSection(0, Bytes("0000 23 616263 0178 75 0179 31 61 017a"));

        CollectionAssert.AreEqual(
            new[]
            {
                new QpackFieldLine("abc", "x"),
                new QpackFieldLine("cookie", "y", IsNeverIndexed: true),
                new QpackFieldLine("a", "z", IsNeverIndexed: true),
            },
            result.FieldLines.ToArray());
    }

    [TestMethod]
    public void DecodeFieldSection_LargerThanTheFieldSectionSize_FailsOnItsStreamAlone()
    {
        var decoder = new QpackDecoder(ServerTableCapacity, 41);

        var result = decoder.DecodeFieldSection(0, Bytes("0000 d1"));

        Assert.AreEqual(QpackDecodingError.FieldSectionTooLarge, result.Error);
        Assert.IsNull(result.ErrorCode);
    }

    [TestMethod]
    public void DecodeFieldSection_ExactlyTheFieldSectionSize_Decodes()
    {
        var decoder = new QpackDecoder(ServerTableCapacity, 42);

        var result = decoder.DecodeFieldSection(0, Bytes("0000 d1"));

        Assert.IsTrue(result.IsDecoded);
        Assert.IsNull(result.ErrorCode);
    }

    [TestMethod]
    [DataRow("00", QpackDecodingError.TruncatedInteger)]
    [DataRow("0000 510b 2f", QpackDecodingError.TruncatedString)]
    [DataRow("0000 ff24", QpackDecodingError.StaticIndexOutsideTable)]
    [DataRow("0000 5f54 00", QpackDecodingError.StaticIndexOutsideTable)]
    [DataRow("ffff ffff ffff ffff ffff ff", QpackDecodingError.IntegerOverflow)]
    [DataRow("0000 2f", QpackDecodingError.TruncatedInteger)]
    public void DecodeFieldSection_MalformedSection_NamesTheError(string section, QpackDecodingError error)
    {
        var decoder = new QpackDecoder(ServerTableCapacity, AnyFieldSectionSize);

        var result = decoder.DecodeFieldSection(0, Bytes(section));

        Assert.AreEqual(error, result.Error);
        Assert.AreEqual(QpackErrorCode.DecompressionFailed, result.ErrorCode);
    }

    [TestMethod]
    public void CancelStream_ServerCapacity_WritesNothing()
    {
        var decoder = new QpackDecoder(ServerTableCapacity, AnyFieldSectionSize);

        decoder.CancelStream(8);

        Assert.IsEmpty(decoder.TakeDecoderStreamInstructions());
    }

    [TestMethod]
    public void CancelStream_NonZeroCapacity_WritesAStreamCancellation()
    {
        var decoder = new QpackDecoder(AppendixTableCapacity, AnyFieldSectionSize);

        decoder.CancelStream(8);

        Assert.AreEqual("48", Hex(decoder.TakeDecoderStreamInstructions()));
        Assert.IsEmpty(decoder.TakeDecoderStreamInstructions());
    }

    [TestMethod]
    public void DecodeFieldSection_StaticOnlySection_WritesNoAcknowledgment()
    {
        var decoder = new QpackDecoder(AppendixTableCapacity, AnyFieldSectionSize);

        decoder.DecodeFieldSection(4, Bytes("0000 d1"));

        Assert.IsEmpty(decoder.TakeDecoderStreamInstructions());
    }

    [TestMethod]
    public void DecodeFieldSection_SecondAcknowledgmentBelowTheFirst_KeepsTheLargerCount()
    {
        var decoder = new QpackDecoder(AppendixTableCapacity, AnyFieldSectionSize);
        decoder.ReceiveEncoderStream(Bytes(TwoInserts));
        decoder.DecodeFieldSection(4, Bytes("0381 10 11"));

        decoder.DecodeFieldSection(8, Bytes("0200 80"));

        Assert.AreEqual("84" + "88", Hex(decoder.TakeDecoderStreamInstructions()));
    }
}
