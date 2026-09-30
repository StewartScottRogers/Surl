using System.Formats.Asn1;

namespace Surl.Kerberos.TestKdc;

[TestClass]
public sealed class KerberosDerWriterTests
{
    [TestMethod]
    [DataRow(1, "1b01")]
    [DataRow(127, "1b7f")]
    [DataRow(128, "1b8180")]
    [DataRow(300, "1b82012c")]
    public void WriteGeneralString_EncodesTheLengthInDer(int length, string expectedHeaderHex)
    {
        AsnWriter writer = KerberosDerWriter.NewWriter();

        KerberosDerWriter.WriteGeneralString(writer, new string('a', length));

        byte[] encoded = writer.Encode();
        Assert.AreEqual(expectedHeaderHex, Convert.ToHexStringLower(encoded[..(expectedHeaderHex.Length / 2)]));
        Assert.HasCount((expectedHeaderHex.Length / 2) + length, encoded);
    }

    [TestMethod]
    public void WriteStringField_IsReadBackByKerberosDer()
    {
        AsnWriter writer = KerberosDerWriter.NewWriter();
        using (writer.PushSequence())
        {
            KerberosDerWriter.WriteStringField(writer, 1, "SURL.TEST");
        }

        AsnReader fields = new AsnReader(writer.Encode(), KerberosDer.Rules).ReadSequence();

        Assert.AreEqual("SURL.TEST", KerberosDer.ReadStringField(fields, 1));
    }

    [TestMethod]
    public void WriteTimeField_DropsFractionalSecondsAndWritesUtc()
    {
        AsnWriter writer = KerberosDerWriter.NewWriter();
        using (writer.PushSequence())
        {
            KerberosDerWriter.WriteTimeField(writer, 0, new DateTimeOffset(2026, 9, 30, 14, 0, 0, 999, TimeSpan.FromHours(2)));
        }

        byte[] encoded = writer.Encode();

        // SEQUENCE { [0] GeneralizedTime "20260930120000Z" }
        Assert.AreEqual("3013a011180f" + Convert.ToHexStringLower("20260930120000Z"u8.ToArray()), Convert.ToHexStringLower(encoded));
        AsnReader fields = new AsnReader(encoded, KerberosDer.Rules).ReadSequence();
        Assert.AreEqual(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero), KerberosDer.ReadTimeField(fields, 0));
    }

    [TestMethod]
    [DataRow(0, 0x80000000u)]
    [DataRow(9, 0x00400000u)]
    [DataRow(31, 0x00000001u)]
    public void Flag_IsBitNCountedFromTheMostSignificant(int bit, uint expected)
    {
        Assert.AreEqual(expected, KerberosDerWriter.Flag(bit));
    }

    [TestMethod]
    public void WriteFlagsField_IsReadBackBitForBit()
    {
        AsnWriter writer = KerberosDerWriter.NewWriter();
        using (writer.PushSequence())
        {
            KerberosDerWriter.WriteFlagsField(writer, 0, KerberosDerWriter.Flag(1) | KerberosDerWriter.Flag(10));
        }

        byte[] flags = KerberosDer.ReadFlagsField(new AsnReader(writer.Encode(), KerberosDer.Rules).ReadSequence(), 0);

        CollectionAssert.AreEqual(new byte[] { 0x40, 0x20, 0x00, 0x00 }, flags);
    }

    [TestMethod]
    [DataRow(new[] { "tester" }, 1)]
    [DataRow(new[] { "HTTP", "web.surl.test" }, 2)]
    public void WritePrincipalNameField_WritesNtPrincipalForOneComponentAndNtSrvInstForMore(string[] components, int expectedNameType)
    {
        AsnWriter writer = KerberosDerWriter.NewWriter();
        using (writer.PushSequence())
        {
            KerberosDerWriter.WritePrincipalNameField(writer, 3, new KerberosPrincipalName("SURL.TEST", components));
        }

        AsnReader field = KerberosDer.ReadField(new AsnReader(writer.Encode(), KerberosDer.Rules).ReadSequence(), 3);
        AsnReader principalName = field.ReadSequence();

        Assert.AreEqual(expectedNameType, KerberosDer.ReadInt32Field(principalName, 0));
    }

    [TestMethod]
    public void WriteEncryptedDataField_WithoutAKeyVersion_LeavesKvnoOut()
    {
        AsnWriter writer = KerberosDerWriter.NewWriter();
        using (writer.PushSequence())
        {
            KerberosDerWriter.WriteEncryptedDataField(writer, 6, KerberosEncryptionType.Aes128CtsHmacSha196, null, [0xAA]);
        }

        KerberosEncryptedData encryptedData = KerberosEncryptedData.ReadField(new AsnReader(writer.Encode(), KerberosDer.Rules).ReadSequence(), 6);

        Assert.AreEqual(17, encryptedData.EncryptionTypeNumber);
        Assert.IsNull(encryptedData.KeyVersionNumber);
        CollectionAssert.AreEqual(new byte[] { 0xAA }, encryptedData.CipherText);
    }
}
