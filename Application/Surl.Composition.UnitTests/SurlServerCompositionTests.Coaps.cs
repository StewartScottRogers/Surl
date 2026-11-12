using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

public sealed partial class SurlServerCompositionTests
{
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
