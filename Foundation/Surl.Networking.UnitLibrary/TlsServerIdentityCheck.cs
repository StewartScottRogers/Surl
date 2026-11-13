using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Networking;

/// <summary>
/// Judges the certificate a server presents to a TLS client (RFC 4513 section 3.1.3, RFC 6125): the
/// chain must build to a system root or to one of the given certificates, and the certificate must name
/// the host. There is no way to skip either check. Remembers why it refused, for the caller to report.
/// </summary>
internal sealed class TlsServerIdentityCheck(IReadOnlyList<X509Certificate2> additionalTrustAnchors, TimeProvider timeProvider)
{
    /// <summary>
    /// Why the last call to <see cref="Validate"/> refused the certificate; <see langword="null"/> if it did not.
    /// </summary>
    public TlsClientFailure? Refusal { get; private set; }

    /// <summary>
    /// The <see cref="RemoteCertificateValidationCallback"/> shape.
    /// </summary>
    /// <param name="sender">Unused.</param>
    /// <param name="certificate">The server's certificate.</param>
    /// <param name="chain">The chain the platform built against the system roots.</param>
    /// <param name="errors">What the platform found wrong.</param>
    /// <returns>Whether the certificate is accepted.</returns>
    public bool Validate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        Refusal = RefusalFor(certificate, chain, errors);

        return Refusal is null;
    }

    private TlsClientFailure? RefusalFor(X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (certificate is null)
        {
            return TlsClientFailure.HandshakeFailed;
        }

        if (errors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors) && !BuildsToGivenCertificate(certificate, chain))
        {
            return TlsClientFailure.UntrustedCertificate;
        }

        return errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch) ? TlsClientFailure.CertificateNameMismatch : null;
    }

    private bool BuildsToGivenCertificate(X509Certificate certificate, X509Chain? presented)
    {
        if (additionalTrustAnchors.Count == 0)
        {
            return false;
        }

        using var custom = new X509Chain();
        custom.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        custom.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        custom.ChainPolicy.VerificationTime = timeProvider.GetUtcNow().UtcDateTime;
        custom.ChainPolicy.CustomTrustStore.AddRange(additionalTrustAnchors.ToArray());

        foreach (var element in presented?.ChainElements ?? (IEnumerable<X509ChainElement>)[])
        {
            custom.ChainPolicy.ExtraStore.Add(element.Certificate);
        }

        return custom.Build(X509CertificateLoader.LoadCertificate(certificate.GetRawCertData()));
    }
}
