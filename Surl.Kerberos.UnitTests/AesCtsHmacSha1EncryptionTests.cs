using System.Text;

namespace Surl.Kerberos;

/// <summary>
/// Pins the RFC 3962 encryption types, <c>aes128-cts-hmac-sha1-96</c> and
/// <c>aes256-cts-hmac-sha1-96</c>, to RFC 3962 appendix B: its CTS vectors pin the cipher
/// (<see cref="AesCiphertextStealingTests" />), and each string-to-key case's final step,
/// <c>DK(PBKDF2 output, "kerberos")</c>, pins the simplified profile's key derivation, with the
/// PBKDF2 output taken as a fixed key (ADR-0057 decision 11).
/// </summary>
[TestClass]
public sealed class AesCtsHmacSha1EncryptionTests
{
    private const int KeyUsage = 2;

    private static readonly byte[] Kerberos = Encoding.ASCII.GetBytes("kerberos");

    private static readonly byte[] Confounder = Convert.FromHexString("000102030405060708090a0b0c0d0e0f");

    // RFC 3962, appendix B: each case's "128-bit PBKDF2 output" and "128-bit AES key".
    [TestMethod]
    [DataRow("cdedb5281bb2f801565a1122b2563515", "42263c6e89f4fc28b8df68ee09799f15", DisplayName = "iteration count 1")]
    [DataRow("01dbee7f4a9e243e988b62c73cda935d", "c651bf29e2300ac27fa469d693bdda13", DisplayName = "iteration count 2")]
    [DataRow("5c08eb61fdf71e4e4ec3cf6ba1f5512b", "4c01cd46d632d01e6dbe230a01ed642a", DisplayName = "iteration count 1200")]
    [DataRow("d1daa78615f287e6a1c8b120d7062a49", "e9b23d52273747dd5c35cb55be619d8e", DisplayName = "iteration count 5, binary salt")]
    [DataRow("139c30c0966bc32ba55fdbf212530ac9", "59d1bb789a828b1aa54ef9c2883f69ed", DisplayName = "pass phrase equals block size")]
    [DataRow("9ccad6d468770cd51b10e6a68721be61", "cb8005dc5f90179a7f02104c0018751d", DisplayName = "pass phrase exceeds block size")]
    [DataRow("6b9cf26d45455a43a5b8bb276a403b39", "f149c1f2e154a73452d43e7fe62a56e5", DisplayName = "g-clef")]
    public void DeriveKey_Rfc3962AppendixB128BitKeys_GivesTheRfcAesKey(string pbkdf2Output, string aesKey)
    {
        byte[] derived = SimplifiedProfileKeyDerivation.DeriveKey(Convert.FromHexString(pbkdf2Output), Kerberos);

        Assert.AreEqual(aesKey, Convert.ToHexStringLower(derived));
    }

    // RFC 3962, appendix B: each case's "256-bit PBKDF2 output" and "256-bit AES key".
    [TestMethod]
    [DataRow(
        "cdedb5281bb2f801565a1122b25635150ad1f7a04bb9f3a333ecc0e2e1f70837",
        "fe697b52bc0d3ce14432ba036a92e65bbb52280990a2fa27883998d72af30161",
        DisplayName = "iteration count 1")]
    [DataRow(
        "01dbee7f4a9e243e988b62c73cda935da05378b93244ec8f48a99e61ad799d86",
        "a2e16d16b36069c135d5e9d2e25f896102685618b95914b467c67622225824ff",
        DisplayName = "iteration count 2")]
    [DataRow(
        "5c08eb61fdf71e4e4ec3cf6ba1f5512ba7e52ddbc5e5142f708a31e2e62b1e13",
        "55a6ac740ad17b4846941051e1e8b0a7548d93b0ab30a8bc3ff16280382b8c2a",
        DisplayName = "iteration count 1200")]
    [DataRow(
        "d1daa78615f287e6a1c8b120d7062a493f98d203e6be49a6adf4fa574b6e64ee",
        "97a4e786be20d81a382d5ebc96d5909cabcdadc87ca48f574504159f16c36e31",
        DisplayName = "iteration count 5, binary salt")]
    [DataRow(
        "139c30c0966bc32ba55fdbf212530ac9c5ec59f1a452f5cc9ad940fea0598ed1",
        "89adee3608db8bc71f1bfbfe459486b05618b70cbae22092534e56c553ba4b34",
        DisplayName = "pass phrase equals block size")]
    [DataRow(
        "9ccad6d468770cd51b10e6a68721be611a8b4d282601db3b36be9246915ec82a",
        "d78c5c9cb872a8c9dad4697f0bb5b2d21496c82beb2caeda2112fceea057401b",
        DisplayName = "pass phrase exceeds block size")]
    [DataRow(
        "6b9cf26d45455a43a5b8bb276a403b39e7fe37a0c41e02c281ff3069e1e94f52",
        "4b6d9839f84406df1f09cc166db4b83c571848b784a3d6bdc346589a3e393f9e",
        DisplayName = "g-clef")]
    public void DeriveKey_Rfc3962AppendixB256BitKeys_GivesTheRfcAesKey(string pbkdf2Output, string aesKey)
    {
        byte[] derived = SimplifiedProfileKeyDerivation.DeriveKey(Convert.FromHexString(pbkdf2Output), Kerberos);

        Assert.AreEqual(aesKey, Convert.ToHexStringLower(derived));
    }

    // RFC 3962 publishes no whole-message encryption vectors, so each enctype is pinned by its
    // pieces above and round-trips here under one of appendix B's derived keys, across the plain
    // text lengths RFC 8009 appendix A uses: empty, short, one block and more than one block.
    [TestMethod]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha196, "42263c6e89f4fc28b8df68ee09799f15", "", DisplayName = "17, empty")]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha196, "42263c6e89f4fc28b8df68ee09799f15", "000102030405", DisplayName = "17, short")]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha196, "42263c6e89f4fc28b8df68ee09799f15", "000102030405060708090a0b0c0d0e0f", DisplayName = "17, one block")]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha196, "42263c6e89f4fc28b8df68ee09799f15", "000102030405060708090a0b0c0d0e0f1011121314", DisplayName = "17, more than a block")]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha196, "fe697b52bc0d3ce14432ba036a92e65bbb52280990a2fa27883998d72af30161", "", DisplayName = "18, empty")]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha196, "fe697b52bc0d3ce14432ba036a92e65bbb52280990a2fa27883998d72af30161", "000102030405", DisplayName = "18, short")]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha196, "fe697b52bc0d3ce14432ba036a92e65bbb52280990a2fa27883998d72af30161", "000102030405060708090a0b0c0d0e0f", DisplayName = "18, one block")]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha196, "fe697b52bc0d3ce14432ba036a92e65bbb52280990a2fa27883998d72af30161", "000102030405060708090a0b0c0d0e0f1011121314", DisplayName = "18, more than a block")]
    public void EncryptThenTryDecrypt_Rfc3962Enctype_RoundTripsWithTheConfounderAndA96BitHmac(KerberosEncryptionType encryptionType, string key, string plainText)
    {
        KerberosEncryptionProfile profile = KerberosEncryptionProfile.For(encryptionType);
        byte[] baseKey = Convert.FromHexString(key);
        byte[] message = Convert.FromHexString(plainText);

        byte[] cipherText = profile.Encrypt(baseKey, KeyUsage, Confounder, message);
        bool decrypted = profile.TryDecrypt(baseKey, KeyUsage, cipherText, out byte[] roundTripped);

        Assert.AreEqual(Confounder.Length + message.Length + 12, cipherText.Length);
        Assert.IsTrue(decrypted);
        CollectionAssert.AreEqual(message, roundTripped);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "first cipher byte")]
    [DataRow(-1, DisplayName = "last HMAC byte")]
    public void TryDecrypt_Rfc3962TamperedCipherText_ReturnsFalseAndNoPlainText(int flippedIndex)
    {
        KerberosEncryptionProfile profile = KerberosEncryptionProfile.For(KerberosEncryptionType.Aes256CtsHmacSha196);
        byte[] baseKey = Convert.FromHexString("fe697b52bc0d3ce14432ba036a92e65bbb52280990a2fa27883998d72af30161");
        byte[] cipherText = profile.Encrypt(baseKey, KeyUsage, Confounder, [0x00, 0x01, 0x02]);
        cipherText[flippedIndex < 0 ? cipherText.Length + flippedIndex : flippedIndex] ^= 0x01;

        bool decrypted = profile.TryDecrypt(baseKey, KeyUsage, cipherText, out byte[] plainText);

        Assert.IsFalse(decrypted);
        Assert.IsEmpty(plainText);
    }

    [TestMethod]
    public void TryDecrypt_Rfc3962WrongKeyUsage_ReturnsFalse()
    {
        KerberosEncryptionProfile profile = KerberosEncryptionProfile.For(KerberosEncryptionType.Aes128CtsHmacSha196);
        byte[] baseKey = Convert.FromHexString("42263c6e89f4fc28b8df68ee09799f15");
        byte[] cipherText = profile.Encrypt(baseKey, KeyUsage, Confounder, [0x00]);

        Assert.IsFalse(profile.TryDecrypt(baseKey, KeyUsage + 1, cipherText, out _));
    }

    [TestMethod]
    public void TryDecrypt_ShorterThanConfounderAndHmac_ReturnsFalseWithoutThrowing()
    {
        KerberosEncryptionProfile profile = KerberosEncryptionProfile.For(KerberosEncryptionType.Aes128CtsHmacSha196);

        bool decrypted = profile.TryDecrypt(new byte[16], KeyUsage, new byte[16 + 12 - 1], out byte[] plainText);

        Assert.IsFalse(decrypted);
        Assert.IsEmpty(plainText);
    }

    [TestMethod]
    [DataRow(15, DisplayName = "one byte short")]
    [DataRow(17, DisplayName = "one byte long")]
    public void Encrypt_ConfounderNotSixteenBytes_ThrowsArgumentException(int confounderLength)
    {
        KerberosEncryptionProfile profile = KerberosEncryptionProfile.For(KerberosEncryptionType.Aes128CtsHmacSha196);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => profile.Encrypt(new byte[16], KeyUsage, new byte[confounderLength], []));

        Assert.AreEqual("confounder", exception.ParamName);
    }
}
