namespace Surl.Tls13;

/// <summary>
/// Thrown when a handshake message or a record cannot be accepted. <see cref="Alert"/> is the
/// RFC 8446 section 6 alert that ends the handshake, or the connection after it. Throwing it
/// sends nothing. Over QUIC, <c>Surl.Quic</c> sends no alert record: it closes the connection
/// with <c>CONNECTION_CLOSE</c>, error <c>0x0100</c> plus the alert (RFC 9001 section 4.8). Over
/// TCP, <c>Surl.Tls13Stream</c> writes it as a fatal alert record, in plaintext before the
/// handshake keys are installed and protected after, ignoring a connection already gone; the
/// exception then reaches the stream's caller.
/// </summary>
public sealed class TlsAlertException : Exception
{
    /// <summary>Creates the exception for an alert, with a message saying what was wrong.</summary>
    /// <param name="alert">The alert that ends the handshake or the connection.</param>
    /// <param name="message">What was wrong.</param>
    public TlsAlertException(TlsAlert alert, string message)
        : base(message)
    {
        Alert = alert;
    }

    /// <summary>The alert that ends the handshake or the connection.</summary>
    public TlsAlert Alert { get; }
}
