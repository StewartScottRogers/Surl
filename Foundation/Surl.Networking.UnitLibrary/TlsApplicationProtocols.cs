using System.Net.Security;

namespace Surl.Networking;

/// <summary>
/// The ALPN protocol IDs a listener offers, by its listen URL's scheme (ADR-0010, section 4,
/// as ADR-0081 decision 2 amends it): <c>h2</c> then <c>http/1.1</c> for <c>https</c>, exactly
/// <c>http/1.1</c> for <c>wss</c>, whose WebSocket upgrade is HTTP/1.1's, and for the extended
/// <c>s3</c>, which the S3 server answers over HTTP/1.1 only (ADR-0108, decision 5), and none for every
/// other scheme, because upstream curl sends ALPN only for HTTP.
/// </summary>
internal static class TlsApplicationProtocols
{
    private static readonly SslApplicationProtocol[] Https = [SslApplicationProtocol.Http2, SslApplicationProtocol.Http11];

    private static readonly SslApplicationProtocol[] Http11Only = [SslApplicationProtocol.Http11];

    /// <summary>
    /// The ALPN protocol IDs offered for <paramref name="scheme"/>, in the server's order of preference.
    /// </summary>
    /// <param name="scheme">The listen URL's scheme, lower-cased.</param>
    /// <returns><c>h2</c> and <c>http/1.1</c> for <c>https</c>; <c>http/1.1</c> for <c>wss</c> and <c>s3</c>; otherwise empty.</returns>
    public static IReadOnlyList<SslApplicationProtocol> ForScheme(string scheme) => scheme switch
    {
        "https" => Https,
        "wss" or "s3" => Http11Only,
        _ => [],
    };
}
