using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Composition;

[TestClass]
public sealed class QuicServerCertificateTests
{
    [TestMethod]
    public void From_EcdsaCertificateWithAnIntermediate_IsItsDerThenTheIntermediatesAndItsKey()
    {
        using var certificate = Create(ECDsa.Create(ECCurve.NamedCurves.nistP256));
        using var intermediate = Create(ECDsa.Create(ECCurve.NamedCurves.nistP384));

        var quic = QuicServerCertificate.From(certificate, [intermediate]);
        using var key = quic.Key;

        Assert.HasCount(2, quic.Chain);
        CollectionAssert.AreEqual(certificate.RawData, quic.Chain[0].ToArray());
        CollectionAssert.AreEqual(intermediate.RawData, quic.Chain[1].ToArray());
        Assert.IsInstanceOfType<ECDsa>(key);
    }

    [TestMethod]
    public void From_RsaCertificate_HasItsRsaKey()
    {
        using var certificate = Create(RSA.Create(2048));

        using var key = QuicServerCertificate.From(certificate, []).Key;

        Assert.IsInstanceOfType<RSA>(key);
    }

    [TestMethod]
    public void From_CertificateWithoutAPrivateKey_Throws()
    {
        using var withKey = Create(ECDsa.Create(ECCurve.NamedCurves.nistP256));
        using var publicOnly = X509CertificateLoader.LoadCertificate(withKey.RawData);

        Assert.ThrowsExactly<ArgumentException>(() => QuicServerCertificate.From(publicOnly, []));
    }

    [TestMethod]
    public void From_NullArgument_Throws()
    {
        using var certificate = Create(ECDsa.Create(ECCurve.NamedCurves.nistP256));

        Assert.ThrowsExactly<ArgumentNullException>(() => QuicServerCertificate.From(null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => QuicServerCertificate.From(certificate, null!));
    }

    private static X509Certificate2 Create(AsymmetricAlgorithm key)
    {
        using (key)
        {
            var request = key is RSA rsa
                ? new CertificateRequest("CN=surl", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)
                : new CertificateRequest("CN=surl", (ECDsa)key, HashAlgorithmName.SHA256);
            var now = DateTimeOffset.UtcNow;
            return request.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
        }
    }
}
