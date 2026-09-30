using System.Formats.Asn1;

namespace Surl.Kerberos;

/// <summary>
/// Pins the edges of <see cref="KerberosDer" /> that a well-formed AP-REQ never reaches: integers
/// outside their range, a <c>KerberosString</c> that is not a <c>GeneralString</c>, and flag bits
/// past the end of a short <c>BIT STRING</c>.
/// </summary>
[TestClass]
public sealed class KerberosDerTests
{
    [TestMethod]
    public void ReadInt32Field_IntegerPastInt32_Throws()
    {
        AsnReader fields = Fields(writer => WriteField(writer, 0, () => writer.WriteInteger(0x80000000L)));

        Assert.ThrowsExactly<AsnContentException>(() => KerberosDer.ReadInt32Field(fields, 0));
    }

    [TestMethod]
    public void ReadOptionalUInt32Field_IntegerPastInt64_Throws()
    {
        AsnReader fields = Fields(writer => WriteField(writer, 7, () => writer.WriteInteger(System.Numerics.BigInteger.Pow(2, 70))));

        Assert.ThrowsExactly<AsnContentException>(() => KerberosDer.ReadOptionalUInt32Field(fields, 7));
    }

    [TestMethod]
    public void ReadOptionalUInt32Field_Absent_IsNull()
    {
        AsnReader fields = Fields(writer => WriteField(writer, 8, () => writer.WriteInteger(1)));

        Assert.IsNull(KerberosDer.ReadOptionalUInt32Field(fields, 7));
    }

    [TestMethod]
    public void ReadStringField_Utf8String_Throws()
    {
        AsnReader fields = Fields(writer => WriteField(writer, 1, () => writer.WriteCharacterString(UniversalTagNumber.UTF8String, "EXAMPLE.COM")));

        Assert.ThrowsExactly<AsnContentException>(() => KerberosDer.ReadStringField(fields, 1));
    }

    [TestMethod]
    public void HasField_NoMoreFields_IsFalse()
    {
        AsnReader fields = Fields(_ => { });

        Assert.IsFalse(KerberosDer.HasField(fields, 0));
    }

    [TestMethod]
    [DataRow(new byte[] { 0x20 }, 2, true, DisplayName = "bit 2 set")]
    [DataRow(new byte[] { 0x20 }, 1, false, DisplayName = "bit 1 clear")]
    [DataRow(new byte[] { 0xFF }, 8, false, DisplayName = "bit past the end")]
    [DataRow(new byte[] { 0x00, 0x01 }, 15, true, DisplayName = "last bit of the second byte")]
    public void IsFlagSet_Bit_ReadsFromTheMostSignificantBitOfTheFirstByte(byte[] flags, int bit, bool expected)
    {
        Assert.AreEqual(expected, KerberosDer.IsFlagSet(flags, bit));
    }

    private static AsnReader Fields(Action<AsnWriter> write)
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            write(writer);
        }

        return new AsnReader(writer.Encode(), AsnEncodingRules.DER).ReadSequence();
    }

    private static void WriteField(AsnWriter writer, int number, Action write)
    {
        using (writer.PushSequence(KerberosDer.ContextTag(number)))
        {
            write();
        }
    }
}
