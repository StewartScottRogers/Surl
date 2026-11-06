namespace Surl.Dtls;

/// <summary>
/// Thrown when the bytes of a handshake message are not well formed: a field runs past the end,
/// a vector's length is outside its bounds, an extension repeats, or bytes are left over. The
/// handshake answers it with the fatal alert <c>decode_error</c> (50) (ADR-0087 decision 10).
/// </summary>
public sealed class DtlsDecodeErrorException : FormatException
{
    /// <summary>Creates the exception with the reason the bytes are malformed.</summary>
    /// <param name="message">What is wrong with the bytes.</param>
    public DtlsDecodeErrorException(string message)
        : base(message)
    {
    }
}
