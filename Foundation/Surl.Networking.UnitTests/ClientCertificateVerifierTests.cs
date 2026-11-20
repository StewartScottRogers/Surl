using System.Net.Security;

namespace Surl.Networking;

[TestClass]
public sealed class ClientCertificateVerifierTests
{
    private static readonly FixedTimeProvider Time = new(TestCertificates.Now);

    [TestMethod]
    public void IsTrusted_SignedByATrustAnchor_IsTrue()
    {
        using var authority = TestCertificates.CreateCertificateAuthority("CN=surl test CA");
        using var client = TestCertificates.CreateClientCertificate(authority);

        Assert.IsTrue(new ClientCertificateVerifier([authority], Time).IsTrusted(client));
    }

    [TestMethod]
    public void IsChainTrusted_LeafAndItsIntermediateUnderAnAnchor_IsTrue()
    {
        using var root = TestCertificates.CreateCertificateAuthority("CN=surl test root");
        using var intermediate = TestCertificates.CreateIntermediateAuthority(root, "CN=surl test intermediate");
        using var client = TestCertificates.CreateClientCertificate(intermediate);

        Assert.IsTrue(new ClientCertificateVerifier([root], Time).IsChainTrusted([client.RawData, intermediate.RawData]));
    }

    [TestMethod]
    public void IsChainTrusted_LeafWithoutItsIntermediate_IsFalse()
    {
        using var root = TestCertificates.CreateCertificateAuthority("CN=surl test root");
        using var intermediate = TestCertificates.CreateIntermediateAuthority(root, "CN=surl test intermediate");
        using var client = TestCertificates.CreateClientCertificate(intermediate);

        Assert.IsFalse(new ClientCertificateVerifier([root], Time).IsChainTrusted([client.RawData]));
    }

    [TestMethod]
    public void IsChainTrusted_AnIntermediateTheAnchorsDidNotSign_IsFalse()
    {
        using var root = TestCertificates.CreateCertificateAuthority("CN=surl test root");
        using var stranger = TestCertificates.CreateCertificateAuthority("CN=stranger CA");
        using var intermediate = TestCertificates.CreateIntermediateAuthority(stranger, "CN=stranger intermediate");
        using var client = TestCertificates.CreateClientCertificate(intermediate);

        Assert.IsFalse(new ClientCertificateVerifier([root], Time).IsChainTrusted([client.RawData, intermediate.RawData]));
    }

    [TestMethod]
    public void IsChainTrusted_EmptyChain_IsFalse()
    {
        using var root = TestCertificates.CreateCertificateAuthority("CN=surl test root");

        Assert.IsFalse(new ClientCertificateVerifier([root], Time).IsChainTrusted([]));
    }

    [TestMethod]
    public void IsTrusted_SignedByAStranger_IsFalse()
    {
        using var authority = TestCertificates.CreateCertificateAuthority("CN=surl test CA");
        using var stranger = TestCertificates.CreateCertificateAuthority("CN=stranger CA");
        using var client = TestCertificates.CreateClientCertificate(stranger);

        Assert.IsFalse(new ClientCertificateVerifier([authority], Time).IsTrusted(client));
    }

    [TestMethod]
    public void IsTrusted_NoCertificate_IsFalse()
    {
        using var authority = TestCertificates.CreateCertificateAuthority("CN=surl test CA");

        Assert.IsFalse(new ClientCertificateVerifier([authority], Time).IsTrusted(null));
    }

    [TestMethod]
    public void IsTrusted_NoExtendedKeyUsage_IsTrue()
    {
        using var authority = TestCertificates.CreateCertificateAuthority("CN=surl test CA");
        using var client = TestCertificates.CreateSignedCertificate(
            authority, null, TestCertificates.Now.AddDays(-1), TestCertificates.Now.AddDays(1));

        Assert.IsTrue(new ClientCertificateVerifier([authority], Time).IsTrusted(client));
    }

    [TestMethod]
    public void IsTrusted_ServerAuthenticationOnly_IsFalse()
    {
        using var authority = TestCertificates.CreateCertificateAuthority("CN=surl test CA");
        using var client = TestCertificates.CreateSignedCertificate(
            authority, TestCertificates.ServerAuthenticationUsage, TestCertificates.Now.AddDays(-1), TestCertificates.Now.AddDays(1));

        Assert.IsFalse(new ClientCertificateVerifier([authority], Time).IsTrusted(client));
    }

    [TestMethod]
    public void IsTrusted_ExpiredAtTheProvidersTime_IsFalse()
    {
        using var authority = TestCertificates.CreateCertificateAuthority("CN=surl test CA");
        using var client = TestCertificates.CreateClientCertificate(authority);
        var later = new FixedTimeProvider(TestCertificates.Now.AddDays(5));

        Assert.IsFalse(new ClientCertificateVerifier([authority], later).IsTrusted(client));
    }

    [TestMethod]
    public void Validate_IgnoresThePlatformsVerdict()
    {
        using var authority = TestCertificates.CreateCertificateAuthority("CN=surl test CA");
        using var client = TestCertificates.CreateClientCertificate(authority);
        var verifier = new ClientCertificateVerifier([authority], Time);

        Assert.IsTrue(verifier.Validate(new object(), client, null, SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.IsFalse(verifier.Validate(new object(), null, null, SslPolicyErrors.None));
    }
}
