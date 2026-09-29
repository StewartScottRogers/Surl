using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Security.Authentication;

namespace Surl.Cli;

/// <summary>
/// ADR-0007 section 2's Phase 1 option table, with ADR-0010 section 3's <c>--cert-type</c>,
/// <c>--key-type</c> and <c>--pass</c>, looked up by long and by short name.
/// </summary>
internal static class CommandLineOptions
{
    /// <summary>Reads an argument of one kind; returns the refusal reason, or null.</summary>
    private delegate string? ReadArgument<T>(string argument, out T value);

#pragma warning disable SYSLIB0039 // --tlsv1.0 and --tlsv1.1 name the old versions on purpose (ADR-0007 section 2).
    private static readonly CommandLineOption[] Table =
    [
        new("help", 'h', CommandLineOptionKind.Help, Negatable: false, SetFlag: null, ApplyArgument: null),
        new("version", 'V', CommandLineOptionKind.Version, Negatable: false, SetFlag: null, ApplyArgument: null),
        Flag("verbose", 'v', negatable: true, (c, on) => c with { Verbose = on }),
        WithArgument<string>("directory", null, OptionArgumentReader.ReadPath, (c, v) => c with { DataDirectory = v }),
        Flag("allow-uploads", null, negatable: true, (c, on) => c with { AllowUploads = on }),
        Flag("list-directories", null, negatable: true, (c, on) => c with { ListDirectories = on }),
        Flag("follow-symlinks", null, negatable: true, (c, on) => c with { FollowSymlinks = on }),
        Flag("serve-dot-files", null, negatable: true, (c, on) => c with { ServeDotFiles = on }),
        WithArgument<int>("max-connections", null, OptionArgumentReader.ReadNumber, (c, v) => c with { MaxConnections = v }),
        WithArgument<int>("max-connections-per-address", null, OptionArgumentReader.ReadNumber, (c, v) => c with { MaxConnectionsPerAddress = v }),
        WithArgument<TimeSpan>("idle-timeout", null, OptionArgumentReader.ReadSeconds, (c, v) => c with { IdleTimeout = v }),
        WithArgument<TimeSpan>("max-time", 'm', OptionArgumentReader.ReadSeconds, (c, v) => c with { MaxTime = v }),
        WithArgument<TimeSpan>("head-timeout", null, OptionArgumentReader.ReadSeconds, (c, v) => c with { Limits = c.Limits with { HeadTimeout = v } }),
        WithArgument<long>("max-request-head", null, OptionArgumentReader.ReadBytes, (c, v) => c with { Limits = c.Limits with { MaxRequestHeadBytes = v } }),
        WithArgument<long>("max-line", null, OptionArgumentReader.ReadBytes, (c, v) => c with { Limits = c.Limits with { MaxLineBytes = v } }),
        WithArgument<long>("max-message", null, OptionArgumentReader.ReadBytes, (c, v) => c with { Limits = c.Limits with { MaxMessageBytes = v } }),
        WithArgument<long>("max-filesize", null, OptionArgumentReader.ReadBytes, (c, v) => c with { Limits = c.Limits with { MaxUploadBytes = v } }),
        Flag("tlsv1.0", null, negatable: false, (c, _) => c with { LowestTlsVersion = SslProtocols.Tls }),
        Flag("tlsv1.1", null, negatable: false, (c, _) => c with { LowestTlsVersion = SslProtocols.Tls11 }),
        Flag("tlsv1.2", null, negatable: false, (c, _) => c with { LowestTlsVersion = SslProtocols.Tls12 }),
        Flag("tlsv1.3", null, negatable: false, (c, _) => c with { LowestTlsVersion = SslProtocols.Tls13 }),
        WithArgument<SslProtocols>("tls-max", null, OptionArgumentReader.ReadTlsVersion, (c, v) => c with { HighestTlsVersion = v }),
        WithArgument<string>("cert", null, OptionArgumentReader.ReadPath, (c, v) => c with { CertificateFile = v }),
        WithArgument<CertificateFileFormat>("cert-type", null, OptionArgumentReader.ReadCertificateType, (c, v) => c with { CertificateType = v }),
        WithArgument<string>("key", null, OptionArgumentReader.ReadPath, (c, v) => c with { KeyFile = v }),
        WithArgument<CertificateFileFormat>("key-type", null, OptionArgumentReader.ReadKeyType, (c, v) => c with { KeyType = v }),
        WithArgument<string>("pass", null, OptionArgumentReader.ReadText, (c, v) => c with { KeyPassphrase = v }),
        WithArgument<string>("cacert", null, OptionArgumentReader.ReadPath, (c, v) => c with { CaCertificateFile = v }),
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

    private static CommandLineOption Flag(string longName, char? shortName, bool negatable, SetFlag setFlag) =>
        new(longName, shortName, CommandLineOptionKind.Flag, negatable, setFlag, ApplyArgument: null);

    private static CommandLineOption WithArgument<T>(
        string longName,
        char? shortName,
        ReadArgument<T> read,
        Func<SurlCommandLine, T, SurlCommandLine> set)
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

        return new(longName, shortName, CommandLineOptionKind.Argument, Negatable: false, SetFlag: null, Apply);
    }
}
