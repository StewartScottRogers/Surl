using System.Security.Cryptography;

namespace Surl.TlsSrp;

/// <summary>The key and chain the signed suites sign with (ADR-0122 decisions 2 and 3).</summary>
[TestClass]
public sealed class TlsSrpServerCertificateTests
{
    private static readonly byte[] Content = "client_random + server_random + params"u8.ToArray();

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new TlsSrpServerCertificate(null!, UnitTestCertificates.Rsa));
        Assert.ThrowsExactly<ArgumentNullException>(() => new TlsSrpServerCertificate(UnitTestCertificates.Chain, null!));
    }

    [TestMethod]
    public void Constructor_EmptyChain_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new TlsSrpServerCertificate([], UnitTestCertificates.Rsa));
    }

    [TestMethod]
    public void Constructor_KeyThatIsNeitherRsaNorDsa_Throws()
    {
        using var key = ECDsa.Create();

        Assert.ThrowsExactly<ArgumentException>(() => new TlsSrpServerCertificate(UnitTestCertificates.Chain, key));
    }

    [TestMethod]
    public void SignedSuites_RsaKey_AreTheThreeRsaSuites()
    {
        var certificate = UnitTestCertificates.RsaCertificate();

        CollectionAssert.AreEqual(new[] { 0xC01B, 0xC01E, 0xC021 }, certificate.SignedSuites.Select(suite => (int)suite).ToArray());
        Assert.AreSame(UnitTestCertificates.Chain, certificate.Chain);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public void SignedSuites_DsaKey_AreTheThreeDssSuites()
    {
        var certificate = UnitTestCertificates.DsaCertificate();

        CollectionAssert.AreEqual(new[] { 0xC01C, 0xC01F, 0xC022 }, certificate.SignedSuites.Select(suite => (int)suite).ToArray());
    }

    [TestMethod]
    public void ChooseScheme_RsaKeyAndNoExtension_IsRsaSha1()
    {
        Assert.AreEqual(TlsSignatureScheme.RsaPkcs1Sha1, UnitTestCertificates.RsaCertificate().ChooseScheme(null));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public void ChooseScheme_DsaKeyAndNoExtension_IsDsaSha1()
    {
        Assert.AreEqual(TlsSignatureScheme.DsaSha1, UnitTestCertificates.DsaCertificate().ChooseScheme(null));
    }

    [TestMethod]
    public void ChooseScheme_RsaKey_IsTheClientsFirstSchemeTheKeyCanMake()
    {
        Assert.AreEqual(TlsSignatureScheme.RsaPssRsaeSha256, UnitTestCertificates.RsaCertificate().ChooseScheme([0x0403, 0x0402, 0x0804, 0x0401]));
    }

    [TestMethod]
    public void ChooseScheme_NoneTheKeyCanMake_IsNull()
    {
        Assert.IsNull(UnitTestCertificates.RsaCertificate().ChooseScheme([0x0403, 0x0402]));
    }

    [TestMethod]
    [DataRow(TlsSignatureScheme.RsaPssRsaeSha256, true)]
    [DataRow(TlsSignatureScheme.RsaPssRsaeSha384, true)]
    [DataRow(TlsSignatureScheme.RsaPkcs1Sha1, false)]
    [DataRow(TlsSignatureScheme.RsaPkcs1Sha256, false)]
    [DataRow(TlsSignatureScheme.RsaPkcs1Sha384, false)]
    public void Sign_Rsa_VerifiesUnderTheSchemesHashAndPadding(TlsSignatureScheme scheme, bool pss)
    {
        var signature = UnitTestCertificates.RsaCertificate().Sign(scheme, Content);

        var hash = scheme switch
        {
            TlsSignatureScheme.RsaPkcs1Sha1 => HashAlgorithmName.SHA1,
            TlsSignatureScheme.RsaPkcs1Sha384 or TlsSignatureScheme.RsaPssRsaeSha384 => HashAlgorithmName.SHA384,
            _ => HashAlgorithmName.SHA256,
        };
        Assert.IsTrue(UnitTestCertificates.Rsa.VerifyData(Content, signature, hash, pss ? RSASignaturePadding.Pss : RSASignaturePadding.Pkcs1));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    [DataRow(TlsSignatureScheme.DsaSha1, 20)]
    [DataRow(TlsSignatureScheme.DsaSha256, 32)]
    [DataRow(TlsSignatureScheme.DsaSha384, 32)]
    public void Sign_Dsa_VerifiesOverTheSchemesDigestCutToTheLengthOfQ(TlsSignatureScheme scheme, int digestLength)
    {
        var signature = UnitTestCertificates.DsaCertificate().Sign(scheme, Content);

        var digest = scheme switch
        {
            TlsSignatureScheme.DsaSha1 => SHA1.HashData(Content),
            TlsSignatureScheme.DsaSha384 => SHA384.HashData(Content),
            _ => SHA256.HashData(Content),
        };
        Assert.IsTrue(UnitTestCertificates.Dsa.VerifySignature(digest[..digestLength], signature, DSASignatureFormat.Rfc3279DerSequence));
    }

    [TestMethod]
    public void Sign_NullContent_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UnitTestCertificates.RsaCertificate().Sign(TlsSignatureScheme.RsaPssRsaeSha256, null!));
    }
}
