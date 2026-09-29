using System.Net.Security;
using System.Security.Authentication;
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
    public void CreateAuthenticationOptions_Defaults_AcceptTls12AndTls13_RefuseRenegotiation_AndKeepTheSystemCipherSuites()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);

        var options = settings.CreateAuthenticationOptions([]);

        Assert.AreSame(TlsVersionRange.Default, settings.AcceptedVersions);
        Assert.AreEqual(SslProtocols.Tls12 | SslProtocols.Tls13, options.EnabledSslProtocols);
        Assert.IsFalse(options.AllowRenegotiation);
#pragma warning disable CA1416 // Reading the policy is what proves it was never set; setting it is what Windows lacks.
        Assert.IsNull(options.CipherSuitesPolicy);
#pragma warning restore CA1416
    }

    [TestMethod]
    public void CreateAuthenticationOptions_AcceptedVersionsSet_EnablesExactlyThoseVersions()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time)
        {
            AcceptedVersions = new TlsVersionRange(SslProtocols.Tls13, SslProtocols.Tls13),
        };

        var options = settings.CreateAuthenticationOptions([]);

        Assert.AreEqual(SslProtocols.Tls13, options.EnabledSslProtocols);
        Assert.IsFalse(options.AllowRenegotiation);
    }

    [TestMethod]
    public void AcceptedVersions_Null_Throws()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();

        Assert.ThrowsExactly<ArgumentNullException>(() => new ServerTlsSettings(certificate, [], [], Time) { AcceptedVersions = null! });
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
        // On Windows the context adds intermediates to the user's CA store, where a same-named
        // certificate with another key makes the chain engine fail ("An unknown chain building
        // error occurred", BL-080). So each run's subjects are unique, and the run removes its
        // intermediate from the store again.
        var run = Guid.NewGuid().ToString("N");
        using var root = TestCertificates.CreateCertificateAuthority($"CN=surl test root {run}");
        using var intermediate = TestCertificates.CreateIntermediateAuthority(root, $"CN=surl test intermediate {run}");
        using var certificate = TestCertificates.CreateSignedCertificate(
            intermediate, TestCertificates.ServerAuthenticationUsage, TestCertificates.Now.AddDays(-1), TestCertificates.Now.AddDays(1));

        try
        {
            using var settings = new ServerTlsSettings(certificate, [intermediate], [], Time);

            var context = settings.CreateAuthenticationOptions([]).ServerCertificateContext!;

            Assert.AreEqual(certificate.Thumbprint, context.TargetCertificate.Thumbprint);
            Assert.AreEqual(intermediate.Thumbprint, context.IntermediateCertificates.Single().Thumbprint);
        }
        finally
        {
            TestCertificates.RemoveFromWindowsIntermediateStores(intermediate);
        }
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
