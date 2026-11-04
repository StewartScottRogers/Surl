using System.Numerics;
using System.Security.Cryptography;
using Surl.Cryptography.Ed25519;
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
    [DataRow("ecdsa-sha2-nistp256", false, "ecdsa-sha2-nistp256")]
    [DataRow("rsa-sha2-512", false, "ssh-rsa")]
    [DataRow("rsa-sha2-256", false, "ssh-rsa")]
    [DataRow("ssh-rsa", false, null)]
    [DataRow("ssh-ed25519", false, "ssh-ed25519")]
    [DataRow("ssh-dss", false, null)]
    [DataRow("rsa-sha2-256", true, "ssh-rsa")]
    [DataRow("ssh-rsa", true, "ssh-rsa")]
    [DataRow("ssh-dss", true, "ssh-dss")]
    [DataRow("ssh-rsa-cert-v01@openssh.com", true, null)]
    public void KeyTypeFor_Algorithm_IsTheKeyTypeItSignsWith(string algorithm, bool allowWeakAlgorithms, string? keyType) =>
        Assert.AreEqual(keyType, SshUserKeySignature.KeyTypeFor(algorithm, allowWeakAlgorithms));

    [TestMethod]
    public void AlgorithmsFor_WeakAllowed_AddsSshRsaAndSshDssAfterTheDefaultOnes()
    {
        CollectionAssert.AreEqual(SshUserKeySignature.Algorithms.ToArray(), SshUserKeySignature.AlgorithmsFor(false).ToArray());
        CollectionAssert.AreEqual(
            new[] { "ssh-ed25519", "ecdsa-sha2-nistp256", "ecdsa-sha2-nistp384", "ecdsa-sha2-nistp521", "rsa-sha2-512", "rsa-sha2-256", "ssh-rsa", "ssh-dss" },
            SshUserKeySignature.AlgorithmsFor(true).ToArray());
    }

    [TestMethod]
    [DataRow("rsa-sha2-256", 2048, false, true, DisplayName = "A 2048-bit RSA key")]
    [DataRow("rsa-sha2-256", 1024, false, false, DisplayName = "A 1024-bit RSA key without weak algorithms")]
    [DataRow("rsa-sha2-512", 1024, true, true, DisplayName = "A 1024-bit RSA key with weak algorithms")]
    [DataRow("ssh-rsa", 2048, false, false, DisplayName = "ssh-rsa without weak algorithms")]
    [DataRow("ssh-rsa", 1024, true, true, DisplayName = "ssh-rsa with a 1024-bit key and weak algorithms")]
    [DataRow("ecdsa-sha2-nistp256", 2048, false, false, DisplayName = "An RSA key for an ECDSA algorithm")]
    public void AcceptsKey_RsaKey_IsAcceptedByItsSizeAndTheAlgorithm(string algorithm, int bits, bool allowWeakAlgorithms, bool accepted)
    {
        var key = bits == 2048 ? SshTestKeys.Rsa2048 : SshTestKeys.Rsa1024;

        Assert.AreEqual(accepted, SshUserKeySignature.AcceptsKey(algorithm, SshHostKey.FromRsa(key).PublicKeyBlob, allowWeakAlgorithms));
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, true)]
    public void AcceptsKey_DsaKey_IsAcceptedOnlyWithWeakAlgorithms(bool allowWeakAlgorithms, bool accepted) =>
        Assert.AreEqual(accepted, SshUserKeySignature.AcceptsKey("ssh-dss", SshHostKey.FromDsa(SshTestKeys.Dsa1024).PublicKeyBlob, allowWeakAlgorithms));

    [TestMethod]
    public void AcceptsKey_RsaBlobCutShortBeforeItsModulus_IsFalse() =>
        Assert.IsFalse(SshUserKeySignature.AcceptsKey("rsa-sha2-256", Concat(String("ssh-rsa"), Mpint(65537)), allowWeakAlgorithms: false));

    [TestMethod]
    public void AcceptsKey_BlobCutShortBeforeItsType_IsFalse() =>
        Assert.IsFalse(SshUserKeySignature.AcceptsKey("rsa-sha2-256", UInt32(9), allowWeakAlgorithms: true));

    [TestMethod]
    public void Verifies_SshRsaSha1SignatureOfTheData_IsTrue()
    {
        var signature = SignatureField("ssh-rsa", SshTestKeys.Rsa1024.SignData(Data, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1));

        Assert.IsTrue(SshUserKeySignature.Verifies("ssh-rsa", RsaBlob(), signature, Data));
    }

    [TestMethod]
    public void Verifies_SshRsaSignatureOverSha256_IsFalse()
    {
        var signature = SignatureField("ssh-rsa", SshTestKeys.Rsa1024.SignData(Data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        Assert.IsFalse(SshUserKeySignature.Verifies("ssh-rsa", RsaBlob(), signature, Data));
    }

    [TestMethod]
    public void Verifies_SshDssSignatureOfTheData_IsTrue()
    {
        var signature = SignatureField("ssh-dss", DsaSignature(Data));

        Assert.IsTrue(SshUserKeySignature.Verifies("ssh-dss", DsaBlob(), signature, Data));
    }

    [TestMethod]
    public void Verifies_SshDssSignatureWithAFlippedByte_IsFalse()
    {
        var raw = DsaSignature(Data);
        raw[^1] ^= 1;

        Assert.IsFalse(SshUserKeySignature.Verifies("ssh-dss", DsaBlob(), SignatureField("ssh-dss", raw), Data));
    }

    [TestMethod]
    [DataRow("signature", DisplayName = "A 41-byte signature")]
    [DataRow("p", DisplayName = "A zero p")]
    [DataRow("q", DisplayName = "A q longer than 160 bits")]
    [DataRow("g", DisplayName = "A g longer than p")]
    [DataRow("y", DisplayName = "A negative y")]
    public void Verifies_SshDssFieldOutOfRange_IsFalse(string field)
    {
        var parameters = SshTestKeys.Dsa1024.ExportParameters(false);
        var p = field == "p" ? BigInteger.Zero : Unsigned(parameters.P!);
        var q = field == "q" ? BigInteger.One << 160 : Unsigned(parameters.Q!);
        var g = field == "g" ? p + (BigInteger.One << 1100) : Unsigned(parameters.G!);
        var y = field == "y" ? BigInteger.MinusOne : Unsigned(parameters.Y!);
        var blob = Concat(String("ssh-dss"), Mpint(p), Mpint(q), Mpint(g), Mpint(y));
        byte[] raw = [.. DsaSignature(Data), .. field == "signature" ? new byte[] { 0 } : []];

        Assert.IsFalse(SshUserKeySignature.Verifies("ssh-dss", blob, SignatureField("ssh-dss", raw), Data));
    }

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

    [TestMethod]
    public void Verifies_Ed25519SignatureOfTheData_IsTrue() =>
        Assert.IsTrue(SshUserKeySignature.Verifies("ssh-ed25519", Ed25519Blob(SshTestKeys.Ed25519PublicKey), Ed25519Signature(), Data));

    [TestMethod]
    public void Verifies_Rfc8032sTestSignature_IsTrue()
    {
        var signature = SignatureField(
            "ssh-ed25519",
            Convert.FromHexString("e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e065224901555fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b"));

        Assert.IsTrue(SshUserKeySignature.Verifies("ssh-ed25519", Ed25519Blob(SshTestKeys.Ed25519PublicKey), signature, []));
    }

    [TestMethod]
    [DataRow(0, DisplayName = "A bit of R flipped")]
    [DataRow(63, DisplayName = "A bit of S flipped")]
    public void Verifies_Ed25519SignatureFlipped_IsFalse(int flippedByte)
    {
        var signature = Ed25519Signature();
        signature[^(64 - flippedByte)] ^= 1;

        Assert.IsFalse(SshUserKeySignature.Verifies("ssh-ed25519", Ed25519Blob(SshTestKeys.Ed25519PublicKey), signature, Data));
    }

    [TestMethod]
    [DataRow(31)]
    [DataRow(33)]
    public void Verifies_Ed25519KeyOfAnotherLength_IsFalse(int length)
    {
        byte[] publicKey = [.. SshTestKeys.Ed25519PublicKey, 0];

        Assert.IsFalse(SshUserKeySignature.Verifies("ssh-ed25519", Ed25519Blob(publicKey[..length]), Ed25519Signature(), Data));
    }

    [TestMethod]
    [DataRow(63)]
    [DataRow(65)]
    public void Verifies_Ed25519SignatureOfAnotherLength_IsFalse(int length)
    {
        byte[] raw = [.. Ed25519.Sign(SshTestKeys.Ed25519Seed, Data), 0];

        Assert.IsFalse(SshUserKeySignature.Verifies("ssh-ed25519", Ed25519Blob(SshTestKeys.Ed25519PublicKey), SignatureField("ssh-ed25519", raw[..length]), Data));
    }

    [TestMethod]
    public void Verifies_Ed25519KeyThatIsNoPoint_IsFalse()
    {
        var notAPoint = Enumerable.Repeat((byte)0xff, 32).ToArray();
        notAPoint[^1] = 0x7f;

        Assert.IsFalse(SshUserKeySignature.Verifies("ssh-ed25519", Ed25519Blob(notAPoint), Ed25519Signature(), Data));
    }

    private static byte[] DsaBlob() => SshHostKey.FromDsa(SshTestKeys.Dsa1024).PublicKeyBlob.ToArray();

    private static byte[] DsaSignature(byte[] data) =>
        SshTestKeys.Dsa1024.SignData(data, HashAlgorithmName.SHA1, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

    private static BigInteger Unsigned(byte[] bigEndian) => new(bigEndian, isUnsigned: true, isBigEndian: true);

    private static byte[] Ed25519Blob(byte[] publicKey) => Concat(String("ssh-ed25519"), Str(publicKey));

    private static byte[] Ed25519Signature() => SignatureField("ssh-ed25519", Ed25519.Sign(SshTestKeys.Ed25519Seed, Data));

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
