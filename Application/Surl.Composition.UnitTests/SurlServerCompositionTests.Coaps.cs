using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

public sealed partial class SurlServerCompositionTests
{
    // Under TLS-SRP the same key ends the start (58) too: TLS-SRP refuses every key that is not RSA
    // before DTLS is asked (ADR-0122 Amendment 5, BL-1880).
    [TestMethod]
    public async Task ComposeAsync_CoapsUnderTlsSrpWithACertificateDtlsCannotServe_FailsWithCertificateProblem()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP521);
        var request = new CertificateRequest("CN=127.0.0.1", key, HashAlgorithmName.SHA256);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(1));
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["cert.pem"] = Encoding.ASCII.GetBytes(certificate.ExportCertificatePem()),
            ["key.pem"] = Encoding.ASCII.GetBytes(key.ExportPkcs8PrivateKeyPem()),
        };
        var seams = new StartTestSeams().Seams with { AreExtensionsTurnedOn = true, ReadStartFile = path => files[path] };
        var commandLine = CommandLineParser.Parse(
            [
                "--tlsuser", "u", "--tlspassword", "p", "--cert", "cert.pem", "--key", "key.pem",
                "https://127.0.0.1:0", "coaps://127.0.0.1:0",
            ],
            ["coap", "coaps"]).CommandLine!;

        var start = await SurlServerComposition.ComposeAsync(commandLine, seams, TestContext.CancellationToken);

        Assert.IsNull(start.Serving);
        Assert.AreEqual(SurlExitCode.CertificateProblem, start.Failure!.ExitCode);
        Assert.AreEqual(
            "(58) Could not load the server certificate: TLS-SRP signs with an RSA key; the --cert certificate's key is not RSA",
            start.Failure.Lines.Single());
    }

    // A coaps listen URL whose --cert key DTLS cannot sign with (ECDSA on P-521) is refused (58) at the
    // start, through CoapsDtlsCertificate.FindRefusal, and releases the lock (BL-1056).
    [TestMethod]
    public async Task ComposeAsync_CoapsWithACertificateDtlsCannotServe_FailsWithCertificateProblemAndReleasesTheLock()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP521);
        var request = new CertificateRequest("CN=127.0.0.1", key, HashAlgorithmName.SHA256);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(1));
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["cert.pem"] = Encoding.ASCII.GetBytes(certificate.ExportCertificatePem()),
            ["key.pem"] = Encoding.ASCII.GetBytes(key.ExportPkcs8PrivateKeyPem()),
        };
        var startSeams = new StartTestSeams();
        var seams = startSeams.Seams with { AreExtensionsTurnedOn = true, ReadStartFile = path => files[path] };
        var commandLine = CommandLineParser.Parse(
            ["--directory", "served", "--cert", "cert.pem", "--key", "key.pem", "coaps://127.0.0.1:0"], ["coap", "coaps"]).CommandLine!;

        var start = await SurlServerComposition.ComposeAsync(commandLine, seams, TestContext.CancellationToken);

        Assert.IsNull(start.Serving);
        Assert.AreEqual(SurlExitCode.CertificateProblem, start.Failure!.ExitCode);
        StringAssert.StartsWith(start.Failure.Lines.Single(), "(58) Could not load the server certificate: ");
        Assert.IsFalse(startSeams.IsHeld("served"));
    }
}
