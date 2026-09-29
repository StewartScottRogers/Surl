using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Networking;

/// <summary>
/// Certificates made at test time with <see cref="CertificateRequest"/> (ADR-0010, section 6):
/// never read from a certificate store or a committed file. Every certificate is valid around
/// <see cref="Now"/>, the instant the tests' <see cref="FixedTimeProvider"/> reports.
/// </summary>
internal static class TestCertificates
{
    // Taken once when the tests start, so the platform's TLS stack, which reads the real clock
    // when it chooses a certificate to present, sees every certificate as valid.
    public static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static readonly Oid ServerAuthentication = new("1.3.6.1.5.5.7.3.1");
    private static readonly Oid ClientAuthentication = new("1.3.6.1.5.5.7.3.2");

    /// <summary>A self-signed ECDSA P-256 server certificate for <c>localhost</c>.</summary>
    public static X509Certificate2 CreateEcdsaServerCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([ServerAuthentication], false));

        return request.CreateSelfSigned(Now.AddDays(-1), Now.AddDays(1));
    }

    /// <summary>A self-signed RSA 2048 server certificate for <c>localhost</c>.</summary>
    public static X509Certificate2 CreateRsaServerCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([ServerAuthentication], false));

        return request.CreateSelfSigned(Now.AddDays(-1), Now.AddDays(1));
    }

    /// <summary>A self-signed ECDSA P-256 certificate authority.</summary>
    public static X509Certificate2 CreateCertificateAuthority(string subject)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        return request.CreateSelfSigned(Now.AddDays(-10), Now.AddDays(10));
    }

    /// <summary>
    /// An ECDSA P-256 intermediate certificate authority signed by <paramref name="root"/>, with
    /// its private key. Unlike a self-signed authority, no platform trims it from a chain as a root.
    /// </summary>
    public static X509Certificate2 CreateIntermediateAuthority(X509Certificate2 root, string subject)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(root, true, false));

        using var signed = request.Create(root, Now.AddDays(-5), Now.AddDays(5), RandomNumberGenerator.GetBytes(8));
        using var withKey = signed.CopyWithPrivateKey(key);

        return X509CertificateLoader.LoadPkcs12(
            withKey.Export(X509ContentType.Pkcs12),
            password: null,
            ServerCertificateImport.KeyStorageFlagsFor(OperatingSystem.IsLinux()) | X509KeyStorageFlags.Exportable);
    }

    /// <summary>
    /// A leaf certificate signed by <paramref name="authority"/>, with its private key, valid
    /// from <paramref name="notBefore"/> to <paramref name="notAfter"/>. With
    /// <paramref name="extendedKeyUsage"/> <see langword="null"/> it carries no extended key usage.
    /// </summary>
    public static X509Certificate2 CreateSignedCertificate(
        X509Certificate2 authority, Oid? extendedKeyUsage, DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=surl test leaf", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(authority, true, false));

        if (extendedKeyUsage is not null)
        {
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([extendedKeyUsage], false));
        }

        using var signed = request.Create(authority, notBefore, notAfter, RandomNumberGenerator.GetBytes(8));
        using var withKey = signed.CopyWithPrivateKey(key);

        // Schannel cannot present an ephemeral key, so the key goes through PKCS#12 as the
        // server's does; it stays exportable, so a leaf can also be handed to ServerTlsSettings.
        return X509CertificateLoader.LoadPkcs12(
            withKey.Export(X509ContentType.Pkcs12),
            password: null,
            ServerCertificateImport.KeyStorageFlagsFor(OperatingSystem.IsLinux()) | X509KeyStorageFlags.Exportable);
    }

    /// <summary>A client certificate for client authentication, valid around <see cref="Now"/>.</summary>
    public static X509Certificate2 CreateClientCertificate(X509Certificate2 authority) =>
        CreateSignedCertificate(authority, ClientAuthentication, Now.AddDays(-1), Now.AddDays(1));

    /// <summary>The client-authentication extended key usage.</summary>
    public static Oid ClientAuthenticationUsage => ClientAuthentication;

    /// <summary>The server-authentication extended key usage.</summary>
    public static Oid ServerAuthenticationUsage => ServerAuthentication;
}
