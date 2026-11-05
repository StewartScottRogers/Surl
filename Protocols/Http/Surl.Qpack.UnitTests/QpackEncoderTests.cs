using Surl.Hpack;
using static Surl.Qpack.Rfc9204Hex;

namespace Surl.Qpack;

[TestClass]
public sealed class QpackEncoderTests
{
    [TestMethod]
    public void EncodeFieldSection_Null_Throws()
    {
        var encoder = new QpackEncoder(HpackStringCoding.WhenShorter);

        Assert.ThrowsExactly<ArgumentNullException>(() => encoder.EncodeFieldSection(null!));
    }

    [TestMethod]
    public void EncodeFieldSection_NoLines_IsThePrefixAlone()
    {
        var encoder = new QpackEncoder(HpackStringCoding.WhenShorter);

        var section = encoder.EncodeFieldSection([]);

        Assert.AreEqual("0000", Hex(section));
    }

    [TestMethod]
    [DataRow(":method", "GET", "d1")]
    [DataRow(":status", "500", "ff08")]
    [DataRow(":authority", "", "c0")]
    public void EncodeFieldSection_WholeLineInTheStaticTable_IsAnIndexedFieldLine(string name, string value, string line)
    {
        var encoder = new QpackEncoder(HpackStringCoding.Raw);

        var section = encoder.EncodeFieldSection([new QpackFieldLine(name, value)]);

        Assert.AreEqual("0000" + line, Hex(section));
    }

    [TestMethod]
    public void EncodeFieldSection_NeverIndexedLineInTheStaticTable_IsALiteralWithTheNBit()
    {
        var encoder = new QpackEncoder(HpackStringCoding.Raw);

        var section = encoder.EncodeFieldSection([new QpackFieldLine(":method", "GET", IsNeverIndexed: true)]);

        Assert.AreEqual("0000" + "7f00" + "03474554", Hex(section));
    }

    [TestMethod]
    public void EncodeFieldSection_NameNotInTheStaticTable_IsALiteralWithLiteralName()
    {
        var encoder = new QpackEncoder(HpackStringCoding.Raw);

        var section = encoder.EncodeFieldSection(
            [new QpackFieldLine("x-a", "b"), new QpackFieldLine("x-secret", "c", IsNeverIndexed: true)]);

        Assert.AreEqual("0000" + "23782d61" + "0162" + "3701782d736563726574" + "0163", Hex(section));
    }

    [TestMethod]
    public void EncodeFieldSection_HuffmanWhenShorter_CodesTheNameAndTheValue()
    {
        var encoder = new QpackEncoder(HpackStringCoding.WhenShorter);

        var section = encoder.EncodeFieldSection([new QpackFieldLine("custom-key", "custom-value")]);

        // RFC 7541 Appendix C.4.3's Huffman codes for custom-key and custom-value.
        Assert.AreEqual("0000" + "2f01" + "25a849e95ba97d7f" + "89" + "25a849e95bb8e8b4bf", Hex(section));
    }

    [TestMethod]
    public void EncodeFieldSection_ThenDecodeWithTheServerCapacity_GivesTheSameLines()
    {
        var encoder = new QpackEncoder(HpackStringCoding.WhenShorter);
        var decoder = new QpackDecoder(0, 1 << 20);
        QpackFieldLine[] lines =
        [
            new(":status", "200"), new("content-type", "text/plain"), new("content-length", "11"),
            new("server", "surl"), new("x-trace", "ÿ\u0000", IsNeverIndexed: true),
        ];

        var result = decoder.DecodeFieldSection(0, encoder.EncodeFieldSection(lines));

        CollectionAssert.AreEqual(lines, result.FieldLines.ToArray());
    }

    [TestMethod]
    public void ReceiveDecoderStream_StreamCancellations_AreAccepted()
    {
        var encoder = new QpackEncoder(HpackStringCoding.WhenShorter);

        var result = encoder.ReceiveDecoderStream(Bytes("48 7f"));
        var rest = encoder.ReceiveDecoderStream(Bytes("01"));

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(rest.IsAccepted);
    }

    [TestMethod]
    [DataRow("84", QpackDecodingError.UnexpectedSectionAcknowledgment)]
    [DataRow("01", QpackDecodingError.InvalidInsertCountIncrement)]
    [DataRow("00", QpackDecodingError.InvalidInsertCountIncrement)]
    public void ReceiveDecoderStream_InstructionAboutTheDynamicTable_FailsWithDecoderStreamError(
        string instruction, QpackDecodingError error)
    {
        var encoder = new QpackEncoder(HpackStringCoding.WhenShorter);

        var result = encoder.ReceiveDecoderStream(Bytes(instruction));

        Assert.AreEqual(error, result.Error);
        Assert.AreEqual(QpackErrorCode.DecoderStreamError, result.ErrorCode);
    }
}
