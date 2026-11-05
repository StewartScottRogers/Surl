namespace Surl.Dtls;

/// <summary>
/// An ECDHE <c>ServerKeyExchange</c> body (RFC 8422 section 5.4 with RFC 5246 section 7.4.3's
/// signature): the named group, the server's public point, and the signature over the randoms and
/// those parameters.
/// </summary>
/// <param name="NamedGroup">The group, such as secp256r1 (23) or x25519 (29).</param>
/// <param name="PublicPoint">The server's ephemeral public key, 1 to 255 bytes.</param>
/// <param name="SignatureScheme">The signature scheme, such as <c>ecdsa_secp256r1_sha256</c> (0x0403).</param>
/// <param name="Signature">The signature, at most 2^16 - 1 bytes.</param>
public sealed record DtlsEcdheServerKeyExchange(
    ushort NamedGroup, byte[] PublicPoint, ushort SignatureScheme, byte[] Signature)
{
    /// <summary>The <c>ECCurveType</c> <c>named_curve</c> (3), the only one RFC 8422 allows.</summary>
    public const byte NamedCurveType = 3;

    /// <summary>Reads an ECDHE <c>ServerKeyExchange</c> body.</summary>
    /// <param name="body">The reassembled message body.</param>
    /// <returns>The key exchange.</returns>
    /// <exception cref="DtlsDecodeErrorException">The body is malformed or names a curve type other than <c>named_curve</c>.</exception>
    public static DtlsEcdheServerKeyExchange Read(ReadOnlySpan<byte> body)
    {
        var reader = new DtlsHandshakeBodyReader(body);
        var curveType = reader.ReadUInt8();
        if (curveType != NamedCurveType)
        {
            throw new DtlsDecodeErrorException($"Curve type {curveType} is not named_curve.");
        }

        var exchange = new DtlsEcdheServerKeyExchange(
            reader.ReadUInt16(), reader.ReadVector8(1, byte.MaxValue), reader.ReadUInt16(), reader.ReadVector16(0, ushort.MaxValue));
        reader.ExpectEnd();
        return exchange;
    }

    /// <summary>
    /// The bytes the signature covers (RFC 8422 section 5.4): the client's random, the server's
    /// random, then the <c>ServerECDHParams</c>.
    /// </summary>
    /// <param name="clientRandom">The <c>ClientHello</c>'s 32 random bytes.</param>
    /// <param name="serverRandom">The <c>ServerHello</c>'s 32 random bytes.</param>
    /// <returns>The bytes to sign or verify.</returns>
    public byte[] SignedContent(ReadOnlySpan<byte> clientRandom, ReadOnlySpan<byte> serverRandom)
    {
        var writer = new DtlsHandshakeBodyWriter();
        writer.WriteFixed(clientRandom, DtlsClientHello.RandomLength);
        writer.WriteFixed(serverRandom, DtlsClientHello.RandomLength);
        WriteParameters(writer);
        return writer.ToArray();
    }

    /// <summary>Writes the body.</summary>
    /// <returns>The body's bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A field is outside its bounds.</exception>
    public byte[] Write()
    {
        var writer = new DtlsHandshakeBodyWriter();
        WriteParameters(writer);
        writer.WriteUInt16(SignatureScheme);
        writer.WriteVector16(Signature, 0, ushort.MaxValue);
        return writer.ToArray();
    }

    private void WriteParameters(DtlsHandshakeBodyWriter writer)
    {
        writer.WriteUInt8(NamedCurveType);
        writer.WriteUInt16(NamedGroup);
        writer.WriteVector8(PublicPoint, 1, byte.MaxValue);
    }
}
