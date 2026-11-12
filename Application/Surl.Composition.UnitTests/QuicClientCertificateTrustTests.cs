using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Composition;

/// <summary>
/// The <c>--cacert</c> trust check <c>--http3</c> gives QUIC's handshake (BL-741, ADR-0010 section 5),
/// over certificates made in memory with the base class library.
/// </summary>
[TestClass]
public sealed class QuicClientCertificateTrustTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Trusts_CertificateIssuedByTheAnchor_IsTrue()
    {
        using var anchor = Authority("CN=anchor");
        using var client = Issue(anchor, "CN=client", ClientAuthenticationUsage());

        Assert.IsTrue(Trust(anchor).Trusts([client.RawData]));
    }

    [TestMethod]
    public void Trusts_CertificateWithoutExtendedKeyUsage_IsTrue()
    {
        using var anchor = Authority("CN=anchor");
        using var client = Issue(anchor, "CN=client", null);

        Assert.IsTrue(Trust(anchor).Trusts([client.RawData]));
    }

    [TestMethod]
    public void Trusts_CertificateIssuedByAnotherAuthority_IsFalse()
    {
        using var anchor = Authority("CN=anchor");
        using var other = Authority("CN=other");
        using var client = Issue(other, "CN=client", ClientAuthenticationUsage());

        Assert.IsFalse(Trust(anchor).Trusts([client.RawData, other.RawData]));
    }

    [TestMethod]
    public void Trusts_CertificateForServerAuthenticationOnly_IsFalse()
    {
        using var anchor = Authority("CN=anchor");
        using var client = Issue(anchor, "CN=client", new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));

        Assert.IsFalse(Trust(anchor).Trusts([client.RawData]));
    }

    [TestMethod]
    public void Trusts_VerificationTimeAfterTheCertificateExpired_IsFalse()
    {
        using var anchor = Authority("CN=anchor");
        using var client = Issue(anchor, "CN=client", ClientAuthenticationUsage());

        var trust = new QuicClientCertificateTrust([anchor], new FixedTimeProvider(Now.AddDays(30)));

        Assert.IsFalse(trust.Trusts([client.RawData]));
    }

    private static QuicClientCertificateTrust Trust(X509Certificate2 anchor) => new([anchor], new FixedTimeProvider(Now));

    private static X509EnhancedKeyUsageExtension ClientAuthenticationUsage() =>
        new([new Oid("1.3.6.1.5.5.7.3.2")], false);

    private static X509Certificate2 Authority(string subject)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        return request.CreateSelfSigned(Now.AddDays(-10), Now.AddDays(365));
    }

    private static X509Certificate2 Issue(X509Certificate2 issuer, string subject, X509EnhancedKeyUsageExtension? usage)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        if (usage is not null)
        {
            request.CertificateExtensions.Add(usage);
        }

        return request.Create(issuer, Now.AddDays(-1), Now.AddDays(10), [1, 2, 3, 4]);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
