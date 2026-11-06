using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Dtls;

/// <summary>
/// What one DTLS server session needs from its caller (ADR-0087 decisions 3, 4, 7 and 9).
/// </summary>
/// <param name="Certificate">
/// The certificate and key the server presents, or <see langword="null"/> when it serves only the
/// PSK suites (ADR-0087 decision 3: accounts and no certificate).
/// </param>
/// <param name="CookieSecrets">The listener's cookie secrets, shared by every association.</param>
/// <param name="TimeProvider">The clock for the retransmission timer and the handshake limit.</param>
/// <param name="HandshakeTimeout">
/// The head timeout: how long a handshake may take from the first hello carrying a valid cookie.
/// </param>
/// <param name="MaximumMessageLength">
/// The <c>--max-message</c> limit on handshake message bodies buffered together.
/// </param>
public sealed record DtlsServerOptions(
    DtlsServerCertificate? Certificate,
    DtlsCookieSecrets CookieSecrets,
    TimeProvider TimeProvider,
    TimeSpan HandshakeTimeout,
    int MaximumMessageLength)
{
    /// <summary>The retransmission timer's first value: 1 second (RFC 6347 section 4.2.4.1).</summary>
    public static readonly TimeSpan InitialRetransmissionTimeout = TimeSpan.FromSeconds(1);

    /// <summary>The retransmission timer's cap: 60 seconds (RFC 6347 section 4.2.4.1).</summary>
    public static readonly TimeSpan MaximumRetransmissionTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Whether CCM suites may be served; <see cref="AesCcm.IsSupported"/> unless a test says
    /// otherwise.
    /// </summary>
    public bool AesCcmSupported { get; init; } = AesCcm.IsSupported;

    /// <summary>
    /// Looks up the key of a PSK identity, or <see langword="null"/> (the default) when no account
    /// exists, so no PSK suite is live (ADR-0087 decisions 2 and 3).
    /// </summary>
    public DtlsPreSharedKeyLookup? PreSharedKeys { get; init; }

    /// <summary>
    /// The <c>--cacert</c> anchors a client certificate must chain to, or <see langword="null"/>
    /// (the default) when no client certificate is asked for. When set, every certificate or
    /// RawPublicKey handshake sends <c>CertificateRequest</c> and requires a client certificate
    /// (ADR-0087 decision 3, ADR-0010 section 5); a PSK handshake never asks for one.
    /// </summary>
    public X509Certificate2Collection? ClientCertificateAuthorities { get; init; }
}
