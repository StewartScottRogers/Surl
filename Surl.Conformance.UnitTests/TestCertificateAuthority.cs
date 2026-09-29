using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Conformance;

/// <summary>
/// A throwaway certificate authority and a server certificate it signed for <c>127.0.0.1</c>,
/// generated with <see cref="CertificateRequest"/> at test time and written as PEM to a
/// temporary directory: <c>ca.pem</c> for curl's <c>--cacert</c>, <c>server.pem</c> and
/// <c>server.key</c> for surl's <c>--cert</c> and <c>--key</c>. Nothing is committed and nothing
/// enters a certificate store. Disposing it deletes the directory.
/// </summary>
internal sealed class TestCertificateAuthority : IDisposable
{
    private readonly DirectoryInfo directory;

    private TestCertificateAuthority(DirectoryInfo directory) => this.directory = directory;

    /// <summary>The CA certificate, PEM.</summary>
    public string CaCertificateFile => Path.Combine(directory.FullName, "ca.pem");

    /// <summary>The server certificate the CA signed, PEM.</summary>
    public string ServerCertificateFile => Path.Combine(directory.FullName, "server.pem");

    /// <summary>The server certificate's PKCS#8 private key, PEM.</summary>
    public string ServerKeyFile => Path.Combine(directory.FullName, "server.key");

    /// <summary>
    /// Generates the CA and the server certificate in a new temporary directory.
    /// </summary>
    public static TestCertificateAuthority Create()
    {
        var authority = new TestCertificateAuthority(Directory.CreateTempSubdirectory("surl-conformance-tls-"));
        var now = DateTimeOffset.UtcNow;

        using var caKey = RSA.Create(2048);
        var caRequest = new CertificateRequest(
            $"CN=surl conformance test CA {Guid.NewGuid():N}", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        using var ca = caRequest.CreateSelfSigned(now.AddHours(-1), now.AddDays(1));

        using var serverKey = RSA.Create(2048);
        var serverRequest = new CertificateRequest("CN=127.0.0.1", serverKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        serverRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        serverRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        serverRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            [new Oid("1.3.6.1.5.5.7.3.1", "Server Authentication")], false));
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Loopback);
        serverRequest.CertificateExtensions.Add(names.Build());
        using var server = serverRequest.Create(ca, now.AddMinutes(-30), now.AddHours(12), Guid.NewGuid().ToByteArray());

        File.WriteAllText(authority.CaCertificateFile, ca.ExportCertificatePem());
        File.WriteAllText(authority.ServerCertificateFile, server.ExportCertificatePem());
        File.WriteAllText(authority.ServerKeyFile, serverKey.ExportPkcs8PrivateKeyPem());

        return authority;
    }

    /// <inheritdoc/>
    public void Dispose() => directory.Delete(recursive: true);
}
