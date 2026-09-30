namespace Surl.Kerberos;

/// <summary>
/// Pins which profile, checksum type, key length and HMAC length each accepted encryption type
/// has (RFC 3962 sections 6 and 7, RFC 8009 sections 5 and 8), and the four enctype numbers of
/// ADR-0057 decision 6.
/// </summary>
[TestClass]
public sealed class KerberosEncryptionProfileTests
{
    [TestMethod]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha196, (int)KerberosChecksumType.HmacSha196Aes128, 16, 12, typeof(AesCtsHmacSha1Profile), DisplayName = "17")]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha196, (int)KerberosChecksumType.HmacSha196Aes256, 32, 12, typeof(AesCtsHmacSha1Profile), DisplayName = "18")]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha256128, (int)KerberosChecksumType.HmacSha256128Aes128, 16, 16, typeof(AesCtsHmacSha2Profile), DisplayName = "19")]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha384192, (int)KerberosChecksumType.HmacSha384192Aes256, 32, 24, typeof(AesCtsHmacSha2Profile), DisplayName = "20")]
    public void For_AcceptedEncryptionType_GivesItsRfcParameters(
        KerberosEncryptionType encryptionType, int checksumType, int keyLength, int hmacLength, Type profileType)
    {
        KerberosEncryptionProfile profile = KerberosEncryptionProfile.For(encryptionType);

        Assert.AreEqual(encryptionType, profile.EncryptionType);
        Assert.AreEqual((KerberosChecksumType)checksumType, profile.ChecksumType);
        Assert.AreEqual(keyLength, profile.KeyLength);
        Assert.AreEqual(hmacLength, profile.HmacLength);
        Assert.IsInstanceOfType(profile, profileType);
    }

    [TestMethod]
    [DataRow(23, DisplayName = "rc4-hmac")]
    [DataRow(16, DisplayName = "des3-cbc-sha1")]
    [DataRow(0, DisplayName = "zero")]
    public void For_EncryptionTypeNotAccepted_ThrowsArgumentOutOfRangeException(int encryptionType)
    {
        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => KerberosEncryptionProfile.For((KerberosEncryptionType)encryptionType));

        Assert.AreEqual("encryptionType", exception.ParamName);
    }

    [TestMethod]
    public void KerberosEncryptionType_HasExactlyTheFourAdr0057Enctypes()
    {
        CollectionAssert.AreEqual(new[] { 17, 18, 19, 20 }, Enum.GetValues<KerberosEncryptionType>().Select(value => (int)value).ToArray());
        CollectionAssert.AreEqual(
            new[] { "Aes128CtsHmacSha196", "Aes256CtsHmacSha196", "Aes128CtsHmacSha256128", "Aes256CtsHmacSha384192" },
            Enum.GetNames<KerberosEncryptionType>());
    }

    [TestMethod]
    public void KerberosEncryptionType_IsTheLibrarysOnlyPublicType()
    {
        Type[] publicTypes = typeof(KerberosEncryptionType).Assembly.GetExportedTypes();

        CollectionAssert.AreEqual(new[] { typeof(KerberosEncryptionType) }, publicTypes);
    }
}
