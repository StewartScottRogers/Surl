using System.Net.Security;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// The ALPN protocol IDs a listener offers, by its listen URL's scheme (ADR-0010, section 4,
/// as ADR-0081 decision 2 amends it): <c>h2</c> then <c>http/1.1</c> for <c>https</c>, exactly
/// <c>http/1.1</c> for <c>wss</c>, whose WebSocket upgrade is HTTP/1.1's, and for the extended
/// <c>s3</c>, which the S3 server answers over HTTP/1.1 only (ADR-0108, decision 5), and none for every
/// other scheme but the extended <c>xmpps</c>, which offers <c>xmpp-client</c> (XEP-0368 section 3, BL-540), because upstream curl sends ALPN only for HTTP.
/// </summary>
internal static class TlsApplicationProtocols
{
    private static readonly SslApplicationProtocol[] Https = [SslApplicationProtocol.Http2, SslApplicationProtocol.Http11];

    private static readonly SslApplicationProtocol[] Http11Only = [SslApplicationProtocol.Http11];

    // XEP-0368 section 3: direct-TLS XMPP clients name the ALPN protocol xmpp-client.
    private static readonly SslApplicationProtocol[] XmppClient = [new SslApplicationProtocol("xmpp-client")];

    // RFC 7858 section 3.1 and RFC 7301: DNS over TLS names its ALPN protocol dot.
    private static readonly SslApplicationProtocol[] DnsOverTls = [new SslApplicationProtocol("dot")];

    /// <summary>
    /// The ALPN protocol IDs offered for <paramref name="scheme"/>, in the server's order of preference.
    /// </summary>
    /// <param name="scheme">The listen URL's scheme, lower-cased.</param>
    /// <returns><c>h2</c> and <c>http/1.1</c> for <c>https</c>; <c>http/1.1</c> for <c>wss</c> and <c>s3</c>; <c>xmpp-client</c> for <c>xmpps</c>; otherwise empty.</returns>
    public static IReadOnlyList<SslApplicationProtocol> ForScheme(string scheme) => scheme switch
    {
        "https" => Https,
        "wss" or "s3" => Http11Only,
        "xmpps" => XmppClient,
        _ => [],
    };

    /// <summary>
    /// The ALPN protocol IDs a listener on <paramref name="listenUrl"/> offers: for a proxy
    /// listener (<see cref="ListenUrl.IsProxy"/>) exactly <c>http/1.1</c> when its scheme is
    /// <c>https</c>, because the HTTPS proxy serves HTTP/1.1 only and curl's
    /// <c>--proxy-http2</c> offers <c>h2</c> first (ADR-0088, decision 6 and Amendment 2);
    /// otherwise <see cref="ForScheme"/>.
    /// </summary>
    /// <param name="listenUrl">The listener's listen URL; its scheme is lower-cased.</param>
    /// <returns>The ALPN protocol IDs offered, in the server's order of preference.</returns>
    public static IReadOnlyList<SslApplicationProtocol> ForListener(ListenUrl listenUrl) => listenUrl switch
    {
        { IsProxy: true, Scheme: "https" } => Http11Only,
        { IsImplicitTls: true, Scheme: "dns" } => DnsOverTls,
        _ => ForScheme(listenUrl.Scheme),
    };
}
