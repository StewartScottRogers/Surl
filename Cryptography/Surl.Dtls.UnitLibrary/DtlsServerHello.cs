namespace Surl.Dtls;

/// <summary>
/// A <c>ServerHello</c> body (RFC 5246 section 7.4.1.3).
/// </summary>
/// <param name="ServerVersion">The version negotiated, <c>0xFEFD</c> for DTLS 1.2.</param>
/// <param name="Random">The server's 32 random bytes.</param>
/// <param name="SessionId">The session ID, 0 to 32 bytes; empty, since nothing resumes (ADR-0087 decision 6).</param>
/// <param name="CipherSuite">The suite chosen.</param>
/// <param name="CompressionMethod">The compression method chosen, <c>null</c> (0).</param>
/// <param name="Extensions">The extensions answered; none writes no extension list at all.</param>
public sealed record DtlsServerHello(
    ushort ServerVersion,
    byte[] Random,
    byte[] SessionId,
    ushort CipherSuite,
    byte CompressionMethod,
    IReadOnlyList<DtlsExtension> Extensions)
{
    /// <summary>Reads a <c>ServerHello</c> body.</summary>
    /// <param name="body">The reassembled message body.</param>
    /// <returns>The hello.</returns>
    /// <exception cref="DtlsDecodeErrorException">The body is malformed.</exception>
    public static DtlsServerHello Read(ReadOnlySpan<byte> body)
    {
        var reader = new DtlsHandshakeBodyReader(body);
        var hello = new DtlsServerHello(
            reader.ReadUInt16(),
            reader.ReadBytes(DtlsClientHello.RandomLength),
            reader.ReadVector8(0, DtlsClientHello.MaximumSessionIdLength),
            reader.ReadUInt16(),
            reader.ReadUInt8(),
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
        writer.WriteUInt16(ServerVersion);
        writer.WriteFixed(Random, DtlsClientHello.RandomLength);
        writer.WriteVector8(SessionId, 0, DtlsClientHello.MaximumSessionIdLength);
        writer.WriteUInt16(CipherSuite);
        writer.WriteUInt8(CompressionMethod);
        DtlsExtension.WriteList(writer, Extensions);
        return writer.ToArray();
    }
}
