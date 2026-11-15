namespace Surl.Networking;

/// <summary>
/// The operating system's answer to <see cref="ITls13Support"/>: macOS's Secure Transport server
/// negotiates no higher than TLS 1.2 (ADR-0157, context), so only macOS lacks a TLS 1.3 server.
/// </summary>
internal sealed class PlatformTls13Support : ITls13Support
{
    /// <inheritdoc/>
    public bool SslStreamLacksTls13Server => OperatingSystem.IsMacOS();
}
