using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using X509CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Surl.Quic;

/// <summary>A self-signed certificate made in memory with the base class library, and connection options that serve it.</summary>
internal static class UnitTestCertificates
{
    public static byte[] SelfSigned(RSA key)
    {
        var request = new X509CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
        return certificate.Export(X509ContentType.Cert);
    }

    /// <summary>Options serving <paramref name="copies"/> copies of a self-signed certificate for <paramref name="key"/>; more copies make a longer handshake flight.</summary>
    public static QuicServerConnectionOptions Options(RSA key, int copies = 1)
    {
        var certificate = SelfSigned(key);
        return new QuicServerConnectionOptions
        {
            CertificateChain = [.. Enumerable.Repeat<ReadOnlyMemory<byte>>(certificate, copies)],
            CertificateKey = key,
        };
    }
}
