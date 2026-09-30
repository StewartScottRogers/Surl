using System.Numerics;
using System.Security.Cryptography;
using static Surl.Protocol.Ssh.SshTestExchange;
using static Surl.Protocol.Ssh.SshTestKeyExchangeClient;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The user-key signature check (RFC 4252 section 7, RFC 5656 section 3.1.2, RFC 8332 section 3)
/// on blobs and signatures built here by hand: what verifies, and every malformed or
/// out-of-range field that does not.
/// </summary>
[TestClass]
public sealed class SshUserKeySignatureTests
{
    private static readonly byte[] Data = Ascii("what the client signed");

    [TestMethod]
    [DataRow("ecdsa-sha2-nistp256", "ecdsa-sha2-nistp256")]
    [DataRow("rsa-sha2-512", "ssh-rsa")]
    [DataRow("rsa-sha2-256", "ssh-rsa")]
    [DataRow("ssh-rsa", null)]
    [DataRow("ssh-ed25519", null)]
    public void KeyTypeFor_Algorithm_IsTheKeyTypeItSignsWith(string algorithm, string? keyType) =>
        Assert.AreEqual(keyType, SshUserKeySignature.KeyTypeFor(algorithm));

    [TestMethod]
    public void Verifies_RsaSignatureOfTheData_IsTrue()
    {
        var signature = SignatureField("rsa-sha2-256", SshTestKeys.Rsa1024.SignData(Data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        Assert.IsTrue(SshUserKeySignature.Verifies("rsa-sha2-256", RsaBlob(), signature, Data));
    }

    [TestMethod]
    public void Verifies_SignatureNamingAnotherAlgorithm_IsFalse()
    {
        var signature = SignatureField("rsa-sha2-512", SshTestKeys.Rsa1024.SignData(Data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        Assert.IsFalse(SshUserKeySignature.Verifies("rsa-sha2-256", RsaBlob(), signature, Data));
    }

    [TestMethod]
    public void Verifies_SignatureFieldCutShort_IsFalse() =>
        Assert.IsFalse(SshUserKeySignature.Verifies("rsa-sha2-256", RsaBlob(), UInt32(9), Data));

    [TestMethod]
    [DataRow(0, DisplayName = "A zero exponent")]
    [DataRow(-3, DisplayName = "A negative exponent")]
    public void Verifies_RsaExponentNotPositive_IsFalse(int exponent)
    {
        var modulus = SshTestKeys.Rsa1024.ExportParameters(false).Modulus!;
        var blob = Concat(String("ssh-rsa"), Mpint(exponent), Mpint(new BigInteger(modulus, isUnsigned: true, isBigEndian: true)));
        var signature = SignatureField("rsa-sha2-256", SshTestKeys.Rsa1024.SignData(Data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        Assert.IsFalse(SshUserKeySignature.Verifies("rsa-sha2-256", blob, signature, Data));
    }

    [TestMethod]
    public void Verifies_RsaModulusNotPositive_IsFalse()
    {
        var blob = Concat(String("ssh-rsa"), Mpint(65537), Mpint(0));

        Assert.IsFalse(SshUserKeySignature.Verifies("rsa-sha2-256", blob, SignatureField("rsa-sha2-256", [1]), Data));
    }

    [TestMethod]
    public void Verifies_RsaSignatureLongerThanTheModulus_IsFalse()
    {
        var signature = SignatureField("rsa-sha2-256", [0, .. SshTestKeys.Rsa1024.SignData(Data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)]);

        Assert.IsFalse(SshUserKeySignature.Verifies("rsa-sha2-256", RsaBlob(), signature, Data));
    }

    [TestMethod]
    public void Verifies_RsaKeyThePlatformCannotLoad_IsFalse()
    {
        var blob = Concat(String("ssh-rsa"), Mpint(3), Mpint(15));

        Assert.IsFalse(SshUserKeySignature.Verifies("rsa-sha2-256", blob, SignatureField("rsa-sha2-256", [1]), Data));
    }

    [TestMethod]
    public void Verifies_EcdsaSignatureOfTheData_IsTrue() =>
        Assert.IsTrue(SshUserKeySignature.Verifies("ecdsa-sha2-nistp256", EcdsaBlob(), EcdsaSignature(), Data));

    [TestMethod]
    public void Verifies_EcdsaBlobNamingAnotherCurve_IsFalse()
    {
        var point = SshHostKey.FromEcdsa(SshTestKeys.EcdsaP256).PublicKeyBlob.ToArray()[(4 + 19 + 4 + 8)..];
        var blob = Concat(String("ecdsa-sha2-nistp256"), String("nistp384"), point);

        Assert.IsFalse(SshUserKeySignature.Verifies("ecdsa-sha2-nistp256", blob, EcdsaSignature(), Data));
    }

    [TestMethod]
    public void Verifies_EcdsaPointNotOnTheCurve_IsFalse()
    {
        var blob = Concat(String("ecdsa-sha2-nistp256"), String("nistp256"), Str([4, .. new byte[64]]));

        Assert.IsFalse(SshUserKeySignature.Verifies("ecdsa-sha2-nistp256", blob, EcdsaSignature(), Data));
    }

    [TestMethod]
    [DataRow(0, 1, DisplayName = "r is zero")]
    [DataRow(-1, 1, DisplayName = "r is negative")]
    [DataRow(1, 0, DisplayName = "s is zero")]
    [DataRow(33, 1, DisplayName = "r is longer than the field")]
    [DataRow(1, 33, DisplayName = "s is longer than the field")]
    public void Verifies_EcdsaValueOutOfRange_IsFalse(int r, int s)
    {
        static BigInteger Value(int value) => value == 33 ? BigInteger.One << 256 : value;
        var signature = SignatureField("ecdsa-sha2-nistp256", Concat(Mpint(Value(r)), Mpint(Value(s))));

        Assert.IsFalse(SshUserKeySignature.Verifies("ecdsa-sha2-nistp256", EcdsaBlob(), signature, Data));
    }

    private static byte[] RsaBlob() => SshHostKey.FromRsa(SshTestKeys.Rsa1024).PublicKeyBlob.ToArray();

    private static byte[] EcdsaBlob() => SshHostKey.FromEcdsa(SshTestKeys.EcdsaP256).PublicKeyBlob.ToArray();

    private static byte[] SignatureField(string algorithm, byte[] signature) => Concat(String(algorithm), Str(signature));

    private static byte[] EcdsaSignature()
    {
        var fixedFields = SshTestKeys.EcdsaP256.SignData(Data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return SignatureField(
            "ecdsa-sha2-nistp256",
            Concat(
                Mpint(new BigInteger(fixedFields.AsSpan(0, 32), isUnsigned: true, isBigEndian: true)),
                Mpint(new BigInteger(fixedFields.AsSpan(32), isUnsigned: true, isBigEndian: true))));
    }
}
