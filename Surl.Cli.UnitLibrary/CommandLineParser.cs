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
    private const string UserLongName = "user";
    private const string SelfSignedOption = "--self-signed";
    private const string PassLongName = "pass";
    private const string HostKeyLongName = "hostkey";
    private const string AuthorizedKeysLongName = "authorized-keys";
    private const string ThrowawayHostKeyOption = "--throwaway-hostkey";

    /// <summary><c>--self-signed</c> given with <c>--cert</c> (ADR-0032 section 1).</summary>
    public const string CannotBeUsedWithCert = "cannot be used with --cert";

    /// <summary><c>--throwaway-hostkey</c> given with <c>--hostkey</c> (ADR-0051 decision 4).</summary>
    public const string CannotBeUsedWithHostKey = "cannot be used with --hostkey";

    /// <summary>
    /// The options refused without <c>--cert</c>, in the order the first one given is reported;
    /// <c>--pass</c> only when no <c>--hostkey</c> is given either, since it decrypts a host key
    /// too (ADR-0051 decision 4).
    /// </summary>
    private static readonly string[] OptionsNeedingCert = [KeyLongName, "key-type", PassLongName];

    /// <summary>
    /// Parses <paramref name="arguments"/>, the command line after the program name.
    /// </summary>
    /// <param name="arguments">The arguments, as the process received them.</param>
    /// <returns>
    /// <see cref="CommandLineParseResult.ShowHelp"/>, with the subject that follows, or
    /// <see cref="CommandLineParseResult.ShowVersion"/> or <see cref="CommandLineParseResult.ShowManual"/>
    /// when <c>-h</c>/<c>--help</c>, <c>-V</c>/<c>--version</c> or <c>-M</c>/<c>--manual</c> is
    /// read before any error (ADR-0034 decision 4);
    /// <see cref="CommandLineParseResult.ShowAiHelp"/>, with the topic that follows, when
    /// <c>--aihelp</c> is read first (ADR-0046 decision 1);
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
            : RefusedOption(ReportedName(negated, argument), CannotBeReversed);
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
        option.Kind is CommandLineOptionKind.Help or CommandLineOptionKind.AiHelp or CommandLineOptionKind.Argument
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

        if (option.Kind == CommandLineOptionKind.Manual)
        {
            return CommandLineParseResult.ShowManual;
        }

        reading.CommandLine = option.SetFlag!(reading.CommandLine, turnOn);
        return null;
    }

    /// <summary>
    /// Applies <c>--help</c>, whose optional subject is <paramref name="value"/>, <c>--aihelp</c>,
    /// whose optional topic is <paramref name="value"/> (ADR-0046 decision 1), or an option
    /// whose required argument is <paramref name="value"/>; null when none was given.
    /// </summary>
    private static CommandLineParseResult? ApplyOptionTakingAValue(
        CommandLineReading reading,
        CommandLineOption option,
        string writtenName,
        string? value) =>
        option.Kind switch
        {
            CommandLineOptionKind.Help => CommandLineParseResult.ShowHelp(value),
            CommandLineOptionKind.AiHelp => CommandLineParseResult.ShowAiHelp(value),
            _ => ApplyArgument(reading, option, writtenName, value),
        };

    private static CommandLineParseResult? ApplyArgument(
        CommandLineReading reading,
        CommandLineOption option,
        string writtenName,
        string? argument)
    {
        var reportedName = ReportedName(option, writtenName);
        if (argument is null)
        {
            return RefusedOption(reportedName, RequiresParameter);
        }

        var failure = option.ApplyArgument!(argument, ref reading.CommandLine);
        if (failure is not null)
        {
            return RefusedOption(reportedName, failure);
        }

        reading.GivenOptions.Add(option.LongName);
        if (option.LongName is UserLongName or AuthorizedKeysLongName)
        {
            reading.OptionNamesByUser(option.LongName).Add(reportedName);
        }

        return null;
    }

    /// <summary>
    /// The name a refusal gives: <paramref name="writtenName"/>, or, for an option whose
    /// argument holds a secret, the option as written without any value (<c>--user</c>,
    /// <c>--no-user</c>, <c>-u</c> of <c>-vua:b</c>), so none of that option's own refusals
    /// echoes its value (ADR-0032 section 1). An unknown option is still named as written.
    /// </summary>
    private static string ReportedName(CommandLineOption option, string writtenName)
    {
        if (!option.ArgumentHoldsSecret)
        {
            return writtenName;
        }

        if (!writtenName.StartsWith(LongOptionPrefix, StringComparison.Ordinal))
        {
            return "-" + option.ShortName;
        }

        var equals = writtenName.IndexOf('=', StringComparison.Ordinal);
        return equals < 0 ? writtenName : writtenName[..equals];
    }

    /// <summary>The checks made once the whole command line is read.</summary>
    private static CommandLineParseResult Finish(CommandLineReading reading)
    {
        var refusedCombination = RefuseCombination(reading);
        if (refusedCombination is not null)
        {
            return refusedCombination;
        }

        if (reading.ListenUrls.Count == 0)
        {
            return CommandLineParseResult.Refused(
                new CommandLineFailure(SurlExitCode.FailedInit, NoUrlSpecified, FollowedByTryHelpLine: true));
        }

        return CommandLineParseResult.Serve(ResolveLogging(reading.CommandLine) with { ListenUrls = [.. reading.ListenUrls] });
    }

    /// <summary>
    /// Refuses the first TLS option given in a combination that cannot work, in this order:
    /// <c>--tls-max</c> below the lowest version, a certificate option ADR-0010 section 3
    /// refuses, then <c>--self-signed</c> with <c>--cert</c> (ADR-0032 section 1); null when
    /// none is.
    /// </summary>
    private static CommandLineParseResult? RefuseTlsCombination(CommandLineReading reading)
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

        return reading.CommandLine.SelfSigned && reading.GivenOptions.Contains(CertLongName)
            ? RefusedOption(SelfSignedOption, CannotBeUsedWithCert)
            : null;
    }

    /// <summary>
    /// Refuses the first option combination that cannot work, in this order: the TLS options,
    /// <c>--throwaway-hostkey</c> with <c>--hostkey</c>, a repeated <c>--user</c> name, then a
    /// repeated <c>--authorized-keys</c> user; null when none is.
    /// </summary>
    private static CommandLineParseResult? RefuseCombination(CommandLineReading reading) =>
        RefuseTlsCombination(reading)
        ?? RefuseThrowawayHostKeyWithHostKey(reading)
        ?? RefuseRepeatedUserName(reading.CommandLine.Accounts.Select(account => account.UserName), reading.AccountOptionNames)
        ?? RefuseRepeatedUserName(reading.CommandLine.AuthorizedKeys.Select(keys => keys.UserName), reading.AuthorizedKeysOptionNames);

    /// <summary>
    /// Refuses <c>--throwaway-hostkey</c> left on with any <c>--hostkey</c> (ADR-0051 decision 4);
    /// null otherwise.
    /// </summary>
    private static CommandLineParseResult? RefuseThrowawayHostKeyWithHostKey(CommandLineReading reading) =>
        reading.CommandLine.ThrowawayHostKey && reading.GivenOptions.Contains(HostKeyLongName)
            ? RefusedOption(ThrowawayHostKeyOption, CannotBeUsedWithHostKey)
            : null;

    /// <summary>
    /// Refuses the first user name an earlier one of the same option already gave, the empty
    /// name included, naming the option that gave it: <c>-u</c>/<c>--user</c> accounts (ADR-0032
    /// section 1) and <c>--authorized-keys</c> users (ADR-0051 decision 6); null when none repeats.
    /// </summary>
    /// <param name="userNames">The user names, in command-line order.</param>
    /// <param name="optionNames">The name each one's option was written with, in the same order.</param>
    private static CommandLineParseResult? RefuseRepeatedUserName(IEnumerable<string> userNames, List<string> optionNames)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var userName in userNames)
        {
            if (!seen.Add(userName))
            {
                return RefusedOption(optionNames[index], $"user {userName} is given twice");
            }

            index++;
        }

        return null;
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
    /// <c>--key-type</c> or <c>--pass</c> without <c>--cert</c> (<c>--pass</c> only without
    /// <c>--hostkey</c> as well), then <c>--key</c> with <c>--cert-type P12</c>; null when none is.
    /// </summary>
    private static string? FindUnusableCertificateOption(CommandLineReading reading)
    {
        if (!reading.GivenOptions.Contains(CertLongName))
        {
            var withoutCertificate = OptionsNeedingCert.FirstOrDefault(name => NeedsCertificate(reading, name));
            return withoutCertificate is null ? null : LongOptionPrefix + withoutCertificate;
        }

        return reading.GivenOptions.Contains(KeyLongName) && reading.CommandLine.CertificateType == CertificateFileFormat.Pkcs12
            ? LongOptionPrefix + KeyLongName
            : null;
    }

    /// <summary>
    /// Whether the certificate option <paramref name="longName"/> was given with nothing it can
    /// apply to once <c>--cert</c> is missing: <c>--pass</c> still applies to a <c>--hostkey</c>.
    /// </summary>
    private static bool NeedsCertificate(CommandLineReading reading, string longName) =>
        reading.GivenOptions.Contains(longName)
        && !(longName == PassLongName && reading.GivenOptions.Contains(HostKeyLongName));

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

        /// <summary>The name each accepted <c>-u</c>/<c>--user</c> was written with, one per account, in order.</summary>
        public List<string> AccountOptionNames { get; } = [];

        /// <summary>The name each accepted <c>--authorized-keys</c> was written with, one per user, in order.</summary>
        public List<string> AuthorizedKeysOptionNames { get; } = [];

        /// <summary>The written names of <c>--user</c> (<paramref name="longName"/> <c>user</c>) or <c>--authorized-keys</c>.</summary>
        public List<string> OptionNamesByUser(string longName) =>
            longName == UserLongName ? AccountOptionNames : AuthorizedKeysOptionNames;

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
