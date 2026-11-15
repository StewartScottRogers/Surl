using Surl.Tls13;

namespace Surl.Tls13Stream;

/// <summary>The client ended the connection with an alert that is not <c>close_notify</c> or <c>user_canceled</c> (RFC 8446 section 6).</summary>
public sealed class Tls13PeerAlertException : IOException
{
    /// <summary>Creates the exception for the alert the client sent.</summary>
    /// <param name="alert">The alert description the client sent.</param>
    public Tls13PeerAlertException(TlsAlert alert)
        : base($"The client sent the fatal alert {alert} ({(byte)alert}).")
    {
        Alert = alert;
    }

    /// <summary>The alert description the client sent.</summary>
    public TlsAlert Alert { get; }
}
