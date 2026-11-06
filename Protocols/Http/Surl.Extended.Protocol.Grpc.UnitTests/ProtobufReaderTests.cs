namespace Surl.Extended.Protocol.Grpc;

[TestClass]
public sealed class ProtobufReaderTests
{
    [TestMethod]
    public void TryReadField_EmptyMessage_ReturnsFalse()
    {
        var reader = new ProtobufReader(ReadOnlyMemory<byte>.Empty);

        Assert.IsTrue(reader.IsAtEnd);
        Assert.IsFalse(reader.TryReadField(out var field));
        Assert.AreEqual(default, field);
    }

    [TestMethod]
    public void TryReadField_Fixed64AndFixed32_ReadsThemLittleEndian()
    {
        var writer = new ProtobufWriter();
        writer.WriteFixed64(1, 0x0102030405060708);
        writer.WriteFixed32(2, 0x0A0B0C0D);
        var bytes = writer.ToArray();
        var reader = new ProtobufReader(bytes);

        CollectionAssert.AreEqual(
            new byte[] { 0x09, 0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x15, 0x0D, 0x0C, 0x0B, 0x0A },
            bytes);
        Assert.IsTrue(reader.TryReadField(out var fixed64));
        Assert.AreEqual(0x0102030405060708UL, fixed64.ReadFixed64());
        Assert.IsTrue(reader.TryReadField(out var fixed32));
        Assert.AreEqual(0x0A0B0C0DU, fixed32.ReadFixed32());
    }

    [TestMethod]
    public void TryReadField_EveryVarintType_ReadsWhatWasWritten()
    {
        var writer = new ProtobufWriter();
        writer.WriteInt32(1, -5);
        writer.WriteInt64(2, long.MinValue);
        writer.WriteSInt32(3, -7);
        writer.WriteSInt64(4, -9);
        writer.WriteBool(5, true);
        writer.WriteBool(6, false);
        writer.WriteUInt64(7, ulong.MaxValue);
        writer.WriteBytes(8, [1, 2]);
        var reader = new ProtobufReader(writer.ToArray());

        Assert.AreEqual(-5, Next(reader).ReadInt32());
        Assert.AreEqual(long.MinValue, Next(reader).ReadInt64());
        Assert.AreEqual(-7, Next(reader).ReadSInt32());
        Assert.AreEqual(-9L, Next(reader).ReadSInt64());
        Assert.IsTrue(Next(reader).ReadBool());
        Assert.IsFalse(Next(reader).ReadBool());
        Assert.AreEqual(ulong.MaxValue, Next(reader).ReadUInt64());
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, Next(reader).ReadBytes().ToArray());
        Assert.IsTrue(reader.IsAtEnd);
    }

    [TestMethod]
    public void TryReadField_UnknownFields_AreKeptByWritingThemBack()
    {
        var original = new ProtobufWriter();
        original.WriteString(1, "known");
        original.WriteUInt64(7, 300);
        original.WriteFixed32(8, 1);
        original.WriteFixed64(9, 2);
        original.WriteBytes(10, [0xFF]);
        byte[] group = [0x5B, 0x08, 0x01, 0x5C];
        var bytes = original.ToArray().Concat(group).ToArray();
        var reader = new ProtobufReader(bytes);
        var copy = new ProtobufWriter();

        while (reader.TryReadField(out var field))
        {
            copy.WriteEncodedField(field);
        }

        CollectionAssert.AreEqual(bytes, copy.ToArray());
    }

    [TestMethod]
    public void TryReadField_Group_IsReadToItsEndWithItsFieldsAsContent()
    {
        byte[] bytes = [0x5B, 0x08, 0x01, 0x63, 0x64, 0x5C, 0x10, 0x02];
        var reader = new ProtobufReader(bytes);

        Assert.IsTrue(reader.TryReadField(out var group));
        Assert.AreEqual(11, group.FieldNumber);
        Assert.AreEqual(ProtobufWireType.StartGroup, group.WireType);
        CollectionAssert.AreEqual(new byte[] { 0x08, 0x01, 0x63, 0x64 }, group.Content.ToArray());
        Assert.IsTrue(reader.TryReadField(out var after));
        Assert.AreEqual(2, after.ReadInt32());
    }

    [TestMethod]
    public void TryReadField_HighestFieldNumber_IsRead()
    {
        var writer = new ProtobufWriter();
        writer.WriteBool(ProtobufReader.MaximumFieldNumber, true);
        var reader = new ProtobufReader(writer.ToArray());

        Assert.AreEqual(ProtobufReader.MaximumFieldNumber, Next(reader).FieldNumber);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x08 }, DisplayName = "varint value missing")]
    [DataRow(new byte[] { 0x08, 0x96 }, DisplayName = "varint value truncated")]
    [DataRow(new byte[] { 0x88 }, DisplayName = "tag truncated")]
    [DataRow(new byte[] { 0x08, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x01 }, DisplayName = "varint past 10 bytes")]
    [DataRow(new byte[] { 0x00, 0x01 }, DisplayName = "field number 0")]
    [DataRow(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x10, 0x01 }, DisplayName = "field number 2^29")]
    [DataRow(new byte[] { 0x0E }, DisplayName = "wire type 6")]
    [DataRow(new byte[] { 0x0F }, DisplayName = "wire type 7")]
    [DataRow(new byte[] { 0x12, 0x05, 0x61 }, DisplayName = "length past the end")]
    [DataRow(new byte[] { 0x12 }, DisplayName = "length missing")]
    [DataRow(new byte[] { 0x09, 0x01, 0x02 }, DisplayName = "fixed64 truncated")]
    [DataRow(new byte[] { 0x0D, 0x01 }, DisplayName = "fixed32 truncated")]
    [DataRow(new byte[] { 0x0C }, DisplayName = "end group never started")]
    [DataRow(new byte[] { 0x0B, 0x08, 0x01 }, DisplayName = "group never ended")]
    [DataRow(new byte[] { 0x0B, 0x14 }, DisplayName = "group ended by another field")]
    public void TryReadField_BytesThatAreNotAField_ThrowsProtobufFormatException(byte[] bytes)
    {
        var reader = new ProtobufReader(bytes);

        Assert.ThrowsExactly<ProtobufFormatException>(() => reader.TryReadField(out _));
    }

    [TestMethod]
    public void TryReadField_GroupsNestedDeeperThanTheLimit_ThrowsProtobufFormatException()
    {
        var depth = ProtobufReader.MaximumGroupDepth + 1;
        var bytes = Enumerable.Repeat((byte)0x0B, depth).Concat(Enumerable.Repeat((byte)0x0C, depth)).ToArray();
        var reader = new ProtobufReader(bytes);

        Assert.ThrowsExactly<ProtobufFormatException>(() => reader.TryReadField(out _));
    }

    [TestMethod]
    public void TryReadField_GroupsNestedToTheLimit_AreRead()
    {
        var depth = ProtobufReader.MaximumGroupDepth;
        var bytes = Enumerable.Repeat((byte)0x0B, depth).Concat(Enumerable.Repeat((byte)0x0C, depth)).ToArray();
        var reader = new ProtobufReader(bytes);

        Assert.IsTrue(reader.TryReadField(out var group));
        Assert.AreEqual(bytes.Length, group.Encoded.Length);
    }

    [TestMethod]
    public void ReadString_BytesThatAreNotUtf8_ThrowsProtobufFormatException()
    {
        var reader = new ProtobufReader(new byte[] { 0x12, 0x02, 0xC3, 0x28 });
        var field = Next(reader);

        var thrown = Assert.ThrowsExactly<ProtobufFormatException>(() => field.ReadString());
        Assert.IsNotNull(thrown.InnerException);
    }

    [TestMethod]
    public void ReadAccessors_KnownFieldWithTheWrongWireType_ThrowProtobufFormatException()
    {
        var varint = Next(new ProtobufReader(new byte[] { 0x08, 0x01 }));
        var text = Next(new ProtobufReader(new byte[] { 0x12, 0x00 }));

        Assert.ThrowsExactly<ProtobufFormatException>(() => text.ReadUInt64());
        Assert.ThrowsExactly<ProtobufFormatException>(() => varint.ReadString());
        Assert.ThrowsExactly<ProtobufFormatException>(() => varint.ReadMessage());
        Assert.ThrowsExactly<ProtobufFormatException>(() => varint.ReadFixed32());
        Assert.ThrowsExactly<ProtobufFormatException>(() => varint.ReadFixed64());
        Assert.ThrowsExactly<ProtobufFormatException>(() => Next(new ProtobufReader(new byte[] { 0x0D, 0, 0, 0, 0 })).ReadRepeatedVarints());
    }

    [TestMethod]
    public void ReadRepeatedVarints_UnpackedField_ReadsItsOneValue()
    {
        var field = Next(new ProtobufReader(new byte[] { 0x30, 0x03 }));

        CollectionAssert.AreEqual(new ulong[] { 3 }, field.ReadRepeatedVarints().ToArray());
    }

    [TestMethod]
    public void ReadRepeatedVarints_PackedValueTruncated_ThrowsProtobufFormatException()
    {
        var field = Next(new ProtobufReader(new byte[] { 0x32, 0x02, 0x03, 0x8E }));

        Assert.ThrowsExactly<ProtobufFormatException>(() => field.ReadRepeatedVarints());
    }

    private static ProtobufField Next(ProtobufReader reader)
    {
        Assert.IsTrue(reader.TryReadField(out var field));
        return field;
    }
}
