namespace Surl.Dtls;

/// <summary>
/// How a server handshake ended, and the alert involved.
/// </summary>
/// <param name="Result">How it ended.</param>
/// <param name="Alert">The alert sent or received, or <see langword="null"/>.</param>
public sealed record DtlsHandshakeOutcome(DtlsHandshakeResult Result, DtlsAlertDescription? Alert);
