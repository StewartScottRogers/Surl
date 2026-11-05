namespace Surl.Tls13;

/// <summary>
/// A Certificate message (RFC 8446 section 4.4.2): the certificate chain, end-entity certificate
/// first. Named <c>CertificateMessage</c> so it is not mistaken for an X.509 certificate.
/// </summary>
/// <param name="CertificateRequestContext">Empty for a server's certificate; the CertificateRequest's context for a client's.</param>
/// <param name="Entries">The chain, end-entity certificate first.</param>
public sealed record CertificateMessage(ReadOnlyMemory<byte> CertificateRequestContext, IReadOnlyList<TlsCertificateEntry> Entries)
{
    /// <summary>Writes the handshake message: type, length and body.</summary>
    /// <returns>The message bytes.</returns>
    public byte[] Write() =>
        TlsWriter.Handshake(TlsHandshakeType.Certificate, writer =>
        {
            writer.WriteVector(1, CertificateRequestContext.Span);
            writer.WriteVector(3, list =>
            {
                foreach (var entry in Entries)
                {
                    list.WriteVector(3, entry.CertificateData.Span);
                    list.WriteExtensions(entry.Extensions);
                }
            });
        });
}
