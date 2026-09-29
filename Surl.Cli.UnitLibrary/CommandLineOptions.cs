using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Security.Authentication;
using Surl.Output;

namespace Surl.Cli;

/// <summary>
/// ADR-0007 section 2's Phase 1 option table, with ADR-0010 section 3's <c>--cert-type</c>,
/// <c>--key-type</c> and <c>--pass</c> and ADR-0033 section 2's logging options, looked up by long and by short name. Each row carries
/// its help (ADR-0034 decision 2), so an option cannot be added without it.
/// </summary>
internal static class CommandLineOptions
{
    /// <summary>Reads an argument of one kind; returns the refusal reason, or null.</summary>
    private delegate string? ReadArgument<T>(string argument, out T value);

#pragma warning disable SYSLIB0039 // --tlsv1.0 and --tlsv1.1 name the old versions on purpose (ADR-0007 section 2).
    private static readonly CommandLineOption[] Table =
    [
        new("help", 'h', CommandLineOptionKind.Help, Negatable: false, SetFlag: null, ApplyArgument: null,
            new("<subject>", "Get help for commands", ["surl"], IsInShortList: true, Default: null)),
        new("version", 'V', CommandLineOptionKind.Version, Negatable: false, SetFlag: null, ApplyArgument: null,
            new(null, "Show version number and quit", ["surl"], IsInShortList: true, Default: null)),
        Flag("verbose", 'v', negatable: true, (c, on) => c with { LogLevel = on ? LogLevel.Verbose : LogLevel.Info },
            new(null, "Log every exchange event", ["logging"], IsInShortList: true, Default: "off")),
        Flag("silent", 's', negatable: true, (c, on) => c with { LogLevel = on ? LogLevel.None : LogLevel.Info },
            new(null, "Silent mode", ["logging"], IsInShortList: true, Default: "off")),
        Flag("show-error", 'S', negatable: true, (c, on) => c with { ShowError = on },
            new(null, "Show error even when -s is used", ["logging"], IsInShortList: false, Default: "off")),
        WithArgument<LogLevel>("log-level", null, OptionArgumentReader.ReadLogLevel, (c, v) => c with { LogLevel = v },
            new("<level>", "Set the log level", ["logging"], IsInShortList: false, Default: "info")),
        WithArgument<string>("trace", null, OptionArgumentReader.ReadPath,
            (c, v) => c with { LogLevel = LogLevel.Trace, TraceFile = v, TraceLayout = TraceDumpLayout.HexAndAscii },
            new("<file>", "Write a debug trace to <file>", ["logging"], IsInShortList: false, Default: "none")),
        WithArgument<string>("trace-ascii", null, OptionArgumentReader.ReadPath,
            (c, v) => c with { LogLevel = LogLevel.Trace, TraceFile = v, TraceLayout = TraceDumpLayout.Ascii },
            new("<file>", "Like --trace, but without hex", ["logging"], IsInShortList: false, Default: "none")),
        Flag("trace-time", null, negatable: true, (c, on) => c with { TraceTime = on },
            new(null, "Add time stamps to log lines", ["logging"], IsInShortList: false, Default: "off")),
        WithArgument<string>("log-file", null, OptionArgumentReader.ReadPath, (c, v) => c with { LogFile = v },
            new("<file>", "Append the log to <file>", ["logging"], IsInShortList: false, Default: "stderr")),
        WithArgument<string>("directory", null, OptionArgumentReader.ReadPath, (c, v) => c with { DataDirectory = v },
            new("<directory>", "Data directory, else in memory", ["content", "dict", "gopher", "http", "mqtt", "tftp"], IsInShortList: true, Default: "in memory")),
        Flag("allow-uploads", null, negatable: true, (c, on) => c with { AllowUploads = on },
            new(null, "Accept uploads into served files", ["content", "security", "tftp"], IsInShortList: true, Default: "off")),
        Flag("list-directories", null, negatable: true, (c, on) => c with { ListDirectories = on },
            new(null, "Answer directory listings", ["content", "gopher", "security"], IsInShortList: true, Default: "off")),
        Flag("follow-symlinks", null, negatable: true, (c, on) => c with { FollowSymlinks = on },
            new(null, "Follow links that stay in the root", ["content", "dict", "gopher", "http", "security", "tftp"], IsInShortList: false, Default: "off")),
        Flag("serve-dot-files", null, negatable: true, (c, on) => c with { ServeDotFiles = on },
            new(null, "Serve names that start with a dot", ["content", "dict", "gopher", "http", "security", "tftp"], IsInShortList: false, Default: "off")),
        WithArgument<int>("max-connections", null, OptionArgumentReader.ReadNumber, (c, v) => c with { MaxConnections = v },
            new("<number>", "Connections at once, all listeners", ["limits"], IsInShortList: false, Default: "1024")),
        WithArgument<int>("max-connections-per-address", null, OptionArgumentReader.ReadNumber, (c, v) => c with { MaxConnectionsPerAddress = v },
            new("<number>", "Connections at once per address", ["limits"], IsInShortList: false, Default: "100")),
        WithArgument<TimeSpan>("idle-timeout", null, OptionArgumentReader.ReadSeconds, (c, v) => c with { IdleTimeout = v },
            new("<seconds>", "Close an exchange idle this long", ["limits"], IsInShortList: false, Default: "120")),
        WithArgument<TimeSpan>("max-time", 'm', OptionArgumentReader.ReadSeconds, (c, v) => c with { MaxTime = v },
            new("<seconds>", "Longest time one exchange may take", ["limits"], IsInShortList: false, Default: "3600")),
        WithArgument<TimeSpan>("head-timeout", null, OptionArgumentReader.ReadSeconds, (c, v) => c with { Limits = c.Limits with { HeadTimeout = v } },
            new("<seconds>", "Time to send a request head", ["dict", "gopher", "http", "limits", "mqtt"], IsInShortList: false, Default: "30")),
        WithArgument<long>("max-request-head", null, OptionArgumentReader.ReadBytes, (c, v) => c with { Limits = c.Limits with { MaxRequestHeadBytes = v } },
            new("<bytes>", "Largest HTTP or RTSP request head", ["http", "limits"], IsInShortList: false, Default: "100k")),
        WithArgument<long>("max-line", null, OptionArgumentReader.ReadBytes, (c, v) => c with { Limits = c.Limits with { MaxLineBytes = v } },
            new("<bytes>", "Longest command line accepted", ["dict", "gopher", "limits", "telnet"], IsInShortList: false, Default: "8192")),
        WithArgument<long>("max-message", null, OptionArgumentReader.ReadBytes, (c, v) => c with { Limits = c.Limits with { MaxMessageBytes = v } },
            new("<bytes>", "Largest framed message accepted", ["limits", "mqtt"], IsInShortList: false, Default: "1M")),
        WithArgument<long>("max-filesize", null, OptionArgumentReader.ReadBytes, (c, v) => c with { Limits = c.Limits with { MaxUploadBytes = v } },
            new("<bytes>", "Largest upload accepted", ["http", "limits", "mqtt", "tftp"], IsInShortList: false, Default: "100M")),
        Flag("tlsv1.0", null, negatable: false, (c, _) => c with { LowestTlsVersion = SslProtocols.Tls },
            new(null, "Accept TLS 1.0 or later", ["security", "tls"], IsInShortList: false, Default: null)),
        Flag("tlsv1.1", null, negatable: false, (c, _) => c with { LowestTlsVersion = SslProtocols.Tls11 },
            new(null, "Accept TLS 1.1 or later", ["security", "tls"], IsInShortList: false, Default: null)),
        Flag("tlsv1.2", null, negatable: false, (c, _) => c with { LowestTlsVersion = SslProtocols.Tls12 },
            new(null, "Accept TLS 1.2 or later (default)", ["tls"], IsInShortList: false, Default: null)),
        Flag("tlsv1.3", null, negatable: false, (c, _) => c with { LowestTlsVersion = SslProtocols.Tls13 },
            new(null, "Accept TLS 1.3 or later", ["tls"], IsInShortList: false, Default: null)),
        WithArgument<SslProtocols>("tls-max", null, OptionArgumentReader.ReadTlsVersion, (c, v) => c with { HighestTlsVersion = v },
            new("<version>", "Highest TLS version accepted", ["tls"], IsInShortList: false, Default: "1.3")),
        WithArgument<string>("cert", null, OptionArgumentReader.ReadPath, (c, v) => c with { CertificateFile = v },
            new("<file>", "Server certificate file", ["tls"], IsInShortList: true, Default: "none")),
        WithArgument<CertificateFileFormat>("cert-type", null, OptionArgumentReader.ReadCertificateType, (c, v) => c with { CertificateType = v },
            new("<type>", "Format of --cert: PEM, DER or P12", ["tls"], IsInShortList: false, Default: "PEM")),
        WithArgument<string>("key", null, OptionArgumentReader.ReadPath, (c, v) => c with { KeyFile = v },
            new("<file>", "Private key for --cert", ["tls"], IsInShortList: true, Default: "the key in the --cert file")),
        WithArgument<CertificateFileFormat>("key-type", null, OptionArgumentReader.ReadKeyType, (c, v) => c with { KeyType = v },
            new("<type>", "Format of --key: PEM or DER", ["tls"], IsInShortList: false, Default: "PEM")),
        WithArgument<string>("pass", null, OptionArgumentReader.ReadText, (c, v) => c with { KeyPassphrase = v },
            new("<phrase>", "Passphrase for the private key", ["tls"], IsInShortList: false, Default: "none")),
        WithArgument<string>("cacert", null, OptionArgumentReader.ReadPath, (c, v) => c with { CaCertificateFile = v },
            new("<file>", "CA certificates for client certs", ["tls"], IsInShortList: false, Default: "none")),
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
        new(longName, shortName, CommandLineOptionKind.Flag, negatable, setFlag, ApplyArgument: null, help);

    private static CommandLineOption WithArgument<T>(
        string longName,
        char? shortName,
        ReadArgument<T> read,
        Func<SurlCommandLine, T, SurlCommandLine> set,
        OptionHelp help)
    {
        string? Apply(string argument, ref SurlCommandLine commandLine)
        {
            var failure = read(argument, out var value);
            if (failure is null)
            {
                commandLine = set(commandLine, value);
            }

            return failure;
        }

        return new(longName, shortName, CommandLineOptionKind.Argument, Negatable: false, SetFlag: null, Apply, help);
    }
}
