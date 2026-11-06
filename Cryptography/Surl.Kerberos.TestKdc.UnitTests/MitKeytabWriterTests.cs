namespace Surl.Kerberos.TestKdc;

[TestClass]
public sealed class MitKeytabWriterTests
{
    [TestMethod]
    public void Write_OneEntry_LaysOutEveryFieldBigEndian()
    {
        KerberosKeytabEntry entry = new(new KerberosPrincipalName("R", ["HTTP", "h"]), 3, KerberosEncryptionType.Aes128CtsHmacSha196, new byte[16]);

        byte[] keytab = MitKeytabWriter.Write([entry], 0x01020304);

        Assert.AreEqual(
            "0502" + "0000002f" + "0002" + "000152" + "000448545450" + "000168" + "00000002" + "01020304" + "03" + "0011" + "0010" + new string('0', 32) + "00000003",
            Convert.ToHexStringLower(keytab));
    }

    [TestMethod]
    public void Write_NoEntries_IsTheVersionAlone()
    {
        Assert.AreEqual("0502", Convert.ToHexStringLower(MitKeytabWriter.Write([], 0)));
    }

    [TestMethod]
    public void Write_Entries_AreReadBackByKerberosKeytabInOrder()
    {
        KerberosKeytabEntry[] entries =
        [
            new(new KerberosPrincipalName("SURL.TEST", ["smtp", "mail.surl.test"]), 1, KerberosEncryptionType.Aes256CtsHmacSha384192, Enumerable.Range(0, 32).Select(value => (byte)value).ToArray()),
            new(new KerberosPrincipalName("SURL.TEST", ["tester"]), 300, KerberosEncryptionType.Aes128CtsHmacSha256128, new byte[16]),
        ];

        KerberosKeytabReadResult result = KerberosKeytab.Read(MitKeytabWriter.Write(entries, 0));

        Assert.HasCount(2, result.Keytab!.Entries);
        for (int index = 0; index < entries.Length; index++)
        {
            Assert.AreEqual(entries[index].Principal, result.Keytab.Entries[index].Principal);
            Assert.AreEqual(entries[index].KeyVersionNumber, result.Keytab.Entries[index].KeyVersionNumber);
            Assert.AreEqual(entries[index].EncryptionType, result.Keytab.Entries[index].EncryptionType);
            CollectionAssert.AreEqual(entries[index].Key.ToArray(), result.Keytab.Entries[index].Key.ToArray());
        }
    }
}
