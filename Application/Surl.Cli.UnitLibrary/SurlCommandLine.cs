using System.Security.Authentication;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// A command line that asks <c>surl</c> to serve: the listen URLs and every Phase 1 option
/// value, with ADR-0007 section 2's default for each option not given (ADR-0031's for
/// <c>--directory</c>). A new instance holds only the defaults and no listen URL.
/// </summary>
/// <remarks>
/// Seconds are <see cref="TimeSpan"/>s and 0 seconds is <see cref="Timeout.InfiniteTimeSpan"/>;
/// a byte or connection count of 0 is no limit (ADR-0006 section 1).
/// </remarks>
public sealed record SurlCommandLine
{
    /// <summary>The listen URLs, in command-line order.</summary>
    public IReadOnlyList<ListenUrl> ListenUrls { get; init; } = [];

    /// <summary>
    /// Every <c>-K</c>/<c>--config</c> file read, as given, in the order each was opened, the ones a
    /// config file names included and <c>-</c> for standard input, so a file other users can read is
    /// warned at start (ADR-0101, decision 5). None by default.
    /// </summary>
    public IReadOnlyList<string> ConfigFiles { get; init; } = [];

    /// <summary>
    /// The Unix domain socket the one listen URL listens on instead of a TCP port, set by the last
    /// of <c>--unix-socket &lt;path&gt;</c> and <c>--abstract-unix-socket &lt;name&gt;</c>
    /// (ADR-0094, decision 1); <see langword="null"/> by default, when every listen URL listens
    /// on TCP. Once the whole line is read, the parser also sets it on that listen URL's
    /// <see cref="ListenUrl.UnixSocket"/>.
    /// </summary>
    public UnixSocketAddress? UnixSocket { get; init; }

    /// <summary>
    /// The data directory as given with <c>--directory</c>; <see langword="null"/> by default,
    /// when surl serves an in-memory file system instead (ADR-0031 decisions 1 and 4).
    /// </summary>
    public string? DataDirectory { get; init; }

    /// <summary>
    /// The log level (ADR-0033 section 2): set by the last of <c>-s</c>, <c>--no-silent</c>,
    /// <c>-v</c>, <c>--no-verbose</c>, <c>--log-level</c>, <c>--trace</c> and
    /// <c>--trace-ascii</c>, then raised from <see cref="LogLevel.None"/> to
    /// <see cref="LogLevel.Error"/> by <see cref="ShowError"/> once the whole line is read.
    /// <see cref="LogLevel.Info"/> by default.
    /// </summary>
    public LogLevel LogLevel { get; init; } = LogLevel.Info;

    /// <summary>
    /// <c>-S</c>/<c>--show-error</c>: whether it was given (and not reversed). The parser has
    /// already applied it to <see cref="LogLevel"/>. Off by default.
    /// </summary>
    public bool ShowError { get; init; }

    /// <summary>
    /// The file of the last <c>--trace</c> or <c>--trace-ascii</c>, as given (<c>-</c> is
    /// stdout), when <see cref="LogLevel"/> is <see cref="LogLevel.Trace"/>; otherwise, and
    /// by default, none.
    /// </summary>
    public string? TraceFile { get; init; }

    /// <summary>
    /// The layout of the last <c>--trace</c> (<see cref="TraceDumpLayout.HexAndAscii"/>) or
    /// <c>--trace-ascii</c> (<see cref="TraceDumpLayout.Ascii"/>);
    /// <see cref="TraceDumpLayout.HexAndAscii"/> by default, as <c>--log-level trace</c>
    /// alone dumps.
    /// </summary>
    public TraceDumpLayout TraceLayout { get; init; } = TraceDumpLayout.HexAndAscii;

    /// <summary><c>--trace-time</c>: stamp each exchange log line with the local time. Off by default.</summary>
    public bool TraceTime { get; init; }

    /// <summary>
    /// <c>--log-file</c>: the file the log stream is appended to, as given (<c>-</c> is
    /// stdout); none by default, when the log stream is stderr.
    /// </summary>
    public string? LogFile { get; init; }

    /// <summary><c>--allow-uploads</c>: accept uploads into the served files. Off by default.</summary>
    public bool AllowUploads { get; init; }

    /// <summary><c>--list-directories</c>: answer directory listings. Off by default.</summary>
    public bool ListDirectories { get; init; }

    /// <summary>
    /// <c>--follow-symlinks</c>: follow a link whose final target stays inside the served
    /// directory. Off by default.
    /// </summary>
    public bool FollowSymlinks { get; init; }

    /// <summary><c>--serve-dot-files</c>: serve paths with a segment starting with <c>.</c>. Off by default.</summary>
    public bool ServeDotFiles { get; init; }

    /// <summary>
    /// <c>--allow-zone-transfer</c>: answer AXFR and IXFR from the DNS server's zones
    /// (ADR-0092 decision 4). Off by default, when every zone transfer is refused.
    /// </summary>
    public bool AllowZoneTransfer { get; init; }

    /// <summary>
    /// <c>--max-connections</c>: concurrent connections and datagram flows, all listeners
    /// together; 0 is no limit. 1024 by default.
    /// </summary>
    public int MaxConnections { get; init; } = 1024;

    /// <summary>
    /// <c>--max-connections-per-address</c>: concurrent connections and flows from one remote
    /// IP address; 0 is no limit. 100 by default.
    /// </summary>
    public int MaxConnectionsPerAddress { get; init; } = 100;

    /// <summary><c>--idle-timeout</c>: close an exchange after this long with no byte moving. 120 seconds by default.</summary>
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary><c>-m</c>/<c>--max-time</c>: the longest one exchange may last. 3600 seconds by default.</summary>
    public TimeSpan MaxTime { get; init; } = TimeSpan.FromSeconds(3600);

    /// <summary>
    /// The per-exchange limits (ADR-0007 section 3): <c>--head-timeout</c> sets
    /// <see cref="ExchangeLimits.HeadTimeout"/>, <c>--max-request-head</c>
    /// <see cref="ExchangeLimits.MaxRequestHeadBytes"/>, <c>--max-line</c>
    /// <see cref="ExchangeLimits.MaxLineBytes"/>, <c>--max-message</c>
    /// <see cref="ExchangeLimits.MaxMessageBytes"/> and <c>--max-filesize</c>
    /// <see cref="ExchangeLimits.MaxUploadBytes"/>. <see cref="ExchangeLimits.Default"/> by default.
    /// </summary>
    public ExchangeLimits Limits { get; init; } = ExchangeLimits.Default;

    /// <summary>
    /// The lowest TLS version accepted, set by the last of <c>--tlsv1.0</c> to <c>--tlsv1.3</c>;
    /// <see cref="SslProtocols.Tls12"/> by default.
    /// </summary>
    public SslProtocols LowestTlsVersion { get; init; } = SslProtocols.Tls12;

    /// <summary><c>--tls-max</c>: the highest TLS version accepted; <see cref="SslProtocols.Tls13"/> by default.</summary>
    public SslProtocols HighestTlsVersion { get; init; } = SslProtocols.Tls13;

    /// <summary><c>--cert</c>: the server certificate file for secure schemes, as given; none by default.</summary>
    public string? CertificateFile { get; init; }

    /// <summary><c>--cert-type</c>: the format of <see cref="CertificateFile"/>; <see cref="CertificateFileFormat.Pem"/> by default.</summary>
    public CertificateFileFormat CertificateType { get; init; } = CertificateFileFormat.Pem;

    /// <summary><c>--key</c>: the private key file for <see cref="CertificateFile"/>, as given; none by default.</summary>
    public string? KeyFile { get; init; }

    /// <summary><c>--key-type</c>: the format of <see cref="KeyFile"/>; <see cref="CertificateFileFormat.Pem"/> by default.</summary>
    public CertificateFileFormat KeyType { get; init; } = CertificateFileFormat.Pem;

    /// <summary>
    /// <c>--pass</c>: the passphrase for an encrypted <see cref="KeyFile"/>, a PKCS#12
    /// <see cref="CertificateFile"/> or an encrypted <see cref="HostKeyFiles"/> key (ADR-0051
    /// decision 4), as given, the empty string included; none by default.
    /// </summary>
    public string? KeyPassphrase { get; init; }

    /// <summary>
    /// <c>--cacert</c>: the file of trust anchors that verify a client certificate, as given;
    /// none by default.
    /// </summary>
    public string? CaCertificateFile { get; init; }

    /// <summary><c>--tlsauthtype</c>: how every TLS listener authenticates; none (certificates) by default (ADR-0122 decision 6).</summary>
    public TlsAuthenticationType? TlsAuthenticationType { get; init; }

    /// <summary><c>--tlsuser</c>: the one TLS-SRP account's user name, as given; none by default (ADR-0122 decision 4).</summary>
    public string? TlsUser { get; init; }

    /// <summary><c>--tlspassword</c>: the one TLS-SRP account's password, as given; none by default (ADR-0122 decision 4).</summary>
    public string? TlsPassword { get; init; }

    /// <summary><c>--srp-verifiers</c>: a GnuTLS <c>tpasswd</c> file of TLS-SRP accounts, as given; none by default (ADR-0122 decision 4).</summary>
    public string? SrpVerifierFile { get; init; }

    /// <summary><c>--srp-groups</c>: the GnuTLS <c>tpasswd.conf</c> file <see cref="SrpVerifierFile"/>'s index column names, as given; none by default.</summary>
    public string? SrpGroupFile { get; init; }

    /// <summary>
    /// Whether every TLS listener serves TLS-SRP instead of a certificate: any of <c>--tlsauthtype</c>,
    /// <c>--tlsuser</c>, <c>--tlspassword</c>, <c>--srp-verifiers</c> and <c>--srp-groups</c> is given
    /// (ADR-0122 decision 6; <c>--tlsuser</c> with <c>--tlspassword</c> implies <c>--tlsauthtype SRP</c>, as in curl).
    /// </summary>
    public bool ServesTlsSrp =>
        TlsAuthenticationType is not null || TlsUser is not null || TlsPassword is not null || SrpVerifierFile is not null || SrpGroupFile is not null;

    /// <summary>
    /// The accounts from every <c>-u</c>/<c>--user</c>, in command-line order: each one adds an
    /// account, and no user name is given twice (ADR-0032 section 1). None by default.
    /// </summary>
    public IReadOnlyList<CommandLineAccount> Accounts { get; init; } = [];

    /// <summary>
    /// <c>--user-file</c>: the file of more accounts, as given; none by default. It is read
    /// when surl starts serving, not while parsing (ADR-0032 section 2).
    /// </summary>
    public string? UserFile { get; init; }

    /// <summary>
    /// The proxy listen URLs from every <c>-x</c>/<c>--proxy</c>, in command-line order, each read by
    /// <see cref="ListenUrlParser.ParseProxy"/> and so <c>http</c> or <c>https</c> with
    /// <see cref="ListenUrl.IsProxy"/> set (ADR-0088 decision 7). None by default.
    /// </summary>
    public IReadOnlyList<ListenUrl> ProxyListenUrls { get; init; } = [];

    /// <summary>
    /// The proxy's accounts from every <c>-U</c>/<c>--proxy-user</c>, in command-line order, read as <c>-u</c>
    /// reads (ADR-0088 decision 5). They are the proxy's alone, separate from <see cref="Accounts"/>. None by
    /// default, when the proxy takes no login.
    /// </summary>
    public IReadOnlyList<CommandLineAccount> ProxyAccounts { get; init; } = [];

    /// <summary>
    /// <c>--proxy-user-file</c>: the file of more proxy accounts in ADR-0032 section 2's format, as given; none by
    /// default. It is read when surl starts serving, not while parsing (ADR-0088 decision 5).
    /// </summary>
    public string? ProxyUserFile { get; init; }

    /// <summary>
    /// The <c>--proxy-auth</c> words of the last <c>--proxy-auth</c>, lower-case and in ADR-0032 section 3's order,
    /// the methods the proxy accepts and offers (ADR-0088 decision 5); <see langword="null"/> when
    /// <c>--proxy-auth</c> was not given. They are the proxy's alone, separate from
    /// <see cref="GivenAuthenticationMethods"/>.
    /// </summary>
    public IReadOnlyList<string>? GivenProxyAuthenticationMethods { get; init; }

    /// <summary>
    /// The methods the proxy accepts: <see cref="GivenProxyAuthenticationMethods"/>, or ADR-0088 decision 5's
    /// default set (<c>digest</c> and <c>basic</c>) when <c>--proxy-auth</c> was not given, in ADR-0032 section 3's
    /// order.
    /// </summary>
    public IReadOnlyList<string> AcceptedProxyAuthenticationMethods => GivenProxyAuthenticationMethods ?? DefaultProxyAuthenticationMethods;

    /// <summary>
    /// The ranges from every <c>--proxy-allow-origin</c>, in command-line order, that widen the proxy's
    /// loopback-only origin rule (ADR-0088 decisions 2 and 7). None by default.
    /// </summary>
    public IReadOnlyList<ProxyAllowedOrigin> ProxyAllowedOrigins { get; init; } = [];

    /// <summary>
    /// The groups from every <c>--group</c>, in command-line order; no group name is given twice
    /// (ADR-0101 decisions 4 and 5). None by default. <c>Surl.Composition</c> expands them to
    /// account names at start (ADR-0102 decision 4.1).
    /// </summary>
    public IReadOnlyList<ConfiguredGroup> Groups { get; init; } = [];

    /// <summary>
    /// The mail domains the mail store serves, each as written, in configuration order (ADR-0101
    /// decision 4); <see cref="MailDomainNames.ReadDomain"/> gives the form they are compared in
    /// (ADR-0102 decision 1.1). None by default; each <c>--mail-domain</c> adds one.
    /// </summary>
    public IReadOnlyList<string> MailDomains { get; init; } = [];

    /// <summary>
    /// The mailboxes, each an account and a mail owner named by its normalised address
    /// (<see cref="ConfiguredMailboxReader"/>, ADR-0102 decision 1.2), in configuration order. None
    /// by default; each <c>--mailbox</c> adds one.
    /// </summary>
    public IReadOnlyList<ConfiguredMailbox> Mailboxes { get; init; } = [];

    /// <summary>
    /// The mailbox CSV files, each path as given, in configuration order. They are read when surl
    /// starts serving, by <see cref="MailboxFileReader"/>, not while parsing (ADR-0101 decision 4).
    /// None by default; each <c>--mailbox-file</c> adds one.
    /// </summary>
    public IReadOnlyList<string> MailboxFiles { get; init; } = [];

    /// <summary>
    /// The mail aliases (<see cref="ConfiguredMailAliasReader"/>, ADR-0102 decision 1.3), in configuration order;
    /// no alias address is given twice, outside every mail domain or also a mailbox (ADR-0101 decision 5,
    /// <see cref="ConfiguredMailConflicts"/>). None by default. <c>Surl.Composition</c> hands them to the mail
    /// store, which checks their targets at start (BL-994).
    /// </summary>
    public IReadOnlyList<ConfiguredMailAlias> MailAliases { get; init; } = [];

    /// <summary>
    /// The mail catch-alls (<see cref="ConfiguredMailCatchAllReader"/>, ADR-0102 decision 1.3), in configuration
    /// order; no domain is given twice or is outside the mail domains (ADR-0101 decision 5,
    /// <see cref="ConfiguredMailConflicts"/>). None by default, so mail to an unknown address is discarded.
    /// </summary>
    public IReadOnlyList<ConfiguredMailCatchAll> MailCatchAlls { get; init; } = [];

    /// <summary>
    /// The shared IMAP folders (<see cref="ConfiguredSharedFolderReader"/>, ADR-0102 decision 1.6), in
    /// configuration order; no name is given twice (ADR-0101 decision 5, <see cref="ConfiguredMailConflicts"/>).
    /// None by default. <c>Surl.Composition</c> expands their members at start (BL-994).
    /// </summary>
    public IReadOnlyList<ConfiguredSharedFolder> SharedFolders { get; init; } = [];

    /// <summary>
    /// The shares, each a top-level content folder on disk (<see cref="ConfiguredShareReader"/>, ADR-0102
    /// decision 2.1), in configuration order; no share name is given twice (ADR-0101 decision 5). None by
    /// default. <c>Surl.Composition</c> opens each path and expands each scheme to its TLS twin at start (BL-988).
    /// </summary>
    public IReadOnlyList<ConfiguredShare> Shares { get; init; } = [];

    /// <summary>
    /// Who may read or write in each share (<see cref="ConfiguredShareAccessReader"/>, ADR-0102 decision 2.2), in
    /// configuration order; each names a <see cref="Shares"/> entry, and no share twice (ADR-0101 decision 5).
    /// None by default, so every share is open as the root is.
    /// </summary>
    public IReadOnlyList<ConfiguredShareAccess> ShareAccess { get; init; } = [];

    /// <summary>
    /// The buckets the object-storage servers map onto content paths (<see cref="ConfiguredBucketReader"/>,
    /// ADR-0102 decision 2.4), in configuration order; no bucket name is given twice or is also a share's
    /// (ADR-0101 decision 5). None by default.
    /// </summary>
    public IReadOnlyList<ConfiguredBucket> Buckets { get; init; } = [];

    /// <summary>
    /// The topic filters declared to the message bus (<see cref="ConfiguredTopicReader"/>, ADR-0102 decision
    /// 3.1), in configuration order; no filter is given twice (ADR-0101 decision 5). None by default.
    /// </summary>
    public IReadOnlyList<ConfiguredTopic> Topics { get; init; } = [];

    /// <summary>
    /// The named queues the message bus creates at start (<see cref="ConfiguredQueueReader"/>, ADR-0102
    /// decision 3.2), in configuration order; no name is given twice (ADR-0101 decision 5). None by default.
    /// </summary>
    public IReadOnlyList<ConfiguredQueue> Queues { get; init; } = [];

    /// <summary>
    /// The topic maps between protocols (<see cref="ConfiguredTopicMapReader"/>, ADR-0102 decision 3.3), in
    /// configuration order; no source is given twice (ADR-0101 decision 5). None by default, so ADR-0100
    /// decision 3.2's default mapping applies.
    /// </summary>
    public IReadOnlyList<ConfiguredTopicMap> TopicMaps { get; init; } = [];

    /// <summary>
    /// Who may publish or subscribe on each filter (<see cref="ConfiguredTopicAccessReader"/>, ADR-0102 decision
    /// 3.4), in configuration order; no filter is given twice (ADR-0101 decision 5). None by default, so every
    /// caller may publish and subscribe.
    /// </summary>
    public IReadOnlyList<ConfiguredTopicAccess> TopicAccess { get; init; } = [];

    /// <summary>
    /// The fault rules from every <c>--fault</c>, in command-line order, at most 256 (ADR-0124
    /// decision 4). None by default. <c>Surl.Composition</c> adds them to the fault plan at start.
    /// </summary>
    public IReadOnlyList<ConfiguredFault> Faults { get; init; } = [];

    /// <summary>
    /// <c>--control</c>: the listen URL of the test control endpoint, always <c>http</c> on loopback
    /// (ADR-0124 decision 7); null, the default, serves none. The last one given wins.
    /// </summary>
    public ListenUrl? ControlListenUrl { get; init; }

    /// <summary>
    /// <c>--control-token-file</c>: the file whose first line is the control endpoint's bearer token,
    /// as given; none by default. It is given exactly when <see cref="ControlListenUrl"/> is, and it is
    /// read when surl starts serving, not while parsing (ADR-0124 decision 7).
    /// </summary>
    public string? ControlTokenFile { get; init; }

    /// <summary>
    /// <c>--random-seed</c>: the seed, 0 to <see cref="long.MaxValue"/>, for the non-secret identifiers an
    /// instance draws, so a test gets the same ones every run (ADR-0124 decision 6); null, the default, leaves
    /// them unseeded. The last one given wins. Nothing secret is ever drawn from it.
    /// </summary>
    public long? RandomSeed { get; init; }

    /// <summary>
    /// <c>--keytab</c>: the MIT keytab file of Kerberos service keys, as given; none by default.
    /// It is read when surl starts serving, not while parsing (ADR-0057 decision 1).
    /// </summary>
    public string? KeytabFile { get; init; }

    /// <summary>
    /// <c>--allow-anonymous</c>: accept every request and login without checking credentials.
    /// Off by default.
    /// </summary>
    public bool AllowAnonymous { get; init; }

    /// <summary>
    /// <c>--allow-plaintext-auth</c>: accept passwords and tokens over unencrypted connections.
    /// Off by default.
    /// </summary>
    public bool AllowPlaintextAuthentication { get; init; }

    /// <summary>
    /// The <c>--auth</c> words of the last <c>--auth</c>, lower-case and in ADR-0032 section 3's
    /// order; <see langword="null"/> when <c>--auth</c> was not given.
    /// </summary>
    public IReadOnlyList<string>? GivenAuthenticationMethods { get; init; }

    /// <summary>
    /// The authentication methods accepted: <see cref="GivenAuthenticationMethods"/>, or
    /// ADR-0049 section 3's default set (<c>digest</c>, <c>cram-md5</c>, <c>basic</c>, <c>plain</c>,
    /// <c>login</c>, <c>bearer</c>, <c>oauthbearer</c>, <c>xoauth2</c>, <c>external</c> and <c>aws-sigv4</c>) when
    /// <c>--auth</c> was not given (ADR-0032 section 1), in ADR-0032 section 3's order.
    /// </summary>
    public IReadOnlyList<string> AcceptedAuthenticationMethods => GivenAuthenticationMethods ?? DefaultAuthenticationMethods;

    /// <summary>
    /// <c>--self-signed</c>: serve a throwaway self-signed certificate when no <c>--cert</c> is
    /// given. Off by default.
    /// </summary>
    public bool SelfSigned { get; init; }

    /// <summary>
    /// <c>--s3-plaintext</c>: serve every <c>s3</c> listen URL as plain HTTP/1.1, with no TLS and no
    /// certificate (ADR-0108 decision 5). Off by default.
    /// </summary>
    public bool S3Plaintext { get; init; }

    /// <summary>
    /// <c>--az-plaintext</c>: serve every <c>az</c> listen URL as plain HTTP/1.1, with no TLS and no
    /// certificate (ADR-0109 decision 6). Off by default.
    /// </summary>
    public bool AzPlaintext { get; init; }

    /// <summary>
    /// <c>--oci-plaintext</c>: serve every <c>oci</c> listen URL as plain HTTP/1.1, with no TLS and no
    /// certificate, its token realm <c>http</c> (ADR-0115 decision 6). Off by default.
    /// </summary>
    public bool OciPlaintext { get; init; }

    /// <summary>
    /// <c>--dns-transport</c>: the transports every <c>dns</c> listen URL serves, <c>udp</c>, <c>tcp</c>
    /// and <c>tls</c>, in the order given (ADR-0092 decision 5). <c>udp</c> then <c>tcp</c> by default.
    /// </summary>
    public IReadOnlyList<string> DnsTransports { get; init; } = DefaultDnsTransports;

    /// <summary>
    /// <c>--kafka-fetch-compression</c>: the codec of the record batches the Kafka broker serves to a fetch,
    /// <c>none</c>, <c>gzip</c>, <c>snappy</c>, <c>lz4</c> or <c>zstd</c> (ADR-0105 decision 4). <c>none</c> by default.
    /// </summary>
    public string KafkaFetchCompression { get; init; } = "none";

    /// <summary>
    /// <c>--mqtt5-disconnect-after-retained</c>: the MQTT 5 broker sends <c>DISCONNECT</c> <c>0x00</c> and closes after a
    /// <c>SUBACK</c> and the retained messages it releases (ADR-0093 decision 9). Off by default.
    /// </summary>
    public bool Mqtt5DisconnectAfterRetained { get; init; }

    /// <summary>The transports <c>--dns-transport</c> defaults to; one shared list, so two default command lines are equal.</summary>
    public static readonly IReadOnlyList<string> DefaultDnsTransports = ["udp", "tcp"];

    /// <summary>
    /// The <c>dns</c> listen URLs written with a port, which <c>--dns-transport</c> puts every
    /// transport on (ADR-0092 decision 5); the others take each transport's own default port.
    /// None by default.
    /// </summary>
    public IReadOnlyList<ListenUrl> DnsListenUrlsWithPort { get; init; } = [];

    /// <summary>
    /// The SSH host private key files from every <c>--hostkey</c>, as given, in command-line order:
    /// each one adds a key (ADR-0051 decision 4). None by default.
    /// </summary>
    public IReadOnlyList<string> HostKeyFiles { get; init; } = [];

    /// <summary>
    /// The OpenSSH host certificate files from every <c>--hostcert</c>, as given, in command-line
    /// order (ADR-0051 decision 4). None by default.
    /// </summary>
    public IReadOnlyList<string> HostCertificateFiles { get; init; } = [];

    /// <summary>
    /// <c>--throwaway-hostkey</c>: make a throwaway RSA host key at start when an <c>scp</c> or
    /// <c>sftp</c> URL is served and no <c>--hostkey</c> is given (ADR-0051 decision 4). Off by default.
    /// </summary>
    public bool ThrowawayHostKey { get; init; }

    /// <summary>
    /// The users and <c>authorized_keys</c> files from every <c>--authorized-keys</c>, in
    /// command-line order; no user name is given twice (ADR-0051 decision 6). None by default.
    /// </summary>
    public IReadOnlyList<CommandLineAuthorizedKeys> AuthorizedKeys { get; init; } = [];

    /// <summary>
    /// <c>--allow-weak-ssh-algorithms</c>: also offer ADR-0051 decision 2's weak SSH algorithms.
    /// Off by default.
    /// </summary>
    public bool AllowWeakSshAlgorithms { get; init; }

    /// <summary>
    /// The SSH cipher names of the last <c>--ssh-ciphers</c>, as given and in the order given, the
    /// only ciphers the SSH server offers (ADR-0066); <see langword="null"/> when it was not given.
    /// </summary>
    public IReadOnlyList<string>? SshCiphers { get; init; }

    /// <summary>
    /// The SSH MAC names of the last <c>--ssh-macs</c>, as given and in the order given, the only
    /// MACs the SSH server offers (ADR-0066); <see langword="null"/> when it was not given.
    /// </summary>
    public IReadOnlyList<string>? SshMacs { get; init; }

    /// <summary>
    /// <c>--ws-echo</c>: the WebSocket server echoes every client message instead of sending the
    /// requested path (ADR-0071 decision 4). Off by default.
    /// </summary>
    public bool WsEcho { get; init; }

    /// <summary>
    /// <c>--http3</c>: every <c>https</c> listen URL also serves HTTP/3 over QUIC on UDP at the
    /// same address and port, and its HTTP/1.x and HTTP/2 answers carry <c>Alt-Svc</c> (ADR-0082
    /// decision 6). Off by default.
    /// </summary>
    public bool Http3 { get; init; }

    /// <summary>The methods accepted without <c>--auth</c>, in ADR-0032 section 3's order.</summary>
    private static readonly IReadOnlyList<string> DefaultAuthenticationMethods =
        ["digest", "cram-md5", "basic", "plain", "login", "bearer", "oauthbearer", "xoauth2", "external", "aws-sigv4"];

    /// <summary>The methods the proxy accepts without <c>--proxy-auth</c>, in ADR-0032 section 3's order.</summary>
    private static readonly IReadOnlyList<string> DefaultProxyAuthenticationMethods = ["digest", "basic"];
}
