namespace Surl.Kerberos;

/// <summary>
/// Pins the RFC 8009 encryption types, <c>aes128-cts-hmac-sha256-128</c> and
/// <c>aes256-cts-hmac-sha384-192</c>, to the key derivations and sample encryptions of RFC 8009
/// appendix A, never to values the code under test computed.
/// </summary>
[TestClass]
public sealed class AesCtsHmacSha2EncryptionTests
{
    // RFC 8009, appendix A, "Sample results for key derivation": the base keys, used with key usage 2.
    private const string Aes128BaseKey = "3705d96080c17728a0e800eab6e0d23c";

    private const string Aes256BaseKey = "6d404d37faf79f9df0d33568d320669800eb4836472ea8a026d16b7182460c52";

    private const int KeyUsage = 2;

    // RFC 8009, appendix A, "Sample results for key derivation".
    [TestMethod]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha256128, Aes128BaseKey, (int)KerberosDerivedKeyPurpose.Checksum, "b31a018a48f54776f403e9a396325dc3", DisplayName = "19, Kc")]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha256128, Aes128BaseKey, (int)KerberosDerivedKeyPurpose.Encryption, "9b197dd1e8c5609d6e67c3e37c62c72e", DisplayName = "19, Ke")]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha256128, Aes128BaseKey, (int)KerberosDerivedKeyPurpose.Integrity, "9fda0e56ab2d85e1569a688696c26a6c", DisplayName = "19, Ki")]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha384192, Aes256BaseKey, (int)KerberosDerivedKeyPurpose.Checksum, "ef5718be86cc84963d8bbb5031e9f5c4ba41f28faf69e73d", DisplayName = "20, Kc")]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha384192, Aes256BaseKey, (int)KerberosDerivedKeyPurpose.Encryption, "56ab22bee63d82d7bc5227f6773f8ea7a5eb1c825160c38312980c442e5c7e49", DisplayName = "20, Ke")]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha384192, Aes256BaseKey, (int)KerberosDerivedKeyPurpose.Integrity, "69b16514e3cd8e56b82010d5c73012b622c4d00ffc23ed1f", DisplayName = "20, Ki")]
    public void DeriveKey_Rfc8009AppendixA_GivesTheRfcKey(KerberosEncryptionType encryptionType, string baseKey, int purpose, string expected)
    {
        byte[] derived = KerberosEncryptionProfile.For(encryptionType).DeriveKey(Convert.FromHexString(baseKey), KeyUsage, (KerberosDerivedKeyPurpose)purpose);

        Assert.AreEqual(expected, Convert.ToHexStringLower(derived));
    }

    // RFC 8009, appendix A, "Sample encryptions": plain text, confounder and cipher text
    // (AES output, then truncated HMAC output), under the base keys above and key usage 2.
    [TestMethod]
    [DataRow(
        KerberosEncryptionType.Aes128CtsHmacSha256128,
        Aes128BaseKey,
        "",
        "7e5895eaf2672435bad817f545a37148",
        "ef85fb890bb8472f4dab20394dca781dad877eda39d50c870c0d5a0a8e48c718",
        DisplayName = "19, empty plain text")]
    [DataRow(
        KerberosEncryptionType.Aes128CtsHmacSha256128,
        Aes128BaseKey,
        "000102030405",
        "7bca285e2fd4130fb55b1a5c83bc5b24",
        "84d7f30754ed987bab0bf3506beb09cfb55402cef7e6877ce99e247e52d16ed4421dfdf8976c",
        DisplayName = "19, less than a block")]
    [DataRow(
        KerberosEncryptionType.Aes128CtsHmacSha256128,
        Aes128BaseKey,
        "000102030405060708090a0b0c0d0e0f",
        "56ab21713ff62c0a1457200f6fa9948f",
        "3517d640f50ddc8ad3628722b3569d2ae07493fa8263254080ea65c1008e8fc295fb4852e7d83e1e7c48c37eebe6b0d3",
        DisplayName = "19, one block")]
    [DataRow(
        KerberosEncryptionType.Aes128CtsHmacSha256128,
        Aes128BaseKey,
        "000102030405060708090a0b0c0d0e0f1011121314",
        "a7a4e29a4728ce10664fb64e49ad3fac",
        "720f73b18d9859cd6ccb4346115cd336c70f58edc0c4437c5573544c31c813bce1e6d072c186b39a413c2f92ca9b8334a287ffcbfc",
        DisplayName = "19, more than a block")]
    [DataRow(
        KerberosEncryptionType.Aes256CtsHmacSha384192,
        Aes256BaseKey,
        "",
        "f764e9fa15c276478b2c7d0c4e5f58e4",
        "41f53fa5bfe7026d91faf9be959195a058707273a96a40f0a01960621ac612748b9bbfbe7eb4ce3c",
        DisplayName = "20, empty plain text")]
    [DataRow(
        KerberosEncryptionType.Aes256CtsHmacSha384192,
        Aes256BaseKey,
        "000102030405",
        "b80d3251c1f6471494256ffe712d0b9a",
        "4ed7b37c2bcac8f74f23c1cf07e62bc7b75fb3f637b9f559c7f664f69eab7b6092237526ea0d1f61cb20d69d10f2",
        DisplayName = "20, less than a block")]
    [DataRow(
        KerberosEncryptionType.Aes256CtsHmacSha384192,
        Aes256BaseKey,
        "000102030405060708090a0b0c0d0e0f",
        "53bf8a0d105265d4e276428624ce5e63",
        "bc47ffec7998eb91e8115cf8d19dac4bbbe2e163e87dd37f49beca92027764f68cf51f14d798c2273f35df574d1f932e40c4ff255b36a266",
        DisplayName = "20, one block")]
    [DataRow(
        KerberosEncryptionType.Aes256CtsHmacSha384192,
        Aes256BaseKey,
        "000102030405060708090a0b0c0d0e0f1011121314",
        "763e65367e864f02f55153c7e3b58af1",
        "40013e2df58e8751957d2878bcd2d6fe101ccfd556cb1eae79db3c3ee86429f2b2a602ac86fef6ecb647d6295fae077a1feb517508d2c16b4192e01f62",
        DisplayName = "20, more than a block")]
    public void Encrypt_Rfc8009AppendixA_GivesTheRfcCipherTextAndDecryptsBack(KerberosEncryptionType encryptionType, string baseKey, string plainText, string confounder, string expected)
    {
        KerberosEncryptionProfile profile = KerberosEncryptionProfile.For(encryptionType);
        byte[] key = Convert.FromHexString(baseKey);
        byte[] message = Convert.FromHexString(plainText);

        byte[] cipherText = profile.Encrypt(key, KeyUsage, Convert.FromHexString(confounder), message);
        bool decrypted = profile.TryDecrypt(key, KeyUsage, cipherText, out byte[] roundTripped);

        Assert.AreEqual(expected, Convert.ToHexStringLower(cipherText));
        Assert.IsTrue(decrypted);
        CollectionAssert.AreEqual(message, roundTripped);
    }

    // RFC 8009, appendix A: the "more than a block" cipher texts, each with one bit flipped.
    [TestMethod]
    [DataRow(
        KerberosEncryptionType.Aes128CtsHmacSha256128,
        Aes128BaseKey,
        "720f73b18d9859cd6ccb4346115cd336c70f58edc0c4437c5573544c31c813bce1e6d072c186b39a413c2f92ca9b8334a287ffcbfc",
        0,
        DisplayName = "19, first cipher byte")]
    [DataRow(
        KerberosEncryptionType.Aes128CtsHmacSha256128,
        Aes128BaseKey,
        "720f73b18d9859cd6ccb4346115cd336c70f58edc0c4437c5573544c31c813bce1e6d072c186b39a413c2f92ca9b8334a287ffcbfc",
        52,
        DisplayName = "19, last HMAC byte")]
    [DataRow(
        KerberosEncryptionType.Aes256CtsHmacSha384192,
        Aes256BaseKey,
        "40013e2df58e8751957d2878bcd2d6fe101ccfd556cb1eae79db3c3ee86429f2b2a602ac86fef6ecb647d6295fae077a1feb517508d2c16b4192e01f62",
        36,
        DisplayName = "20, last cipher byte")]
    [DataRow(
        KerberosEncryptionType.Aes256CtsHmacSha384192,
        Aes256BaseKey,
        "40013e2df58e8751957d2878bcd2d6fe101ccfd556cb1eae79db3c3ee86429f2b2a602ac86fef6ecb647d6295fae077a1feb517508d2c16b4192e01f62",
        37,
        DisplayName = "20, first HMAC byte")]
    public void TryDecrypt_Rfc8009TamperedCipherText_ReturnsFalseAndNoPlainText(KerberosEncryptionType encryptionType, string baseKey, string cipherText, int flippedIndex)
    {
        byte[] tampered = Convert.FromHexString(cipherText);
        tampered[flippedIndex] ^= 0x80;

        bool decrypted = KerberosEncryptionProfile.For(encryptionType).TryDecrypt(Convert.FromHexString(baseKey), KeyUsage, tampered, out byte[] plainText);

        Assert.IsFalse(decrypted);
        Assert.IsEmpty(plainText);
    }

    [TestMethod]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha256128, Aes128BaseKey, 16 + 16 - 1, DisplayName = "19")]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha384192, Aes256BaseKey, 16 + 24 - 1, DisplayName = "20")]
    public void TryDecrypt_ShorterThanConfounderAndHmac_ReturnsFalseWithoutThrowing(KerberosEncryptionType encryptionType, string baseKey, int length)
    {
        bool decrypted = KerberosEncryptionProfile.For(encryptionType).TryDecrypt(Convert.FromHexString(baseKey), KeyUsage, new byte[length], out byte[] plainText);

        Assert.IsFalse(decrypted);
        Assert.IsEmpty(plainText);
    }
}
