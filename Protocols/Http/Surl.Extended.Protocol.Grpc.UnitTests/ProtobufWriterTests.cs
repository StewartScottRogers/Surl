namespace Surl.Extended.Protocol.Grpc;

[TestClass]
public sealed class ProtobufWriterTests
{
    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(ProtobufReader.MaximumFieldNumber + 1)]
    public void WriteUInt64_FieldNumberOutsideOneTo2Pow29Minus1_ThrowsArgumentOutOfRangeException(int fieldNumber)
    {
        var writer = new ProtobufWriter();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => writer.WriteUInt64(fieldNumber, 1));
    }

    [TestMethod]
    public void WriteString_NonAscii_WritesUtf8()
    {
        var writer = new ProtobufWriter();

        writer.WriteString(1, "é");

        CollectionAssert.AreEqual(new byte[] { 0x0A, 0x02, 0xC3, 0xA9 }, writer.ToArray());
    }

    [TestMethod]
    public void WriteBool_TrueAndFalse_WritesOneAndZero()
    {
        var writer = new ProtobufWriter();

        writer.WriteBool(1, true);
        writer.WriteBool(2, false);

        CollectionAssert.AreEqual(new byte[] { 0x08, 0x01, 0x10, 0x00 }, writer.ToArray());
    }

    [TestMethod]
    public void WriteSInt32_MinusOne_WritesZigZagOne()
    {
        var writer = new ProtobufWriter();

        writer.WriteSInt32(1, -1);
        writer.WriteSInt64(2, -2);

        CollectionAssert.AreEqual(new byte[] { 0x08, 0x01, 0x10, 0x03 }, writer.ToArray());
    }

    [TestMethod]
    public void WritePackedVarints_NoValues_WritesAnEmptyLengthDelimitedField()
    {
        var writer = new ProtobufWriter();

        writer.WritePackedVarints(4, []);

        CollectionAssert.AreEqual(new byte[] { 0x22, 0x00 }, writer.ToArray());
    }

    [TestMethod]
    public void Writers_NullArguments_ThrowArgumentNullException()
    {
        var writer = new ProtobufWriter();

        Assert.ThrowsExactly<ArgumentNullException>(() => writer.WriteString(1, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => writer.WriteMessage(1, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => writer.WritePackedVarints(1, null!));
    }
}
