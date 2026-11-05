using System.Text;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// AMQP 0-9-1's data types read (section 4.2.5), and every field-value type octet of RabbitMQ's
/// errata round-tripped in a field table, nested tables and arrays included.
/// </summary>
[TestClass]
public sealed class Amqp0WireReaderTests
{
    public static IEnumerable<object[]> EachFieldValueType =>
    [
        [Amqp0FieldValue.Boolean(true), new byte[] { (byte)'t', 1 }],
        [Amqp0FieldValue.Boolean(false), new byte[] { (byte)'t', 0 }],
        [Amqp0FieldValue.Int8(-2), new byte[] { (byte)'b', 0xFE }],
        [Amqp0FieldValue.UInt8(200), new byte[] { (byte)'B', 200 }],
        [Amqp0FieldValue.Int16(-2), new byte[] { (byte)'s', 0xFF, 0xFE }],
        [Amqp0FieldValue.UInt16(0xBEEF), new byte[] { (byte)'u', 0xBE, 0xEF }],
        [Amqp0FieldValue.Int32(-2), new byte[] { (byte)'I', 0xFF, 0xFF, 0xFF, 0xFE }],
        [Amqp0FieldValue.UInt32(0xDEADBEEF), new byte[] { (byte)'i', 0xDE, 0xAD, 0xBE, 0xEF }],
        [Amqp0FieldValue.Int64(-2), new byte[] { (byte)'l', 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFE }],
        [Amqp0FieldValue.Int64SpecificationOctet(258), new byte[] { (byte)'L', 0, 0, 0, 0, 0, 0, 1, 2 }],
        [Amqp0FieldValue.Float32(1.5f), new byte[] { (byte)'f', 0x3F, 0xC0, 0, 0 }],
        [Amqp0FieldValue.Float64(-2.0), new byte[] { (byte)'d', 0xC0, 0, 0, 0, 0, 0, 0, 0 }],
        [Amqp0FieldValue.Decimal(new Amqp0Decimal(2, 12345)), new byte[] { (byte)'D', 2, 0, 0, 0x30, 0x39 }],
        [Amqp0FieldValue.LongString("guest"), new byte[] { (byte)'S', 0, 0, 0, 5, (byte)'g', (byte)'u', (byte)'e', (byte)'s', (byte)'t' }],
        [Amqp0FieldValue.ByteArray([0x00, 0xFF]), new byte[] { (byte)'x', 0, 0, 0, 2, 0x00, 0xFF }],
        [Amqp0FieldValue.Array([Amqp0FieldValue.UInt8(1), Amqp0FieldValue.Void]), new byte[] { (byte)'A', 0, 0, 0, 3, (byte)'B', 1, (byte)'V' }],
        [Amqp0FieldValue.Timestamp(1_700_000_000), new byte[] { (byte)'T', 0, 0, 0, 0, 0x65, 0x53, 0xF1, 0x00 }],
        [Amqp0FieldValue.Table(new([new("k", Amqp0FieldValue.Boolean(true))])), new byte[] { (byte)'F', 0, 0, 0, 4, 1, (byte)'k', (byte)'t', 1 }],
        [Amqp0FieldValue.Void, new byte[] { (byte)'V' }],
    ];

    [TestMethod]
    [DynamicData(nameof(EachFieldValueType))]
    public void ReadFieldTable_EachFieldValueType_RoundTripsInATable(Amqp0FieldValue value, byte[] expectedValueBytes)
    {
        var table = new Amqp0FieldTable([new("v", value)]);
        var writer = new Amqp0WireWriter();
        writer.WriteFieldTable(table);
        var encoded = writer.ToArray();

        var read = new Amqp0WireReader(encoded).ReadFieldTable();

        CollectionAssert.AreEqual(Table("v", expectedValueBytes), encoded);
        Assert.HasCount(1, read.Fields);
        Assert.AreEqual("v", read.Fields[0].Name);
        Assert.AreEqual(value.Type, read.Fields[0].Value.Type);
        CollectionAssert.AreEqual(encoded, Encode(read));
    }

    [TestMethod]
    public void ReadFieldTable_ScalarValues_AreReadAsTheirTypes()
    {
        var read = Read(
            [new("t", Amqp0FieldValue.Boolean(true)), new("b", Amqp0FieldValue.Int8(-1)), new("B", Amqp0FieldValue.UInt8(255)),
             new("s", Amqp0FieldValue.Int16(-300)), new("u", Amqp0FieldValue.UInt16(60000)), new("I", Amqp0FieldValue.Int32(-70000)),
             new("i", Amqp0FieldValue.UInt32(4_000_000_000)), new("l", Amqp0FieldValue.Int64(long.MinValue)),
             new("L", Amqp0FieldValue.Int64SpecificationOctet(long.MaxValue)), new("f", Amqp0FieldValue.Float32(0.25f)),
             new("d", Amqp0FieldValue.Float64(double.Epsilon)), new("D", Amqp0FieldValue.Decimal(new(3, 1))),
             new("T", Amqp0FieldValue.Timestamp(ulong.MaxValue)), new("V", Amqp0FieldValue.Void)]);

        Assert.IsTrue((bool)read.Find("t")!.Value!);
        Assert.AreEqual((sbyte)-1, read.Find("b")!.Value);
        Assert.AreEqual((byte)255, read.Find("B")!.Value);
        Assert.AreEqual((short)-300, read.Find("s")!.Value);
        Assert.AreEqual((ushort)60000, read.Find("u")!.Value);
        Assert.AreEqual(-70000, read.Find("I")!.Value);
        Assert.AreEqual(4_000_000_000u, read.Find("i")!.Value);
        Assert.AreEqual(long.MinValue, read.Find("l")!.Value);
        Assert.AreEqual(long.MaxValue, read.Find("L")!.Value);
        Assert.AreEqual(0.25f, read.Find("f")!.Value);
        Assert.AreEqual(double.Epsilon, read.Find("d")!.Value);
        Assert.AreEqual(new Amqp0Decimal(3, 1), read.Find("D")!.Value);
        Assert.AreEqual(ulong.MaxValue, read.Find("T")!.Value);
        Assert.IsNull(read.Find("V")!.Value);
    }

    [TestMethod]
    public void ReadFieldTable_BooleanOctetOtherThanOne_IsTrue()
    {
        var read = new Amqp0WireReader(Table("t", [(byte)'t', 7])).ReadFieldTable();

        Assert.IsTrue((bool)read.Fields[0].Value.Value!);
    }

    [TestMethod]
    public void ReadFieldTable_NestedTablesAndArrays_RoundTrip()
    {
        var capabilities = new Amqp0FieldTable([new("basic.nack", Amqp0FieldValue.Boolean(true)), new("publisher_confirms", Amqp0FieldValue.Boolean(false))]);
        var inner = Amqp0FieldValue.Array([Amqp0FieldValue.Table(capabilities), Amqp0FieldValue.Array([Amqp0FieldValue.LongString("deep")]), Amqp0FieldValue.Array([])]);
        var table = new Amqp0FieldTable([new("capabilities", Amqp0FieldValue.Table(capabilities)), new("list", inner), new("empty", Amqp0FieldValue.Table(Amqp0FieldTable.Empty))]);

        var read = Read(table.Fields);

        var readCapabilities = (Amqp0FieldTable)read.Find("capabilities")!.Value!;
        Assert.IsTrue((bool)readCapabilities.Find("basic.nack")!.Value!);
        Assert.IsFalse((bool)readCapabilities.Find("publisher_confirms")!.Value!);
        var list = (IReadOnlyList<Amqp0FieldValue>)read.Find("list")!.Value!;
        Assert.HasCount(3, list);
        Assert.AreEqual(Amqp0FieldValueType.Table, list[0].Type);
        var deep = (IReadOnlyList<Amqp0FieldValue>)list[1].Value!;
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("deep"), (byte[])deep[0].Value!);
        Assert.IsEmpty((IReadOnlyList<Amqp0FieldValue>)list[2].Value!);
        Assert.IsEmpty(((Amqp0FieldTable)read.Find("empty")!.Value!).Fields);
        CollectionAssert.AreEqual(Encode(table), Encode(read));
    }

    [TestMethod]
    public void ReadFieldTable_NestedToTheLimit_IsRead()
    {
        var value = Amqp0FieldValue.Void;
        for (var level = 0; level < Amqp0WireReader.MaxNestingDepth; level++)
        {
            value = Amqp0FieldValue.Array([value]);
        }

        var read = Read([new("n", value)]);

        Assert.HasCount(1, read.Fields);
    }

    [TestMethod]
    public void ReadFieldTable_NestedPastTheLimit_IsASyntaxError()
    {
        var value = Amqp0FieldValue.Void;
        for (var level = 0; level <= Amqp0WireReader.MaxNestingDepth; level++)
        {
            value = Amqp0FieldValue.Table(new([new("n", value)]));
        }

        var encoded = Encode(new([new("n", value)]));

        Assert.ThrowsExactly<Amqp0SyntaxException>(() => new Amqp0WireReader(encoded).ReadFieldTable());
    }

    [TestMethod]
    [DataRow((byte)'U', DisplayName = "U, the PDF's signed 16-bit octet")]
    [DataRow((byte)'Z', DisplayName = "Z")]
    [DataRow((byte)0, DisplayName = "NUL")]
    public void ReadFieldTable_UnknownTypeOctet_IsASyntaxError(byte type)
    {
        Assert.ThrowsExactly<Amqp0SyntaxException>(() => new Amqp0WireReader(Table("v", [type, 0, 0])).ReadFieldTable());
    }

    [TestMethod]
    public void ReadFieldTable_SizePastThePayload_IsASyntaxError()
    {
        Assert.ThrowsExactly<Amqp0SyntaxException>(() => new Amqp0WireReader(new byte[] { 0, 0, 0, 9, 1, (byte)'v', (byte)'V' }).ReadFieldTable());
    }

    [TestMethod]
    public void ReadFieldTable_ValuePastTheTablesSize_IsASyntaxError()
    {
        // The table says 4 bytes; its Int32 value needs 4 more than it holds.
        byte[] bytes = [0, 0, 0, 4, 1, (byte)'v', (byte)'I', 0, 0, 0, 0, 0];

        Assert.ThrowsExactly<Amqp0SyntaxException>(() => new Amqp0WireReader(bytes).ReadFieldTable());
    }

    [TestMethod]
    public void ReadShortString_MalformedUtf8_IsASyntaxError()
    {
        var exception = Assert.ThrowsExactly<Amqp0SyntaxException>(() => new Amqp0WireReader(new byte[] { 1, 0xFF }).ReadShortString());

        Assert.IsInstanceOfType<DecoderFallbackException>(exception.InnerException);
    }

    [TestMethod]
    public void ReadShortString_Utf8_IsRead()
    {
        Assert.AreEqual("é", new Amqp0WireReader(new byte[] { 2, 0xC3, 0xA9 }).ReadShortString());
    }

    [TestMethod]
    public void ReadIntegers_AreBigEndian()
    {
        var reader = new Amqp0WireReader(new byte[] { 1, 0, 2, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 4, 9 });

        Assert.AreEqual((byte)1, reader.ReadOctet());
        Assert.AreEqual((ushort)2, reader.ReadShort());
        Assert.AreEqual(3u, reader.ReadLong());
        Assert.AreEqual(4UL, reader.ReadLongLong());
        Assert.AreEqual(1, reader.Remaining);
        CollectionAssert.AreEqual(new byte[] { 9 }, reader.ReadRemaining().ToArray());
        Assert.AreEqual(0, reader.Remaining);
        Assert.IsTrue(reader.ReadRemaining().IsEmpty);
    }

    [TestMethod]
    public void ReadLongString_IsReadWhole()
    {
        CollectionAssert.AreEqual(new byte[] { 0, 1 }, new Amqp0WireReader(new byte[] { 0, 0, 0, 2, 0, 1 }).ReadLongString());
    }

    [TestMethod]
    public void ReadLongString_LengthPastThePayload_IsASyntaxError()
    {
        Assert.ThrowsExactly<Amqp0SyntaxException>(() => new Amqp0WireReader(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0 }).ReadLongString());
    }

    [TestMethod]
    public void ReadBits_NineBits_ArePackedLowestBitFirstAcrossTwoOctets()
    {
        var bits = new Amqp0WireReader(new byte[] { 0b1000_0101, 0b0000_0001 }).ReadBits(9);

        CollectionAssert.AreEqual(new[] { true, false, true, false, false, false, false, true, true }, bits);
    }

    [TestMethod]
    public void ReadBits_ZeroBits_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Amqp0WireReader(new byte[] { 0 }).ReadBits(0));
    }

    [TestMethod]
    public void ReadOctet_PastTheEnd_IsASyntaxError()
    {
        Assert.ThrowsExactly<Amqp0SyntaxException>(() => new Amqp0WireReader(ReadOnlyMemory<byte>.Empty).ReadOctet());
    }

    private static Amqp0FieldTable Read(IReadOnlyList<Amqp0Field> fields) => new Amqp0WireReader(Encode(new(fields))).ReadFieldTable();

    private static byte[] Encode(Amqp0FieldTable table)
    {
        var writer = new Amqp0WireWriter();
        writer.WriteFieldTable(table);
        return writer.ToArray();
    }

    // A table of one field, laid out by hand: the size, the name as a short string, and the value.
    private static byte[] Table(string name, byte[] valueBytes)
    {
        var size = 1 + name.Length + valueBytes.Length;
        return [0, 0, (byte)(size >> 8), (byte)size, (byte)name.Length, .. Encoding.ASCII.GetBytes(name), .. valueBytes];
    }
}
