using System.Security.Cryptography;

namespace Surl.Conformance;

/// <summary>
/// The certificate the http/3 gateway half serves over QUIC (ADR-0154 decision 4): the generated
/// <c>test-localhost</c> certificate's DER and its private key, which QUIC's handshake takes as bytes
/// and a key rather than as the <c>ServerTlsSettings</c> the TCP listeners use.
/// </summary>
/// <param name="Chain">The server certificate and its intermediates, DER, leaf first.</param>
/// <param name="Key">The certificate's private key.</param>
internal sealed record Http3GatewayCertificate(IReadOnlyList<ReadOnlyMemory<byte>> Chain, AsymmetricAlgorithm Key);
