using System.Formats.Asn1;

namespace Surl.Kerberos.TestKdc;

[TestClass]
public sealed class KerberosKdcMessagesTests
{
    private static readonly KerberosPrincipalName Tester = new("SURL.TEST", ["tester"]);

    [TestMethod]
    public void WriteError_WithoutAClientOrData_LeavesCrealmCnameAndEdataOut()
    {
        DateTimeOffset now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

        KdcError error = KdcError.Read(KerberosKdcMessages.WriteError(
            now.AddTicks(1234567), new KerberosKdcRefusalException(KerberosErrorCode.Generic, "malformed request"), null, KerberosTestKdc.TicketGrantingPrincipal));

        Assert.AreEqual(KerberosErrorCode.Generic, error.ErrorCode);
        Assert.AreEqual("malformed request", error.Text);
        Assert.IsNull(error.Client);
        Assert.IsNull(error.ErrorData);
        Assert.AreEqual(KerberosTestKdc.TicketGrantingPrincipal, error.Server);
    }

    [TestMethod]
    public void WriteError_SusecIsTheMicrosecondsOfTheKdcsTime()
    {
        DateTimeOffset now = new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero).AddTicks(1234567);

        byte[] bytes = KerberosKdcMessages.WriteError(now, new KerberosKdcRefusalException(KerberosErrorCode.Generic, "x", [0x30, 0x00]), Tester, KerberosTestKdc.TicketGrantingPrincipal);

        AsnReader fields = KerberosDer.ReadApplicationSequence(bytes, 30);
        KerberosDer.ReadInt32Field(fields, 0);
        KerberosDer.ReadInt32Field(fields, 1);
        Assert.AreEqual(now.AddTicks(-1234567), KerberosDer.ReadTimeField(fields, 4));
        Assert.AreEqual(123456, KerberosDer.ReadInt32Field(fields, 5));
        CollectionAssert.AreEqual(new byte[] { 0x30, 0x00 }, KdcError.Read(bytes).ErrorData);
    }

    [TestMethod]
    public void WriteEncTicketPart_IsReadBackBySurlKerberos()
    {
        DateTimeOffset now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
        KerberosIssuedTicket ticket = new(
            KerberosDerWriter.Flag(10), KerberosEncryptionType.Aes128CtsHmacSha196, new byte[16], Tester, KerberosTestKdc.TicketGrantingPrincipal, now, now.AddMinutes(1), now.AddHours(1));

        KerberosTicketPart part = KerberosTicketPart.Read(KerberosKdcMessages.WriteEncTicketPart(ticket));

        Assert.IsFalse(part.IsInvalid);
        Assert.AreEqual(Tester, part.Client);
        Assert.AreEqual(17, part.SessionKey.KeyTypeNumber);
        Assert.AreEqual(now, part.AuthTime);
        Assert.AreEqual(now.AddMinutes(1), part.StartTime);
        Assert.AreEqual(now.AddHours(1), part.EndTime);
    }

    [TestMethod]
    public void WriteEncryptionTypeInfo2_WritesOneEntryPerEnctypeWithTheSaltAndNoS2kParams()
    {
        byte[] bytes = KerberosKdcMessages.WriteEncryptionTypeInfo2([KerberosEncryptionType.Aes256CtsHmacSha384192, KerberosEncryptionType.Aes128CtsHmacSha196], "SURL.TESTtester");

        string salt = Convert.ToHexStringLower("SURL.TESTtester"u8.ToArray());

        // SEQUENCE { SEQUENCE { [0] 20, [1] GeneralString }, SEQUENCE { [0] 17, [1] GeneralString } }
        Assert.AreEqual("3034" + "3018a003020114a1111b0f" + salt + "3018a003020111a1111b0f" + salt, Convert.ToHexStringLower(bytes));
    }
}
