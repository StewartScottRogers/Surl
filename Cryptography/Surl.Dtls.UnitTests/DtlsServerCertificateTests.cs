using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Dtls;

/// <summary>
/// <see cref="DtlsServerCertificate"/>: which keys it takes, which scheme it picks from the
/// client's list (ADR-0087 decision 5), and that each signature verifies.
/// </summary>
[TestClass]
public sealed class DtlsServerCertificateTests
{
    private static readonly byte[] Content = [1, 2, 3, 4];

    [TestMethod]
    public void Constructor_EmptyChain_Throws()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        Assert.ThrowsExactly<ArgumentException>(() => new DtlsServerCertificate([], key));
    }

    [TestMethod]
    public void Constructor_EcdsaOnP521_Throws()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP521);

        Assert.ThrowsExactly<ArgumentException>(() => new DtlsServerCertificate([[0x30]], key));
    }

    [TestMethod]
    public void Constructor_Rsa1024_Throws()
    {
        using var key = RSA.Create(1024);

        Assert.ThrowsExactly<ArgumentException>(() => new DtlsServerCertificate([[0x30]], key));
    }

    [TestMethod]
    public void KeyAlgorithm_EachKind_IsReported()
    {
        Assert.AreEqual(DtlsCertificateKeyAlgorithm.Ecdsa, UnitTestCertificates.EcdsaP384.KeyAlgorithm);
        Assert.AreEqual(DtlsCertificateKeyAlgorithm.Rsa, UnitTestCertificates.Rsa2048.KeyAlgorithm);
        Assert.HasCount(1, UnitTestCertificates.Rsa2048.Chain);
    }

    [TestMethod]
    public void ChooseScheme_NoneTheKeyCanMake_IsNull()
    {
        Assert.IsNull(UnitTestCertificates.EcdsaP256.ChooseScheme([0x0503, 0x0804]));
        Assert.IsNull(UnitTestCertificates.EcdsaP384.ChooseScheme([0x0403]));
        Assert.IsNull(UnitTestCertificates.Rsa2048.ChooseScheme([0x0403, 0x0201]));
    }

    [TestMethod]
    [DataRow((ushort)0x0804)]
    [DataRow((ushort)0x0805)]
    [DataRow((ushort)0x0401)]
    [DataRow((ushort)0x0501)]
    public void Sign_EachRsaScheme_VerifiesWithItsHashAndPadding(ushort offered)
    {
        var certificate = UnitTestCertificates.Rsa2048;
        var scheme = certificate.ChooseScheme([0x0403, offered])!.Value;

        var signature = certificate.Sign(scheme, Content);

        using var loaded = PublicKeyOf(certificate);
        using var publicKey = loaded.GetRSAPublicKey()!;
        var hash = offered is 0x0805 or 0x0501 ? HashAlgorithmName.SHA384 : HashAlgorithmName.SHA256;
        var padding = offered is 0x0401 or 0x0501 ? RSASignaturePadding.Pkcs1 : RSASignaturePadding.Pss;
        Assert.AreEqual((DtlsSignatureScheme)offered, scheme);
        Assert.IsTrue(publicKey.VerifyData(Content, signature, hash, padding));
    }

    [TestMethod]
    [DataRow(256, (ushort)0x0403)]
    [DataRow(384, (ushort)0x0503)]
    public void Sign_EachEcdsaCurve_VerifiesAsADerSignature(int size, ushort offered)
    {
        var certificate = size == 256 ? UnitTestCertificates.EcdsaP256 : UnitTestCertificates.EcdsaP384;
        var scheme = certificate.ChooseScheme([0x0804, offered])!.Value;

        var signature = certificate.Sign(scheme, Content);

        using var loaded = PublicKeyOf(certificate);
        using var publicKey = loaded.GetECDsaPublicKey()!;
        var hash = size == 256 ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA384;
        Assert.IsTrue(publicKey.VerifyData(Content, signature, hash, DSASignatureFormat.Rfc3279DerSequence));
    }

    private static X509Certificate2 PublicKeyOf(DtlsServerCertificate certificate) =>
        X509CertificateLoader.LoadCertificate(certificate.Chain[0]);
}
