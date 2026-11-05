namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// The Streamable HTTP <c>Origin</c> check against DNS rebinding (ADR-0111 decision 4.7). A request without
/// <c>Origin</c> is accepted, since only a browser sends one. A request with one is accepted only when it parses
/// as <c>&lt;scheme&gt;://&lt;host&gt;[:&lt;port&gt;]</c> and either its host is loopback (<c>localhost</c>,
/// <c>127.0.0.0/8</c>, <c>[::1]</c>) or its scheme, host and port are the request's own.
/// </summary>
internal static class McpOriginCheck
{
    /// <summary>Whether the request's <c>Origin</c> is allowed.</summary>
    /// <param name="origin">The <c>Origin</c> value, or <see langword="null" /> when none was sent.</param>
    /// <param name="requestScheme">The request's own scheme: <c>http</c> for <c>mcp</c>, <c>https</c> for <c>mcps</c>.</param>
    /// <param name="requestHost">The request's <c>Host</c>, or <see langword="null" /> when it names none.</param>
    /// <returns><see langword="true" /> when the request may be served.</returns>
    public static bool IsAllowed(string? origin, string requestScheme, string? requestHost)
    {
        if (origin is null)
        {
            return true;
        }

        if (!TryParseOrigin(origin, out var parsed))
        {
            return false;
        }

        return parsed.IsLoopback || IsSameOrigin(parsed, requestScheme, requestHost);
    }

    // Only a bare scheme://host[:port] parses: no user information, path, query or fragment.
    private static bool TryParseOrigin(string origin, out Uri parsed)
    {
        return Uri.TryCreate(origin, UriKind.Absolute, out parsed!) && !origin.EndsWith('/') && IsBareHttpOrigin(parsed);
    }

    private static bool IsBareHttpOrigin(Uri parsed) =>
        parsed.Scheme is "http" or "https"
        && parsed.UserInfo.Length == 0
        && parsed.PathAndQuery == "/"
        && parsed.Fragment.Length == 0;

    private static bool IsSameOrigin(Uri origin, string requestScheme, string? requestHost) =>
        origin.Scheme == requestScheme
        && Uri.TryCreate($"{requestScheme}://{requestHost}", UriKind.Absolute, out var own)
        && origin.Host.Equals(own.Host, StringComparison.OrdinalIgnoreCase)
        && origin.Port == own.Port;
}
