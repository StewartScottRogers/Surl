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

    /// <summary>
    /// Reads a CertificateVerify message's body, the message without its four-byte handshake
    /// header. An overrunning length or trailing bytes is <see cref="TlsAlert.DecodeError"/>.
    /// </summary>
    internal static CertificateVerify ReadBody(ReadOnlySpan<byte> body)
    {
        var reader = new TlsReader(body);
        var algorithm = (TlsSignatureScheme)reader.ReadUInt16();
        var signature = reader.ReadVector16(0).ToArray();
        reader.ExpectEnd("the CertificateVerify message");
        return new CertificateVerify(algorithm, signature);
    }
}
