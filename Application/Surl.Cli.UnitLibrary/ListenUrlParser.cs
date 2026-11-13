using System.Buffers;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// Reads one command-line argument as a listen URL, <c>scheme://host[:port][/]</c>, by the
/// rules of ADR-0007 section 4, in their order.
/// </summary>
/// <remarks>
/// Hand-written rather than built on <see cref="Uri"/>, which unescapes, normalises hosts and
/// fills default ports its own way. Whether a registered protocol server claims the scheme
/// (rule 3) is checked later by <c>Surl.Console</c>, not here.
/// </remarks>
public static class ListenUrlParser
{
    /// <summary>The scheme that serves MCP on surl's own standard input and output.</summary>
    public const string StdioScheme = "mcp+stdio";

    private const string StdioWithAPart = "mcp+stdio: names no host, port, path, query or fragment";
    private const string SchemeSeparator = "://";
    private const string EncodedZoneSeparator = "%25";
    private const int HighestPort = 65535;
    private const int HighestIpv4Part = 255;
    private const int Ipv4PartCount = 4;

    private const string MalformedInput = "Malformed input to a URL function";
    private const string UserOrPassword = "A listen URL cannot have a user name or password";
    private const string BadIpv6Address = "Bad IPv6 address";
    private const string BadIpv4Address = "Bad IPv4 address";
    private const string NoHostPart = "No host part in the URL";
    private const string BadPort = "Port number was not a decimal number between 0 and 65535";
    private const string PathQueryOrFragment = "A listen URL cannot have a path, query or fragment";

    private const string AsciiLettersAndDigits = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    // The schemes whose server answers at the listen URL's path (ADR-0153): the SignalR hub's endpoint base.
    private static readonly HashSet<string> PathSchemes = ["signalr", "signalrs"];

    private static readonly SearchValues<char> SchemeCharacters = SearchValues.Create(AsciiLettersAndDigits + "+-.");
    private static readonly SearchValues<char> HostNameCharacters = SearchValues.Create(AsciiLettersAndDigits + "-.");
    private static readonly SearchValues<char> ZoneCharacters = SearchValues.Create(AsciiLettersAndDigits + "-._~");
    private static readonly SearchValues<char> AuthorityEnd = SearchValues.Create("/?#");

    /// <summary>
    /// Parses <paramref name="argument"/> as a listen URL.
    /// </summary>
    /// <param name="argument">One positional command-line argument.</param>
    /// <returns>
    /// The listen URL, with the scheme lower-cased, the host as written less any IPv6
    /// brackets (a zone decoded from <c>%25</c> to <c>%</c>) and the port or the scheme's
    /// default port; or a failure with <see cref="SurlExitCode.MalformedUrl"/> or
    /// <see cref="SurlExitCode.UnsupportedProtocol"/> and the message after <c>surl: </c>.
    /// </returns>
    public static ListenUrlParseResult Parse(string argument) => Parse(argument, []);

    /// <summary>
    /// Parses <paramref name="argument"/> as a listen URL, accepting besides upstream curl's
    /// schemes each of <paramref name="extendedSchemes"/> that
    /// <see cref="ExtendedSchemeDefaultPorts"/> gives a default port (ADR-0083, decisions 6 and 7).
    /// </summary>
    /// <param name="argument">One positional command-line argument.</param>
    /// <param name="extendedSchemes">
    /// The lower-case schemes the registered extended protocol servers claim; empty when
    /// <c>SURL_EXTENSIONS=0</c> turns them off, so each is then refused as an unknown scheme is.
    /// An original scheme listed here keeps upstream curl's default port.
    /// </param>
    /// <returns>As <see cref="Parse(string)"/> returns.</returns>
    public static ListenUrlParseResult Parse(string argument, IReadOnlyCollection<string> extendedSchemes)
    {
        ArgumentNullException.ThrowIfNull(argument);
        ArgumentNullException.ThrowIfNull(extendedSchemes);

        return Parse(argument, (string scheme, out int defaultPort) => TryGetDefaultPort(scheme, extendedSchemes, out defaultPort));
    }

    /// <summary>
    /// Parses <paramref name="argument"/> as a proxy listen URL, the argument of <c>-x</c>/<c>--proxy</c>
    /// (ADR-0088 decision 7 and Amendment 2): <c>http</c> or <c>https</c> only, by the same rules as
    /// <see cref="Parse(string)"/>, with upstream curl's proxy default ports (1080 for <c>http</c>, 443 for
    /// <c>https</c>) and <see cref="ListenUrl.IsProxy"/> set.
    /// </summary>
    /// <param name="argument">The argument of one <c>--proxy</c>.</param>
    /// <returns>
    /// The proxy listen URL; or a failure with <see cref="SurlExitCode.UnsupportedProtocol"/> for any other
    /// scheme, or <see cref="SurlExitCode.MalformedUrl"/> as <see cref="Parse(string)"/> refuses.
    /// </returns>
    public static ListenUrlParseResult ParseProxy(string argument)
    {
        ArgumentNullException.ThrowIfNull(argument);

        var parsed = Parse(argument, ProxyDefaultPorts.TryGetDefaultPort);
        return parsed.Succeeded ? ListenUrlParseResult.Accepted(parsed.ListenUrl with { IsProxy = true }) : parsed;
    }

    /// <summary>Looks up the default port of a lower-case scheme; false when the scheme is not accepted.</summary>
    private delegate bool TryGetSchemeDefaultPort(string scheme, out int defaultPort);

    private static ListenUrlParseResult Parse(string argument, TryGetSchemeDefaultPort tryGetDefaultPort)
    {
        if (argument.StartsWith(StdioScheme + ":", StringComparison.OrdinalIgnoreCase))
        {
            return ParseStdio(argument, tryGetDefaultPort);
        }

        var separator = argument.IndexOf(SchemeSeparator, StringComparison.Ordinal);
        if (separator < 0 || !IsSchemeText(argument.AsSpan(0, separator)))
        {
            return Rejected(MalformedInput);
        }

        var scheme = argument[..separator].ToLowerInvariant();
        if (!tryGetDefaultPort(scheme, out var defaultPort))
        {
            return ListenUrlParseResult.Refused(SurlExitCode.UnsupportedProtocol, $"(1) Protocol \"{scheme}\" not supported");
        }

        var afterScheme = argument[(separator + SchemeSeparator.Length)..];
        var authorityLength = afterScheme.AsSpan().IndexOfAny(AuthorityEnd);
        if (authorityLength < 0)
        {
            authorityLength = afterScheme.Length;
        }

        return ParseAuthority(scheme, defaultPort, afterScheme[..authorityLength], afterScheme[authorityLength..]);
    }

    // mcp+stdio: or mcp+stdio:// names no host, port, path, query or fragment (ADR-0111 decision 5): it means surl's own
    // standard input and output, so its listen URL carries an empty host and port 0.
    private static ListenUrlParseResult ParseStdio(string argument, TryGetSchemeDefaultPort tryGetDefaultPort)
    {
        if (!tryGetDefaultPort(StdioScheme, out _))
        {
            return ListenUrlParseResult.Refused(SurlExitCode.UnsupportedProtocol, $"(1) Protocol \"{StdioScheme}\" not supported");
        }

        var rest = argument[(StdioScheme.Length + 1)..];
        return rest is "" or "//"
            ? ListenUrlParseResult.Accepted(new ListenUrl(StdioScheme, string.Empty, 0))
            : Rejected(StdioWithAPart);
    }

    // Upstream curl's table first, so no extended scheme shadows an original one.
    private static bool TryGetDefaultPort(string scheme, IReadOnlyCollection<string> extendedSchemes, out int defaultPort)
    {
        // mcp+stdio names no port: claimed, it is accepted with port 0 and ParseStdio reads the rest.
        defaultPort = 0;
        return scheme == StdioScheme
            ? extendedSchemes.Contains(scheme, StringComparer.Ordinal)
            : SchemeDefaultPorts.TryGetDefaultPort(scheme, out defaultPort)
                || (extendedSchemes.Contains(scheme, StringComparer.Ordinal) && ExtendedSchemeDefaultPorts.TryGetDefaultPort(scheme, out defaultPort));
    }

    private static ListenUrlParseResult ParseAuthority(string scheme, int defaultPort, string authority, string afterAuthority)
    {
        if (authority.Contains('@'))
        {
            return Rejected(UserOrPassword);
        }

        var hostFailure = ReadHost(authority, out var host, out var portText);
        if (hostFailure is not null)
        {
            return Rejected(hostFailure);
        }

        return ParsePortAndPath(new ListenUrl(scheme, host, defaultPort), portText, afterAuthority);
    }

    /// <summary>Applies rules 6 and 7 to a listen URL that so far carries its scheme's default port.</summary>
    private static ListenUrlParseResult ParsePortAndPath(ListenUrl withDefaultPort, string? portText, string afterAuthority)
    {
        if (!TryReadPort(portText, withDefaultPort.Port, out var port))
        {
            return Rejected(BadPort);
        }

        if (IsEmptyOrSlash(afterAuthority))
        {
            return ListenUrlParseResult.Accepted(withDefaultPort with { Port = port });
        }

        // ADR-0153: only the schemes that serve at a path keep one; a query or fragment is refused for all.
        return PathSchemes.Contains(withDefaultPort.Scheme) && !afterAuthority.AsSpan().ContainsAny('?', '#')
            ? ListenUrlParseResult.Accepted(withDefaultPort with { Port = port, Path = afterAuthority })
            : Rejected(PathQueryOrFragment);
    }

    private static bool IsEmptyOrSlash(string text) => text.Length == 0 || text == "/";

    private static ListenUrlParseResult Rejected(string reason) =>
        ListenUrlParseResult.Refused(SurlExitCode.MalformedUrl, $"(3) URL rejected: {reason}");

    private static bool IsSchemeText(ReadOnlySpan<char> scheme) =>
        !scheme.IsEmpty && char.IsAsciiLetter(scheme[0]) && !scheme.ContainsAnyExcept(SchemeCharacters);

    /// <summary>Splits the authority into host and port text; returns the failure reason, or null.</summary>
    private static string? ReadHost(string authority, out string host, out string? portText) =>
        authority.StartsWith('[')
            ? ReadBracketedHost(authority, out host, out portText)
            : ReadUnbracketedHost(authority, out host, out portText);

    /// <summary>Reads <c>[ipv6]</c> or <c>[ipv6]:port</c>; returns the failure reason, or null.</summary>
    private static string? ReadBracketedHost(string authority, out string host, out string? portText)
    {
        host = string.Empty;
        portText = null;

        var close = authority.IndexOf(']');
        if (close < 0 || !TryReadIpv6Literal(authority[1..close], out host))
        {
            return BadIpv6Address;
        }

        var afterBracket = authority[(close + 1)..];
        if (afterBracket.Length == 0)
        {
            return null;
        }

        if (afterBracket[0] != ':')
        {
            return MalformedInput;
        }

        portText = afterBracket[1..];
        return null;
    }

    private static bool TryReadIpv6Literal(string literal, out string host)
    {
        var zoneStart = literal.IndexOf(EncodedZoneSeparator, StringComparison.Ordinal);
        if (zoneStart < 0)
        {
            host = literal;
            return IsIpv6Address(literal);
        }

        var address = literal[..zoneStart];
        var zone = literal[(zoneStart + EncodedZoneSeparator.Length)..];
        host = $"{address}%{zone}";
        return IsIpv6Address(address) && IsZoneName(zone);
    }

    private static bool IsIpv6Address(string text) =>
        !text.Contains('%')
        && IPAddress.TryParse(text, out var address)
        && address.AddressFamily == AddressFamily.InterNetworkV6;

    private static bool IsZoneName(string zone) =>
        zone.Length > 0 && !zone.AsSpan().ContainsAnyExcept(ZoneCharacters);

    /// <summary>Reads <c>host</c> or <c>host:port</c>; returns the failure reason, or null.</summary>
    private static string? ReadUnbracketedHost(string authority, out string host, out string? portText)
    {
        var colon = authority.IndexOf(':');
        if (colon < 0)
        {
            host = authority;
            portText = null;
            return CheckUnbracketedHost(host);
        }

        host = authority[..colon];
        portText = authority[(colon + 1)..];
        return portText.Contains(':') ? MalformedInput : CheckUnbracketedHost(host);
    }

    private static string? CheckUnbracketedHost(string host)
    {
        if (host.Length == 0)
        {
            return NoHostPart;
        }

        var parts = host.Split('.');
        return IsIpv4Shaped(parts) ? CheckIpv4Parts(parts) : CheckHostName(host);
    }

    private static bool IsIpv4Shaped(string[] parts) =>
        parts.Length == Ipv4PartCount && parts.All(IsDecimalNumber);

    private static string? CheckIpv4Parts(string[] parts) =>
        parts.All(IsIpv4Part) ? null : BadIpv4Address;

    private static string? CheckHostName(string host) =>
        host.AsSpan().ContainsAnyExcept(HostNameCharacters) ? MalformedInput : null;

    private static bool IsDecimalNumber(string text) =>
        text.Length > 0 && !text.AsSpan().ContainsAnyExceptInRange('0', '9');

    private static bool IsIpv4Part(string part) =>
        int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value <= HighestIpv4Part;

    private static bool TryReadPort(string? portText, int defaultPort, out int port)
    {
        port = defaultPort;
        if (string.IsNullOrEmpty(portText))
        {
            return true;
        }

        return int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port <= HighestPort;
    }
}
