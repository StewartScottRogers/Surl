using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Networking;

/// <summary>
/// The process's server-side TLS settings (ADR-0010, sections 3 and 5): the certificate every
/// listener serves, its intermediates, and the trust anchors that verify a client certificate.
/// Every <see cref="System.Net.Security.SslStream"/> handshake Surl runs uses one instance.
/// </summary>
/// <remarks>
/// The certificate is exported to PKCS#12 in memory and re-imported for serving (ADR-0010,
/// section 6), because Schannel cannot serve an ephemeral key. Disposing the settings disposes
/// that re-imported copy, which on Windows deletes its temporary key container.
/// </remarks>
public sealed class ServerTlsSettings : IDisposable
{
    private readonly X509Certificate2 servingCertificate;
    private readonly SslStreamCertificateContext certificateContext;
    private readonly ClientCertificateVerifier? clientCertificateVerifier;

    /// <summary>
    /// Creates the settings for serving <paramref name="certificate"/>.
    /// </summary>
    /// <param name="certificate">
    /// The server certificate, with an exportable private key (as <see cref="System.Security.Cryptography.X509Certificates.CertificateRequest"/>,
    /// a PEM import, or a PKCS#12 import with <see cref="X509KeyStorageFlags.Exportable"/> give). The caller keeps ownership of it.
    /// </param>
    /// <param name="intermediateCertificates">The intermediates sent after it in the handshake, in order; empty for none.</param>
    /// <param name="clientTrustAnchors">
    /// The certificates <c>--cacert</c> names. Empty: no client certificate is requested. Not
    /// empty: every handshake requires a client certificate that chains to one of them.
    /// </param>
    /// <param name="timeProvider">Supplies the time a client certificate chain is verified at.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="certificate"/> has no private key.</exception>
    public ServerTlsSettings(
        X509Certificate2 certificate,
        IReadOnlyList<X509Certificate2> intermediateCertificates,
        IReadOnlyList<X509Certificate2> clientTrustAnchors,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        ArgumentNullException.ThrowIfNull(intermediateCertificates);
        ArgumentNullException.ThrowIfNull(clientTrustAnchors);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (!certificate.HasPrivateKey)
        {
            throw new ArgumentException("A server certificate needs its private key.", nameof(certificate));
        }

        servingCertificate = ServerCertificateImport.ReimportForServing(certificate, OperatingSystem.IsLinux());
        certificateContext = SslStreamCertificateContext.Create(
            servingCertificate, [.. intermediateCertificates], offline: true);
        clientCertificateVerifier = clientTrustAnchors.Count == 0
            ? null
            : new ClientCertificateVerifier(clientTrustAnchors, timeProvider);
    }

    /// <summary>
    /// Whether every handshake requires a client certificate (ADR-0010, section 5).
    /// </summary>
    public bool RequiresClientCertificate => clientCertificateVerifier is not null;

    /// <summary>
    /// The TLS versions every handshake accepts (ADR-0006, section 4); TLS 1.2 and TLS 1.3
    /// unless set.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public TlsVersionRange AcceptedVersions
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = TlsVersionRange.Default;

    /// <summary>
    /// Disposes the re-imported serving certificate. Calling it twice is harmless.
    /// </summary>
    public void Dispose() => servingCertificate.Dispose();

    /// <summary>
    /// The options for one server handshake: the certificate context, the ALPN protocol IDs
    /// offered, client-certificate verification when it is on, the <see cref="AcceptedVersions"/>,
    /// and renegotiation refused (ADR-0006, section 4: client-initiated renegotiation is a
    /// CPU-exhaustion lever). Cipher suites are the operating system's defaults, so
    /// <see cref="SslServerAuthenticationOptions.CipherSuitesPolicy"/> is never set.
    /// </summary>
    /// <param name="applicationProtocols">The ALPN protocol IDs offered; empty offers none.</param>
    /// <returns>Fresh options, safe to hand to one handshake.</returns>
    internal SslServerAuthenticationOptions CreateAuthenticationOptions(IReadOnlyList<SslApplicationProtocol> applicationProtocols) =>
        new()
        {
            ServerCertificateContext = certificateContext,
            ApplicationProtocols = applicationProtocols.Count == 0 ? null : [.. applicationProtocols],
            ClientCertificateRequired = RequiresClientCertificate,
            RemoteCertificateValidationCallback = clientCertificateVerifier is null ? null : clientCertificateVerifier.Validate,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            EnabledSslProtocols = AcceptedVersions.AcceptedProtocols,
            AllowRenegotiation = false,
        };
}
