namespace Surl.Dtls;

/// <summary>
/// How a server handshake ended.
/// </summary>
public enum DtlsHandshakeResult
{
    /// <summary>Both <c>Finished</c> messages verified; application data may flow.</summary>
    Completed,

    /// <summary>The server ended the handshake with the fatal alert <see cref="DtlsHandshakeOutcome.Alert"/>.</summary>
    AlertSent,

    /// <summary>The client sent a fatal alert.</summary>
    AlertReceived,

    /// <summary>The handshake ran past its time limit and was abandoned with no alert (ADR-0087 decision 9).</summary>
    TimedOut,
}
