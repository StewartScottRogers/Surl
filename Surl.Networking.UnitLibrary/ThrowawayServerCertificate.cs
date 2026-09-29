using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Networking;

/// <summary>
/// Makes the throwaway certificate Surl serves when a secure scheme is given no <c>--cert</c>
/// (ADR-0010, section 3): self-signed RSA 2048 with SHA-256 and PKCS#1 padding, subject
/// <c>CN=surl throwaway</c>, valid from one hour before now to thirty days after, not a CA,
/// for server authentication, naming <c>localhost</c>, <c>127.0.0.1</c>, <c>::1</c> and every
/// listen host. It lives only in memory.
/// </summary>
public static class ThrowawayServerCertificate
{
    /// <summary>
    /// The throwaway certificate's subject.
    /// </summary>
    public const string Subject = "CN=surl throwaway";

    private static readonly string[] AlwaysNamedHosts = ["localhost", "127.0.0.1", "::1"];

    /// <summary>
    /// Makes a throwaway certificate with its private key.
    /// </summary>
    /// <param name="timeProvider">Supplies "now" for the validity period.</param>
    /// <param name="listenHosts">The listen URLs' hosts: IP literals (IPv6 with or without brackets) or host names.</param>
    /// <returns>A new certificate the caller owns and disposes.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static X509Certificate2 Create(TimeProvider timeProvider, IEnumerable<string> listenHosts)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(listenHosts);

        using var key = RSA.Create(2048);
        var request = new CertificateRequest(Subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            [new Oid("1.3.6.1.5.5.7.3.1", "Server Authentication")], false));
        request.CertificateExtensions.Add(SubjectAlternativeNamesFor(listenHosts));

        var now = timeProvider.GetUtcNow();

        return request.CreateSelfSigned(now.AddHours(-1), now.AddDays(30));
    }

    /// <summary>
    /// The certificate's SHA-256 fingerprint as upper-case hexadecimal, for the verbose note
    /// <c>Serving a throwaway certificate, SHA-256 &lt;fingerprint&gt;</c>.
    /// </summary>
    /// <param name="certificate">The certificate.</param>
    /// <returns>64 hexadecimal digits.</returns>
    public static string Sha256FingerprintOf(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        return certificate.GetCertHashString(HashAlgorithmName.SHA256);
    }

    private static X509Extension SubjectAlternativeNamesFor(IEnumerable<string> listenHosts)
    {
        var builder = new SubjectAlternativeNameBuilder();
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var host in AlwaysNamedHosts.Concat(listenHosts.Select(host => host.Trim('[', ']'))))
        {
            if (hosts.Add(host))
            {
                AddSubjectAlternativeName(builder, host);
            }
        }

        return builder.Build();
    }

    private static void AddSubjectAlternativeName(SubjectAlternativeNameBuilder builder, string host)
    {
        if (IPAddress.TryParse(host, out var address))
        {
            builder.AddIpAddress(address);
        }
        else
        {
            builder.AddDnsName(host);
        }
    }
}
