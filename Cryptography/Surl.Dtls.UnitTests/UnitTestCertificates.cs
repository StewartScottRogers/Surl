using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using X509CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Surl.Dtls;

/// <summary>
/// Self-signed server certificates made in memory for the handshake tests: ECDSA on P-256 and
/// P-384, and RSA 2048, each made once.
/// </summary>
internal static class UnitTestCertificates
{
    private static readonly Lazy<DtlsServerCertificate> LazyEcdsaP256 = new(() => Make(ECDsa.Create(ECCurve.NamedCurves.nistP256)));
    private static readonly Lazy<DtlsServerCertificate> LazyEcdsaP384 = new(() => Make(ECDsa.Create(ECCurve.NamedCurves.nistP384)));
    private static readonly Lazy<RSA> LazyRsa2048Key = new(() => RSA.Create(2048));
    private static readonly Lazy<DtlsServerCertificate> LazyRsa2048 = new(() => Make(Rsa2048Key));

    public static RSA Rsa2048Key => LazyRsa2048Key.Value;

    public static DtlsServerCertificate EcdsaP256 => LazyEcdsaP256.Value;

    public static DtlsServerCertificate EcdsaP384 => LazyEcdsaP384.Value;

    public static DtlsServerCertificate Rsa2048 => LazyRsa2048.Value;

    public static byte[] SelfSigned(AsymmetricAlgorithm key)
    {
        var request = key is RSA rsa
            ? new X509CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            : new X509CertificateRequest("CN=localhost", (ECDsa)key, HashAlgorithmName.SHA256);
        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
        return certificate.Export(X509ContentType.Cert);
    }

    private static DtlsServerCertificate Make(AsymmetricAlgorithm key) => new([SelfSigned(key)], key);
}
