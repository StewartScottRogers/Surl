namespace Surl.Dtls;

/// <summary>
/// A <c>Certificate</c> body in RawPublicKey mode (RFC 7250 section 3): the key's DER
/// <c>SubjectPublicKeyInfo</c> alone, in place of the certificate list (ADR-0087 decision 3).
/// </summary>
/// <param name="SubjectPublicKeyInfo">The DER <c>SubjectPublicKeyInfo</c>, at least one byte.</param>
public sealed record DtlsRawPublicKeyCertificate(byte[] SubjectPublicKeyInfo)
{
    /// <summary>Reads a RawPublicKey <c>Certificate</c> body.</summary>
    /// <param name="body">The reassembled message body.</param>
    /// <returns>The key.</returns>
    /// <exception cref="DtlsDecodeErrorException">The body is malformed.</exception>
    public static DtlsRawPublicKeyCertificate Read(ReadOnlySpan<byte> body)
    {
        var reader = new DtlsHandshakeBodyReader(body);
        var certificate = new DtlsRawPublicKeyCertificate(reader.ReadVector24(1, DtlsHandshakeHeader.MaximumLength));
        reader.ExpectEnd();
        return certificate;
    }

    /// <summary>Writes the body.</summary>
    /// <returns>The body's bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The key is empty.</exception>
    public byte[] Write()
    {
        var writer = new DtlsHandshakeBodyWriter();
        writer.WriteVector24(SubjectPublicKeyInfo, 1, DtlsHandshakeHeader.MaximumLength);
        return writer.ToArray();
    }
}
