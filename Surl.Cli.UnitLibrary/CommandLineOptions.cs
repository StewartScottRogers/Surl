using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Security.Authentication;
using Surl.Output;

namespace Surl.Cli;

/// <summary>
/// ADR-0007 section 2's Phase 1 option table, with ADR-0010 section 3's <c>--cert-type</c>,
/// <c>--key-type</c> and <c>--pass</c>, ADR-0033 section 2's logging options and ADR-0032 section 1's
/// authentication options, looked up by long and by short name. Each row carries
/// its help (ADR-0034 decision 2), so an option cannot be added without it.
/// </summary>
internal static class CommandLineOptions
{
    // ADR-0034 decision 5's paragraphs, each checked against the code it describes.
    private const string AllowAnonymousExplanation =
        "Accepts every request and every login without checking credentials: HTTP serves every request as "
        + "anonymous and sends no challenge, MQTT answers every well-formed CONNECT with CONNACK 0 whatever credentials it "
        + "carries, SMTP takes mail with no login, and SSH logs every client in, with any credential or none. "
        + "A test uses it to fetch or publish without setting up accounts. It is not the default because anyone "
        + "who can reach a listener then gets everything surl serves, and can publish and subscribe over MQTT and send mail, "
        + "with no login at all. surl warns on every start while it is on, from the info log level up.";

    private const string AllowPlaintextAuthExplanation =
        "Accepts passwords and tokens sent over an unencrypted connection: HTTP Basic and Bearer over http:// "
        + "and an MQTT password over mqtt:// are checked instead of refused unchecked (403 Forbidden, CONNACK 5), "
        + "Basic and Bearer are offered in a 401 over http://, and SMTP offers PLAIN and LOGIN over smtp:// without STARTTLS. A test uses it to log in without a certificate. "
        + "It is not the default because anyone who can watch the network reads the password as it is sent. "
        + "surl warns on every start while it is on, from the info log level up.";

    private const string AuthExplanation =
        "Sets the authentication methods surl accepts and offers, a comma-separated list in any case. For HTTP: "
        + "negotiate, ntlm, digest, basic, bearer and aws-sigv4. For SMTP, IMAP and POP3 logins, each SASL "
        + "mechanism by its name in lower case, as curl's login option AUTH=<mech> names it: ntlm, digest-md5, "
        + "cram-md5, plain, login, oauthbearer, xoauth2 and external, and apop for POP3's APOP; of those three "
        + "protocols this build serves only SMTP yet. external logs in as the TLS client certificate --cacert verifies, so it is "
        + "offered only on a connection that sent one. gssapi is read, but a start that gives it is refused as "
        + "not available in this build (exit code 2). surl refuses a word outside the "
        + "list as an option badly used (exit code 2). A test uses it to offer one method alone, such as --auth "
        + "digest for curl's --digest. ntlm and negotiate are not in the default because an NTLM response is built "
        + "on MD4 and HMAC-MD5 of the password and is open to relay and offline cracking, and Negotiate carries "
        + "NTLM; digest-md5 is not because RFC 6331 made it Historic and curl picks it over every other mechanism; "
        + "apop is not because its MD5 construction leaks password characters to anyone who can choose the "
        + "timestamp it signs. surl warns on every start while --auth is given, from the info log level up, "
        + "naming the methods it accepts.";

    private const string SelfSignedExplanation =
        "Serves a throwaway self-signed certificate, made at start, for a listen URL of a scheme that starts "
        + "with TLS (such as https) when no --cert is given; without it, and without --cert, such a URL is "
        + "refused at start. A test uses it to serve a secure scheme without a certificate file; curl then needs "
        + "-k. It is not the default because no client can verify the certificate, so a client cannot tell surl "
        + "from anyone else on the path. It cannot be used with --cert. surl warns when it makes the certificate, "
        + "from the info log level up.";

    private const string ThrowawayHostKeyExplanation =
        "Makes a throwaway RSA 3072-bit SSH host key at start for an scp or sftp listen URL when no --hostkey is "
        + "given; without it, and without --hostkey, such a URL is refused at start. A test uses it to serve "
        + "SSH without a key file; curl then needs the key's SHA-256 hash pinned, or -k. It is not the default "
        + "because no client can know the key beforehand, so a client cannot tell surl from anyone else on the path. "
        + "It cannot be used with --hostkey. surl warns when it makes the key, from the info log level up, naming "
        + "the hash to pin.";

#pragma warning disable SYSLIB0039 // --tlsv1.0 and --tlsv1.1 name the old versions on purpose (ADR-0007 section 2).
    private static readonly CommandLineOption[] Table =
    [
        new("help", 'h', CommandLineOptionKind.Help, Negatable: false, SetFlag: null, ApplyArgument: null, OptionArgumentType.OptionalSubject,
            new("<subject>", "Get help for commands", ["surl"], IsInShortList: true, Default: null)),
        new("aihelp", null, CommandLineOptionKind.AiHelp, Negatable: false, SetFlag: null, ApplyArgument: null, OptionArgumentType.OptionalTopic,
            new("<topic>", "Markdown help for AI agents", ["surl"], IsInShortList: true, Default: null)),
        new("version", 'V', CommandLineOptionKind.Version, Negatable: false, SetFlag: null, ApplyArgument: null, OptionArgumentType.None,
            new(null, "Show version number and quit", ["surl"], IsInShortList: true, Default: null)),
        new("manual", 'M', CommandLineOptionKind.Manual, Negatable: false, SetFlag: null, ApplyArgument: null, OptionArgumentType.None,
            new(null, "Display the full manual", ["surl"], IsInShortList: false, Default: null)),
        Flag("verbose", 'v', negatable: true, (c, on) => c with { LogLevel = on ? LogLevel.Verbose : LogLevel.Info },
            new(null, "Log every exchange event", ["logging"], IsInShortList: true, Default: "off")),
        Flag("silent", 's', negatable: true, (c, on) => c with { LogLevel = on ? LogLevel.None : LogLevel.Info },
            new(null, "Silent mode", ["logging"], IsInShortList: true, Default: "off")),
        Flag("show-error", 'S', negatable: true, (c, on) => c with { ShowError = on },
            new(null, "Show error even when -s is used", ["logging"], IsInShortList: false, Default: "off")),
        WithArgument<LogLevel>("log-level", null, OptionArgumentReader.LogLevelWord, (c, v) => c with { LogLevel = v },
            new("<level>", "Set the log level", ["logging"], IsInShortList: false, Default: "info")),
        WithArgument<string>("trace", null, OptionArgumentReader.Path,
            (c, v) => c with { LogLevel = LogLevel.Trace, TraceFile = v, TraceLayout = TraceDumpLayout.HexAndAscii },
            new("<file>", "Write a debug trace to <file>", ["logging"], IsInShortList: false, Default: "none")),
        WithArgument<string>("trace-ascii", null, OptionArgumentReader.Path,
            (c, v) => c with { LogLevel = LogLevel.Trace, TraceFile = v, TraceLayout = TraceDumpLayout.Ascii },
            new("<file>", "Like --trace, but without hex", ["logging"], IsInShortList: false, Default: "none")),
        Flag("trace-time", null, negatable: true, (c, on) => c with { TraceTime = on },
            new(null, "Add time stamps to log lines", ["logging"], IsInShortList: false, Default: "off")),
        WithArgument<string>("log-file", null, OptionArgumentReader.Path, (c, v) => c with { LogFile = v },
            new("<file>", "Append the log to <file>", ["logging"], IsInShortList: false, Default: "stderr")),
        WithArgument<string>("directory", null, OptionArgumentReader.Path, (c, v) => c with { DataDirectory = v },
            new("<directory>", "Data directory, else in memory", ["content", "dict", "gopher", "http", "mqtt", "smtp", "ssh", "tftp"], IsInShortList: true, Default: "in memory")),
        Flag("allow-uploads", null, negatable: true, (c, on) => c with { AllowUploads = on },
            new(null, "Accept uploads into served files", ["content", "security", "ssh", "tftp"], IsInShortList: true, Default: "off")),
        Flag("list-directories", null, negatable: true, (c, on) => c with { ListDirectories = on },
            new(null, "Answer directory listings", ["content", "gopher", "security", "ssh"], IsInShortList: true, Default: "off")),
        Flag("follow-symlinks", null, negatable: true, (c, on) => c with { FollowSymlinks = on },
            new(null, "Follow links that stay in the root", ["content", "dict", "gopher", "http", "security", "ssh", "tftp"], IsInShortList: false, Default: "off")),
        Flag("serve-dot-files", null, negatable: true, (c, on) => c with { ServeDotFiles = on },
            new(null, "Serve names that start with a dot", ["content", "dict", "gopher", "http", "security", "ssh", "tftp"], IsInShortList: false, Default: "off")),
        WithArgument<int>("max-connections", null, OptionArgumentReader.Number, (c, v) => c with { MaxConnections = v },
            new("<number>", "Connections at once, all listeners", ["limits"], IsInShortList: false, Default: "1024")),
        WithArgument<int>("max-connections-per-address", null, OptionArgumentReader.Number, (c, v) => c with { MaxConnectionsPerAddress = v },
            new("<number>", "Connections at once per address", ["limits"], IsInShortList: false, Default: "100")),
        WithArgument<TimeSpan>("idle-timeout", null, OptionArgumentReader.Seconds, (c, v) => c with { IdleTimeout = v },
            new("<seconds>", "Close an exchange idle this long", ["limits"], IsInShortList: false, Default: "120")),
        WithArgument<TimeSpan>("max-time", 'm', OptionArgumentReader.Seconds, (c, v) => c with { MaxTime = v },
            new("<seconds>", "Longest time one exchange may take", ["limits"], IsInShortList: false, Default: "3600")),
        WithArgument<TimeSpan>("head-timeout", null, OptionArgumentReader.Seconds, (c, v) => c with { Limits = c.Limits with { HeadTimeout = v } },
            new("<seconds>", "Time to send a request head", ["dict", "gopher", "http", "limits", "mqtt", "smtp", "ssh"], IsInShortList: false, Default: "30")),
        WithArgument<long>("max-request-head", null, OptionArgumentReader.Bytes, (c, v) => c with { Limits = c.Limits with { MaxRequestHeadBytes = v } },
            new("<bytes>", "Largest HTTP or RTSP request head", ["http", "limits"], IsInShortList: false, Default: "100k")),
        WithArgument<long>("max-line", null, OptionArgumentReader.Bytes, (c, v) => c with { Limits = c.Limits with { MaxLineBytes = v } },
            new("<bytes>", "Longest command line accepted", ["dict", "gopher", "limits", "smtp", "telnet"], IsInShortList: false, Default: "8192")),
        WithArgument<long>("max-message", null, OptionArgumentReader.Bytes, (c, v) => c with { Limits = c.Limits with { MaxMessageBytes = v } },
            new("<bytes>", "Largest framed message accepted", ["limits", "mqtt", "ssh"], IsInShortList: false, Default: "1M")),
        WithArgument<long>("max-filesize", null, OptionArgumentReader.Bytes, (c, v) => c with { Limits = c.Limits with { MaxUploadBytes = v } },
            new("<bytes>", "Largest upload accepted", ["http", "limits", "mqtt", "smtp", "ssh", "tftp"], IsInShortList: false, Default: "100M")),
        Flag("tlsv1.0", null, negatable: false, (c, _) => c with { LowestTlsVersion = SslProtocols.Tls },
            new(null, "Accept TLS 1.0 or later", ["security", "tls"], IsInShortList: false, Default: null)),
        Flag("tlsv1.1", null, negatable: false, (c, _) => c with { LowestTlsVersion = SslProtocols.Tls11 },
            new(null, "Accept TLS 1.1 or later", ["security", "tls"], IsInShortList: false, Default: null)),
        Flag("tlsv1.2", null, negatable: false, (c, _) => c with { LowestTlsVersion = SslProtocols.Tls12 },
            new(null, "Accept TLS 1.2 or later (default)", ["tls"], IsInShortList: false, Default: null)),
        Flag("tlsv1.3", null, negatable: false, (c, _) => c with { LowestTlsVersion = SslProtocols.Tls13 },
            new(null, "Accept TLS 1.3 or later", ["tls"], IsInShortList: false, Default: null)),
        WithArgument<SslProtocols>("tls-max", null, OptionArgumentReader.TlsVersion, (c, v) => c with { HighestTlsVersion = v },
            new("<version>", "Highest TLS version accepted", ["tls"], IsInShortList: false, Default: "1.3")),
        WithArgument<string>("cert", null, OptionArgumentReader.Path, (c, v) => c with { CertificateFile = v },
            new("<file>", "Server certificate file", ["tls"], IsInShortList: true, Default: "none")),
        WithArgument<CertificateFileFormat>("cert-type", null, OptionArgumentReader.CertificateType, (c, v) => c with { CertificateType = v },
            new("<type>", "Format of --cert: PEM, DER or P12", ["tls"], IsInShortList: false, Default: "PEM")),
        WithArgument<string>("key", null, OptionArgumentReader.Path, (c, v) => c with { KeyFile = v },
            new("<file>", "Private key for --cert", ["tls"], IsInShortList: true, Default: "the key in the --cert file")),
        WithArgument<CertificateFileFormat>("key-type", null, OptionArgumentReader.KeyType, (c, v) => c with { KeyType = v },
            new("<type>", "Format of --key: PEM or DER", ["tls"], IsInShortList: false, Default: "PEM")),
        WithArgument<string>("pass", null, OptionArgumentReader.Text, (c, v) => c with { KeyPassphrase = v },
            new("<phrase>", "Passphrase for --key and --hostkey", ["ssh", "tls"], IsInShortList: false, Default: "none")),
        WithArgument<string>("cacert", null, OptionArgumentReader.Path, (c, v) => c with { CaCertificateFile = v },
            new("<file>", "CA certificates for client certs", ["tls"], IsInShortList: false, Default: "none")),
        WithArgument<CommandLineAccount>("user", 'u', OptionArgumentReader.Account, (c, v) => c with { Accounts = [.. c.Accounts, v] },
            new("<user:password>", "Add an account (repeatable)", ["auth", "http", "mqtt", "smtp", "ssh"], IsInShortList: true, Default: "no accounts"))
            with { ArgumentHoldsSecret = true },
        WithArgument<string>("user-file", null, OptionArgumentReader.Path, (c, v) => c with { UserFile = v },
            new("<file>", "Read accounts from a file", ["auth", "http", "mqtt", "smtp", "ssh"], IsInShortList: true, Default: "none")),
        Flag("allow-anonymous", null, negatable: true, (c, on) => c with { AllowAnonymous = on },
            new(null, "Accept any login, or none (warns)", ["auth", "http", "mqtt", "security", "smtp", "ssh", "testing"], IsInShortList: false, Default: "off",
                AllowAnonymousExplanation)),
        Flag("allow-plaintext-auth", null, negatable: true, (c, on) => c with { AllowPlaintextAuthentication = on },
            new(null, "Accept passwords in clear (warns)", ["auth", "http", "mqtt", "security", "smtp", "testing"], IsInShortList: false, Default: "off",
                AllowPlaintextAuthExplanation)),
        WithArgument<IReadOnlyList<string>>("auth", null, OptionArgumentReader.AuthenticationMethods, (c, v) => c with { GivenAuthenticationMethods = v },
            new("<methods>", "Authentication methods accepted", ["auth", "http", "security", "smtp", "testing"], IsInShortList: false, Default: "digest,cram-md5,basic,plain,login,bearer,oauthbearer,xoauth2,external,aws-sigv4",
                AuthExplanation)),
        Flag("self-signed", null, negatable: true, (c, on) => c with { SelfSigned = on },
            new(null, "Throwaway certificate (warns)", ["security", "testing", "tls"], IsInShortList: false, Default: "off",
                SelfSignedExplanation)),
        WithArgument<string>("hostkey", null, OptionArgumentReader.Path, (c, v) => c with { HostKeyFiles = [.. c.HostKeyFiles, v] },
            new("<file>", "SSH host private key file", ["auth", "ssh"], IsInShortList: false, Default: "none")),
        WithArgument<string>("hostcert", null, OptionArgumentReader.Path, (c, v) => c with { HostCertificateFiles = [.. c.HostCertificateFiles, v] },
            new("<file>", "SSH host certificate file", ["auth", "ssh"], IsInShortList: false, Default: "none")),
        Flag("throwaway-hostkey", null, negatable: true, (c, on) => c with { ThrowawayHostKey = on },
            new(null, "Throwaway SSH host key (warns)", ["security", "ssh", "testing"], IsInShortList: false, Default: "off",
                ThrowawayHostKeyExplanation)),
        WithArgument<CommandLineAuthorizedKeys>("authorized-keys", null, OptionArgumentReader.AuthorizedKeys,
            (c, v) => c with { AuthorizedKeys = [.. c.AuthorizedKeys, v] },
            new("<user:file>", "SSH public keys a user may use", ["auth", "ssh"], IsInShortList: false, Default: "none")),
        Flag("allow-weak-ssh-algorithms", null, negatable: true, (c, on) => c with { AllowWeakSshAlgorithms = on },
            new(null, "Offer weak SSH algorithms (warns)", ["security", "ssh"], IsInShortList: false, Default: "off")),
    ];
#pragma warning restore SYSLIB0039

    private static readonly FrozenDictionary<string, CommandLineOption> ByLongName =
        Table.ToFrozenDictionary(option => option.LongName, StringComparer.Ordinal);

    private static readonly FrozenDictionary<char, CommandLineOption> ByShortName =
        Table.Where(option => option.ShortName is not null).ToFrozenDictionary(option => option.ShortName!.Value);

    /// <summary>Every option in the table, in table order.</summary>
    public static IReadOnlyList<CommandLineOption> All => Table;

    /// <summary>Finds an option by its long name, spelled in full, without <c>--</c>.</summary>
    /// <param name="longName">The long name (<c>max-time</c>).</param>
    /// <param name="option">The option, when found.</param>
    /// <returns><see langword="true"/> when the table has the option.</returns>
    public static bool TryFindLong(string longName, [NotNullWhen(true)] out CommandLineOption? option) =>
        ByLongName.TryGetValue(longName, out option);

    /// <summary>Finds an option by its short name, without <c>-</c>.</summary>
    /// <param name="shortName">The short name (<c>m</c>).</param>
    /// <param name="option">The option, when found.</param>
    /// <returns><see langword="true"/> when the table has the option.</returns>
    public static bool TryFindShort(char shortName, [NotNullWhen(true)] out CommandLineOption? option) =>
        ByShortName.TryGetValue(shortName, out option);

    private static CommandLineOption Flag(string longName, char? shortName, bool negatable, SetFlag setFlag, OptionHelp help) =>
        new(
            longName,
            shortName,
            CommandLineOptionKind.Flag,
            negatable,
            setFlag,
            ApplyArgument: null,
            negatable ? OptionArgumentType.NegatableFlag(longName) : OptionArgumentType.NotNegatableFlag,
            help);

    private static CommandLineOption WithArgument<T>(
        string longName,
        char? shortName,
        OptionArgumentReading<T> reading,
        Func<SurlCommandLine, T, SurlCommandLine> set,
        OptionHelp help)
    {
        string? Apply(string argument, ref SurlCommandLine commandLine)
        {
            var failure = reading.Read(argument, out var value);
            if (failure is null)
            {
                commandLine = set(commandLine, value);
            }

            return failure;
        }

        return new(longName, shortName, CommandLineOptionKind.Argument, Negatable: false, SetFlag: null, Apply, reading.Type, help);
    }
}
