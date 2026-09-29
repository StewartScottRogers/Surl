namespace Surl.Protocol.Abstractions;

/// <summary>
/// Thrown by <see cref="IConnection.UpgradeToTlsAsync"/> when a TLS handshake fails: the
/// client's alert, a version outside the accepted range, a client certificate that fails
/// verification, or the peer closing (ADR-0010, section 1). The connection is unusable
/// afterwards. It is an <see cref="IOException"/>, like every other transport failure.
/// </summary>
public sealed class TlsHandshakeException : IOException
{
    /// <summary>
    /// Creates the exception for a handshake that failed with <paramref name="innerException"/>.
    /// </summary>
    /// <param name="message">The message of the platform exception behind the failure.</param>
    /// <param name="innerException">The platform exception behind the failure, or <see langword="null"/>.</param>
    public TlsHandshakeException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
