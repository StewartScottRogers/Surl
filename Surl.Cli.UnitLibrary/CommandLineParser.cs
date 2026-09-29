using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// Reads a whole <c>surl</c> command line by ADR-0007 sections 1, 2 and 5: options and
/// listen URLs in any order, left to right, the first error ending the reading.
/// </summary>
/// <remarks>
/// Table-driven: every option is a row of <see cref="CommandLineOptions"/>, so a new option
/// is a new row, not a new branch. Whether a registered protocol server claims each scheme,
/// and whether the data directory exists, are checked later by <c>Surl.Console</c>.
/// </remarks>
public static class CommandLineParser
{
    /// <summary>An option that is not in the table.</summary>
    public const string IsUnknown = "is unknown";

    /// <summary><c>--no-</c> on an option that is not negatable.</summary>
    public const string CannotBeReversed = "the given option cannot be reversed with a --no- prefix";

    /// <summary><c>=value</c> on an option that takes no argument.</summary>
    public const string DoesNotTakeParameter = "does not take a parameter";

    /// <summary>An option that takes an argument, with none after it.</summary>
    public const string RequiresParameter = "requires parameter";

    /// <summary>The message for a command line with no listen URL.</summary>
    public const string NoUrlSpecified = "(2) no URL specified";

    private const string EndOfOptions = "--";
    private const string LongOptionPrefix = "--";
    private const string NegationPrefix = "no-";
    private const string TlsMaxOption = "--tls-max";
    private const string CertLongName = "cert";
    private const string KeyLongName = "key";

    /// <summary>The options refused without <c>--cert</c>, in the order the first one given is reported.</summary>
    private static readonly string[] OptionsNeedingCert = [KeyLongName, "key-type", "pass"];

    /// <summary>
    /// Parses <paramref name="arguments"/>, the command line after the program name.
    /// </summary>
    /// <param name="arguments">The arguments, as the process received them.</param>
    /// <returns>
    /// <see cref="CommandLineParseResult.ShowHelp"/>, with the subject that follows, or
    /// <see cref="CommandLineParseResult.ShowVersion"/> when <c>-h</c>/<c>--help</c> or
    /// <c>-V</c>/<c>--version</c> is read before any error (ADR-0034 decision 4);
    /// a serve result carrying the <see cref="SurlCommandLine"/>; or the first failure, with
    /// its <see cref="SurlExitCode"/> and message.
    /// </returns>
    public static CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var reading = new CommandLineReading(arguments);
        while (reading.TryTakeArgument(out var argument))
        {
            var ended = ReadArgument(reading, argument);
            if (ended is not null)
            {
                return ended;
            }
        }

        return Finish(reading);
    }

    /// <summary>Reads one argument; returns the result that ends reading, or null to read on.</summary>
    private static CommandLineParseResult? ReadArgument(CommandLineReading reading, string argument)
    {
        if (argument.Length == 0)
        {
            return RefusedOption(string.Empty, OptionArgumentReader.Blank);
        }

        if (reading.OptionsEnded || argument[0] != '-')
        {
            return ReadListenUrl(reading, argument);
        }

        if (argument == EndOfOptions)
        {
            reading.OptionsEnded = true;
            return null;
        }

        return argument.StartsWith(LongOptionPrefix, StringComparison.Ordinal)
            ? ReadLongOption(reading, argument)
            : ReadShortOptions(reading, argument);
    }

    private static CommandLineParseResult? ReadListenUrl(CommandLineReading reading, string argument)
    {
        var parsed = ListenUrlParser.Parse(argument);
        if (!parsed.Succeeded)
        {
            return CommandLineParseResult.Refused(parsed.Failure);
        }

        reading.ListenUrls.Add(parsed.ListenUrl);
        return null;
    }

    /// <summary>Reads <c>--name</c>, <c>--name=value</c> or <c>--no-name</c>.</summary>
    private static CommandLineParseResult? ReadLongOption(CommandLineReading reading, string argument)
    {
        var equals = argument.IndexOf('=', StringComparison.Ordinal);
        var name = equals < 0 ? argument[LongOptionPrefix.Length..] : argument[LongOptionPrefix.Length..equals];
        var attached = equals < 0 ? null : argument[(equals + 1)..];

        return CommandLineOptions.TryFindLong(name, out var option)
            ? ApplyOption(reading, option, argument, attached, turnOn: true)
            : ReadNegatedOption(reading, name, argument, attached);
    }

    /// <summary>Reads a long option whose name is not in the table: <c>--no-name</c>, or unknown.</summary>
    private static CommandLineParseResult? ReadNegatedOption(CommandLineReading reading, string name, string argument, string? attached)
    {
        if (!name.StartsWith(NegationPrefix, StringComparison.Ordinal)
            || !CommandLineOptions.TryFindLong(name[NegationPrefix.Length..], out var negated))
        {
            return RefusedOption(argument, IsUnknown);
        }

        return negated.Negatable
            ? ApplyOption(reading, negated, argument, attached, turnOn: false)
            : RefusedOption(argument, CannotBeReversed);
    }

    /// <summary>Reads <c>-x</c> or a bundle such as <c>-vm30</c>; an option taking an argument ends the bundle.</summary>
    private static CommandLineParseResult? ReadShortOptions(CommandLineReading reading, string argument)
    {
        if (argument.Length == 1)
        {
            return RefusedOption(argument, IsUnknown);
        }

        for (var index = 1; index < argument.Length; index++)
        {
            var (endsBundle, result) = ReadShortOption(reading, argument, index);
            if (endsBundle)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the short option at <paramref name="index"/> of a bundle; says whether reading
    /// the bundle ends there, and with which result (null to read on).
    /// </summary>
    private static (bool EndsBundle, CommandLineParseResult? Result) ReadShortOption(CommandLineReading reading, string argument, int index)
    {
        if (!CommandLineOptions.TryFindShort(argument[index], out var option))
        {
            return (true, RefusedOption(argument, IsUnknown));
        }

        if (option.Kind == CommandLineOptionKind.Help)
        {
            return (true, CommandLineParseResult.ShowHelp(RestOrNextArgument(reading, argument, index)));
        }

        if (option.Kind == CommandLineOptionKind.Argument)
        {
            return (true, ApplyArgument(reading, option, argument, RestOrNextArgument(reading, argument, index)));
        }

        var ended = ApplyOption(reading, option, argument, attached: null, turnOn: true);
        return (ended is not null, ended);
    }

    /// <summary>
    /// The rest of a bundle after the short option at <paramref name="index"/> (<c>30</c> of
    /// <c>-m30</c>), or, when nothing follows it, the next argument (null when there is none).
    /// </summary>
    private static string? RestOrNextArgument(CommandLineReading reading, string argument, int index)
    {
        var attached = argument[(index + 1)..];
        return attached.Length > 0 ? attached : reading.TakeArgumentOrNull();
    }

    /// <summary>Applies an option found by name; <paramref name="attached"/> is the text after <c>=</c>, if any.</summary>
    private static CommandLineParseResult? ApplyOption(
        CommandLineReading reading,
        CommandLineOption option,
        string writtenName,
        string? attached,
        bool turnOn) =>
        option.Kind is CommandLineOptionKind.Help or CommandLineOptionKind.Argument
            ? ApplyOptionTakingAValue(reading, option, writtenName, attached ?? reading.TakeArgumentOrNull())
            : ApplyOptionTakingNoValue(reading, option, writtenName, attached, turnOn);

    /// <summary>Applies <c>-V</c> or a flag; <paramref name="attached"/>, the text after <c>=</c>, refuses either.</summary>
    private static CommandLineParseResult? ApplyOptionTakingNoValue(
        CommandLineReading reading,
        CommandLineOption option,
        string writtenName,
        string? attached,
        bool turnOn)
    {
        if (attached is not null)
        {
            return RefusedOption(writtenName, DoesNotTakeParameter);
        }

        if (option.Kind == CommandLineOptionKind.Version)
        {
            return CommandLineParseResult.ShowVersion;
        }

        reading.CommandLine = option.SetFlag!(reading.CommandLine, turnOn);
        return null;
    }

    /// <summary>
    /// Applies <c>--help</c>, whose optional subject is <paramref name="value"/>, or an option
    /// whose required argument is <paramref name="value"/>; null when none was given.
    /// </summary>
    private static CommandLineParseResult? ApplyOptionTakingAValue(
        CommandLineReading reading,
        CommandLineOption option,
        string writtenName,
        string? value) =>
        option.Kind == CommandLineOptionKind.Help
            ? CommandLineParseResult.ShowHelp(value)
            : ApplyArgument(reading, option, writtenName, value);

    private static CommandLineParseResult? ApplyArgument(
        CommandLineReading reading,
        CommandLineOption option,
        string writtenName,
        string? argument)
    {
        if (argument is null)
        {
            return RefusedOption(writtenName, RequiresParameter);
        }

        var failure = option.ApplyArgument!(argument, ref reading.CommandLine);
        if (failure is not null)
        {
            return RefusedOption(writtenName, failure);
        }

        reading.GivenOptions.Add(option.LongName);
        return null;
    }

    /// <summary>The checks made once the whole command line is read.</summary>
    private static CommandLineParseResult Finish(CommandLineReading reading)
    {
        if (reading.CommandLine.LowestTlsVersion > reading.CommandLine.HighestTlsVersion)
        {
            return RefusedOption(TlsMaxOption, OptionArgumentReader.BadlyUsed);
        }

        var unusableCertificateOption = FindUnusableCertificateOption(reading);
        if (unusableCertificateOption is not null)
        {
            return RefusedOption(unusableCertificateOption, OptionArgumentReader.BadlyUsed);
        }

        if (reading.ListenUrls.Count == 0)
        {
            return CommandLineParseResult.Refused(
                new CommandLineFailure(SurlExitCode.FailedInit, NoUrlSpecified, FollowedByTryHelpLine: true));
        }

        return CommandLineParseResult.Serve(ResolveLogging(reading.CommandLine) with { ListenUrls = [.. reading.ListenUrls] });
    }

    /// <summary>
    /// Applies ADR-0033 section 2 once the whole line is read: <c>-S</c> raises level
    /// <see cref="LogLevel.None"/> to <see cref="LogLevel.Error"/>, and a trace file is kept
    /// only when the final level is <see cref="LogLevel.Trace"/>.
    /// </summary>
    private static SurlCommandLine ResolveLogging(SurlCommandLine commandLine)
    {
        if (commandLine.ShowError && commandLine.LogLevel == LogLevel.None)
        {
            return commandLine with { LogLevel = LogLevel.Error, TraceFile = null };
        }

        return commandLine.LogLevel == LogLevel.Trace ? commandLine : commandLine with { TraceFile = null };
    }

    /// <summary>
    /// The first certificate option ADR-0010 section 3 refuses in combination: <c>--key</c>,
    /// <c>--key-type</c> or <c>--pass</c> without <c>--cert</c>, then <c>--key</c> with
    /// <c>--cert-type P12</c>; null when none is.
    /// </summary>
    private static string? FindUnusableCertificateOption(CommandLineReading reading)
    {
        if (!reading.GivenOptions.Contains(CertLongName))
        {
            var withoutCertificate = OptionsNeedingCert.FirstOrDefault(reading.GivenOptions.Contains);
            return withoutCertificate is null ? null : LongOptionPrefix + withoutCertificate;
        }

        return reading.GivenOptions.Contains(KeyLongName) && reading.CommandLine.CertificateType == CertificateFileFormat.Pkcs12
            ? LongOptionPrefix + KeyLongName
            : null;
    }

    private static CommandLineParseResult RefusedOption(string writtenName, string reason) =>
        CommandLineParseResult.Refused(
            new CommandLineFailure(SurlExitCode.FailedInit, $"option {writtenName}: {reason}", FollowedByTryHelpLine: true));

    /// <summary>The state of one left-to-right reading of a command line.</summary>
    private sealed class CommandLineReading(IReadOnlyList<string> arguments)
    {
        private int _next;

        /// <summary>The command line so far; a field so an option's argument can replace it by reference.</summary>
        public SurlCommandLine CommandLine = new();

        public List<ListenUrl> ListenUrls { get; } = [];

        /// <summary>The long names of the options whose argument was read and accepted.</summary>
        public HashSet<string> GivenOptions { get; } = new(StringComparer.Ordinal);

        /// <summary><see langword="true"/> once <c>--</c> is read: every later argument is a listen URL.</summary>
        public bool OptionsEnded { get; set; }

        public bool TryTakeArgument(out string argument)
        {
            var taken = TakeArgumentOrNull();
            argument = taken ?? string.Empty;
            return taken is not null;
        }

        public string? TakeArgumentOrNull() => _next < arguments.Count ? arguments[_next++] : null;
    }
}
