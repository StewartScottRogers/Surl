namespace Surl.Tls13;

/// <summary>
/// Thrown when a handshake message cannot be accepted. <see cref="Alert"/> is the RFC 8446
/// section 6 alert that ends the handshake; the caller reports it, since TLS over QUIC sends no
/// alert record.
/// </summary>
public sealed class TlsAlertException : Exception
{
    /// <summary>Creates the exception for an alert, with a message saying what was wrong.</summary>
    /// <param name="alert">The alert that ends the handshake.</param>
    /// <param name="message">What was wrong with the message.</param>
    public TlsAlertException(TlsAlert alert, string message)
        : base(message)
    {
        Alert = alert;
    }

    /// <summary>The alert that ends the handshake.</summary>
    public TlsAlert Alert { get; }
}
