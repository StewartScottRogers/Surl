using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Surl.Networking;

// BL-705: the TLS option files are read through a caller's reader, not the disk.
[TestClass]
public sealed class TlsFileSeamTests
{
    [TestMethod]
    public void ServerCertificateFileLoader_CertificateAndKeyFromTheReader_LoadsThem()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        var files = new Dictionary<string, byte[]>
        {
            ["c.pem"] = Encoding.ASCII.GetBytes(certificate.ExportCertificatePem()),
            ["k.pem"] = Encoding.ASCII.GetBytes(certificate.GetECDsaPrivateKey()!.ExportPkcs8PrivateKeyPem()),
        };

        using var loaded = ServerCertificateFileLoader.Load(
            "c.pem", ServerCertificateFormat.Pem, "k.pem", ServerKeyFormat.Pem, null, path => files[path]);

        Assert.AreEqual(certificate.Thumbprint, loaded.Certificate.Thumbprint);
    }

    [TestMethod]
    public void ServerCertificateFileLoader_DerCertificateAndKeyFromTheReader_LoadsThem()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        var files = new Dictionary<string, byte[]>
        {
            ["c.der"] = certificate.RawData,
            ["k.der"] = certificate.GetECDsaPrivateKey()!.ExportPkcs8PrivateKey(),
        };

        using var loaded = ServerCertificateFileLoader.Load(
            "c.der", ServerCertificateFormat.Der, "k.der", ServerKeyFormat.Der, null, path => files[path]);

        Assert.AreEqual(certificate.Thumbprint, loaded.Certificate.Thumbprint);
    }

    [TestMethod]
    public void ServerCertificateFileLoader_ReaderThatFails_IsServerCertificateUnusable()
    {
        var exception = Assert.ThrowsExactly<TlsFileLoadException>(() => ServerCertificateFileLoader.Load(
            "c.pem", ServerCertificateFormat.Pem, null, ServerKeyFormat.Pem, null, _ => throw new FileNotFoundException()));

        Assert.AreEqual(TlsFileLoadFailure.ServerCertificateUnusable, exception.Failure);
    }

    [TestMethod]
    public void ClientTrustAnchorFileLoader_CertificateFromTheReader_LoadsIt()
    {
        using var authority = TestCertificates.CreateCertificateAuthority("CN=authority");
        var pem = Encoding.ASCII.GetBytes(authority.ExportCertificatePem());

        var anchors = ClientTrustAnchorFileLoader.Load("ca.pem", _ => pem);

        Assert.AreEqual(authority.Thumbprint, anchors.Single().Thumbprint);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ClientTrustAnchorFileLoader_ReaderThatFindsNoFile_IsCaCertificateNotFound(bool isDirectory)
    {
        var exception = Assert.ThrowsExactly<TlsFileLoadException>(() => ClientTrustAnchorFileLoader.Load(
            "ca.pem", _ => throw (isDirectory ? new DirectoryNotFoundException() : new FileNotFoundException())));

        Assert.AreEqual(TlsFileLoadFailure.CaCertificateNotFound, exception.Failure);
    }

    [TestMethod]
    public void ClientTrustAnchorFileLoader_ReaderThatFailsOtherwise_IsCaCertificateUnreadable()
    {
        var exception = Assert.ThrowsExactly<TlsFileLoadException>(() => ClientTrustAnchorFileLoader.Load(
            "ca.pem", _ => throw new IOException("busy")));

        Assert.AreEqual(TlsFileLoadFailure.CaCertificateUnreadable, exception.Failure);
    }
}
