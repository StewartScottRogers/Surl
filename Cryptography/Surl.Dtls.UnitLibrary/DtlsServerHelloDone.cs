namespace Surl.Dtls;

/// <summary>
/// A <c>ServerHelloDone</c> body (RFC 5246 section 7.4.5), which is empty.
/// </summary>
public static class DtlsServerHelloDone
{
    /// <summary>Checks a <c>ServerHelloDone</c> body is empty.</summary>
    /// <param name="body">The reassembled message body.</param>
    /// <exception cref="DtlsDecodeErrorException">The body is not empty.</exception>
    public static void Read(ReadOnlySpan<byte> body) => new DtlsHandshakeBodyReader(body).ExpectEnd();

    /// <summary>Writes the empty body.</summary>
    /// <returns>No bytes.</returns>
    public static byte[] Write() => [];
}
