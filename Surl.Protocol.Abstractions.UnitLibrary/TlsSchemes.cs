namespace Surl.Protocol.Abstractions;

/// <summary>
/// The listen URL schemes that are TLS from the first byte (ADR-0010, section 1): the
/// schemes upstream curl opens with a TLS handshake. The engine and the listener factory
/// read the same list.
/// </summary>
public static class TlsSchemes
{
    private static readonly HashSet<string> ImplicitTlsSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "https", "wss", "ftps", "imaps", "pop3s", "smtps", "ldaps", "gophers", "mqtts", "smbs",
    };

    /// <summary>
    /// Whether a connection for <paramref name="scheme"/> starts with a TLS handshake.
    /// </summary>
    /// <param name="scheme">A URL scheme, compared case-insensitively.</param>
    /// <returns>
    /// <see langword="true"/> for exactly <c>https</c>, <c>wss</c>, <c>ftps</c>, <c>imaps</c>,
    /// <c>pop3s</c>, <c>smtps</c>, <c>ldaps</c>, <c>gophers</c>, <c>mqtts</c> and <c>smbs</c>.
    /// <c>ftps</c> is implicit FTPS; explicit FTPS is <c>ftp</c> with <c>AUTH TLS</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="scheme"/> is <see langword="null"/>.</exception>
    public static bool IsImplicitTls(string scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);

        return ImplicitTlsSchemes.Contains(scheme);
    }
}
