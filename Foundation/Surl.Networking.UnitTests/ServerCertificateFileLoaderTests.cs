using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Networking;

[TestClass]
public sealed class ServerCertificateFileLoaderTests
{
    private const string Passphrase = "surl pass";
    private static readonly FixedTimeProvider Time = new(TestCertificates.Now);

    [TestMethod]
    public void Load_NullCertificatePath_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(
            () => ServerCertificateFileLoader.Load(null!, ServerCertificateFormat.Pem, null, ServerKeyFormat.Pem, null));

    [TestMethod]
    public void Load_KeyWithP12_Throws() =>
        Assert.ThrowsExactly<ArgumentException>(
            () => ServerCertificateFileLoader.Load("cert.p12", ServerCertificateFormat.P12, "key.pem", ServerKeyFormat.Pem, null));

    [TestMethod]
    public void Load_PemCertificateWithPkcs8KeyInSameFile_LoadsAServableCertificate()
    {
        using var files = new TemporaryTlsFiles();
        using var rsa = RSA.Create(2048);
        using var certificate = TemporaryTlsFiles.CreateCertificate(rsa);
        var path = files.Write("server.pem", certificate.ExportCertificatePem() + "\n" + rsa.ExportPkcs8PrivateKeyPem());

        using var loaded = ServerCertificateFileLoader.Load(path, ServerCertificateFormat.Pem, null, ServerKeyFormat.Pem, null);
        using var settings = new ServerTlsSettings(loaded.Certificate, loaded.IntermediateCertificates, [], Time);

        Assert.IsTrue(loaded.Certificate.HasPrivateKey);
        Assert.AreEqual(certificate.Thumbprint, loaded.Certificate.Thumbprint);
        Assert.IsEmpty(loaded.IntermediateCertificates);
    }

    [TestMethod]
    public void Load_PemWithIntermediatesAndSeparateKey_KeepsTheIntermediatesInFileOrder()
    {
        using var files = new TemporaryTlsFiles();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var certificate = TemporaryTlsFiles.CreateCertificate(ecdsa);
        using var first = TestCertificates.CreateCertificateAuthority("CN=first");
        using var second = TestCertificates.CreateCertificateAuthority("CN=second");
        var certificatePath = files.Write(
            "chain.pem", certificate.ExportCertificatePem() + "\n" + first.ExportCertificatePem() + "\n" + second.ExportCertificatePem());
        var keyPath = files.Write("key.pem", ecdsa.ExportPkcs8PrivateKeyPem());

        using var loaded = ServerCertificateFileLoader.Load(certificatePath, ServerCertificateFormat.Pem, keyPath, ServerKeyFormat.Pem, null);

        Assert.AreEqual(certificate.Thumbprint, loaded.Certificate.Thumbprint);
        CollectionAssert.AreEqual(
            new[] { first.Thumbprint, second.Thumbprint },
            loaded.IntermediateCertificates.Select(intermediate => intermediate.Thumbprint).ToArray());
    }

    [TestMethod]
    public void Load_PemPkcs1RsaKey_Loads()
    {
        using var files = new TemporaryTlsFiles();
        using var rsa = RSA.Create(2048);
        using var certificate = TemporaryTlsFiles.CreateCertificate(rsa);
        var certificatePath = files.Write("server.pem", certificate.ExportCertificatePem());
        var keyPath = files.Write("key.pem", rsa.ExportRSAPrivateKeyPem());

        using var loaded = ServerCertificateFileLoader.Load(certificatePath, ServerCertificateFormat.Pem, keyPath, ServerKeyFormat.Pem, null);

        Assert.IsTrue(loaded.Certificate.HasPrivateKey);
    }

    [TestMethod]
    [DataRow("P-384")]
    [DataRow("P-521")]
    public void Load_PemSec1EcKey_LoadsAServableCertificate(string curve)
    {
        using var files = new TemporaryTlsFiles();
        using var ecdsa = ECDsa.Create(curve == "P-384" ? ECCurve.NamedCurves.nistP384 : ECCurve.NamedCurves.nistP521);
        using var certificate = TemporaryTlsFiles.CreateCertificate(ecdsa);
        var path = files.Write("server.pem", ecdsa.ExportECPrivateKeyPem() + "\n" + certificate.ExportCertificatePem());

        using var loaded = ServerCertificateFileLoader.Load(path, ServerCertificateFormat.Pem, null, ServerKeyFormat.Pem, null);
        using var settings = new ServerTlsSettings(loaded.Certificate, loaded.IntermediateCertificates, [], Time);

        Assert.IsTrue(loaded.Certificate.HasPrivateKey);
    }

    [TestMethod]
    public void Load_PemEncryptedPkcs8KeyWithPass_Loads()
    {
        using var files = new TemporaryTlsFiles();
        using var rsa = RSA.Create(2048);
        using var certificate = TemporaryTlsFiles.CreateCertificate(rsa);
        var path = files.Write(
            "server.pem", certificate.ExportCertificatePem() + "\n" + TemporaryTlsFiles.EncryptedPkcs8Pem(rsa, Passphrase));

        using var loaded = ServerCertificateFileLoader.Load(path, ServerCertificateFormat.Pem, null, ServerKeyFormat.Pem, Passphrase);

        Assert.IsTrue(loaded.Certificate.HasPrivateKey);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("wrong pass")]
    public void Load_EncryptedKeyWithoutTheRightPass_IsServerCertificateUnusable(string? passphrase)
    {
        using var files = new TemporaryTlsFiles();
        using var rsa = RSA.Create(2048);
        using var certificate = TemporaryTlsFiles.CreateCertificate(rsa);
        var path = files.Write(
            "server.pem", certificate.ExportCertificatePem() + "\n" + TemporaryTlsFiles.EncryptedPkcs8Pem(rsa, Passphrase));

        AssertUnusable(() => ServerCertificateFileLoader.Load(path, ServerCertificateFormat.Pem, null, ServerKeyFormat.Pem, passphrase));
    }

    [TestMethod]
    public void Load_DerCertificateWithDerPkcs8Key_Loads()
    {
        using var files = new TemporaryTlsFiles();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var certificate = TemporaryTlsFiles.CreateCertificate(ecdsa);
        var certificatePath = files.Write("server.der", certificate.RawData);
        var keyPath = files.Write("key.der", ecdsa.ExportPkcs8PrivateKey());

        using var loaded = ServerCertificateFileLoader.Load(certificatePath, ServerCertificateFormat.Der, keyPath, ServerKeyFormat.Der, null);
        using var settings = new ServerTlsSettings(loaded.Certificate, loaded.IntermediateCertificates, [], Time);

        Assert.AreEqual(certificate.Thumbprint, loaded.Certificate.Thumbprint);
        Assert.IsTrue(loaded.Certificate.HasPrivateKey);
    }

    [TestMethod]
    public void Load_DerCertificateWithDerEncryptedPkcs8Key_Loads()
    {
        using var files = new TemporaryTlsFiles();
        using var rsa = RSA.Create(2048);
        using var certificate = TemporaryTlsFiles.CreateCertificate(rsa);
        var certificatePath = files.Write("server.der", certificate.RawData);
        var keyPath = files.Write("key.der", TemporaryTlsFiles.EncryptedPkcs8Der(rsa, Passphrase));

        using var loaded = ServerCertificateFileLoader.Load(certificatePath, ServerCertificateFormat.Der, keyPath, ServerKeyFormat.Der, Passphrase);

        Assert.IsTrue(loaded.Certificate.HasPrivateKey);
    }

    [TestMethod]
    public void Load_P12WithIntermediate_LoadsTheCertificateWithItsKey()
    {
        using var files = new TemporaryTlsFiles();
        using var rsa = RSA.Create(2048);
        using var certificate = TemporaryTlsFiles.CreateCertificate(rsa);
        using var withKey = certificate.CopyWithPrivateKey(rsa);
        using var intermediate = TestCertificates.CreateCertificateAuthority("CN=intermediate");
        using var intermediatePublic = X509CertificateLoader.LoadCertificate(intermediate.RawData);
        var path = files.Write("server.p12", new X509Certificate2Collection { withKey, intermediatePublic }.Export(X509ContentType.Pkcs12, Passphrase)!);

        using var loaded = ServerCertificateFileLoader.Load(path, ServerCertificateFormat.P12, null, ServerKeyFormat.Pem, Passphrase);
        using var settings = new ServerTlsSettings(loaded.Certificate, loaded.IntermediateCertificates, [], Time);

        Assert.AreEqual(certificate.Thumbprint, loaded.Certificate.Thumbprint);
        Assert.AreEqual(intermediate.Thumbprint, loaded.IntermediateCertificates.Single().Thumbprint);
    }

    [TestMethod]
    public void Load_P12WithWrongPass_IsServerCertificateUnusable()
    {
        using var files = new TemporaryTlsFiles();
        using var rsa = RSA.Create(2048);
        using var certificate = TemporaryTlsFiles.CreateCertificate(rsa);
        using var withKey = certificate.CopyWithPrivateKey(rsa);
        var path = files.Write("server.p12", withKey.Export(X509ContentType.Pkcs12, Passphrase));

        AssertUnusable(() => ServerCertificateFileLoader.Load(path, ServerCertificateFormat.P12, null, ServerKeyFormat.Pem, "wrong pass"));
    }

    [TestMethod]
    public void Load_P12WithoutAPrivateKey_IsServerCertificateUnusable()
    {
        using var files = new TemporaryTlsFiles();
        using var rsa = RSA.Create(2048);
        using var certificate = TemporaryTlsFiles.CreateCertificate(rsa);
        var path = files.Write("server.p12", certificate.Export(X509ContentType.Pkcs12, Passphrase));

        AssertUnusable(() => ServerCertificateFileLoader.Load(path, ServerCertificateFormat.P12, null, ServerKeyFormat.Pem, Passphrase));
    }

    [TestMethod]
    public void Load_P12WithUnservableKey_IsServerCertificateUnusable()
    {
        using var files = new TemporaryTlsFiles();
        using var rsa = RSA.Create(1024);
        using var certificate = TemporaryTlsFiles.CreateCertificate(rsa);
        using var withKey = certificate.CopyWithPrivateKey(rsa);
        var path = files.Write("server.p12", withKey.Export(X509ContentType.Pkcs12, Passphrase));

        AssertUnusable(() => ServerCertificateFileLoader.Load(path, ServerCertificateFormat.P12, null, ServerKeyFormat.Pem, Passphrase));
    }

    [TestMethod]
    [DataRow(ServerCertificateFormat.Pem)]
    [DataRow(ServerCertificateFormat.Der)]
    [DataRow(ServerCertificateFormat.P12)]
    public void Load_MissingCertificateFile_IsServerCertificateUnusable(ServerCertificateFormat format)
    {
        using var files = new TemporaryTlsFiles();

        AssertUnusable(() => ServerCertificateFileLoader.Load(files.PathOf("absent.pem"), format, null, ServerKeyFormat.Pem, null));
    }

    [TestMethod]
    public void Load_CertificatePathIsADirectory_IsServerCertificateUnusable()
    {
        using var files = new TemporaryTlsFiles();

        AssertUnusable(() => ServerCertificateFileLoader.Load(files.DirectoryPath, ServerCertificateFormat.Pem, null, ServerKeyFormat.Pem, null));
    }

    [TestMethod]
    public void Load_EmptyCertificatePath_IsServerCertificateUnusable() =>
        AssertUnusable(() => ServerCertificateFileLoader.Load(string.Empty, ServerCertificateFormat.Pem, null, ServerKeyFormat.Pem, null));

    [TestMethod]
    public void Load_MissingKeyFile_IsServerCertificateUnusable()
    {
        using var files = new TemporaryTlsFiles();
        using var rsa = RSA.Create(2048);
        using var certificate = TemporaryTlsFiles.CreateCertificate(rsa);
        var certificatePath = files.Write("server.pem", certificate.ExportCertificatePem());

        AssertUnusable(() => ServerCertificateFileLoader.Load(
            certificatePath, ServerCertificateFormat.Pem, files.PathOf("absent.key"), ServerKeyFormat.Pem, null));
    }

    [TestMethod]
    public void Load_DerFileNamedAsPem_IsServerCertificateUnusable()
    {
        using var files = new TemporaryTlsFiles();
        using var rsa = RSA.Create(2048);
        using var certificate = TemporaryTlsFiles.CreateCertificate(rsa);
        var path = files.Write("server.der", certificate.RawData);

        AssertUnusable(() => ServerCertificateFileLoader.Load(path, ServerCertificateFormat.Pem, null, ServerKeyFormat.Pem, null));
    }

    [TestMethod]
    public void Load_PemFileNamedAsDer_IsServerCertificateUnusable()
    {
        using var files = new TemporaryTlsFiles();
        using var rsa = RSA.Create(2048);
        using var certificate = TemporaryTlsFiles.CreateCertificate(rsa);
        var certificatePath = files.Write("server.pem", certificate.ExportCertificatePem());
        var keyPath = files.Write("key.der", rsa.ExportPkcs8PrivateKey());

        AssertUnusable(() => ServerCertificateFileLoader.Load(certificatePath, ServerCertificateFormat.Der, keyPath, ServerKeyFormat.Der, null));
    }

    [TestMethod]
    public void Load_PemFileNamedAsP12_IsServerCertificateUnusable()
    {
        using var files = new TemporaryTlsFiles();
        using var rsa = RSA.Create(2048);
        using var certificate = TemporaryTlsFiles.CreateCertificate(rsa);
        var path = files.Write("server.pem", certificate.ExportCertificatePem() + "\n" + rsa.ExportPkcs8PrivateKeyPem());

        AssertUnusable(() => ServerCertificateFileLoader.Load(path, ServerCertificateFormat.P12, null, ServerKeyFormat.Pem, null));
    }

    [TestMethod]
    public void Load_PemCertificateBlockThatIsNotACertificate_IsServerCertificateUnusable()
    {
        using var files = new TemporaryTlsFiles();
        var path = files.Write("server.pem", "-----BEGIN CERTIFICATE-----\nAAECAw==\n-----END CERTIFICATE-----\n");

        AssertUnusable(() => ServerCertificateFileLoader.Load(path, ServerCertificateFormat.Pem, null, ServerKeyFormat.Pem, null));
    }

    [TestMethod]
    public void Load_DerCertificateWithoutKey_IsServerCertificateUnusable()
    {
        using var files = new TemporaryTlsFiles();
        using var rsa = RSA.Create(2048);
        using var certificate = TemporaryTlsFiles.CreateCertificate(rsa);
        var path = files.Write("server.der", certificate.RawData);

        AssertUnusable(() => ServerCertificateFileLoader.Load(path, ServerCertificateFormat.Der, null, ServerKeyFormat.Pem, null));
    }

    [TestMethod]
    public void Load_PemWithoutAnyKey_IsServerCertificateUnusable()
    {
        using var files = new TemporaryTlsFiles();
        using var rsa = RSA.Create(2048);
        using var certificate = TemporaryTlsFiles.CreateCertificate(rsa);
        using var intermediate = TestCertificates.CreateCertificateAuthority("CN=intermediate");
        var path = files.Write("server.pem", certificate.ExportCertificatePem() + "\n" + intermediate.ExportCertificatePem());

        AssertUnusable(() => ServerCertificateFileLoader.Load(path, ServerCertificateFormat.Pem, null, ServerKeyFormat.Pem, null));
    }

    [TestMethod]
    public void Load_DerKeyFileThatIsNotDer_IsServerCertificateUnusable()
    {
        using var files = new TemporaryTlsFiles();
        using var rsa = RSA.Create(2048);
        using var certificate = TemporaryTlsFiles.CreateCertificate(rsa);
        var certificatePath = files.Write("server.pem", certificate.ExportCertificatePem());
        var keyPath = files.Write("key.der", rsa.ExportPkcs8PrivateKeyPem());

        AssertUnusable(() => ServerCertificateFileLoader.Load(certificatePath, ServerCertificateFormat.Pem, keyPath, ServerKeyFormat.Der, null));
    }

    [TestMethod]
    public void Load_KeyThatDoesNotMatchTheCertificate_IsServerCertificateUnusable()
    {
        using var files = new TemporaryTlsFiles();
        using var rsa = RSA.Create(2048);
        using var otherRsa = RSA.Create(2048);
        using var certificate = TemporaryTlsFiles.CreateCertificate(rsa);
        using var intermediate = TestCertificates.CreateCertificateAuthority("CN=intermediate");
        var certificatePath = files.Write("server.pem", certificate.ExportCertificatePem() + "\n" + intermediate.ExportCertificatePem());
        var keyPath = files.Write("key.pem", otherRsa.ExportPkcs8PrivateKeyPem());

        AssertUnusable(() => ServerCertificateFileLoader.Load(certificatePath, ServerCertificateFormat.Pem, keyPath, ServerKeyFormat.Pem, null));
    }

    [TestMethod]
    public void Load_EcKeyForAnRsaCertificate_IsServerCertificateUnusable()
    {
        using var files = new TemporaryTlsFiles();
        using var rsa = RSA.Create(2048);
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var certificate = TemporaryTlsFiles.CreateCertificate(rsa);
        var path = files.Write("server.pem", certificate.ExportCertificatePem() + "\n" + ecdsa.ExportECPrivateKeyPem());

        AssertUnusable(() => ServerCertificateFileLoader.Load(path, ServerCertificateFormat.Pem, null, ServerKeyFormat.Pem, null));
    }

    [TestMethod]
    public void Load_RsaKeyForAnEcCertificate_IsServerCertificateUnusable()
    {
        using var files = new TemporaryTlsFiles();
        using var rsa = RSA.Create(2048);
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var certificate = TemporaryTlsFiles.CreateCertificate(ecdsa);
        var path = files.Write("server.pem", certificate.ExportCertificatePem() + "\n" + rsa.ExportRSAPrivateKeyPem());

        AssertUnusable(() => ServerCertificateFileLoader.Load(path, ServerCertificateFormat.Pem, null, ServerKeyFormat.Pem, null));
    }

    [TestMethod]
    public void Load_Rsa1024Key_IsServerCertificateUnusable()
    {
        using var files = new TemporaryTlsFiles();
        using var rsa = RSA.Create(1024);
        using var certificate = TemporaryTlsFiles.CreateCertificate(rsa);
        var path = files.Write("server.pem", certificate.ExportCertificatePem() + "\n" + rsa.ExportPkcs8PrivateKeyPem());

        AssertUnusable(() => ServerCertificateFileLoader.Load(path, ServerCertificateFormat.Pem, null, ServerKeyFormat.Pem, null));
    }

    [TestMethod]
    public void Load_Ed25519Certificate_IsServerCertificateUnusable()
    {
        using var files = new TemporaryTlsFiles();
        using var certificate = TemporaryTlsFiles.CreateCertificateFor(TemporaryTlsFiles.Ed25519SubjectPublicKeyInfo());
        var certificatePath = files.Write("server.der", certificate.RawData);
        var keyPath = files.Write("key.der", TemporaryTlsFiles.Ed25519Pkcs8PrivateKey());

        AssertUnusable(() => ServerCertificateFileLoader.Load(certificatePath, ServerCertificateFormat.Der, keyPath, ServerKeyFormat.Der, null));
    }

    [TestMethod]
    public void Load_Ed25519KeyForAnRsaCertificate_IsServerCertificateUnusable()
    {
        using var files = new TemporaryTlsFiles();
        using var rsa = RSA.Create(2048);
        using var certificate = TemporaryTlsFiles.CreateCertificate(rsa);
        var certificatePath = files.Write("server.der", certificate.RawData);
        var keyPath = files.Write("key.der", TemporaryTlsFiles.Ed25519Pkcs8PrivateKey());

        AssertUnusable(() => ServerCertificateFileLoader.Load(certificatePath, ServerCertificateFormat.Der, keyPath, ServerKeyFormat.Der, null));
    }

    [TestMethod]
    public void Load_EcKeyOnAnUnservedCurve_IsServerCertificateUnusable()
    {
        using var files = new TemporaryTlsFiles();
        using var certificate = TemporaryTlsFiles.CreateCertificateFor(TemporaryTlsFiles.BrainpoolSubjectPublicKeyInfo());
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var path = files.Write("server.pem", certificate.ExportCertificatePem() + "\n" + ecdsa.ExportPkcs8PrivateKeyPem());

        AssertUnusable(() => ServerCertificateFileLoader.Load(path, ServerCertificateFormat.Pem, null, ServerKeyFormat.Pem, null));
    }

    [TestMethod]
    public void TlsFileLoadException_CarriesItsFailure()
    {
        var inner = new IOException("inner");

        var exception = new TlsFileLoadException(TlsFileLoadFailure.CaCertificateUnreadable, "message", inner);

        Assert.AreEqual(TlsFileLoadFailure.CaCertificateUnreadable, exception.Failure);
        Assert.AreEqual("message", exception.Message);
        Assert.AreSame(inner, exception.InnerException);
    }

    private static void AssertUnusable(Action load)
    {
        var exception = Assert.ThrowsExactly<TlsFileLoadException>(load);

        Assert.AreEqual(TlsFileLoadFailure.ServerCertificateUnusable, exception.Failure);
    }
}
