using System.Security.Cryptography;
using static Surl.Protocol.Ssh.SshTestExchange;
using static Surl.Protocol.Ssh.SshTestKeyExchangeClient;

namespace Surl.Protocol.Ssh;

[TestClass]
public sealed class SshHostKeyTests
{
    [TestMethod]
    public void FromRsa_Key_IsAnSshRsaBlobSigningWithRsaSha2AndSshRsa()
    {
        var parameters = SshTestKeys.Rsa2048.ExportParameters(false);

        var key = SshHostKey.FromRsa(SshTestKeys.Rsa2048);

        Assert.AreEqual("ssh-rsa", key.KeyType);
        CollectionAssert.AreEqual(new[] { "rsa-sha2-512", "rsa-sha2-256", "ssh-rsa" }, key.SignatureAlgorithms.ToArray());
        CollectionAssert.AreEqual(
            Concat(String("ssh-rsa"), Mpint(SshKeyFileBuilder.Unsigned(parameters.Exponent!)), Mpint(SshKeyFileBuilder.Unsigned(parameters.Modulus!))),
            key.PublicKeyBlob.ToArray());
    }

    [TestMethod]
    [DataRow("rsa-sha2-512")]
    [DataRow("rsa-sha2-256")]
    [DataRow("ssh-rsa")]
    public void Sign_Rsa_IsAPkcs1V15SignatureWithTheAlgorithmsHash(string algorithm)
    {
        var hash = algorithm switch
        {
            "rsa-sha2-512" => HashAlgorithmName.SHA512,
            "rsa-sha2-256" => HashAlgorithmName.SHA256,
            _ => HashAlgorithmName.SHA1,
        };

        var reader = new SshWireReader(SshHostKey.FromRsa(SshTestKeys.Rsa2048).Sign(algorithm, [7, 8]));

        CollectionAssert.AreEqual(Ascii(algorithm), reader.ReadString().ToArray());
        Assert.IsTrue(SshTestKeys.Rsa2048.VerifyData(new byte[] { 7, 8 }, reader.ReadString().ToArray(), hash, RSASignaturePadding.Pkcs1));
    }

    [TestMethod]
    public void FromDsa_Key_IsAnSshDssBlobSigningWithSshDss()
    {
        var parameters = SshTestKeys.Dsa1024.ExportParameters(false);

        var key = SshHostKey.FromDsa(SshTestKeys.Dsa1024);

        Assert.AreEqual("ssh-dss", key.KeyType);
        CollectionAssert.AreEqual(new[] { "ssh-dss" }, key.SignatureAlgorithms.ToArray());
        CollectionAssert.AreEqual(
            Concat(
                String("ssh-dss"),
                Mpint(SshKeyFileBuilder.Unsigned(parameters.P!)),
                Mpint(SshKeyFileBuilder.Unsigned(parameters.Q!)),
                Mpint(SshKeyFileBuilder.Unsigned(parameters.G!)),
                Mpint(SshKeyFileBuilder.Unsigned(parameters.Y!))),
            key.PublicKeyBlob.ToArray());
    }

    // RFC 4253 section 6.6: the signature blob is r and s as 160-bit unsigned integers, 40 bytes, over SHA-1.
    [TestMethod]
    public void Sign_Dsa_IsFortyBytesOfRAndSOverSha1()
    {
        var reader = new SshWireReader(SshHostKey.FromDsa(SshTestKeys.Dsa1024).Sign("ssh-dss", [7, 8]));

        CollectionAssert.AreEqual(Ascii("ssh-dss"), reader.ReadString().ToArray());
        var signature = reader.ReadString().ToArray();
        Assert.HasCount(40, signature);
        Assert.IsTrue(SshTestKeys.Dsa1024.VerifyData(new byte[] { 7, 8 }, signature, HashAlgorithmName.SHA1, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    [TestMethod]
    public void FromDsa_KeyWithA256BitQ_IsRefused()
    {
        var parameters = SshTestKeys.Dsa1024.ExportParameters(true);
        parameters.Q = new byte[32];
        using var longQ = new DsaExportingParameters(parameters);

        Assert.ThrowsExactly<ArgumentException>(() => SshHostKey.FromDsa(longQ));
    }

    [TestMethod]
    public void FromDsa_Null_IsRefused() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => SshHostKey.FromDsa(null!));

    [TestMethod]
    public void FromEcdsa_Key_IsAnEcdsaBlobOnItsCurve()
    {
        var parameters = SshTestKeys.EcdsaP521.ExportParameters(false);

        var key = SshHostKey.FromEcdsa(SshTestKeys.EcdsaP521);

        Assert.AreEqual("ecdsa-sha2-nistp521", key.KeyType);
        CollectionAssert.AreEqual(new[] { "ecdsa-sha2-nistp521" }, key.SignatureAlgorithms.ToArray());
        CollectionAssert.AreEqual(
            Concat(String("ecdsa-sha2-nistp521"), String("nistp521"), Str(SshKeyFileBuilder.Point(parameters))),
            key.PublicKeyBlob.ToArray());
    }

    [TestMethod]
    [DataRow(28, DisplayName = "A 224-bit point")]
    [DataRow(32, DisplayName = "A 256-bit point not on P-256, as a brainpoolP256r1 or secp256k1 key has")]
    public void FromEcdsa_KeyOnAnotherCurve_IsRefused(int coordinateBytes)
    {
        using var key = new OtherCurveEcdsa(coordinateBytes);

        var refusal = Assert.ThrowsExactly<ArgumentException>(() => SshHostKey.FromEcdsa(key));

        StringAssert.StartsWith(refusal.Message, "An SSH ECDSA host key is on P-256, P-384 or P-521; this one is on another curve.");
    }

    [TestMethod]
    public void Ed25519FromSeed_Seed_IsAnSshEd25519BlobOfItsPublicKey()
    {
        var key = SshEd25519HostKey.FromSeed(SshTestKeys.Ed25519Seed);

        Assert.AreEqual("ssh-ed25519", key.KeyType);
        CollectionAssert.AreEqual(new[] { "ssh-ed25519" }, key.SignatureAlgorithms.ToArray());
        CollectionAssert.AreEqual(Concat(String("ssh-ed25519"), Str(SshTestKeys.Ed25519PublicKey)), key.PublicKeyBlob.ToArray());
    }

    [TestMethod]
    public void Sign_Ed25519_IsRfc8032sSignatureUnderTheAlgorithmsName()
    {
        var reader = new SshWireReader(SshTestKeys.Ed25519HostKey.Sign("ssh-ed25519", []));

        CollectionAssert.AreEqual(Ascii("ssh-ed25519"), reader.ReadString().ToArray());
        CollectionAssert.AreEqual(
            Convert.FromHexString("e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e065224901555fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b"),
            reader.ReadString().ToArray(),
            "RFC 8032 section 7.1, TEST 1: the empty message.");
    }

    [TestMethod]
    [DataRow(31)]
    [DataRow(33)]
    public void Ed25519FromSeed_SeedOfAnotherLength_IsRefused(int length)
    {
        Assert.ThrowsExactly<CryptographicException>(() => SshEd25519HostKey.FromSeed(new byte[length]));
    }

    [TestMethod]
    public void FromRsaAndFromEcdsa_Null_AreRefused()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => SshHostKey.FromRsa(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => SshHostKey.FromEcdsa(null!));
    }

    // An ECDSA key whose public point lies on none of the NIST curves SSH names; it is never
    // asked to sign.
    private sealed class OtherCurveEcdsa(int coordinateBytes) : ECDsa
    {
        public override ECParameters ExportParameters(bool includePrivateParameters) => new()
        {
            Curve = ECCurve.CreateFromValue("1.3.36.3.3.2.8.1.1.7"),
            Q = new ECPoint { X = Enumerable.Repeat((byte)1, coordinateBytes).ToArray(), Y = Enumerable.Repeat((byte)2, coordinateBytes).ToArray() },
            D = new byte[coordinateBytes],
        };

        public override byte[] SignHash(byte[] hash) => throw new NotSupportedException();

        public override bool VerifyHash(byte[] hash, byte[] signature) => throw new NotSupportedException();
    }

    // A DSA key that exports the parameters it was given; it is never asked to sign.
    private sealed class DsaExportingParameters(DSAParameters parameters) : DSA
    {
        public override DSAParameters ExportParameters(bool includePrivateParameters) => parameters;

        public override void ImportParameters(DSAParameters parameters) => throw new NotSupportedException();

        public override byte[] CreateSignature(byte[] rgbHash) => throw new NotSupportedException();

        public override bool VerifySignature(byte[] rgbHash, byte[] rgbSignature) => throw new NotSupportedException();
    }
}
