using System.Text;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// Field values made by their factories, each under its errata type octet.
/// </summary>
[TestClass]
public sealed class Amqp0FieldValueTests
{
    [TestMethod]
    public void LongString_Text_IsItsUtf8()
    {
        var value = Amqp0FieldValue.LongString("é");

        Assert.AreEqual(Amqp0FieldValueType.LongString, value.Type);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("é"), (byte[])value.Value!);
    }

    [TestMethod]
    public void LongString_Octets_AreCopied()
    {
        byte[] octets = [1, 2];

        var value = Amqp0FieldValue.LongString(octets.AsSpan());
        octets[0] = 9;

        CollectionAssert.AreEqual(new byte[] { 1, 2 }, (byte[])value.Value!);
    }

    [TestMethod]
    public void TypeOctets_AreTheErrataOctets()
    {
        Assert.AreEqual("tbBsuIiLlfdDSxATFV", new string([.. new[]
        {
            Amqp0FieldValueType.Boolean, Amqp0FieldValueType.Int8, Amqp0FieldValueType.UInt8, Amqp0FieldValueType.Int16,
            Amqp0FieldValueType.UInt16, Amqp0FieldValueType.Int32, Amqp0FieldValueType.UInt32, Amqp0FieldValueType.Int64SpecificationOctet,
            Amqp0FieldValueType.Int64, Amqp0FieldValueType.Float32, Amqp0FieldValueType.Float64, Amqp0FieldValueType.Decimal,
            Amqp0FieldValueType.LongString, Amqp0FieldValueType.ByteArray, Amqp0FieldValueType.Array, Amqp0FieldValueType.Timestamp,
            Amqp0FieldValueType.Table, Amqp0FieldValueType.Void,
        }.Select(type => (char)type)]));
    }

    [TestMethod]
    public void Factories_Null_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Amqp0FieldValue.LongString((string)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Amqp0FieldValue.Array(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Amqp0FieldValue.Table(null!));
    }
}
