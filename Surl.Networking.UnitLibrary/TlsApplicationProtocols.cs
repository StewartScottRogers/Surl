using System.Net.Security;

namespace Surl.Networking;

/// <summary>
/// The ALPN protocol IDs a listener offers, by its listen URL's scheme (ADR-0010, section 4):
/// exactly <c>http/1.1</c> for <c>https</c> and <c>wss</c>, and none for every other scheme,
/// because upstream curl sends ALPN only for HTTP.
/// </summary>
internal static class TlsApplicationProtocols
{
    private static readonly SslApplicationProtocol[] Http = [SslApplicationProtocol.Http11];

    /// <summary>
    /// The ALPN protocol IDs offered for <paramref name="scheme"/>.
    /// </summary>
    /// <param name="scheme">The listen URL's scheme, lower-cased.</param>
    /// <returns><c>http/1.1</c> for <c>https</c> and <c>wss</c>; otherwise empty.</returns>
    public static IReadOnlyList<SslApplicationProtocol> ForScheme(string scheme) =>
        scheme is "https" or "wss" ? Http : [];
}
