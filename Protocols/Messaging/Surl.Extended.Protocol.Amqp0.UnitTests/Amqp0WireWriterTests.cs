namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// AMQP 0-9-1's data types written (section 4.2.5).
/// </summary>
[TestClass]
public sealed class Amqp0WireWriterTests
{
    [TestMethod]
    public void WriteIntegers_AreBigEndian()
    {
        var writer = new Amqp0WireWriter();

        writer.WriteOctet(1);
        writer.WriteShort(0x0203);
        writer.WriteLong(0x04050607);
        writer.WriteLongLong(0x08090A0B0C0D0E0F);

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 }, writer.ToArray());
    }

    [TestMethod]
    public void WriteBits_NineBits_ArePackedLowestBitFirstAcrossTwoOctets()
    {
        var writer = new Amqp0WireWriter();

        writer.WriteBits([true, false, true, false, false, false, false, true, true]);

        CollectionAssert.AreEqual(new byte[] { 0b1000_0101, 0b0000_0001 }, writer.ToArray());
    }

    [TestMethod]
    public void WriteBits_None_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new Amqp0WireWriter().WriteBits([]));
    }

    [TestMethod]
    public void WriteShortString_IsLengthAndUtf8()
    {
        var writer = new Amqp0WireWriter();

        writer.WriteShortString("é");

        CollectionAssert.AreEqual(new byte[] { 2, 0xC3, 0xA9 }, writer.ToArray());
    }

    [TestMethod]
    public void WriteShortString_255Bytes_IsWritten()
    {
        var writer = new Amqp0WireWriter();

        writer.WriteShortString(new string('a', 255));

        Assert.HasCount(256, writer.ToArray());
    }

    [TestMethod]
    public void WriteShortString_256Bytes_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new Amqp0WireWriter().WriteShortString(new string('a', 256)));
    }

    [TestMethod]
    public void WriteShortString_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new Amqp0WireWriter().WriteShortString(null!));
    }

    [TestMethod]
    public void WriteLongString_IsLengthAndOctets()
    {
        var writer = new Amqp0WireWriter();

        writer.WriteLongString([0xAB]);

        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 1, 0xAB }, writer.ToArray());
    }

    [TestMethod]
    public void WriteFieldTable_Empty_IsAZeroSize()
    {
        var writer = new Amqp0WireWriter();

        writer.WriteFieldTable(Amqp0FieldTable.Empty);

        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0 }, writer.ToArray());
    }

    [TestMethod]
    public void WriteFieldArray_IsSizeAndValues()
    {
        var writer = new Amqp0WireWriter();

        writer.WriteFieldArray([Amqp0FieldValue.Boolean(true)]);

        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 2, (byte)'t', 1 }, writer.ToArray());
    }

    [TestMethod]
    public void WriteNulls_Throw()
    {
        var writer = new Amqp0WireWriter();

        Assert.ThrowsExactly<ArgumentNullException>(() => writer.WriteFieldTable(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => writer.WriteFieldArray(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => writer.WriteFieldValue(null!));
    }
}
