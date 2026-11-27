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
    /// The ALPN protocols the server agrees, in its order of preference. Over QUIC the list is
    /// <c>h3</c> for HTTP/3 or <c>doq</c> for DNS over QUIC; over TCP it is the listener's list,
    /// such as <c>h2</c> then <c>http/1.1</c> for <c>https</c>. Empty means ALPN is neither checked
    /// nor answered. When the list is not empty, the first of its protocols that the ClientHello
    /// offers is agreed, and a ClientHello whose ALPN extension offers none of them ends with
    /// <see cref="TlsAlert.NoApplicationProtocol"/>. A ClientHello with no ALPN extension then ends
    /// with the same alert over QUIC (<see cref="QuicTransportParameters"/> set, RFC 9001
    /// section 8.1) and is served without ALPN over TCP (null, ADR-0010 section 4).
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

    /// <summary>
    /// Decides whether a client's certificate chain (DER, end-entity certificate first) is trusted,
    /// as ADR-0010 section 5 does for <c>--cacert</c>. When set, the server sends a
    /// CertificateRequest and requires a client certificate: an empty client Certificate ends with
    /// <see cref="TlsAlert.CertificateRequired"/>, and a chain this returns false for with
    /// <see cref="TlsAlert.BadCertificate"/>. Null means no client certificate is requested.
    /// </summary>
    public Func<IReadOnlyList<ReadOnlyMemory<byte>>, bool>? TrustsClientCertificateChain { get; init; }
}
