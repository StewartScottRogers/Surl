namespace Surl.Tls13;

/// <summary>A CertificateVerify message (RFC 8446 section 4.4.3): the signature scheme and the signature over the transcript.</summary>
/// <param name="Algorithm">The signature scheme used.</param>
/// <param name="Signature">The signature.</param>
public sealed record CertificateVerify(TlsSignatureScheme Algorithm, ReadOnlyMemory<byte> Signature)
{
    /// <summary>Writes the handshake message: type, length and body.</summary>
    /// <returns>The message bytes.</returns>
    public byte[] Write() =>
        TlsWriter.Handshake(TlsHandshakeType.CertificateVerify, writer =>
        {
            writer.WriteUInt16((ushort)Algorithm);
            writer.WriteVector(2, Signature.Span);
        });
}
