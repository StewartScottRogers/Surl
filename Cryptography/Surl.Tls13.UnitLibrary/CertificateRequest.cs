namespace Surl.Tls13;

/// <summary>A CertificateRequest message (RFC 8446 section 4.3.2), sent when the server asks for a client certificate.</summary>
/// <param name="CertificateRequestContext">The context the client echoes in its Certificate; empty during the handshake.</param>
/// <param name="Extensions">The extensions, signature_algorithms among them, in the order they are written.</param>
public sealed record CertificateRequest(ReadOnlyMemory<byte> CertificateRequestContext, IReadOnlyList<TlsExtension> Extensions)
{
    /// <summary>Writes the handshake message: type, length and body.</summary>
    /// <returns>The message bytes.</returns>
    public byte[] Write() =>
        TlsWriter.Handshake(TlsHandshakeType.CertificateRequest, writer =>
        {
            writer.WriteVector(1, CertificateRequestContext.Span);
            writer.WriteExtensions(Extensions);
        });
}
