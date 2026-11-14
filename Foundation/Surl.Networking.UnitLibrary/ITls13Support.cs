namespace Surl.Networking;

/// <summary>
/// Says whether the platform's <see cref="System.Net.Security.SslStream"/> can serve a TLS 1.3
/// handshake (ADR-0157, decision 1), so tests can set it and the platform check lives in one place.
/// </summary>
internal interface ITls13Support
{
    /// <summary>
    /// Whether <see cref="System.Net.Security.SslStream"/> has no TLS 1.3 server on this platform,
    /// so a TLS 1.3-only client has to be served by the hand-built stream.
    /// </summary>
    bool SslStreamLacksTls13Server { get; }
}
