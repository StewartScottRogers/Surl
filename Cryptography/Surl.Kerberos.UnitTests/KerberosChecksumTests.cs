namespace Surl.Kerberos;

/// <summary>
/// Pins the keyed checksums of the four encryption types: RFC 8009 appendix A's sample checksums
/// for types 19 and 20, and for types 15 and 16, which RFC 3962 publishes no vector for, that a
/// checksum verifies and a changed message or checksum does not.
/// </summary>
[TestClass]
public sealed class KerberosChecksumTests
{
    private const int KeyUsage = 2;

    private static readonly byte[] Message = Convert.FromHexString("000102030405060708090a0b0c0d0e0f1011121314");

    // RFC 8009, appendix A, "Sample checksums": the base keys of "Sample results for key
    // derivation", key usage 2, and the 21-byte plain text.
    [TestMethod]
    [DataRow(
        KerberosEncryptionType.Aes128CtsHmacSha256128,
        "3705d96080c17728a0e800eab6e0d23c",
        "d78367186643d67b411cba9139fc1dee",
        DisplayName = "hmac-sha256-128-aes128")]
    [DataRow(
        KerberosEncryptionType.Aes256CtsHmacSha384192,
        "6d404d37faf79f9df0d33568d320669800eb4836472ea8a026d16b7182460c52",
        "45ee791567eefca37f4ac1e0222de80d43c3bfa06699672a",
        DisplayName = "hmac-sha384-192-aes256")]
    public void ComputeChecksum_Rfc8009AppendixA_GivesTheRfcChecksumAndVerifies(KerberosEncryptionType encryptionType, string baseKey, string expected)
    {
        KerberosEncryptionProfile profile = KerberosEncryptionProfile.For(encryptionType);
        byte[] key = Convert.FromHexString(baseKey);

        byte[] checksum = profile.ComputeChecksum(key, KeyUsage, Message);

        Assert.AreEqual(expected, Convert.ToHexStringLower(checksum));
        Assert.IsTrue(profile.VerifyChecksum(key, KeyUsage, Message, checksum));
    }

    // RFC 3962, appendix B's derived AES keys, used as fixed base keys.
    [TestMethod]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha196, "42263c6e89f4fc28b8df68ee09799f15", DisplayName = "hmac-sha1-96-aes128")]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha196, "fe697b52bc0d3ce14432ba036a92e65bbb52280990a2fa27883998d72af30161", DisplayName = "hmac-sha1-96-aes256")]
    public void ComputeChecksum_Rfc3962Enctype_Is96BitsAndVerifies(KerberosEncryptionType encryptionType, string baseKey)
    {
        KerberosEncryptionProfile profile = KerberosEncryptionProfile.For(encryptionType);
        byte[] key = Convert.FromHexString(baseKey);

        byte[] checksum = profile.ComputeChecksum(key, KeyUsage, Message);

        Assert.HasCount(12, checksum);
        Assert.IsTrue(profile.VerifyChecksum(key, KeyUsage, Message, checksum));
    }

    [TestMethod]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha196, "42263c6e89f4fc28b8df68ee09799f15", DisplayName = "15")]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha196, "fe697b52bc0d3ce14432ba036a92e65bbb52280990a2fa27883998d72af30161", DisplayName = "16")]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha256128, "3705d96080c17728a0e800eab6e0d23c", DisplayName = "19")]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha384192, "6d404d37faf79f9df0d33568d320669800eb4836472ea8a026d16b7182460c52", DisplayName = "20")]
    public void VerifyChecksum_ChangedMessageChecksumOrKeyUsage_ReturnsFalse(KerberosEncryptionType encryptionType, string baseKey)
    {
        KerberosEncryptionProfile profile = KerberosEncryptionProfile.For(encryptionType);
        byte[] key = Convert.FromHexString(baseKey);
        byte[] checksum = profile.ComputeChecksum(key, KeyUsage, Message);
        byte[] changedMessage = [.. Message];
        changedMessage[^1] ^= 0x01;
        byte[] changedChecksum = [.. checksum];
        changedChecksum[0] ^= 0x01;

        Assert.IsFalse(profile.VerifyChecksum(key, KeyUsage, changedMessage, checksum));
        Assert.IsFalse(profile.VerifyChecksum(key, KeyUsage, Message, changedChecksum));
        Assert.IsFalse(profile.VerifyChecksum(key, KeyUsage, Message, checksum.AsSpan(1)));
        Assert.IsFalse(profile.VerifyChecksum(key, KeyUsage + 1, Message, checksum));
    }
}
