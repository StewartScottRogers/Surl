using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Networking;

[TestClass]
public sealed class ServerTlsSettingsTests
{
    private static readonly FixedTimeProvider Time = new(TestCertificates.Now);

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();

        Assert.ThrowsExactly<ArgumentNullException>(() => new ServerTlsSettings(null!, [], [], Time));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ServerTlsSettings(certificate, null!, [], Time));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ServerTlsSettings(certificate, [], null!, Time));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ServerTlsSettings(certificate, [], [], null!));
    }

    [TestMethod]
    public void Constructor_CertificateWithoutPrivateKey_Throws()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var publicOnly = X509CertificateLoader.LoadCertificate(certificate.RawData);

        Assert.ThrowsExactly<ArgumentException>(() => new ServerTlsSettings(publicOnly, [], [], Time));
    }

    [TestMethod]
    public void CreateAuthenticationOptions_NoTrustAnchors_RequestsNoClientCertificate()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);

        var options = settings.CreateAuthenticationOptions([]);

        Assert.IsFalse(settings.RequiresClientCertificate);
        Assert.IsFalse(options.ClientCertificateRequired);
        Assert.IsNull(options.RemoteCertificateValidationCallback);
        Assert.IsNull(options.ApplicationProtocols);
        Assert.IsNotNull(options.ServerCertificateContext);
        Assert.AreEqual(X509RevocationMode.NoCheck, options.CertificateRevocationCheckMode);
    }

    [TestMethod]
    public void CreateAuthenticationOptions_TrustAnchorsAndAlpn_RequiresAClientCertificateAndOffersTheProtocols()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var authority = TestCertificates.CreateCertificateAuthority("CN=surl test CA");
        using var settings = new ServerTlsSettings(certificate, [], [authority], Time);

        var options = settings.CreateAuthenticationOptions([SslApplicationProtocol.Http11]);

        Assert.IsTrue(settings.RequiresClientCertificate);
        Assert.IsTrue(options.ClientCertificateRequired);
        Assert.IsNotNull(options.RemoteCertificateValidationCallback);
        CollectionAssert.AreEqual(new[] { SslApplicationProtocol.Http11 }, options.ApplicationProtocols);
    }

    [TestMethod]
    public void CreateAuthenticationOptions_Intermediates_AreInTheCertificateContext()
    {
        // A self-signed authority would be trimmed from the context as a root on Linux and macOS.
        // On Windows the context adds intermediates to the user's CA store, so each run's subject
        // is unique: the chain engine would otherwise pick up an earlier run's namesake.
        using var root = TestCertificates.CreateCertificateAuthority("CN=surl test root");
        using var intermediate = TestCertificates.CreateIntermediateAuthority(root, $"CN=surl test intermediate {Guid.NewGuid():N}");
        using var certificate = TestCertificates.CreateSignedCertificate(
            intermediate, TestCertificates.ServerAuthenticationUsage, TestCertificates.Now.AddDays(-1), TestCertificates.Now.AddDays(1));
        using var settings = new ServerTlsSettings(certificate, [intermediate], [], Time);

        var context = settings.CreateAuthenticationOptions([]).ServerCertificateContext!;

        Assert.AreEqual(certificate.Thumbprint, context.TargetCertificate.Thumbprint);
        Assert.AreEqual(intermediate.Thumbprint, context.IntermediateCertificates.Single().Thumbprint);
    }

    [TestMethod]
    public void Dispose_Twice_IsHarmless()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        var settings = new ServerTlsSettings(certificate, [], [], Time);

        settings.Dispose();
        settings.Dispose();

        Assert.IsTrue(certificate.HasPrivateKey);
    }
}
