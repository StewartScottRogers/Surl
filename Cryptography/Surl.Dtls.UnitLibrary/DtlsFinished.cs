namespace Surl.Dtls;

/// <summary>
/// A <c>Finished</c> body (RFC 5246 section 7.4.9): the 12 bytes of <c>verify_data</c> that
/// <see cref="DtlsPseudorandomFunction.ClientFinishedVerifyData"/> or
/// <see cref="DtlsPseudorandomFunction.ServerFinishedVerifyData"/> computes.
/// </summary>
/// <param name="VerifyData">The 12 bytes of <c>verify_data</c>.</param>
public sealed record DtlsFinished(byte[] VerifyData)
{
    /// <summary>The length of <c>verify_data</c> for every suite ADR-0087 serves: 12 bytes.</summary>
    public const int VerifyDataLength = 12;

    /// <summary>Reads a <c>Finished</c> body.</summary>
    /// <param name="body">The reassembled message body.</param>
    /// <returns>The verify data.</returns>
    /// <exception cref="DtlsDecodeErrorException">The body is not 12 bytes.</exception>
    public static DtlsFinished Read(ReadOnlySpan<byte> body)
    {
        var reader = new DtlsHandshakeBodyReader(body);
        var finished = new DtlsFinished(reader.ReadBytes(VerifyDataLength));
        reader.ExpectEnd();
        return finished;
    }

    /// <summary>Writes the body.</summary>
    /// <returns>The body's bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The verify data is not 12 bytes.</exception>
    public byte[] Write()
    {
        var writer = new DtlsHandshakeBodyWriter();
        writer.WriteFixed(VerifyData, VerifyDataLength);
        return writer.ToArray();
    }
}
