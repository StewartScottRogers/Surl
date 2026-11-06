namespace Surl.Dtls;

/// <summary>
/// A <c>CertificateVerify</c> body (RFC 5246 section 7.4.8): the client's signature over the
/// handshake messages so far.
/// </summary>
/// <param name="SignatureScheme">The signature scheme, such as <c>rsa_pss_rsae_sha256</c> (0x0804).</param>
/// <param name="Signature">The signature, at most 2^16 - 1 bytes.</param>
public sealed record DtlsCertificateVerify(ushort SignatureScheme, byte[] Signature)
{
    /// <summary>Reads a <c>CertificateVerify</c> body.</summary>
    /// <param name="body">The reassembled message body.</param>
    /// <returns>The signature.</returns>
    /// <exception cref="DtlsDecodeErrorException">The body is malformed.</exception>
    public static DtlsCertificateVerify Read(ReadOnlySpan<byte> body)
    {
        var reader = new DtlsHandshakeBodyReader(body);
        var verify = new DtlsCertificateVerify(reader.ReadUInt16(), reader.ReadVector16(0, ushort.MaxValue));
        reader.ExpectEnd();
        return verify;
    }

    /// <summary>Writes the body.</summary>
    /// <returns>The body's bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The signature is longer than 2^16 - 1 bytes.</exception>
    public byte[] Write()
    {
        var writer = new DtlsHandshakeBodyWriter();
        writer.WriteUInt16(SignatureScheme);
        writer.WriteVector16(Signature, 0, ushort.MaxValue);
        return writer.ToArray();
    }
}
