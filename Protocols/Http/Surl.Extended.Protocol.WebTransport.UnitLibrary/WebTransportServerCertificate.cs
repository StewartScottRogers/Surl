using System.Security.Cryptography;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>The certificate a <see cref="WebTransportProtocolServer"/> serves in QUIC's TLS 1.3.</summary>
/// <param name="Chain">The certificate, then its intermediates, each DER-encoded.</param>
/// <param name="Key">The certificate's private key: RSA, or ECDSA on P-256, P-384 or P-521.</param>
public sealed record WebTransportServerCertificate(IReadOnlyList<ReadOnlyMemory<byte>> Chain, AsymmetricAlgorithm Key);
