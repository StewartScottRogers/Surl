using System.Security.Cryptography;

namespace Surl.Tls13;

/// <summary>What a <see cref="TlsServerHandshake"/> serves: its certificate and key, the ALPN protocols it agrees, and what its EncryptedExtensions carry.</summary>
public sealed class TlsServerHandshakeOptions
{
    /// <summary>The certificate chain in DER, end-entity certificate first (ADR-0010 section 3).</summary>
    public required IReadOnlyList<ReadOnlyMemory<byte>> CertificateChain { get; init; }

    /// <summary>
    /// The end-entity certificate's private key, which signs CertificateVerify: an <see cref="RSA"/>
    /// key signs with rsa_pss_rsae_sha256, an <see cref="ECDsa"/> key with the scheme of its curve
    /// (P-256, P-384 or P-521) (ADR-0082 decision 3).
    /// </summary>
    public required AsymmetricAlgorithm CertificateKey { get; init; }

    /// <summary>
    /// The ALPN protocols the server agrees, in its order of preference; for HTTP/3, just <c>h3</c>.
    /// A ClientHello offering none of them ends with <see cref="TlsAlert.NoApplicationProtocol"/>
    /// (RFC 9001 section 8.1). Empty means ALPN is not negotiated.
    /// </summary>
    public IReadOnlyList<string> ApplicationProtocols { get; init; } = [];

    /// <summary>
    /// The server's encoded QUIC transport parameters, sent in EncryptedExtensions (RFC 9001
    /// section 8.2). When set, a ClientHello without quic_transport_parameters ends with
    /// <see cref="TlsAlert.MissingExtension"/>. Null means the extension is neither sent nor required.
    /// </summary>
    public byte[]? QuicTransportParameters { get; init; }

    /// <summary>Extensions EncryptedExtensions carries first, in this order, before ALPN and quic_transport_parameters.</summary>
    public IReadOnlyList<TlsExtension> AdditionalEncryptedExtensions { get; init; } = [];
}
