using System.Security.Cryptography;
using System.Text;

namespace Surl.Protocol.Ssh;

[TestClass]
public sealed class SshHmacTests
{
    // RFC 2202 test case 5: data "Test With Truncation"; key 0x0c repeated, 20 bytes for
    // HMAC-SHA-1 and 16 for HMAC-MD5; the digest and its first 96 bits.
    [TestMethod]
    [DataRow("hmac-sha1", 20, "4c1a03424b55e07fe7f27be1d58bb9324a9a5a04", DisplayName = "RFC 2202 section 3, test case 5: HMAC-SHA-1")]
    [DataRow("hmac-sha1-96", 20, "4c1a03424b55e07fe7f27be1", DisplayName = "RFC 2202 section 3, test case 5: HMAC-SHA-1-96")]
    [DataRow("hmac-md5", 16, "56461ef2342edc00f9bab995690efd4c", DisplayName = "RFC 2202 section 2, test case 5: HMAC-MD5")]
    [DataRow("hmac-md5-96", 16, "56461ef2342edc00f9bab995", DisplayName = "RFC 2202 section 2, test case 5: HMAC-MD5-96")]
    public void Tag_Rfc2202TestCase5_IsItsDigestOrItsFirst96Bits(string name, int keyLength, string expected)
    {
        var mac = SshHmac.ForName(name)!;

        var tag = mac.Tag(Enumerable.Repeat((byte)0x0c, keyLength).ToArray(), Encoding.ASCII.GetBytes("Test With Truncation"));

        Assert.AreEqual(expected, Convert.ToHexStringLower(tag));
        Assert.AreEqual(keyLength, mac.KeyLength);
        Assert.AreEqual(expected.Length / 2, mac.MacLength);
    }

    // RFC 2286 section 2, test case 1: key 0x0b repeated 20 times, data "Hi There".
    [TestMethod]
    [DataRow("hmac-ripemd160")]
    [DataRow("hmac-ripemd160@openssh.com")]
    public void Tag_Rfc2286TestCase1_IsItsHmacRipemd160Digest(string name)
    {
        var mac = SshHmac.ForName(name)!;

        var tag = mac.Tag(Enumerable.Repeat((byte)0x0b, 20).ToArray(), Encoding.ASCII.GetBytes("Hi There"));

        Assert.AreEqual("24cb4bd67d20fc1a5d2ed7732dcc39377f0a5668", Convert.ToHexStringLower(tag));
        Assert.AreEqual(20, mac.KeyLength);
        Assert.AreEqual(20, mac.MacLength);
        Assert.IsFalse(mac.EncryptThenMac);
    }

    [TestMethod]
    public void ForName_Sha1EncryptThenMac_IsHmacSha1ComputedOverTheCiphertext() =>
        Assert.AreEqual(new SshHmac(HashAlgorithmName.SHA1, 20, 20, true), SshHmac.ForName("hmac-sha1-etm@openssh.com"));
}
