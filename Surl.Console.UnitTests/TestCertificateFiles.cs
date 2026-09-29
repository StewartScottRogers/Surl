using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Console;

/// <summary>
/// A temporary directory holding a self-signed RSA 2048 certificate for <c>127.0.0.1</c> as
/// <c>cert.pem</c>, its PKCS#8 key as <c>key.pem</c>, and <c>garbage.pem</c>, which holds no
/// certificate. Generated at test time; nothing is committed. Disposing it deletes the directory.
/// </summary>
internal sealed class TestCertificateFiles : IDisposable
{
    private readonly DirectoryInfo directory;

    private TestCertificateFiles(DirectoryInfo directory) => this.directory = directory;

    /// <summary>The PEM certificate file.</summary>
    public string CertificateFile => Path.Combine(directory.FullName, "cert.pem");

    /// <summary>The PEM private key file.</summary>
    public string KeyFile => Path.Combine(directory.FullName, "key.pem");

    /// <summary>A file that holds no certificate.</summary>
    public string GarbageFile => Path.Combine(directory.FullName, "garbage.pem");

    /// <summary>A path where no file exists.</summary>
    public string MissingFile => Path.Combine(directory.FullName, "missing.pem");

    /// <summary>
    /// Generates the files in a new temporary directory.
    /// </summary>
    public static TestCertificateFiles Create()
    {
        var files = new TestCertificateFiles(Directory.CreateTempSubdirectory("surl-console-tls-"));
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=127.0.0.1", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(1));

        File.WriteAllText(files.CertificateFile, certificate.ExportCertificatePem());
        File.WriteAllText(files.KeyFile, key.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(files.GarbageFile, "not a certificate");

        return files;
    }

    /// <inheritdoc/>
    public void Dispose() => directory.Delete(recursive: true);
}
