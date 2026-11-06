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

    /// <summary>
    /// Reads a Certificate message's body, the message without its four-byte handshake header. An
    /// overrunning length, an empty cert_data or trailing bytes is <see cref="TlsAlert.DecodeError"/>.
    /// </summary>
    internal static CertificateMessage ReadBody(ReadOnlySpan<byte> body)
    {
        var reader = new TlsReader(body);
        var context = reader.ReadVector8(0).ToArray();
        var list = new TlsReader(reader.ReadVector24(0));
        reader.ExpectEnd("the Certificate message");
        var entries = new List<TlsCertificateEntry>();
        while (!list.IsAtEnd)
        {
            var certificateData = list.ReadVector24(1).ToArray();
            entries.Add(new TlsCertificateEntry(certificateData, TlsExtensionBlock.Read(list.ReadVector16(0))));
        }

        return new CertificateMessage(context, entries);
    }
}
