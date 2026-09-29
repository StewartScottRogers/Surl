using System.Security.Authentication;
using Surl.Cli;
using Surl.Networking;

namespace Surl.Console;

[TestClass]
public sealed class ServerTlsCompositionTests
{
    [TestMethod]
    public void Compose_NoImplicitTlsListenUrl_HasNoSettingsAndReadsNoFile()
    {
        using var composition = ServerTlsComposition.Compose(
            Parse("--cert", "no-such-cert.pem", "--cacert", "no-such-ca.pem", "http://127.0.0.1:0/"), TimeProvider.System);

        Assert.IsNull(composition.Settings);
        Assert.IsNull(composition.ThrowawayCertificateFingerprint);
    }

    [TestMethod]
    public void Compose_HttpsWithoutCert_ServesAThrowawayCertificateWithTheDefaultVersions()
    {
        using var composition = ServerTlsComposition.Compose(Parse("https://127.0.0.1:0/"), TimeProvider.System);

        Assert.IsNotNull(composition.Settings);
        Assert.IsFalse(composition.Settings.RequiresClientCertificate);
        Assert.AreEqual(SslProtocols.Tls12, composition.Settings.AcceptedVersions.Lowest);
        Assert.AreEqual(SslProtocols.Tls13, composition.Settings.AcceptedVersions.Highest);
        Assert.AreEqual(SslProtocols.Tls12 | SslProtocols.Tls13, composition.Settings.AcceptedVersions.AcceptedProtocols);
        Assert.IsNotNull(composition.ThrowawayCertificateFingerprint);
        Assert.MatchesRegex("^[0-9A-F]{64}$", composition.ThrowawayCertificateFingerprint);
    }

    [TestMethod]
    public void Compose_HttpsWithTlsVersionOptions_AcceptsTheRangeTheyName()
    {
        using var composition = ServerTlsComposition.Compose(
            Parse("--tlsv1.3", "https://127.0.0.1:0/"), TimeProvider.System);

        Assert.AreEqual(SslProtocols.Tls13, composition.Settings!.AcceptedVersions.Lowest);
        Assert.AreEqual(SslProtocols.Tls13, composition.Settings.AcceptedVersions.Highest);
        Assert.AreEqual(SslProtocols.Tls13, composition.Settings.AcceptedVersions.AcceptedProtocols);
    }

    [TestMethod]
    public void Compose_HttpsWithTlsMax12_AcceptsOnlyTls12()
    {
        using var composition = ServerTlsComposition.Compose(
            Parse("--tls-max", "1.2", "https://127.0.0.1:0/"), TimeProvider.System);

        Assert.AreEqual(SslProtocols.Tls12, composition.Settings!.AcceptedVersions.AcceptedProtocols);
    }

    [TestMethod]
    public void Compose_HttpsWithCertKeyAndCacert_ServesTheCertificateAndRequiresAClientCertificate()
    {
        using var files = TestCertificateFiles.Create();

        using var composition = ServerTlsComposition.Compose(
            Parse("--cert", files.CertificateFile, "--key", files.KeyFile, "--cacert", files.CertificateFile, "https://127.0.0.1:0/"),
            TimeProvider.System);

        Assert.IsNotNull(composition.Settings);
        Assert.IsTrue(composition.Settings.RequiresClientCertificate);
        Assert.IsNull(composition.ThrowawayCertificateFingerprint);
    }

    [TestMethod]
    public void Compose_HttpsWithMissingCert_ThrowsServerCertificateUnusable()
    {
        using var files = TestCertificateFiles.Create();

        var failure = Assert.ThrowsExactly<TlsFileLoadException>(() => ServerTlsComposition.Compose(
            Parse("--cert", files.MissingFile, "--cacert", files.CertificateFile, "https://127.0.0.1:0/"), TimeProvider.System));

        Assert.AreEqual(TlsFileLoadFailure.ServerCertificateUnusable, failure.Failure);
    }

    [TestMethod]
    public void Compose_HttpsWithMissingCacert_ThrowsCaCertificateNotFound()
    {
        using var files = TestCertificateFiles.Create();

        var failure = Assert.ThrowsExactly<TlsFileLoadException>(() => ServerTlsComposition.Compose(
            Parse("--cacert", files.MissingFile, "https://127.0.0.1:0/"), TimeProvider.System));

        Assert.AreEqual(TlsFileLoadFailure.CaCertificateNotFound, failure.Failure);
    }

    [TestMethod]
    public void Dispose_Twice_IsHarmless()
    {
        var composition = ServerTlsComposition.Compose(Parse("https://127.0.0.1:0/"), TimeProvider.System);

        composition.Dispose();
        composition.Dispose();

        Assert.IsNotNull(composition.Settings);
    }

    [TestMethod]
    [DataRow(CertificateFileFormat.Pem, ServerCertificateFormat.Pem)]
    [DataRow(CertificateFileFormat.Der, ServerCertificateFormat.Der)]
    [DataRow(CertificateFileFormat.Pkcs12, ServerCertificateFormat.P12)]
    public void MapCertificateFormat_EachCertType_NamesTheLoadersFormat(CertificateFileFormat given, ServerCertificateFormat expected)
    {
        Assert.AreEqual(expected, ServerTlsComposition.MapCertificateFormat(given));
    }

    [TestMethod]
    [DataRow(CertificateFileFormat.Pem, ServerKeyFormat.Pem)]
    [DataRow(CertificateFileFormat.Der, ServerKeyFormat.Der)]
    public void MapKeyFormat_EachKeyType_NamesTheLoadersFormat(CertificateFileFormat given, ServerKeyFormat expected)
    {
        Assert.AreEqual(expected, ServerTlsComposition.MapKeyFormat(given));
    }

    private static SurlCommandLine Parse(params string[] args) =>
        CommandLineParser.Parse(args).CommandLine ?? throw new AssertFailedException("The command line was refused.");
}
