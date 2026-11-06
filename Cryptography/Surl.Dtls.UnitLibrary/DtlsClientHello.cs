namespace Surl.Dtls;

/// <summary>
/// A DTLS 1.2 <c>ClientHello</c> body (RFC 6347 section 4.2.1, which adds the cookie to RFC 5246
/// section 7.4.1.2's fields).
/// </summary>
/// <param name="ClientVersion">The highest version the client offers, <c>0xFEFD</c> for DTLS 1.2.</param>
/// <param name="Random">The client's 32 random bytes.</param>
/// <param name="SessionId">The session the client would resume, 0 to 32 bytes.</param>
/// <param name="Cookie">The cookie from a <c>HelloVerifyRequest</c>, 0 to 255 bytes.</param>
/// <param name="CipherSuites">The suites offered, in the client's order, at least one.</param>
/// <param name="CompressionMethods">The compression methods offered, at least one.</param>
/// <param name="Extensions">The extensions, in the client's order, no type twice.</param>
public sealed record DtlsClientHello(
    ushort ClientVersion,
    byte[] Random,
    byte[] SessionId,
    byte[] Cookie,
    IReadOnlyList<ushort> CipherSuites,
    byte[] CompressionMethods,
    IReadOnlyList<DtlsExtension> Extensions)
{
    /// <summary>The length of a hello's random: 32 bytes.</summary>
    public const int RandomLength = 32;

    /// <summary>The longest session ID: 32 bytes.</summary>
    public const int MaximumSessionIdLength = 32;

    /// <summary>The longest cookie: 255 bytes (RFC 6347 section 4.2.1).</summary>
    public const int MaximumCookieLength = byte.MaxValue;

    /// <summary>Reads a <c>ClientHello</c> body.</summary>
    /// <param name="body">The reassembled message body.</param>
    /// <returns>The hello.</returns>
    /// <exception cref="DtlsDecodeErrorException">The body is malformed.</exception>
    public static DtlsClientHello Read(ReadOnlySpan<byte> body)
    {
        var reader = new DtlsHandshakeBodyReader(body);
        var hello = new DtlsClientHello(
            reader.ReadUInt16(),
            reader.ReadBytes(RandomLength),
            reader.ReadVector8(0, MaximumSessionIdLength),
            reader.ReadVector8(0, MaximumCookieLength),
            reader.ReadUInt16Vector16(2, ushort.MaxValue - 1),
            reader.ReadVector8(1, byte.MaxValue),
            DtlsExtension.ReadList(ref reader));
        reader.ExpectEnd();
        return hello;
    }

    /// <summary>Writes the body.</summary>
    /// <returns>The body's bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A field is outside its bounds.</exception>
    public byte[] Write()
    {
        var writer = new DtlsHandshakeBodyWriter();
        writer.WriteUInt16(ClientVersion);
        writer.WriteFixed(Random, RandomLength);
        writer.WriteVector8(SessionId, 0, MaximumSessionIdLength);
        writer.WriteVector8(Cookie, 0, MaximumCookieLength);
        writer.WriteUInt16Vector16(CipherSuites, 2, ushort.MaxValue - 1);
        writer.WriteVector8(CompressionMethods, 1, byte.MaxValue);
        DtlsExtension.WriteList(writer, Extensions);
        return writer.ToArray();
    }
}
