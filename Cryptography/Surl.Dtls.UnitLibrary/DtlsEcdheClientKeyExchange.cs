namespace Surl.Dtls;

/// <summary>
/// An ECDHE <c>ClientKeyExchange</c> body (RFC 8422 section 5.7): the client's ephemeral public
/// point.
/// </summary>
/// <param name="PublicPoint">The client's public key, 1 to 255 bytes.</param>
public sealed record DtlsEcdheClientKeyExchange(byte[] PublicPoint)
{
    /// <summary>Reads an ECDHE <c>ClientKeyExchange</c> body.</summary>
    /// <param name="body">The reassembled message body.</param>
    /// <returns>The key exchange.</returns>
    /// <exception cref="DtlsDecodeErrorException">The body is malformed.</exception>
    public static DtlsEcdheClientKeyExchange Read(ReadOnlySpan<byte> body)
    {
        var reader = new DtlsHandshakeBodyReader(body);
        var exchange = new DtlsEcdheClientKeyExchange(reader.ReadVector8(1, byte.MaxValue));
        reader.ExpectEnd();
        return exchange;
    }

    /// <summary>Writes the body.</summary>
    /// <returns>The body's bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The point is empty or longer than 255 bytes.</exception>
    public byte[] Write()
    {
        var writer = new DtlsHandshakeBodyWriter();
        writer.WriteVector8(PublicPoint, 1, byte.MaxValue);
        return writer.ToArray();
    }
}
