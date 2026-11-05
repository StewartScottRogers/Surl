namespace Surl.Dtls;

/// <summary>
/// A <c>CertificateRequest</c> body (RFC 5246 section 7.4.4): the certificate types, signature
/// schemes and certificate authorities the server accepts.
/// </summary>
/// <param name="CertificateTypes">The certificate types, such as <c>rsa_sign</c> (1) and <c>ecdsa_sign</c> (64), at least one.</param>
/// <param name="SignatureSchemes">The signature schemes accepted, at least one.</param>
/// <param name="CertificateAuthorities">The DER distinguished names of the accepted authorities; empty (ADR-0087 decision 3).</param>
public sealed record DtlsCertificateRequest(
    byte[] CertificateTypes, IReadOnlyList<ushort> SignatureSchemes, IReadOnlyList<byte[]> CertificateAuthorities)
{
    /// <summary>Reads a <c>CertificateRequest</c> body.</summary>
    /// <param name="body">The reassembled message body.</param>
    /// <returns>The request.</returns>
    /// <exception cref="DtlsDecodeErrorException">The body is malformed.</exception>
    public static DtlsCertificateRequest Read(ReadOnlySpan<byte> body)
    {
        var reader = new DtlsHandshakeBodyReader(body);
        var certificateTypes = reader.ReadVector8(1, byte.MaxValue);
        var signatureSchemes = reader.ReadUInt16Vector16(2, ushort.MaxValue - 1);
        var authoritiesReader = new DtlsHandshakeBodyReader(reader.ReadVector16(0, ushort.MaxValue));
        reader.ExpectEnd();

        var authorities = new List<byte[]>();
        while (!authoritiesReader.IsAtEnd)
        {
            authorities.Add(authoritiesReader.ReadVector16(1, ushort.MaxValue));
        }

        return new DtlsCertificateRequest(certificateTypes, signatureSchemes, authorities);
    }

    /// <summary>Writes the body.</summary>
    /// <returns>The body's bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A field is outside its bounds.</exception>
    public byte[] Write()
    {
        var authoritiesWriter = new DtlsHandshakeBodyWriter();
        foreach (var authority in CertificateAuthorities)
        {
            authoritiesWriter.WriteVector16(authority, 1, ushort.MaxValue);
        }

        var writer = new DtlsHandshakeBodyWriter();
        writer.WriteVector8(CertificateTypes, 1, byte.MaxValue);
        writer.WriteUInt16Vector16(SignatureSchemes, 2, ushort.MaxValue - 1);
        writer.WriteVector16(authoritiesWriter.ToArray(), 0, ushort.MaxValue);
        return writer.ToArray();
    }
}
