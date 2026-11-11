using System.Security.Authentication;
using System.Security.Cryptography;
using Surl.Cli;
using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class ServerTlsCompositionTests
{
    [TestMethod]
    public void FindListenUrlWithoutCertificate_NeitherCertNorSelfSigned_IsTheFirstImplicitTlsListenUrl()
    {
        Assert.AreEqual(
            new ListenUrl("mqtts", "127.0.0.1", 0),
            ServerTlsComposition.FindListenUrlWithoutCertificate(
                Parse("http://127.0.0.1:0/", "mqtts://127.0.0.1:0/", "https://127.0.0.1:0/")));
    }

    [TestMethod]
    [DataRow("--self-signed", "https://127.0.0.1:0/")]
    [DataRow("--cert", "cert.pem", "https://127.0.0.1:0/")]
    [DataRow("http://127.0.0.1:0/")]
    public void FindListenUrlWithoutCertificate_CertificateGivenOrNotNeeded_IsNull(params string[] args)
    {
        Assert.IsNull(ServerTlsComposition.FindListenUrlWithoutCertificate(Parse(args)));
    }

    [TestMethod]
    public void FindListenUrlWithoutCertificate_NatssWithoutACertificate_IsTheNatssListenUrl()
    {
        Assert.AreEqual(
            new ListenUrl("natss", "127.0.0.1", 0),
            ServerTlsComposition.FindListenUrlWithoutCertificate(ParseWithNats("nats://127.0.0.1:0/", "natss://127.0.0.1:0/")));
    }

    [TestMethod]
    public void FindListenUrlWithoutCertificate_NatsWithoutACertificate_IsNull()
    {
        Assert.IsNull(ServerTlsComposition.FindListenUrlWithoutCertificate(ParseWithNats("nats://127.0.0.1:0/")));
    }

    [TestMethod]
    public void Compose_NatssWithSelfSigned_ServesAThrowawayCertificateForItsUpgrade()
    {
        using var composition = ServerTlsComposition.Compose(ParseWithNats("--self-signed", "natss://127.0.0.1:0/"), TimeProvider.System);

        Assert.IsNotNull(composition.Settings);
        Assert.IsNotNull(composition.ThrowawayCertificateFingerprint);
    }

    [TestMethod]
    public void Compose_NatsWithSelfSigned_HasNoSettings()
    {
        using var composition = ServerTlsComposition.Compose(ParseWithNats("--self-signed", "nats://127.0.0.1:0/"), TimeProvider.System);

        Assert.IsNull(composition.Settings);
    }

    [TestMethod]
    public void Compose_NoImplicitTlsListenUrl_HasNoSettingsAndReadsNoFile()
    {
        using var composition = ServerTlsComposition.Compose(
            Parse("--cert", "no-such-cert.pem", "--cacert", "no-such-ca.pem", "http://127.0.0.1:0/"), TimeProvider.System);

        Assert.IsNull(composition.Settings);
        Assert.IsNull(composition.ThrowawayCertificateFingerprint);
    }

    [TestMethod]
    public void Compose_SmtpWithSelfSigned_ServesAThrowawayCertificateForStartTls()
    {
        using var composition = ServerTlsComposition.Compose(Parse("--self-signed", "smtp://127.0.0.1:0/"), TimeProvider.System);

        Assert.IsNotNull(composition.Settings);
        Assert.IsNotNull(composition.ThrowawayCertificateFingerprint);
    }

    [TestMethod]
    public void Compose_SmtpWithoutACertificate_HasNoSettings()
    {
        using var composition = ServerTlsComposition.Compose(Parse("smtp://127.0.0.1:0/"), TimeProvider.System);

        Assert.IsNull(composition.Settings);
    }

    [TestMethod]
    [DataRow(true, new[] { "--self-signed", "smtp://127.0.0.1:0/" })]
    [DataRow(true, new[] { "--cert", "cert.pem", "smtp://127.0.0.1:0/" })]
    [DataRow(false, new[] { "smtp://127.0.0.1:0/" })]
    public void IsCertificateConfigured_CommandLine_IsTrueOnlyWithCertOrSelfSigned(bool expected, string[] args)
    {
        Assert.AreEqual(expected, ServerTlsComposition.IsCertificateConfigured(Parse(args)));
    }

    [TestMethod]
    [DataRow(true, new[] { "--self-signed", "smtp://127.0.0.1:0/" })]
    [DataRow(true, new[] { "--tlsuser", "u", "--tlspassword", "p", "smtp://127.0.0.1:0/" })]
    [DataRow(false, new[] { "smtp://127.0.0.1:0/" })]
    public void IsTlsUpgradeAvailable_CommandLine_IsTrueWithACertificateOrTlsSrp(bool expected, string[] args)
    {
        Assert.AreEqual(expected, ServerTlsComposition.IsTlsUpgradeAvailable(Parse(args)));
    }

    [TestMethod]
    [DataRow("https://127.0.0.1:0/")]
    [DataRow("smtp://127.0.0.1:0/")]
    public void Compose_TlsSrp_ServesTheAccountsWithNoCertificate(string listenUrl)
    {
        using var composition = ServerTlsComposition.Compose(Parse("--tlsuser", "jsmith", "--tlspassword", "abc", listenUrl), TimeProvider.System);

        Assert.IsNotNull(composition.Settings?.TlsSrpAccounts);
        Assert.IsNull(composition.ThrowawayCertificateFingerprint);
        Assert.IsNull(composition.QuicCertificate);
        Assert.IsFalse(composition.Settings.TlsSrpAccounts.Find("jsmith"u8.ToArray(), RandomNumberGenerator.Create()).IsSimulated);
    }

    [TestMethod]
    public void Compose_TlsSrpMalformedGroupFile_ThrowsCouldNotReadFileNamingTheFileAndLine()
    {
        var failure = Assert.ThrowsExactly<TlsSrpAccountsException>(() => ServerTlsComposition.Compose(
            Parse("--srp-verifiers", "db", "--srp-groups", "conf", "https://127.0.0.1:0/"), TimeProvider.System, _ => "bad\n"u8.ToArray()));

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, failure.ExitCode);
        StringAssert.StartsWith(failure.Message, "(37) Could not read the --srp-groups file 'conf': line 1: ");
    }

    [TestMethod]
    public void Compose_TlsSrpGroupFileAccessDenied_ThrowsCouldNotReadFile()
    {
        var failure = Assert.ThrowsExactly<TlsSrpAccountsException>(() => ServerTlsComposition.Compose(
            Parse("--srp-verifiers", "db", "--srp-groups", "conf", "https://127.0.0.1:0/"), TimeProvider.System, _ => throw new UnauthorizedAccessException("denied")));

        Assert.AreEqual("(37) Could not read the --srp-groups file 'conf': denied", failure.Message);
    }

    [TestMethod]
    public void ComputeEntry_RandomDrawsASaltStartingWithZero_DrawsAgain()
    {
        using var random = new ScriptedRandomNumberGenerator([[0, .. new byte[15]], [7, .. new byte[15]]]);

        var entry = TlsSrpAccountsComposition.ComputeEntry("jsmith", "abc", random);

        Assert.AreEqual((byte)7, entry.Salt[0]);
        Assert.AreEqual(Cryptography.Srp.SrpGroup.Rfc5054Group2048, entry.Group);
        CollectionAssert.AreEqual(
            Cryptography.Srp.Srp6a.ToUnsignedBigEndian(Cryptography.Srp.Srp6a.ComputeVerifier(entry.Group, entry.Salt, "jsmith"u8, "abc"u8)),
            entry.Verifier);
    }

    [TestMethod]
    public void FindListenUrlWithoutCertificate_TlsSrp_IsNullForTcpAndTheWebTransportListenUrlOtherwise()
    {
        Assert.IsNull(ServerTlsComposition.FindListenUrlWithoutCertificate(Parse("--tlsuser", "u", "--tlspassword", "p", "https://127.0.0.1:0/")));
        Assert.AreEqual(
            new ListenUrl("webtransport", "127.0.0.1", 0),
            ServerTlsComposition.FindListenUrlWithoutCertificate(
                ParseWithWebTransport("--tlsuser", "u", "--tlspassword", "p", "https://127.0.0.1:0/", "webtransport://127.0.0.1:0/")));
    }

    private sealed class ScriptedRandomNumberGenerator(byte[][] draws) : RandomNumberGenerator
    {
        private int next;

        public override void GetBytes(byte[] data) => draws[next++].CopyTo(data, 0);
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
    public void Compose_HttpsWithoutHttp3_HasNoQuicCertificate()
    {
        using var composition = ServerTlsComposition.Compose(Parse("https://127.0.0.1:0/"), TimeProvider.System);

        Assert.IsNull(composition.QuicCertificate);
    }

    [TestMethod]
    public void Compose_HttpsWithHttp3AndSelfSigned_HasTheThrowawayCertificateForQuic()
    {
        using var composition = ServerTlsComposition.Compose(Parse("--self-signed", "--http3", "https://127.0.0.1:0/"), TimeProvider.System);

        var quic = composition.QuicCertificate!;
        Assert.HasCount(1, quic.Chain);
        Assert.AreEqual(composition.ThrowawayCertificateFingerprint, Convert.ToHexString(SHA256.HashData(quic.Chain[0].Span)));
        Assert.IsNotNull(quic.Key);
    }

    [TestMethod]
    public void FindListenUrlWithoutCertificate_WebTransportWithoutACertificate_IsTheWebTransportListenUrl()
    {
        Assert.AreEqual(
            new ListenUrl("webtransport", "127.0.0.1", 0),
            ServerTlsComposition.FindListenUrlWithoutCertificate(ParseWithWebTransport("webtransport://127.0.0.1:0/")));
    }

    [TestMethod]
    public void Compose_WebTransportWithSelfSignedAndNoHttp3_HasTheThrowawayCertificateForQuic()
    {
        using var composition = ServerTlsComposition.Compose(ParseWithWebTransport("--self-signed", "webtransport://127.0.0.1:0/"), TimeProvider.System);

        var quic = composition.QuicCertificate!;
        Assert.AreEqual(composition.ThrowawayCertificateFingerprint, Convert.ToHexString(SHA256.HashData(quic.Chain[0].Span)));
    }

    [TestMethod]
    public void Compose_HttpsWithHttp3AndCert_HasTheCertificateForQuic()
    {
        using var files = TestCertificateFiles.Create();

        using var composition = ServerTlsComposition.Compose(
            Parse("--cert", files.CertificateFile, "--key", files.KeyFile, "--http3", "https://127.0.0.1:0/"), TimeProvider.System);

        Assert.HasCount(1, composition.QuicCertificate!.Chain);
        Assert.IsNotNull(composition.QuicCertificate.Key);
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

    private static SurlCommandLine ParseWithWebTransport(params string[] args) =>
        CommandLineParser.Parse(args, ["webtransport"]).CommandLine ?? throw new AssertFailedException("The command line was refused.");

    private static SurlCommandLine ParseWithNats(params string[] args) =>
        CommandLineParser.Parse(args, ["nats", "natss"]).CommandLine ?? throw new AssertFailedException("The command line was refused.");
}
