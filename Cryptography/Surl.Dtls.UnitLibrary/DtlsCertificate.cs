namespace Surl.Dtls;

/// <summary>
/// A <c>Certificate</c> body in X.509 mode (RFC 5246 section 7.4.2): the DER certificates, the
/// sender's own first. A client with no certificate sends an empty list.
/// </summary>
/// <param name="Certificates">The DER-encoded certificates in order, each at least one byte.</param>
public sealed record DtlsCertificate(IReadOnlyList<byte[]> Certificates)
{
    /// <summary>Reads a <c>Certificate</c> body.</summary>
    /// <param name="body">The reassembled message body.</param>
    /// <returns>The certificates.</returns>
    /// <exception cref="DtlsDecodeErrorException">The body is malformed.</exception>
    public static DtlsCertificate Read(ReadOnlySpan<byte> body)
    {
        var reader = new DtlsHandshakeBodyReader(body);
        var listReader = new DtlsHandshakeBodyReader(reader.ReadVector24(0, DtlsHandshakeHeader.MaximumLength));
        reader.ExpectEnd();

        var certificates = new List<byte[]>();
        while (!listReader.IsAtEnd)
        {
            certificates.Add(listReader.ReadVector24(1, DtlsHandshakeHeader.MaximumLength));
        }

        return new DtlsCertificate(certificates);
    }

    /// <summary>Writes the body.</summary>
    /// <returns>The body's bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A certificate is empty, or the list is longer than 2^24 - 1 bytes.</exception>
    public byte[] Write()
    {
        var listWriter = new DtlsHandshakeBodyWriter();
        foreach (var certificate in Certificates)
        {
            listWriter.WriteVector24(certificate, 1, DtlsHandshakeHeader.MaximumLength);
        }

        var writer = new DtlsHandshakeBodyWriter();
        writer.WriteVector24(listWriter.ToArray(), 0, DtlsHandshakeHeader.MaximumLength);
        return writer.ToArray();
    }
}
