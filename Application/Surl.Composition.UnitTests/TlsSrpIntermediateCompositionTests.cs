using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Surl.Cli;
using Surl.Networking;

namespace Surl.Composition;

[TestClass]
public sealed class TlsSrpIntermediateCompositionTests
{
    [TestMethod]
    public void Compose_TlsSrpOverACertChainWithAnIntermediate_PutsTheIntermediateAfterTheLeaf()
    {
        using var key = RSA.Create(2048);
        var leafRequest = new CertificateRequest("CN=127.0.0.1", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var leaf = leafRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var intermediateKey = RSA.Create(2048);
        var intermediateRequest = new CertificateRequest("CN=intermediate", intermediateKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        intermediateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var intermediate = intermediateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(1));
        var directory = Directory.CreateTempSubdirectory("surl-srp-chain-");
        try
        {
            var certificateFile = Path.Combine(directory.FullName, "chain.pem");
            var keyFile = Path.Combine(directory.FullName, "key.pem");
            File.WriteAllText(certificateFile, leaf.ExportCertificatePem() + "\n" + intermediate.ExportCertificatePem());
            File.WriteAllText(keyFile, key.ExportPkcs8PrivateKeyPem());
            var commandLine = (CommandLineParser.Parse(["--tlsuser", "u", "--tlspassword", "p", "https://127.0.0.1:0/"]).CommandLine
                ?? throw new AssertFailedException("The command line was refused.")) with
            { CertificateFile = certificateFile, KeyFile = keyFile };

            using var composition = ServerTlsComposition.Compose(commandLine, TimeProvider.System);

            var served = typeof(ServerTlsSettings).GetProperty("TlsSrpCertificate", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(composition.Settings) as Surl.TlsSrp.TlsSrpServerCertificate;
            Assert.IsNotNull(served);
            Assert.HasCount(2, served.Chain);
            CollectionAssert.AreEqual(leaf.RawData, served.Chain[0]);
            CollectionAssert.AreEqual(intermediate.RawData, served.Chain[1]);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
