namespace Surl.Dtls;

/// <summary>
/// A PSK <c>ClientKeyExchange</c> body (RFC 4279 section 2): the identity whose key the client
/// uses.
/// </summary>
/// <param name="Identity">The PSK identity, at most 2^16 - 1 bytes; an account name's UTF-8 bytes (ADR-0087 decision 3).</param>
public sealed record DtlsPskClientKeyExchange(byte[] Identity)
{
    /// <summary>Reads a PSK <c>ClientKeyExchange</c> body.</summary>
    /// <param name="body">The reassembled message body.</param>
    /// <returns>The key exchange.</returns>
    /// <exception cref="DtlsDecodeErrorException">The body is malformed.</exception>
    public static DtlsPskClientKeyExchange Read(ReadOnlySpan<byte> body)
    {
        var reader = new DtlsHandshakeBodyReader(body);
        var exchange = new DtlsPskClientKeyExchange(reader.ReadVector16(0, ushort.MaxValue));
        reader.ExpectEnd();
        return exchange;
    }

    /// <summary>Writes the body.</summary>
    /// <returns>The body's bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The identity is longer than 2^16 - 1 bytes.</exception>
    public byte[] Write()
    {
        var writer = new DtlsHandshakeBodyWriter();
        writer.WriteVector16(Identity, 0, ushort.MaxValue);
        return writer.ToArray();
    }
}
