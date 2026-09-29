using System.Collections.Frozen;

namespace Surl.Cli;

/// <summary>
/// The schemes a listen URL may name, each with the port a listen URL without one binds:
/// upstream curl 8.21.0's default port per scheme, as measured in ADR-0007
/// ("Upstream curl 8.21.0's default port per scheme").
/// </summary>
public static class SchemeDefaultPorts
{
    private static readonly FrozenDictionary<string, int> DefaultPortByScheme = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["dict"] = 2628,
        ["ftp"] = 21,
        ["ftps"] = 990,
        ["gopher"] = 70,
        ["gophers"] = 70,
        ["http"] = 80,
        ["https"] = 443,
        ["imap"] = 143,
        ["imaps"] = 993,
        ["ldap"] = 389,
        ["ldaps"] = 636,
        ["mqtt"] = 1883,
        ["mqtts"] = 8883,
        ["pop3"] = 110,
        ["pop3s"] = 995,
        ["rtsp"] = 554,
        ["scp"] = 22,
        ["sftp"] = 22,
        ["smb"] = 445,
        ["smbs"] = 445,
        ["smtp"] = 25,
        ["smtps"] = 465,
        ["telnet"] = 23,
        ["tftp"] = 69,
        ["ws"] = 80,
        ["wss"] = 443,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Every scheme a listen URL may name, lower-case, in no particular order.
    /// </summary>
    public static IReadOnlyCollection<string> Schemes => DefaultPortByScheme.Keys;

    /// <summary>
    /// Looks up the default port of a lower-case scheme.
    /// </summary>
    /// <param name="scheme">The scheme, lower-case (<c>http</c>, not <c>HTTP</c>).</param>
    /// <param name="defaultPort">The scheme's default port, or 0 when the scheme is not accepted.</param>
    /// <returns><see langword="true"/> when a listen URL may name <paramref name="scheme"/>.</returns>
    public static bool TryGetDefaultPort(string scheme, out int defaultPort) =>
        DefaultPortByScheme.TryGetValue(scheme, out defaultPort);
}
