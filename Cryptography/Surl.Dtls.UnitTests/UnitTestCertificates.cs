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

    /// <summary>The start of <see cref="ManualTimeProvider"/>'s clock, around which client certificates are valid.</summary>
    public static readonly DateTimeOffset ClockStart = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private static readonly Lazy<X509Certificate2> LazyClientAuthority = new(() => MakeAuthority("CN=Surl Unit Test Client CA"));
    private static readonly Lazy<X509Certificate2> LazyOtherAuthority = new(() => MakeAuthority("CN=Surl Unit Test Other CA"));

    /// <summary>The <c>--cacert</c> anchor client certificates are issued under.</summary>
    public static X509Certificate2 ClientAuthority => LazyClientAuthority.Value;

    /// <summary>An authority the server does not trust.</summary>
    public static X509Certificate2 OtherAuthority => LazyOtherAuthority.Value;

    /// <summary>
    /// A client certificate for <paramref name="key"/> issued by <paramref name="issuer"/>, valid
    /// for <paramref name="validFor"/> from <paramref name="notBefore"/> (by default a year either
    /// side of <see cref="ClockStart"/>).
    /// </summary>
    public static byte[] IssueClientCertificate(
        AsymmetricAlgorithm key, X509Certificate2? issuer = null, DateTimeOffset? notBefore = null, TimeSpan? validFor = null)
    {
        var request = key is RSA rsa
            ? new X509CertificateRequest("CN=client", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            : new X509CertificateRequest("CN=client", (ECDsa)key, HashAlgorithmName.SHA256);
        return Issue(request, issuer, notBefore, validFor);
    }

    /// <summary>A client certificate whose key is Ed25519 (RFC 8410), which the BCL reads as neither RSA nor ECDSA.</summary>
    public static byte[] IssueEd25519ClientCertificate()
    {
        var publicKey = new PublicKey(
            new Oid("1.3.101.112"), null, new AsnEncodedData(RandomNumberGenerator.GetBytes(32)));
        return Issue(new X509CertificateRequest(new X500DistinguishedName("CN=client"), publicKey, HashAlgorithmName.SHA256), null, null, null);
    }

    private static byte[] Issue(X509CertificateRequest request, X509Certificate2? issuer, DateTimeOffset? notBefore, TimeSpan? validFor)
    {
        var authority = issuer ?? ClientAuthority;
        var start = notBefore ?? ClockStart.AddYears(-1);
        using var issuerKey = authority.GetECDsaPrivateKey()!;
        using var certificate = request.Create(
            authority.SubjectName,
            X509SignatureGenerator.CreateForECDsa(issuerKey),
            start,
            start + (validFor ?? TimeSpan.FromDays(730)),
            RandomNumberGenerator.GetBytes(8));
        return certificate.Export(X509ContentType.Cert);
    }

    private static X509Certificate2 MakeAuthority(string subject)
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new X509CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        return request.CreateSelfSigned(ClockStart.AddYears(-5), ClockStart.AddYears(5));
    }
}
