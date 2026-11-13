namespace Surl.Conformance;

/// <summary>
/// Which <c>&lt;servercmd&gt;</c> commands each server half implements, by the first word of the
/// line as the case writes it (ADR-0080 decisions 2 and 3). A case none of whose named servers'
/// halves implements one of its commands (BL-898: any one named server's suffices) is skipped <c>servercmd-unsupported:&lt;command&gt;</c>, and so is a
/// case with a <c>&lt;postcmd&gt;</c>, which no server half implements
/// (<c>servercmd-unsupported:postcmd</c>). A server half that lands adds its row here, the same
/// words its protocol library accepts.
/// </summary>
public static class UpstreamTestCaseServerCommands
{
    /// <summary>
    /// The HTTP server half's commands (BL-391), the same words
    /// <c>Surl.Protocol.Http</c>'s <c>HttpUpstreamTestCaseReplies.SupportedServerCommands</c> accepts.
    /// <c>delay:</c>, <c>stream</c>, <c>rtp:</c>, <c>connection-monitor</c> and <c>upgrade</c> are not.
    /// </summary>
    public static readonly IReadOnlySet<string> Http = new HashSet<string>(
        ["Testnum", "auth_required", "idle", "no-expect", "skip:", "swsclose", "writedelay:"],
        StringComparer.Ordinal);

    /// <summary>
    /// The FTP server half's commands (BL-394), the same words <c>Surl.Protocol.Ftp</c>'s
    /// <c>FtpUpstreamTestCaseReplies.SupportedServerCommands</c> accepts. <c>DELAY</c>,
    /// <c>RETRWEIRDO</c>, <c>SLOWDOWN</c>, <c>SLOWDOWNDATA</c>, <c>PASVBADIP</c> and the
    /// <c>NODATACONN</c> forms are not.
    /// </summary>
    public static readonly IReadOnlySet<string> Ftp = new HashSet<string>(
        ["Testnum", "REPLY", "REPLYLF", "COUNT", "RETRNOSIZE", "RETRSIZE", "NOSAVE", "STOR"],
        StringComparer.Ordinal);

    /// <summary>
    /// The TFTP server half's commands (BL-396), the same words <c>Surl.Protocol.Tftp</c>'s
    /// <c>TftpUpstreamTestCaseReplies.SupportedServerCommands</c> accepts.
    /// </summary>
    public static readonly IReadOnlySet<string> Tftp = new HashSet<string>(["Testnum", "writedelay:"], StringComparer.Ordinal);

    /// <summary>
    /// The commands of the server halves that implement none (BL-396): Gopher's and DICT's, to
    /// which FILEFORMAT gives none, TELNET's, and MQTT's, whose <c>error-CONNACK</c>,
    /// <c>remlen-CONNACK</c>, <c>PUBLISH-before-SUBACK</c>, <c>short-PUBLISH</c>,
    /// <c>PINGRESP-as-CONNACK</c>, <c>DISCONNECT-malformed</c> and <c>excessive-remaining</c>
    /// each make the server send a deliberately broken or refusing packet and are not implemented;
    /// Surl's own SCP, SFTP and SMB servers, which serve the case's files (BL-397) and to
    /// which FILEFORMAT gives none; and the <c>httptls+srp</c> server half, which answers every
    /// request with gnutls-serv's page (<see cref="UpstreamTestCaseGnuTlsServPage"/>, BL-568), as
    /// gnutls-serv takes no commands.
    /// </summary>
    public static readonly IReadOnlySet<string> None = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// The IMAP, POP3 and SMTP server halves' commands (BL-876 to BL-879), the same words
    /// <c>Surl.LineProtocol</c>'s <c>MailUpstreamTestCaseReplies.SupportedServerCommands</c>
    /// accepts. <c>DELAY</c>, <c>SLOWDOWN</c>, <c>SLOWDOWNDATA</c> and <c>NOSAVE</c> are not.
    /// </summary>
    public static readonly IReadOnlySet<string> Mail = new HashSet<string>(
        ["Testnum", "REPLY", "REPLYLF", "COUNT", "CAPA", "AUTH", "POSTFETCH"],
        StringComparer.Ordinal);

    /// <summary>
    /// The HTTP proxy server halves' commands (BL-556), the same words
    /// <c>Surl.Protocol.HttpProxy</c>'s <c>HttpProxyUpstreamTestCaseReplies.SupportedServerCommands</c>
    /// accepts: only <c>Testnum</c>, since none of the cases naming <c>http-proxy</c> or
    /// <c>https-proxy</c> at the tag has another.
    /// </summary>
    public static readonly IReadOnlySet<string> HttpProxy = new HashSet<string>(["Testnum"], StringComparer.Ordinal);

    /// <summary>
    /// The SOCKS server halves' commands (BL-562), the words upstream's <c>socksd</c> reads and
    /// <c>Surl.Protocol.Socks</c>'s <c>SocksUpstreamTestCaseReplies</c> accepts.
    /// </summary>
    public static readonly IReadOnlySet<string> Socks = new HashSet<string>(
        ["Testnum", "response", "method", "user", "password", "backendport"], StringComparer.Ordinal);

    /// <summary>
    /// The DNS server half's lines (BL-570, ADR-0123): <c>Testnum</c> and the names of the
    /// <c>&lt;reply&gt;&lt;dns&gt;</c> lines <c>Surl.Extended.Protocol.DnsQuery</c>'s
    /// <c>DnsQueryUpstreamTestCaseReplies.SupportedLineNames</c> accepts, <c>A</c> and <c>AAAA</c>.
    /// <c>HTTPS</c> and the <c>Delay-</c> lines are not.
    /// </summary>
    public static readonly IReadOnlySet<string> Dns = new HashSet<string>(["Testnum", "A", "AAAA"], StringComparer.Ordinal);

    /// <summary>The name a case's <c>&lt;postcmd&gt;</c> is checked under; no server half implements it.</summary>
    public const string PostCommand = "postcmd";

    private static readonly Dictionary<string, IReadOnlySet<string>> ByServer = new(StringComparer.Ordinal)
    {
        ["http"] = Http,
        ["http-ipv6"] = Http,
        ["https"] = Http,
        ["https-mtls"] = Http,
        ["http-proxy"] = HttpProxy,
        ["https-proxy"] = HttpProxy,
        ["socks4"] = Socks,
        ["socks5"] = Socks,
        ["socks5unix"] = Socks,
        ["ftp"] = Ftp,
        ["ftp-ipv6"] = Ftp,
        ["ftps"] = Ftp,
        ["tftp"] = Tftp,
        ["gopher"] = None,
        ["gopher-ipv6"] = None,
        ["gophers"] = None,
        ["mqtt"] = None,
        ["mqtts"] = None,
        ["dict"] = None,
        ["telnet"] = None,
        ["scp"] = None,
        ["sftp"] = None,
        ["smb"] = None,
        ["imap"] = Mail,
        ["pop3"] = Mail,
        ["smtp"] = Mail,
        ["httptls+srp"] = None,
        ["httptls+srp-ipv6"] = None,
        ["dns"] = Dns,
    };

    /// <summary>
    /// Whether the server half for <paramref name="server"/> implements <paramref name="command"/>.
    /// </summary>
    /// <param name="server">The <c>&lt;server&gt;</c> name, lower case.</param>
    /// <param name="command">The command's first word, or <see cref="PostCommand"/>.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public static bool IsSupported(string server, string command)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(command);

        return ByServer.TryGetValue(server, out var commands) && commands.Contains(command);
    }
}
