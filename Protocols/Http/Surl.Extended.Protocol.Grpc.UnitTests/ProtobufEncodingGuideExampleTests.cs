namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// The worked examples of the protobuf encoding guide (https://protobuf.dev/programming-guides/encoding/),
/// each encoded and decoded byte for byte. The bytes are the guide's own.
/// </summary>
[TestClass]
public sealed class ProtobufEncodingGuideExampleTests
{
    private static readonly byte[] Field1Is150 = [0x08, 0x96, 0x01];
    private static readonly byte[] Field2IsTesting = [0x12, 0x07, 0x74, 0x65, 0x73, 0x74, 0x69, 0x6E, 0x67];
    private static readonly byte[] Field3IsEmbedded150 = [0x1A, 0x03, 0x08, 0x96, 0x01];
    private static readonly byte[] Field6IsPacked = [0x32, 0x06, 0x03, 0x8E, 0x02, 0x9E, 0xA7, 0x05];

    [TestMethod]
    public void WriteUInt32_Field1Is150_WritesTheGuidesSimpleMessage()
    {
        var writer = new ProtobufWriter();

        writer.WriteUInt32(1, 150);

        CollectionAssert.AreEqual(Field1Is150, writer.ToArray());
    }

    [TestMethod]
    public void TryReadField_TheGuidesSimpleMessage_ReadsField1As150()
    {
        var reader = new ProtobufReader(Field1Is150);

        Assert.IsTrue(reader.TryReadField(out var field));
        Assert.AreEqual(1, field.FieldNumber);
        Assert.AreEqual(ProtobufWireType.Varint, field.WireType);
        Assert.AreEqual(150, field.ReadInt32());
        Assert.IsFalse(reader.TryReadField(out _));
    }

    [TestMethod]
    public void WriteString_Field2IsTesting_WritesTheGuidesStringExample()
    {
        var writer = new ProtobufWriter();

        writer.WriteString(2, "testing");

        CollectionAssert.AreEqual(Field2IsTesting, writer.ToArray());
    }

    [TestMethod]
    public void TryReadField_TheGuidesStringExample_ReadsField2AsTesting()
    {
        var reader = new ProtobufReader(Field2IsTesting);

        Assert.IsTrue(reader.TryReadField(out var field));
        Assert.AreEqual(2, field.FieldNumber);
        Assert.AreEqual("testing", field.ReadString());
        Assert.IsTrue(reader.IsAtEnd);
    }

    [TestMethod]
    public void WriteMessage_Field3IsTest1With150_WritesTheGuidesEmbeddedMessageExample()
    {
        var embedded = new ProtobufWriter();
        embedded.WriteUInt32(1, 150);
        var writer = new ProtobufWriter();

        writer.WriteMessage(3, embedded);

        CollectionAssert.AreEqual(Field3IsEmbedded150, writer.ToArray());
    }

    [TestMethod]
    public void TryReadField_TheGuidesEmbeddedMessageExample_ReadsField3AsTest1With150()
    {
        var reader = new ProtobufReader(Field3IsEmbedded150);

        Assert.IsTrue(reader.TryReadField(out var field));
        Assert.AreEqual(3, field.FieldNumber);
        var embedded = field.ReadMessage();
        Assert.IsTrue(embedded.TryReadField(out var inner));
        Assert.AreEqual(1, inner.FieldNumber);
        Assert.AreEqual(150U, inner.ReadUInt32());
        Assert.IsFalse(embedded.TryReadField(out _));
    }

    [TestMethod]
    public void WritePackedVarints_Field6Is3And270And86942_WritesTheGuidesPackedExample()
    {
        var writer = new ProtobufWriter();

        writer.WritePackedVarints(6, [3, 270, 86942]);

        CollectionAssert.AreEqual(Field6IsPacked, writer.ToArray());
    }

    [TestMethod]
    public void TryReadField_TheGuidesPackedExample_ReadsField6As3And270And86942()
    {
        var reader = new ProtobufReader(Field6IsPacked);

        Assert.IsTrue(reader.TryReadField(out var field));
        Assert.AreEqual(6, field.FieldNumber);
        CollectionAssert.AreEqual(new ulong[] { 3, 270, 86942 }, field.ReadRepeatedVarints().ToArray());
    }

    [TestMethod]
    public void WriteInt32_MinusTwo_WritesTheGuidesTenByteSignExtension()
    {
        var writer = new ProtobufWriter();

        writer.WriteInt32(1, -2);

        CollectionAssert.AreEqual(
            new byte[] { 0x08, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 },
            writer.ToArray());
    }

    [TestMethod]
    [DataRow(0, 0U)]
    [DataRow(-1, 1U)]
    [DataRow(1, 2U)]
    [DataRow(-2, 3U)]
    [DataRow(0x7FFFFFFF, 0xFFFFFFFEU)]
    [DataRow(-0x80000000, 0xFFFFFFFFU)]
    public void ProtobufZigZag_TheGuidesTable_Encodes32AndDecodesBack(int signed, uint encoded)
    {
        Assert.AreEqual(encoded, ProtobufZigZag.Encode32(signed));
        Assert.AreEqual(signed, ProtobufZigZag.Decode32(encoded));
    }

    [TestMethod]
    [DataRow(0L, 0UL)]
    [DataRow(-1L, 1UL)]
    [DataRow(1L, 2UL)]
    [DataRow(-2L, 3UL)]
    [DataRow(long.MaxValue, ulong.MaxValue - 1)]
    [DataRow(long.MinValue, ulong.MaxValue)]
    public void ProtobufZigZag_TheGuidesTable_Encodes64AndDecodesBack(long signed, ulong encoded)
    {
        Assert.AreEqual(encoded, ProtobufZigZag.Encode64(signed));
        Assert.AreEqual(signed, ProtobufZigZag.Decode64(encoded));
    }
}
