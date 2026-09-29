using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Networking;

[TestClass]
public sealed class ThrowawayServerCertificateTests
{
    private static readonly DateTimeOffset Now = TestCertificates.Now;

    [TestMethod]
    public void Create_IsTheCertificateTheAdrDescribes()
    {
        using var certificate = ThrowawayServerCertificate.Create(new FixedTimeProvider(Now), []);

        Assert.AreEqual(ThrowawayServerCertificate.Subject, certificate.Subject);
        Assert.AreEqual(certificate.Subject, certificate.Issuer);
        Assert.IsTrue(certificate.HasPrivateKey);
        Assert.AreEqual(2048, certificate.GetRSAPublicKey()!.KeySize);
        Assert.AreEqual("1.2.840.113549.1.1.11", certificate.SignatureAlgorithm.Value);
        AssertWithinOneSecond(Now.AddHours(-1), certificate.NotBefore);
        AssertWithinOneSecond(Now.AddDays(30), certificate.NotAfter);
        Assert.IsFalse(certificate.Extensions.OfType<X509BasicConstraintsExtension>().Single().CertificateAuthority);
        Assert.AreEqual(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            certificate.Extensions.OfType<X509KeyUsageExtension>().Single().KeyUsages);
        Assert.AreEqual(
            "1.3.6.1.5.5.7.3.1",
            certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single().EnhancedKeyUsages[0].Value);
    }

    [TestMethod]
    public void Create_NamesLoopbackAndEveryListenHostOnce()
    {
        using var certificate = ThrowawayServerCertificate.Create(
            new FixedTimeProvider(Now), ["surl.example", "[::1]", "LOCALHOST", "192.0.2.7", "fe80::1"]);

        var names = certificate.Extensions.OfType<X509SubjectAlternativeNameExtension>().Single();

        CollectionAssert.AreEqual(new[] { "localhost", "surl.example" }, names.EnumerateDnsNames().ToArray());
        CollectionAssert.AreEqual(
            new[] { IPAddress.Loopback, IPAddress.IPv6Loopback, IPAddress.Parse("192.0.2.7"), IPAddress.Parse("fe80::1") },
            names.EnumerateIPAddresses().ToArray());
    }

    [TestMethod]
    public void Create_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ThrowawayServerCertificate.Create(null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => ThrowawayServerCertificate.Create(new FixedTimeProvider(Now), null!));
    }

    [TestMethod]
    public void Sha256FingerprintOf_IsTheUpperCaseHexOfTheCertificatesHash()
    {
        using var certificate = ThrowawayServerCertificate.Create(new FixedTimeProvider(Now), []);

        var fingerprint = ThrowawayServerCertificate.Sha256FingerprintOf(certificate);

        Assert.AreEqual(Convert.ToHexString(SHA256.HashData(certificate.RawData)), fingerprint);
        Assert.HasCount(64, fingerprint);
    }

    [TestMethod]
    public void Sha256FingerprintOf_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ThrowawayServerCertificate.Sha256FingerprintOf(null!));

    // A certificate holds whole seconds, so its validity is compared to the second.
    private static void AssertWithinOneSecond(DateTimeOffset expected, DateTime actual) =>
        Assert.IsLessThan(1.0, Math.Abs((expected.UtcDateTime - actual.ToUniversalTime()).TotalSeconds));

    [TestMethod]
    public void Create_CanBeServed()
    {
        using var certificate = ThrowawayServerCertificate.Create(new FixedTimeProvider(Now), ["127.0.0.1"]);

        using var settings = new ServerTlsSettings(certificate, [], [], new FixedTimeProvider(Now));

        Assert.AreEqual(certificate.Thumbprint, settings.CreateAuthenticationOptions([]).ServerCertificateContext!.TargetCertificate.Thumbprint);
    }
}
