namespace Surl.Tls13;

/// <summary>One CertificateEntry of a Certificate message (RFC 8446 section 4.4.2): an X.509 certificate in DER and its extensions.</summary>
/// <param name="CertificateData">The DER-encoded certificate.</param>
/// <param name="Extensions">The entry's extensions, usually none.</param>
public sealed record TlsCertificateEntry(ReadOnlyMemory<byte> CertificateData, IReadOnlyList<TlsExtension> Extensions);
