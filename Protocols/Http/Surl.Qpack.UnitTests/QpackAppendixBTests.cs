using Surl.Hpack;
using static Surl.Qpack.Rfc9204Hex;

namespace Surl.Qpack;

/// <summary>
/// Every example of RFC 9204 Appendix B, replayed in order through one decoder whose maximum table
/// capacity is the examples' 220: each field section decodes to the RFC's field lines, each encoder
/// instruction leaves the RFC's table, and the decoder writes the RFC's decoder instructions. The one
/// example that uses only the static table and literals, B.1, is encoded back to the RFC's bytes.
/// The bytes are the RFC's (ADR-0003).
/// </summary>
[TestClass]
public sealed class QpackAppendixBTests
{
    private const int AppendixTableCapacity = 220;

    private const int AnyFieldSectionSize = 1 << 20;

    private const string B1Section = "0000 510b 2f69 6e64 6578 2e68 746d 6c";

    private const string B2EncoderStream =
        "3fbd01 c00f 7777 772e 6578 616d 706c 652e 636f 6d c10c 2f73 616d 706c 652f 7061 7468";

    private const string B2Section = "0381 10 11";

    private const string B3EncoderStream = "4a63 7573 746f 6d2d 6b65 790c 6375 7374 6f6d 2d76 616c 7565";

    private const string B4EncoderStream = "02";

    private const string B4Section = "0500 80 c1 81";

    private const string B5EncoderStream = "810d 6375 7374 6f6d 2d76 616c 7565 32";

    private static readonly QpackFieldLine Authority = new(":authority", "www.example.com");

    private static readonly QpackFieldLine SamplePath = new(":path", "/sample/path");

    private static readonly QpackFieldLine CustomKey = new("custom-key", "custom-value");

    [TestMethod]
    public void B1_LiteralFieldLineWithNameReference_DecodesToThePath()
    {
        var decoder = NewDecoder();

        var result = decoder.DecodeFieldSection(0, Bytes(B1Section));

        Assert.IsTrue(result.IsDecoded);
        CollectionAssert.AreEqual(new[] { new QpackFieldLine(":path", "/index.html") }, result.FieldLines.ToArray());
        Assert.AreEqual(0, decoder.TableSize);
        Assert.IsEmpty(decoder.TakeDecoderStreamInstructions());
    }

    [TestMethod]
    public void B1_LiteralFieldLineWithNameReference_EncodesToTheRfcBytes()
    {
        var encoder = new QpackEncoder(HpackStringCoding.Raw);

        var section = encoder.EncodeFieldSection([new QpackFieldLine(":path", "/index.html")]);

        Assert.AreEqual(Hex(Bytes(B1Section)), Hex(section));
    }

    [TestMethod]
    public void B2_EncoderStream_SetsTheCapacityAndInsertsTwoEntries()
    {
        var decoder = NewDecoder();

        var result = decoder.ReceiveEncoderStream(Bytes(B2EncoderStream));

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(220, decoder.TableCapacity);
        AssertTable(decoder, 0, 106, Authority, SamplePath);
    }

    [TestMethod]
    public void B2_FieldSectionWithPostBaseIndexes_DecodesAndIsAcknowledged()
    {
        var decoder = NewDecoder();
        decoder.ReceiveEncoderStream(Bytes(B2EncoderStream));

        var result = decoder.DecodeFieldSection(4, Bytes(B2Section));

        CollectionAssert.AreEqual(new[] { Authority, SamplePath }, result.FieldLines.ToArray());
        Assert.AreEqual("84", Hex(decoder.TakeDecoderStreamInstructions()));
        AssertTable(decoder, 0, 106, Authority, SamplePath);
    }

    [TestMethod]
    public void B3_SpeculativeInsert_IsCountedByAnInsertCountIncrement()
    {
        var decoder = DecoderAfterB2();

        var result = decoder.ReceiveEncoderStream(Bytes(B3EncoderStream));

        Assert.IsTrue(result.IsAccepted);
        AssertTable(decoder, 0, 160, Authority, SamplePath, CustomKey);
        Assert.AreEqual("01", Hex(decoder.TakeDecoderStreamInstructions()));
    }

    [TestMethod]
    public void B4_Duplicate_ReinsertsTheOldestEntry()
    {
        var decoder = DecoderAfterB3();

        var result = decoder.ReceiveEncoderStream(Bytes(B4EncoderStream));

        Assert.IsTrue(result.IsAccepted);
        AssertTable(decoder, 0, 217, Authority, SamplePath, CustomKey, Authority);
    }

    [TestMethod]
    public void B4_FieldSectionWithRelativeIndexes_DecodesToTheRfcFieldLines()
    {
        var decoder = DecoderAfterB3();
        decoder.ReceiveEncoderStream(Bytes(B4EncoderStream));

        var result = decoder.DecodeFieldSection(8, Bytes(B4Section));

        CollectionAssert.AreEqual(
            new[] { Authority, new QpackFieldLine(":path", "/"), CustomKey }, result.FieldLines.ToArray());
    }

    [TestMethod]
    public void B4_StreamCancellation_WritesTheRfcInstructionFirst()
    {
        var decoder = DecoderAfterB3();
        decoder.ReceiveEncoderStream(Bytes(B4EncoderStream));

        decoder.CancelStream(8);

        // RFC 9204 leaves when to send an Insert Count Increment to the decoder; this one sends the
        // Duplicate's straight after the cancellation.
        Assert.AreEqual("48" + "01", Hex(decoder.TakeDecoderStreamInstructions()));
    }

    [TestMethod]
    public void B5_InsertWithDynamicNameReference_EvictsTheOldestEntry()
    {
        var decoder = DecoderAfterB3();
        decoder.ReceiveEncoderStream(Bytes(B4EncoderStream));
        decoder.CancelStream(8);
        decoder.TakeDecoderStreamInstructions();

        var result = decoder.ReceiveEncoderStream(Bytes(B5EncoderStream));

        Assert.IsTrue(result.IsAccepted);
        AssertTable(
            decoder, 1, 215, SamplePath, CustomKey, Authority, new QpackFieldLine("custom-key", "custom-value2"));
        Assert.AreEqual("01", Hex(decoder.TakeDecoderStreamInstructions()));
    }

    [TestMethod]
    public void B2_EncoderStreamOneByteAtATime_LeavesTheSameTable()
    {
        var decoder = NewDecoder();

        foreach (var octet in Bytes(B2EncoderStream))
        {
            Assert.IsTrue(decoder.ReceiveEncoderStream([octet]).IsAccepted);
        }

        AssertTable(decoder, 0, 106, Authority, SamplePath);
        Assert.AreEqual(0, decoder.UnconsumedEncoderStreamCount);
    }

    private static QpackDecoder NewDecoder() => new(AppendixTableCapacity, AnyFieldSectionSize);

    private static QpackDecoder DecoderAfterB2()
    {
        var decoder = NewDecoder();
        decoder.ReceiveEncoderStream(Bytes(B2EncoderStream));
        decoder.DecodeFieldSection(4, Bytes(B2Section));
        decoder.TakeDecoderStreamInstructions();
        return decoder;
    }

    private static QpackDecoder DecoderAfterB3()
    {
        var decoder = DecoderAfterB2();
        decoder.ReceiveEncoderStream(Bytes(B3EncoderStream));
        decoder.TakeDecoderStreamInstructions();
        return decoder;
    }

    private static void AssertTable(
        QpackDecoder decoder, long firstAbsoluteIndex, long size, params QpackFieldLine[] entries)
    {
        Assert.AreEqual(firstAbsoluteIndex, decoder.FirstAbsoluteIndex);
        Assert.AreEqual(size, decoder.TableSize);
        Assert.AreEqual(firstAbsoluteIndex + entries.Length, decoder.InsertCount);
        CollectionAssert.AreEqual(entries, decoder.DynamicTableEntries.ToArray());
    }
}
