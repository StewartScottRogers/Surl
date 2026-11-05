using System.Globalization;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// Where a proxy request goes: the origin's host and port, and for a forwarded request its
/// scheme and the origin-form target sent on (ADR-0088 decisions 3 and 4).
/// </summary>
/// <param name="Scheme">The absolute-form's scheme in lower case; empty for a <c>CONNECT</c> authority.</param>
/// <param name="Authority">The authority as the request wrote it: <c>host</c>, <c>host:port</c> or <c>[v6]:port</c>.</param>
/// <param name="Host">The origin's host: a name, an IPv4 literal, or an IPv6 literal without brackets.</param>
/// <param name="Port">The origin's port, 1 to 65535.</param>
/// <param name="OriginForm">The path and query sent to the origin (RFC 9112 section 3.2.1); empty for a <c>CONNECT</c> authority.</param>
internal sealed record ProxyTarget(string Scheme, string Authority, string Host, int Port, string OriginForm)
{
    private const int HttpDefaultPort = 80;

    /// <summary>
    /// Reads an absolute-form request target (RFC 9112 section 3.2.2): <c>scheme://authority</c>
    /// and an optional path and query. A target without a scheme, or with no host, is none.
    /// The port is the authority's, or 80 when it names none.
    /// </summary>
    /// <param name="requestTarget">The request line's target.</param>
    /// <returns>The target, or <see langword="null"/> when it is not absolute-form.</returns>
    public static ProxyTarget? FromAbsoluteForm(string requestTarget)
    {
        var schemeEnd = requestTarget.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd <= 0)
        {
            return null;
        }

        var rest = requestTarget[(schemeEnd + 3)..];
        var pathStart = rest.IndexOfAny(['/', '?']);
        var authority = pathStart < 0 ? rest : rest[..pathStart];
        var path = pathStart < 0 ? "/" : rest[pathStart..];
        var origin = ParseAuthority(authority, HttpDefaultPort);

        return origin is null
            ? null
            : new ProxyTarget(requestTarget[..schemeEnd].ToLowerInvariant(), authority, origin.Value.Host, origin.Value.Port, path.StartsWith('?') ? "/" + path : path);
    }

    /// <summary>
    /// Reads a <c>CONNECT</c> request's authority-form target (RFC 9112 section 3.2.3):
    /// <c>host:port</c>, an IPv6 literal in brackets. A missing or zero port is none.
    /// </summary>
    /// <param name="requestTarget">The request line's target.</param>
    /// <returns>The target, or <see langword="null"/> when it is not authority-form.</returns>
    public static ProxyTarget? FromAuthorityForm(string requestTarget)
    {
        var origin = ParseAuthority(requestTarget, defaultPort: 0);

        return origin is null ? null : new ProxyTarget(string.Empty, requestTarget, origin.Value.Host, origin.Value.Port, string.Empty);
    }

    // host, host:port, [v6] or [v6]:port; a port of 1 to 65535 digits only. No userinfo.
    private static (string Host, int Port)? ParseAuthority(string authority, int defaultPort)
    {
        var hostEnd = authority.StartsWith('[') ? authority.IndexOf(']') + 1 : authority.LastIndexOf(':');
        if (hostEnd < 0)
        {
            hostEnd = authority.Length;
        }

        var host = authority[..hostEnd];
        var portText = authority[hostEnd..];
        var port = portText.Length == 0 ? defaultPort : ParsePort(portText);
        host = host.StartsWith('[') ? host[1..^1] : host;

        return host.Length == 0 || host.Contains('@') || port is < 1 or > 65535 ? null : (host, port);
    }

    private static int ParsePort(string portText) =>
        portText.StartsWith(':') && int.TryParse(portText.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            ? port
            : -1;
}
