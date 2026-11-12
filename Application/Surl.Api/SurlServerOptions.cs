using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// What <see cref="SurlServer.StartAsync(SurlServerOptions, CancellationToken)"/> starts: the listen
/// URLs, the content, any other <c>surl</c> option, and what the host provides (ADR-0099, decision 4).
/// Every command-line property is a spelling of the <c>surl</c> option it names, and the options are
/// parsed as <c>surl</c> parses them, so every default is <c>surl</c>'s and the start is validated
/// exactly as <c>surl</c> validates it.
/// </summary>
public sealed class SurlServerOptions
{
    /// <summary>
    /// The listen URLs, as <c>surl</c> takes them (<c>http://127.0.0.1:0</c>,
    /// <c>ftp://127.0.0.1:0</c>), in order; port 0 asks for an ephemeral port. At least one is needed.
    /// </summary>
    public IList<string> ListenUrls { get; } = [];

    /// <summary>
    /// The data directory served and its service state kept in (<c>--directory</c>, ADR-0031), or
    /// <see langword="null"/> to serve <see cref="Files"/> in memory. Not set together with
    /// <see cref="Files"/>.
    /// </summary>
    public string? DataDirectory { get; set; }

    /// <summary>
    /// The files served in memory when no <see cref="DataDirectory"/> is given: each key a path
    /// relative to the served root, its segments separated by <c>/</c>, each value the file's bytes.
    /// A path may not be empty, start with <c>/</c>, hold an empty, <c>.</c> or <c>..</c> segment, a
    /// <c>\</c> or a <c>:</c>, or name a directory another path is inside.
    /// </summary>
    public IDictionary<string, byte[]> Files { get; } = new Dictionary<string, byte[]>(StringComparer.Ordinal);

    /// <summary>Accepts uploads into served files (<c>--allow-uploads</c>); off by default.</summary>
    public bool AllowUploads { get; set; }

    /// <summary>Answers directory listings (<c>--list-directories</c>); off by default.</summary>
    public bool ListDirectories { get; set; }

    /// <summary>Follows links that stay in the served root (<c>--follow-symlinks</c>); off by default.</summary>
    public bool FollowSymlinks { get; set; }

    /// <summary>Serves names that start with a dot (<c>--serve-dot-files</c>); off by default.</summary>
    public bool ServeDotFiles { get; set; }

    /// <summary>
    /// The accounts a client may log in as (<c>--user</c>, ADR-0032 section 1), in order; none by
    /// default. A user name may not hold <c>:</c>.
    /// </summary>
    public IList<SurlAccount> Accounts { get; } = [];

    /// <summary>
    /// The accounts file read at start (<c>--user-file</c>), or <see langword="null"/>, the default,
    /// for none. A malformed line refuses the start with <see cref="Protocol.Abstractions.SurlExitCode.FailedInit"/>
    /// naming the line, an unreadable file with <see cref="Protocol.Abstractions.SurlExitCode.CouldNotReadFile"/>.
    /// </summary>
    public string? UserFile { get; set; }

    /// <summary>
    /// The authentication methods accepted and offered (<c>--auth</c>, given once with the words joined
    /// by commas), or <see langword="null"/>, the default, for <c>surl</c>'s default set.
    /// </summary>
    public IList<string>? AuthenticationMethods { get; set; }

    /// <summary>Accepts any login, or none (<c>--allow-anonymous</c>, warns on start); off by default.</summary>
    public bool AllowAnonymous { get; set; }

    /// <summary>Accepts passwords sent over an unencrypted connection (<c>--allow-plaintext-auth</c>, warns on start); off by default.</summary>
    public bool AllowPlaintextAuthentication { get; set; }

    /// <summary>The server certificate file (<c>--cert</c>), or <see langword="null"/>, the default, for none.</summary>
    public string? CertificateFile { get; set; }

    /// <summary>The private key file for <see cref="CertificateFile"/> (<c>--key</c>), or <see langword="null"/>, the default, for the key in the certificate file.</summary>
    public string? KeyFile { get; set; }

    /// <summary>The passphrase of the key file and host key files (<c>--pass</c>), or <see langword="null"/>, the default, for none.</summary>
    public string? KeyPassphrase { get; set; }

    /// <summary>Serves a throwaway self-signed certificate when no certificate file is given (<c>--self-signed</c>, warns on start); off by default.</summary>
    public bool SelfSigned { get; set; }

    /// <summary>The CA certificates client certificates are verified against (<c>--cacert</c>), or <see langword="null"/>, the default, for none.</summary>
    public string? CaCertificateFile { get; set; }

    /// <summary>The SSH host private key files (<c>--hostkey</c>), in order; none by default.</summary>
    public IList<string> HostKeyFiles { get; } = [];

    /// <summary>Makes a throwaway SSH host key at start when no host key file is given (<c>--throwaway-hostkey</c>, warns on start); off by default.</summary>
    public bool ThrowawayHostKey { get; set; }

    /// <summary>Connections at once across all listeners (<c>--max-connections</c>), or <see langword="null"/>, the default, for <c>surl</c>'s 1024.</summary>
    public int? MaxConnections { get; set; }

    /// <summary>Connections at once from one address (<c>--max-connections-per-address</c>), or <see langword="null"/>, the default, for <c>surl</c>'s 100.</summary>
    public int? MaxConnectionsPerAddress { get; set; }

    /// <summary>
    /// How long an exchange may sit idle (<c>--idle-timeout</c>), or <see langword="null"/>, the
    /// default, for <c>surl</c>'s 120 seconds. <see cref="TimeSpan.Zero"/> and
    /// <see cref="Timeout.InfiniteTimeSpan"/> mean no limit, as <c>0</c> does; any other negative
    /// duration is misuse.
    /// </summary>
    public TimeSpan? IdleTimeout { get; set; }

    /// <summary>
    /// The longest time one exchange may take (<c>--max-time</c>), or <see langword="null"/>, the
    /// default, for <c>surl</c>'s 3600 seconds. <see cref="TimeSpan.Zero"/> and
    /// <see cref="Timeout.InfiniteTimeSpan"/> mean no limit, as <c>0</c> does; any other negative
    /// duration is misuse.
    /// </summary>
    public TimeSpan? MaxTime { get; set; }

    /// <summary>The largest upload accepted, in bytes (<c>--max-filesize</c>), or <see langword="null"/>, the default, for <c>surl</c>'s 100 MiB.</summary>
    public long? MaxFileSize { get; set; }

    /// <summary>
    /// The fault rules the server starts with (<c>--fault</c>, once each, in order; ADR-0124 decision 4), numbered
    /// <c>f1</c>, <c>f2</c> ... in <see cref="SurlServer.Faults"/>; none by default. Any rule writes <c>surl</c>'s
    /// <c>--fault</c> warning to <see cref="Log"/>, and a rule <c>surl</c> would refuse refuses the start with its
    /// text. May not hold null.
    /// </summary>
    public IList<SurlFault> Faults { get; } = [];

    /// <summary>
    /// The groups of accounts (<c>--group</c>, once each, in order; BL-668, ADR-0101 decision 4), each item as
    /// <c>surl</c> reads the option's value: <c>&lt;name&gt;=&lt;member&gt;,...</c>. None by default.
    /// </summary>
    public IList<string> Groups { get; } = [];

    /// <summary>The domains mail is served for (<c>--mail-domain</c>, once each); none by default, so the domain is ignored.</summary>
    public IList<string> MailDomains { get; } = [];

    /// <summary>
    /// The mailboxes and their logins (<c>--mailbox</c>, once each): <c>&lt;address&gt;:&lt;password&gt;[;...]</c>.
    /// None by default. A mailbox holds a password, so it is never written to the log.
    /// </summary>
    public IList<string> Mailboxes { get; } = [];

    /// <summary>The CSV files mailboxes are read from at start (<c>--mailbox-file</c>, once each); none by default.</summary>
    public IList<string> MailboxFiles { get; } = [];

    /// <summary>The forwarded addresses (<c>--mail-alias</c>, once each): <c>&lt;alias&gt;=&lt;to&gt;,...</c>; none by default.</summary>
    public IList<string> MailAliases { get; } = [];

    /// <summary>The catch-all addresses (<c>--mail-catch-all</c>, once each): <c>&lt;domain&gt;=&lt;to&gt;</c>; none by default, so mail to an unknown address is discarded.</summary>
    public IList<string> MailCatchAlls { get; } = [];

    /// <summary>The shared IMAP folders (<c>--shared-folder</c>, once each): <c>&lt;name&gt;=&lt;who&gt;,...</c>; none by default.</summary>
    public IList<string> SharedFolders { get; } = [];

    /// <summary>The folders served as shares (<c>--share</c>, once each): <c>&lt;name&gt;=&lt;path&gt;[;...]</c>; none by default, so there is one served root.</summary>
    public IList<string> Shares { get; } = [];

    /// <summary>The share grants (<c>--share-access</c>, once each): <c>&lt;share&gt;=&lt;member&gt;:&lt;r/rw&gt;,...</c>; none by default, so a share is open.</summary>
    public IList<string> ShareAccess { get; } = [];

    /// <summary>The buckets mapped to paths (<c>--bucket</c>, once each): <c>&lt;name&gt;=&lt;path&gt;</c>; none by default.</summary>
    public IList<string> Buckets { get; } = [];

    /// <summary>The declared message topics (<c>--topic</c>, once each): <c>&lt;filter&gt;[;...]</c>; none by default.</summary>
    public IList<string> Topics { get; } = [];

    /// <summary>The named message queues (<c>--queue</c>, once each): <c>&lt;name&gt;[;durable]</c>; none by default.</summary>
    public IList<string> Queues { get; } = [];

    /// <summary>The topic aliases (<c>--topic-map</c>, once each): <c>&lt;scheme&gt;:&lt;name&gt;=&lt;scheme&gt;:&lt;name&gt;</c>; none by default, so the default name mapping applies.</summary>
    public IList<string> TopicMaps { get; } = [];

    /// <summary>The topic grants (<c>--topic-access</c>, once each): <c>&lt;filter&gt;=&lt;member&gt;:&lt;right&gt;,...</c>; none by default, so every caller may publish and subscribe.</summary>
    public IList<string> TopicAccess { get; } = [];

    /// <summary>
    /// Config files read at start (<c>--config</c>, ADR-0101 decision 2), in order, after every property above and before
    /// <see cref="AdditionalArguments"/>, so <see cref="AdditionalArguments"/> can override a file and a file the properties.
    /// A refusal inside a file is <c>surl</c>'s, prefixed by <c>&lt;file&gt;:&lt;line&gt;: </c>. <c>-</c> (standard input) is refused.
    /// None by default.
    /// </summary>
    public IList<string> ConfigFiles { get; } = [];

    /// <summary>
    /// Any other <c>surl</c> option, as <c>surl</c>'s own words, read after the properties above.
    /// <c>--help</c>, <c>--aihelp</c>, <c>--manual</c>, <c>--version</c>, <c>--log-file</c>,
    /// <c>--trace</c> and <c>--trace-ascii</c> belong to a host and are refused.
    /// </summary>
    public IList<string> AdditionalArguments { get; } = [];

    /// <summary>
    /// Where the start-up warnings, the status lines and the exchange log are written, at
    /// <see cref="LogLevel"/>, byte for byte as <c>surl</c> writes them; <see langword="null"/>, the
    /// default, writes nothing.
    /// </summary>
    public TextWriter? Log { get; set; }

    /// <summary>
    /// The level <see cref="Log"/> is written at (<c>--log-level</c>, ADR-0033);
    /// <see cref="Output.LogLevel.Info"/> by default.
    /// </summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Info;

    /// <summary>The one clock every exchange runs on; <see cref="TimeProvider.System"/> by default.</summary>
    /// <exception cref="ArgumentNullException">Set to <see langword="null"/>.</exception>
    public TimeProvider TimeProvider
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = TimeProvider.System;

    /// <summary>
    /// Whether the extended protocol servers are served by this server (ADR-0083, decision 6), or
    /// <see langword="null"/>, the default, to read the process's <c>SURL_EXTENSIONS</c> at start.
    /// </summary>
    public bool? AreExtensionsTurnedOn { get; set; }

    /// <summary>
    /// Whether the server records what each client sent and was sent, for
    /// <see cref="SurlServer.Exchanges"/> (ADR-0099, decision 7); off by default, and nothing is
    /// recorded or kept when off. It is not a <c>surl</c> option and adds no word.
    /// </summary>
    public bool RecordExchanges { get; set; }

    /// <summary>
    /// The most exchanges <see cref="SurlServer.Exchanges"/> keeps, 1000 by default; past it the
    /// oldest is dropped and <see cref="SurlServer.DroppedExchangeCount"/> grows. Must be positive.
    /// </summary>
    public int MaxRecordedExchanges { get; set; } = 1000;

    /// <summary>
    /// The most bytes kept of one exchange, both directions together, 1 MiB by default; past it no
    /// more of that exchange's bytes are kept and its <see cref="SurlExchangeRecord.IsTruncated"/> is
    /// true. Must be positive.
    /// </summary>
    public int MaxRecordedBytesPerExchange { get; set; } = 1_048_576;

    /// <summary>
    /// The most events <see cref="SurlServer.FindEvents"/> and <see cref="SurlServer.WaitForEventAsync"/> keep,
    /// without their bytes, 10000 by default (ADR-0124 decision 1); past it the oldest is dropped and
    /// <see cref="SurlServer.DroppedEventCount"/> grows. Kept only while <see cref="RecordExchanges"/> is on.
    /// Must be positive.
    /// </summary>
    public int MaxRecordedEvents { get; set; } = 10000;

    /// <summary>The shortest <see cref="HookTimeLimit"/>: 1 millisecond.</summary>
    public static readonly TimeSpan MinHookTimeLimit = TimeSpan.FromMilliseconds(1);

    /// <summary>The longest <see cref="HookTimeLimit"/>: 30 seconds.</summary>
    public static readonly TimeSpan MaxHookTimeLimit = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long a response override (<see cref="SurlServerFaults.AddOverride"/>) may take to decide, on
    /// <see cref="TimeProvider"/> (ADR-0124, decision 5): 1 second by default, from <see cref="MinHookTimeLimit"/> to
    /// <see cref="MaxHookTimeLimit"/>. Past it the point is served as if the override were not there and a
    /// <see cref="HookFailedEvent"/> with <see cref="HookFailure.TimedOut"/> is published. It is not a <c>surl</c>
    /// option and adds no word.
    /// </summary>
    public TimeSpan HookTimeLimit { get; set; } = TimeSpan.FromSeconds(1);
}
