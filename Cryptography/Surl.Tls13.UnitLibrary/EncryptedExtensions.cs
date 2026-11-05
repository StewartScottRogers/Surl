namespace Surl.Tls13;

/// <summary>An EncryptedExtensions message (RFC 8446 section 4.3.1).</summary>
/// <param name="Extensions">The extensions, in the order they are written.</param>
public sealed record EncryptedExtensions(IReadOnlyList<TlsExtension> Extensions)
{
    /// <summary>Writes the handshake message: type, length and body.</summary>
    /// <returns>The message bytes.</returns>
    public byte[] Write() =>
        TlsWriter.Handshake(TlsHandshakeType.EncryptedExtensions, writer => writer.WriteExtensions(Extensions));
}
