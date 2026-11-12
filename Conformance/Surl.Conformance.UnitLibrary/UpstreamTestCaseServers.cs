namespace Surl.Conformance;

/// <summary>
/// ADR-0080 decision 2's table of the server names test cases list: each name's scheme and
/// whether Surl's server half for it is built. Building a server is one row's change.
/// </summary>
public static class UpstreamTestCaseServers
{
    /// <summary>
    /// The names that need no server half: curl reads the file system, or the case runs no
    /// server (<c>none</c>, as runtests spells it).
    /// </summary>
    public static readonly IReadOnlySet<string> NoServerHalf =
        new HashSet<string>(["file", "none"], StringComparer.Ordinal);

    /// <summary>
    /// Every server name a case at tag <c>curl-8_21_0</c> lists, but those in
    /// <see cref="NoServerHalf"/>, in ADR-0080 decision 2's order.
    /// </summary>
    public static readonly IReadOnlyList<UpstreamTestCaseServer> All =
    [
        new("http", "http", IsBuilt: true),
        new("http-ipv6", "http", IsBuilt: true),
        new("https", "https", IsBuilt: true),
        new("https-mtls", "https", IsBuilt: true),
        new("rtsp", "rtsp", IsBuilt: true),
        new("rtsp-ipv6", "rtsp", IsBuilt: true),
        new("ftp", "ftp", IsBuilt: true),
        new("ftp-ipv6", "ftp", IsBuilt: true),
        new("ftps", "ftps", IsBuilt: true),
        new("imap", "imap", IsBuilt: true),
        new("imaps", "imaps", IsBuilt: true),
        new("pop3", "pop3", IsBuilt: true),
        new("pop3s", "pop3s", IsBuilt: true),
        new("smtp", "smtp", IsBuilt: true),
        new("smtps", "smtps", IsBuilt: true),
        new("dict", "dict", IsBuilt: true),
        new("gopher", "gopher", IsBuilt: true),
        new("gopher-ipv6", "gopher", IsBuilt: true),
        new("gophers", "gophers", IsBuilt: true),
        new("telnet", "telnet", IsBuilt: true),
        new("mqtt", "mqtt", IsBuilt: true),
        new("mqtts", "mqtts", IsBuilt: true),
        new("tftp", "tftp", IsBuilt: true),
        new("scp", "scp", IsBuilt: true),
        new("sftp", "sftp", IsBuilt: true),
        new("smb", "smb", IsBuilt: true),
        new("http/2", "http", IsBuilt: false),
        new("http/3", "http", IsBuilt: false),
        new("http-proxy", "http", IsBuilt: true, PortVariable: "PROXYPORT"),
        new("https-proxy", "https", IsBuilt: true, PortVariable: "HTTPSPROXYPORT"),
        new("socks4", "http", IsBuilt: true, PortVariable: "SOCKSPORT"),
        new("socks5", "http", IsBuilt: true, PortVariable: "SOCKSPORT"),
        new("socks5unix", "http", IsBuilt: true, UnixSocketPathVariable: "SOCKSUNIXPATH"),
        new("http-unix", "http", IsBuilt: true, UnixSocketPathVariable: "HTTPUNIXPATH"),
        new("httptls+srp", "https", IsBuilt: true, PortVariable: "HTTPTLSPORT"),
        new("httptls+srp-ipv6", "https", IsBuilt: true, PortVariable: "HTTPTLS6PORT"),
        new("dns", "http", IsBuilt: true, PortVariable: "DNSPORT"),
    ];

    private static readonly Dictionary<string, UpstreamTestCaseServer> ByName =
        All.ToDictionary(server => server.Name, StringComparer.Ordinal);

    /// <summary>
    /// Finds the row for a server name.
    /// </summary>
    /// <param name="name">The name, lower case, as a case's <c>&lt;server&gt;</c> line's first word.</param>
    /// <returns>The row, or <see langword="null"/> for a name the table does not hold.</returns>
    public static UpstreamTestCaseServer? Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return ByName.GetValueOrDefault(name);
    }
}
