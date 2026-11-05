using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Dtls;

/// <summary>
/// <see cref="DtlsClientCertificate"/>: the chain check and the <c>CertificateVerify</c> check under
/// each scheme <c>CertificateRequest</c> names (RFC 5246 section 7.4.8).
/// </summary>
[TestClass]
public sealed class DtlsClientCertificateTests
{
    private static readonly byte[] Content = [1, 2, 3, 4];

    [TestMethod]
    [DataRow((ushort)0x0401, true)]
    [DataRow((ushort)0x0501, true)]
    [DataRow((ushort)0x0804, true)]
    [DataRow((ushort)0x0805, true)]
    [DataRow((ushort)0x0403, false)]
    public void Verifies_RsaKey_AcceptsOnlyRsaSchemesWithTheirHashAndPadding(ushort scheme, bool expected)
    {
        using var key = RSA.Create(2048);
        using var certificate = Validate(key);
        var hash = scheme is 0x0501 or 0x0805 ? HashAlgorithmName.SHA384 : HashAlgorithmName.SHA256;
        var padding = scheme is 0x0401 or 0x0501 ? RSASignaturePadding.Pkcs1 : RSASignaturePadding.Pss;

        var verifies = certificate.Verifies(new DtlsCertificateVerify(scheme, key.SignData(Content, hash, padding)), Content);

        Assert.AreEqual(expected, verifies);
    }

    [TestMethod]
    [DataRow((ushort)0x0403, true)]
    [DataRow((ushort)0x0503, true)]
    [DataRow((ushort)0x0804, false)]
    public void Verifies_EcdsaKey_AcceptsOnlyEcdsaSchemes(ushort scheme, bool expected)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var certificate = Validate(key);
        var hash = scheme == 0x0503 ? HashAlgorithmName.SHA384 : HashAlgorithmName.SHA256;

        var verifies = certificate.Verifies(
            new DtlsCertificateVerify(scheme, key.SignData(Content, hash, DSASignatureFormat.Rfc3279DerSequence)), Content);

        Assert.AreEqual(expected, verifies);
    }

    [TestMethod]
    public void Validate_ChainWithItsAuthority_ReturnsTheLeaf()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var leaf = UnitTestCertificates.IssueClientCertificate(key);

        using var certificate = DtlsClientCertificate.Validate(
            [leaf, UnitTestCertificates.ClientAuthority.RawData],
            [UnitTestCertificates.ClientAuthority],
            UnitTestCertificates.ClockStart);

        CollectionAssert.AreEqual(leaf, certificate.Leaf.RawData);
    }

    [TestMethod]
    public void Validate_SecondCertificateDoesNotRead_ThrowsBadCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var alert = Assert.ThrowsExactly<DtlsAlertException>(() => DtlsClientCertificate.Validate(
            [UnitTestCertificates.IssueClientCertificate(key), [0x30, 0x00]],
            [UnitTestCertificates.ClientAuthority],
            UnitTestCertificates.ClockStart));

        Assert.AreEqual(DtlsAlertDescription.BadCertificate, alert.Description);
    }

    private static DtlsClientCertificate Validate(AsymmetricAlgorithm key) => DtlsClientCertificate.Validate(
        [UnitTestCertificates.IssueClientCertificate(key)],
        new X509Certificate2Collection(UnitTestCertificates.ClientAuthority),
        UnitTestCertificates.ClockStart);
}
