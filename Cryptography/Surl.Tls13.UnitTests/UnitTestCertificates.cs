using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using X509CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Surl.Tls13;

/// <summary>Self-signed certificates for handshake tests, made in memory with the base class library, and the options that serve them.</summary>
internal static class UnitTestCertificates
{
    public static byte[] SelfSigned(AsymmetricAlgorithm key)
    {
        var request = key is RSA rsa
            ? new X509CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            : new X509CertificateRequest("CN=localhost", (ECDsa)key, HashAlgorithmName.SHA256);
        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
        return certificate.Export(X509ContentType.Cert);
    }

    /// <summary>Options serving a self-signed certificate for <paramref name="key"/>, agreeing ALPN h3 and sending transport parameters.</summary>
    public static TlsServerHandshakeOptions QuicOptions(AsymmetricAlgorithm key) => new()
    {
        CertificateChain = [SelfSigned(key)],
        CertificateKey = key,
        ApplicationProtocols = ["h3"],
        QuicTransportParameters = [0x0F, 0x01, 0xAB],
    };
}
