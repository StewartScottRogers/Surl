using System.Collections.Frozen;

namespace Surl.Cli;

/// <summary>
/// The schemes a proxy listen URL (<c>-x</c>/<c>--proxy</c>) may name, each with the port one without a
/// port binds: upstream curl 8.21.0's default proxy port per scheme (curl's manpage, <c>-x</c>; ADR-0088
/// decision 7), and the four SOCKS schemes, whose default port is 1080 as curl's is (ADR-0121 decision 5).
/// </summary>
public static class ProxyDefaultPorts
{
    private static readonly FrozenDictionary<string, int> DefaultPortByScheme = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["http"] = 1080,
        ["https"] = 443,
        ["socks4"] = 1080,
        ["socks4a"] = 1080,
        ["socks5"] = 1080,
        ["socks5h"] = 1080,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Every scheme a proxy listen URL may name, lower-case, in no particular order.</summary>
    public static IReadOnlyCollection<string> Schemes => DefaultPortByScheme.Keys;

    /// <summary>Looks up the default proxy port of a lower-case scheme.</summary>
    /// <param name="scheme">The scheme, lower-case.</param>
    /// <param name="defaultPort">The scheme's default proxy port, or 0 when the scheme is not accepted.</param>
    /// <returns><see langword="true"/> when a proxy listen URL may name <paramref name="scheme"/>.</returns>
    public static bool TryGetDefaultPort(string scheme, out int defaultPort) =>
        DefaultPortByScheme.TryGetValue(scheme, out defaultPort);
}
