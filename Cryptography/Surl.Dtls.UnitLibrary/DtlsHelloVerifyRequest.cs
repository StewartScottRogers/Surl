namespace Surl.Dtls;

/// <summary>
/// A <c>HelloVerifyRequest</c> body (RFC 6347 section 4.2.1): the version and the cookie the
/// client must send back.
/// </summary>
/// <param name="ServerVersion">
/// <c>0xFEFF</c>, DTLS 1.0, whatever version is negotiated later (ADR-0087 decision 4).
/// </param>
/// <param name="Cookie">The cookie, 0 to 255 bytes.</param>
public sealed record DtlsHelloVerifyRequest(ushort ServerVersion, byte[] Cookie)
{
    /// <summary>DTLS 1.0's version on the wire, which a <c>HelloVerifyRequest</c> carries.</summary>
    public const ushort Dtls10Version = 0xFEFF;

    /// <summary>Reads a <c>HelloVerifyRequest</c> body.</summary>
    /// <param name="body">The reassembled message body.</param>
    /// <returns>The request.</returns>
    /// <exception cref="DtlsDecodeErrorException">The body is malformed.</exception>
    public static DtlsHelloVerifyRequest Read(ReadOnlySpan<byte> body)
    {
        var reader = new DtlsHandshakeBodyReader(body);
        var request = new DtlsHelloVerifyRequest(
            reader.ReadUInt16(), reader.ReadVector8(0, DtlsClientHello.MaximumCookieLength));
        reader.ExpectEnd();
        return request;
    }

    /// <summary>Writes the body.</summary>
    /// <returns>The body's bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The cookie is longer than 255 bytes.</exception>
    public byte[] Write()
    {
        var writer = new DtlsHandshakeBodyWriter();
        writer.WriteUInt16(ServerVersion);
        writer.WriteVector8(Cookie, 0, DtlsClientHello.MaximumCookieLength);
        return writer.ToArray();
    }
}
