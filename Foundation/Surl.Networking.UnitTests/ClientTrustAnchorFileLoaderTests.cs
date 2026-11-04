namespace Surl.Networking;

[TestClass]
public sealed class ClientTrustAnchorFileLoaderTests
{
    [TestMethod]
    public void Load_NullPath_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ClientTrustAnchorFileLoader.Load(null!));

    [TestMethod]
    public void Load_PemWithTwoCertificates_ReturnsBothInFileOrder()
    {
        using var files = new TemporaryTlsFiles();
        using var first = TestCertificates.CreateCertificateAuthority("CN=first");
        using var second = TestCertificates.CreateCertificateAuthority("CN=second");
        var path = files.Write("ca.pem", first.ExportCertificatePem() + "\n" + second.ExportCertificatePem());

        var anchors = ClientTrustAnchorFileLoader.Load(path);

        CollectionAssert.AreEqual(
            new[] { first.Thumbprint, second.Thumbprint },
            anchors.Select(anchor => anchor.Thumbprint).ToArray());
    }

    [TestMethod]
    public void Load_OneDerCertificate_ReturnsIt()
    {
        using var files = new TemporaryTlsFiles();
        using var authority = TestCertificates.CreateCertificateAuthority("CN=authority");
        var path = files.Write("ca.der", authority.RawData);

        var anchors = ClientTrustAnchorFileLoader.Load(path);

        Assert.AreEqual(authority.Thumbprint, anchors.Single().Thumbprint);
    }

    [TestMethod]
    [DataRow("absent.pem")]
    [DataRow("absent-directory/absent.pem")]
    public void Load_FileThatDoesNotExist_IsCaCertificateNotFound(string name)
    {
        using var files = new TemporaryTlsFiles();

        var exception = Assert.ThrowsExactly<TlsFileLoadException>(() => ClientTrustAnchorFileLoader.Load(files.PathOf(name)));

        Assert.AreEqual(TlsFileLoadFailure.CaCertificateNotFound, exception.Failure);
    }

    [TestMethod]
    public void Load_EmptyFile_IsCaCertificateUnreadable()
    {
        using var files = new TemporaryTlsFiles();

        AssertUnreadable(files.Write("ca.pem", string.Empty));
    }

    [TestMethod]
    public void Load_TextWithoutACertificate_IsCaCertificateUnreadable()
    {
        using var files = new TemporaryTlsFiles();

        AssertUnreadable(files.Write("ca.pem", "not a certificate\n"));
    }

    [TestMethod]
    public void Load_PemCertificateBlockThatIsNotACertificate_IsCaCertificateUnreadable()
    {
        using var files = new TemporaryTlsFiles();

        AssertUnreadable(files.Write("ca.pem", "-----BEGIN CERTIFICATE-----\nAAECAw==\n-----END CERTIFICATE-----\n"));
    }

    [TestMethod]
    public void Load_DerValueThatIsNotACertificate_IsCaCertificateUnreadable()
    {
        using var files = new TemporaryTlsFiles();

        AssertUnreadable(files.Write("ca.der", new byte[] { 0x30, 0x03, 0x02, 0x01, 0x00 }));
    }

    [TestMethod]
    public void Load_Directory_IsCaCertificateUnreadable()
    {
        using var files = new TemporaryTlsFiles();

        AssertUnreadable(files.DirectoryPath);
    }

    private static void AssertUnreadable(string path)
    {
        var exception = Assert.ThrowsExactly<TlsFileLoadException>(() => ClientTrustAnchorFileLoader.Load(path));

        Assert.AreEqual(TlsFileLoadFailure.CaCertificateUnreadable, exception.Failure);
    }
}
