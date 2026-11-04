using System.Text;

namespace Surl.Cryptography;

/// <summary>
/// Pins <see cref="Md4" /> to hashes published in RFC 1320 and [MS-NLMP], never to hashes
/// the code under test computed.
/// </summary>
[TestClass]
public sealed class Md4Tests
{
    // RFC 1320, appendix A.5, "Test suite".
    [TestMethod]
    [DataRow("", "31d6cfe0d16ae931b73c59d7e0c089c0", DisplayName = "empty message")]
    [DataRow("a", "bde52cb31de33e46245e05fbdbd6fb24", DisplayName = "a")]
    [DataRow("abc", "a448017aaf21d8525fc10ae87aa6729d", DisplayName = "abc")]
    [DataRow("message digest", "d9130a8164549fe818874806e1c7014b", DisplayName = "message digest")]
    [DataRow("abcdefghijklmnopqrstuvwxyz", "d79e1c308aa5bbcdeea8ed63df412da9", DisplayName = "the alphabet")]
    [DataRow(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789",
        "043f8582f241db351ce627e153e7f0e4",
        DisplayName = "the alphanumerics: 62 bytes, padding needs a second block")]
    [DataRow(
        "12345678901234567890123456789012345678901234567890123456789012345678901234567890",
        "e33b4ddc9c38f2199c3e7b164fcc0536",
        DisplayName = "80 digits: one whole block and a 16-byte tail")]
    public void HashData_Rfc1320TestSuite_GivesTheRfcHash(string message, string expected)
    {
        byte[] hash = Md4.HashData(Encoding.ASCII.GetBytes(message));

        Assert.AreEqual(expected, Convert.ToHexStringLower(hash));
    }

    // [MS-NLMP] section 4.2.2.1.2, "NTOWFv1()": NTOWFv1 of the password "Password" is
    // MD4(UNICODE("Password")) = a4 f4 9c 40 65 10 bd ca b6 82 4e e7 c3 0f d8 52.
    [TestMethod]
    public void HashData_MsNlmpNtowfv1OfPassword_GivesTheSpecificationHash()
    {
        byte[] hash = Md4.HashData(Encoding.Unicode.GetBytes("Password"));

        Assert.AreEqual("a4f49c406510bdcab6824ee7c30fd852", Convert.ToHexStringLower(hash));
    }
}
