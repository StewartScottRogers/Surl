using System.Text;

namespace Surl.Kerberos.TestKdc;

[TestClass]
public sealed class KerberosStringToKeyTests
{
    private static readonly byte[] Password = Encoding.UTF8.GetBytes("password");

    // RFC 3962 appendix B, pass phrase "password", salt "ATHENA.MIT.EDUraeburn".
    [TestMethod]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha196, 1, "42263c6e89f4fc28b8df68ee09799f15")]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha196, 1, "fe697b52bc0d3ce14432ba036a92e65bbb52280990a2fa27883998d72af30161")]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha196, 1200, "4c01cd46d632d01e6dbe230a01ed642a")]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha196, 1200, "55a6ac740ad17b4846941051e1e8b0a7548d93b0ab30a8bc3ff16280382b8c2a")]
    public void DeriveKey_Rfc3962Vectors_GiveThePublishedKeys(KerberosEncryptionType encryptionType, int iterationCount, string expectedKey)
    {
        byte[] key = KerberosStringToKey.DeriveKey(encryptionType, Password, Encoding.UTF8.GetBytes("ATHENA.MIT.EDUraeburn"), iterationCount);

        Assert.AreEqual(expectedKey, Convert.ToHexStringLower(key));
    }

    // RFC 8009 appendix A, pass phrase "password", 32768 iterations, salt 10DF9DD783E5BC8ACEA1730E74355F61 "ATHENA.MIT.EDUraeburn".
    [TestMethod]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha256128, "089bca48b105ea6ea77ca5d2f39dc5e7")]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha384192, "45bd806dbf6a833a9cffc1c94589a222367a79bc21c413718906e9f578a78467")]
    public void DeriveKey_Rfc8009Vectors_GiveThePublishedBaseKeys(KerberosEncryptionType encryptionType, string expectedKey)
    {
        byte[] salt = [.. Convert.FromHexString("10DF9DD783E5BC8ACEA1730E74355F61"), .. Encoding.ASCII.GetBytes("ATHENA.MIT.EDUraeburn")];

        byte[] key = KerberosStringToKey.DeriveKey(encryptionType, Password, salt, KerberosStringToKey.Rfc8009DefaultIterationCount);

        Assert.AreEqual(expectedKey, Convert.ToHexStringLower(key));
    }

    [TestMethod]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha196, KerberosStringToKey.Rfc3962DefaultIterationCount)]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha196, KerberosStringToKey.Rfc3962DefaultIterationCount)]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha256128, KerberosStringToKey.Rfc8009DefaultIterationCount)]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha384192, KerberosStringToKey.Rfc8009DefaultIterationCount)]
    public void DeriveKey_WithoutAnIterationCount_UsesTheEnctypesDefault(KerberosEncryptionType encryptionType, int defaultIterationCount)
    {
        byte[] key = KerberosStringToKey.DeriveKey(encryptionType, "password", "SURL.TESTtester");

        CollectionAssert.AreEqual(
            KerberosStringToKey.DeriveKey(encryptionType, Password, Encoding.UTF8.GetBytes("SURL.TESTtester"), defaultIterationCount),
            key);
    }

    [TestMethod]
    public void DefaultSalt_IsTheRealmThenEveryComponent()
    {
        Assert.AreEqual("SURL.TESThostweb.surl.test", KerberosStringToKey.DefaultSalt(new KerberosPrincipalName("SURL.TEST", ["host", "web.surl.test"])));
    }
}
