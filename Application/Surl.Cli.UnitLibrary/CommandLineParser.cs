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
    private const string GroupLongName = "group";
    private const string UserLdapLongName = "user-ldap";
    private const string FaultLongName = "fault";
    private const string MailDomainLongName = "mail-domain";
    private const string MailboxLongName = "mailbox";
    private const string MailAliasLongName = "mail-alias";
    private const string MailCatchAllLongName = "mail-catch-all";
    private const string SharedFolderLongName = "shared-folder";
    private const string ShareLongName = "share";
    private const string ShareAccessLongName = "share-access";
    private const string BucketLongName = "bucket";
    private const string TopicLongName = "topic";
    private const string QueueLongName = "queue";
    private const string TopicMapLongName = "topic-map";
    private const string TopicAccessLongName = "topic-access";
    private const string ConfigLongName = "config";
    private const string ConfigOption = "--config";
    private const string UrlLongName = "url";
    private const string ControlLongName = "control";
    private const string ProxyLongName = "proxy";
    private const string StandardInputPath = "-";
    private const string StandardInputName = "<stdin>";

    /// <summary>The most config files open at once, the command line's own <c>-K</c> counted, as curl (ADR-0101 decision 1 row 14).</summary>
    public const int MaxConfigFileDepth = 5;

    /// <summary>A sixth config file open at once (ADR-0101 decision 5 row 8).</summary>
    public const string NestTooDeep = "config files nest more than 5 deep";

    /// <summary>A config file that includes one already being read, or a second <c>-K -</c>, after its name (ADR-0101 decision 5 row 9).</summary>
    public const string IsAlreadyBeingRead = "is already being read";

    /// <summary>A config file past 16 MiB, after its name (ADR-0101 decision 5 row 2).</summary>
    public const string FileTooLarge = "the file is larger than 16777216 bytes";
    private const string ThrowawayHostKeyOption = "--throwaway-hostkey";
    private const string Http3Option = "--http3";
    private const string HttpsScheme = "https";
    private const string DnsScheme = "dns";

    /// <summary><c>--http3</c> given with no <c>https</c> listen URL (ADR-0082 decision 6).</summary>
    public const string NeedsAnHttpsListenUrl = "needs an https listen URL";

    /// <summary><c>--http3</c> given with <c>--tls-max</c> below 1.3 (ADR-0082 decision 6).</summary>
    public const string NeedsTls13 = "needs --tls-max 1.3: QUIC is TLS 1.3 only";

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
    public static CommandLineParseResult Parse(IReadOnlyList<string> arguments) => Parse(arguments, []);

    /// <summary>
    /// Parses <paramref name="arguments"/>, accepting besides upstream curl's schemes the listen
    /// URLs of <paramref name="extendedSchemes"/> (ADR-0083, decision 6).
    /// </summary>
    /// <param name="arguments">The arguments, as the process received them.</param>
    /// <param name="extendedSchemes">
    /// The schemes the registered extended protocol servers claim, as
    /// <see cref="ListenUrlParser.Parse(string, IReadOnlyCollection{string})"/> takes them.
    /// </param>
    /// <returns>As <see cref="Parse(IReadOnlyList{string})"/> returns.</returns>
    public static CommandLineParseResult Parse(IReadOnlyList<string> arguments, IReadOnlyCollection<string> extendedSchemes) =>
        Parse(arguments, extendedSchemes, UnreadableConfigFileSource.Instance);

    /// <summary>
    /// Parses <paramref name="arguments"/> as <see cref="Parse(IReadOnlyList{string}, IReadOnlyCollection{string})"/>
    /// does, reading each <c>-K</c>/<c>--config</c> file through <paramref name="configFiles"/> and
    /// its options in place, as if their words stood where the <c>-K</c> stands (ADR-0101 decision 1).
    /// </summary>
    /// <param name="arguments">The arguments, as the process received them.</param>
    /// <param name="extendedSchemes">The schemes the registered extended protocol servers claim.</param>
    /// <param name="configFiles">Reads the config files; the overloads without it read none.</param>
    /// <returns>
    /// As <see cref="Parse(IReadOnlyList{string})"/> returns; a refusal inside a config file is
    /// prefixed by <c>&lt;file&gt;:&lt;line&gt;: </c> (ADR-0101 decision 5).
    /// </returns>
    public static CommandLineParseResult Parse(
        IReadOnlyList<string> arguments, IReadOnlyCollection<string> extendedSchemes, IConfigFileSource configFiles)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(extendedSchemes);
        ArgumentNullException.ThrowIfNull(configFiles);

        var reading = new CommandLineReading(arguments, extendedSchemes, configFiles);
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
        var parsed = ListenUrlParser.Parse(argument, reading.ExtendedSchemes);
        if (!parsed.Succeeded)
        {
            return CommandLineParseResult.Refused(parsed.Failure);
        }

        reading.ListenUrls.Add(parsed.ListenUrl);
        if (parsed.ListenUrl.Scheme == DnsScheme && GivesPort(argument))
        {
            reading.CommandLine = reading.CommandLine with { DnsListenUrlsWithPort = [.. reading.CommandLine.DnsListenUrlsWithPort, parsed.ListenUrl] };
        }

        return null;
    }

    /// <summary>Whether a listen URL's authority ends in <c>:port</c>, after an IPv6 literal's bracket when it has one.</summary>
    private static bool GivesPort(string listenUrlText)
    {
        var afterScheme = listenUrlText[(listenUrlText.IndexOf("://", StringComparison.Ordinal) + 3)..];
        var authorityEnd = afterScheme.AsSpan().IndexOfAny('/', '?', '#');
        var authority = afterScheme.AsSpan(0, authorityEnd < 0 ? afterScheme.Length : authorityEnd);
        return authority.StartsWith('[') ? authority[(authority.IndexOf(']') + 1)..].Contains(':') : authority.Contains(':');
    }

    /// <summary>
    /// Reads a <c>-x</c>/<c>--proxy</c> listen URL with <see cref="ListenUrlParser.ParseProxy"/> and adds it to
    /// <see cref="SurlCommandLine.ProxyListenUrls"/> in command-line order, or refuses it (ADR-0088 decision 7).
    /// </summary>
    private static CommandLineParseResult? ReadProxyListenUrl(CommandLineReading reading, string argument)
    {
        var parsed = ListenUrlParser.ParseProxy(argument);
        if (!parsed.Succeeded)
        {
            return CommandLineParseResult.Refused(parsed.Failure);
        }

        reading.CommandLine = reading.CommandLine with { ProxyListenUrls = [.. reading.CommandLine.ProxyListenUrls, parsed.ListenUrl] };
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

        return ActOnAcceptedArgument(reading, option.LongName, reportedName, argument);
    }

    /// <summary>
    /// What an accepted argument does besides setting its value: counts a <c>--group</c> or a
    /// <c>--fault</c>, reads a <c>-K</c> file in place, adds a <c>--url</c> listen URL, reads the
    /// <c>--control</c> listen URL, or does what <see cref="ActOnAcceptedMailOption"/> does; null to read on.
    /// </summary>
    private static CommandLineParseResult? ActOnAcceptedArgument(
        CommandLineReading reading, string longName, string reportedName, string argument) =>
        ActOnAcceptedMailOption(reading, longName, reportedName, argument)
        ?? ActOnAcceptedContentOption(reading, longName, reportedName)
        ?? ActOnAcceptedMessagingOption(reading, longName, reportedName)
        ?? (longName == UserLdapLongName ? ReadUserLdap(reading, reportedName) : null)
        ?? longName switch
        {
            FaultLongName => CountFault(reading, reportedName),
            GroupLongName => CountItemOption(reading.GroupOptionNames, reportedName),
            ConfigLongName => ReadConfigFile(reading, argument),
            UrlLongName => ReadListenUrl(reading, argument),
            ControlLongName => ReadControlListenUrl(reading, argument),
            ProxyLongName => ReadProxyListenUrl(reading, argument),
            _ => null,
        };

    /// <summary>
    /// What an accepted <c>--mail-domain</c> or <c>--mailbox</c> does besides setting its value:
    /// counts it, and reads a mailbox's scheme list (<see cref="ReadMailbox"/>); null to read on,
    /// and for every other option.
    /// </summary>
    private static CommandLineParseResult? ActOnAcceptedMailOption(
        CommandLineReading reading, string longName, string reportedName, string argument) =>
        longName switch
        {
            MailDomainLongName => CountItemOption(reading.MailDomainOptionNames, reportedName),
            MailboxLongName => ReadMailbox(reading, reportedName, argument),
            MailAliasLongName => CountItemOption(reading.MailAliasOptionNames, reportedName),
            MailCatchAllLongName => CountItemOption(reading.MailCatchAllOptionNames, reportedName),
            SharedFolderLongName => CountItemOption(reading.SharedFolderOptionNames, reportedName),
            _ => null,
        };

    /// <summary>
    /// Reads <c>--control</c>'s argument into <see cref="SurlCommandLine.ControlListenUrl"/>, the
    /// last one given winning, or refuses it (ADR-0124 decision 7).
    /// </summary>
    private static CommandLineParseResult? ReadControlListenUrl(CommandLineReading reading, string argument)
    {
        var parsed = ControlEndpointOptionReader.Read(argument);
        if (!parsed.Succeeded)
        {
            return CommandLineParseResult.Refused(parsed.Failure);
        }

        reading.CommandLine = reading.CommandLine with { ControlListenUrl = parsed.ListenUrl };
        return null;
    }

    /// <summary>
    /// Reads the config file <paramref name="path"/> in place (ADR-0101 decision 1): refuses a file
    /// already being read and a sixth file open at once, then reads its option lines in order.
    /// </summary>
    private static CommandLineParseResult? ReadConfigFile(CommandLineReading reading, string path)
    {
        var isStandardInput = path == StandardInputPath;
        var name = isStandardInput ? StandardInputName : path;
        var identity = isStandardInput ? StandardInputName : reading.ConfigFileSource.IdentityOf(path);
        var refused = RefuseConfigFileOpening(reading, name, isStandardInput ? reading.StandardInputRead : reading.OpenConfigFiles.Contains(identity));
        if (refused is not null)
        {
            return refused;
        }

        var content = reading.ConfigFileSource.Read(path, ConfigFileText.MaxFileBytes + 1);
        if (content is null)
        {
            return CommandLineParseResult.Refused(
                new CommandLineFailure(SurlExitCode.CouldNotReadFile, $"(37) Could not read config file {name}"));
        }

        reading.StandardInputRead |= isStandardInput;
        reading.OpenConfigFiles.Add(identity);
        var result = ReadConfigFileContent(reading, name, content);
        reading.OpenConfigFiles.RemoveAt(reading.OpenConfigFiles.Count - 1);
        return result;
    }

    /// <summary>
    /// Refuses opening the config file <paramref name="name"/> when it is already being read (or is a
    /// second <c>-K -</c>) or when <see cref="MaxConfigFileDepth"/> files are open (ADR-0101 decision 5
    /// rows 8 and 9); null otherwise.
    /// </summary>
    private static CommandLineParseResult? RefuseConfigFileOpening(CommandLineReading reading, string name, bool isAlreadyRead)
    {
        if (isAlreadyRead)
        {
            return RefusedOption(ConfigOption, $"{name} {IsAlreadyBeingRead}");
        }

        return reading.OpenConfigFiles.Count == MaxConfigFileDepth ? RefusedOption(ConfigOption, NestTooDeep) : null;
    }

    /// <summary>Reads a config file's lines in order; the first refusal or ending result ends the reading.</summary>
    private static CommandLineParseResult? ReadConfigFileContent(CommandLineReading reading, string name, byte[] content)
    {
        if (content.Length > ConfigFileText.MaxFileBytes)
        {
            reading.FailureLocated = true;
            return CommandLineParseResult.Refused(
                new CommandLineFailure(SurlExitCode.FailedInit, $"{name}: {FileTooLarge}", FollowedByTryHelpLine: true));
        }

        foreach (var line in ConfigFileText.Lines(content))
        {
            var result = line.Failure is null
                ? ReadConfigLine(reading, line.Words)
                : CommandLineParseResult.Refused(new CommandLineFailure(SurlExitCode.FailedInit, line.Failure, FollowedByTryHelpLine: true));
            if (result is not null)
            {
                return Locate(reading, name, line.Number, result);
            }
        }

        return null;
    }

    /// <summary>
    /// Reads one config line's words as if they stood alone on the command line: an option taking a
    /// value takes only the line's own, and a value no option took is refused.
    /// </summary>
    private static CommandLineParseResult? ReadConfigLine(CommandLineReading reading, IReadOnlyList<string> words)
    {
        var optionWord = words[0];
        reading.LineWords.Push(new Queue<string>(words.Skip(1)));
        var result = optionWord == EndOfOptions ? RefusedOption(optionWord, IsUnknown) : ReadArgument(reading, optionWord);
        var valueLeft = reading.LineWords.Pop().Count > 0;
        return result ?? (valueLeft ? RefuseValueLeft(optionWord) : null);
    }

    /// <summary>
    /// Refuses a config line's value no option took: <c>does not take a parameter</c> for a flag
    /// (<c>verbose = 1</c>), and the text-after-a-value refusal when the value was already written
    /// into the option word (<c>--header=X Y</c>).
    /// </summary>
    private static CommandLineParseResult RefuseValueLeft(string optionWord)
    {
        var name = ConfigFileText.NameWithoutValue(optionWord);
        return name == optionWord
            ? RefusedOption(name, DoesNotTakeParameter)
            : CommandLineParseResult.Refused(
                new CommandLineFailure(SurlExitCode.FailedInit, ConfigFileText.TextFollowsTheValue(name), FollowedByTryHelpLine: true));
    }

    /// <summary>
    /// Prefixes a refusal made by line <paramref name="number"/> of <paramref name="name"/> with
    /// <c>&lt;file&gt;:&lt;line&gt;: </c>, once: a refusal a nested file already located keeps its own place.
    /// </summary>
    private static CommandLineParseResult Locate(CommandLineReading reading, string name, int number, CommandLineParseResult result)
    {
        if (result.Outcome != CommandLineOutcome.Refused || reading.FailureLocated)
        {
            return result;
        }

        reading.FailureLocated = true;
        var failure = result.Failure!;
        return CommandLineParseResult.Refused(failure with { Message = $"{name}:{number}: {failure.Message}" });
    }

    /// <summary>
    /// What an accepted <c>--share</c>, <c>--share-access</c> or <c>--bucket</c> does besides setting its
    /// value: keeps the name it was written with and refuses the one past
    /// <see cref="ConfigurationItemReader.MaxOccurrences"/>; null to read on, and for every other option.
    /// </summary>
    private static CommandLineParseResult? ActOnAcceptedContentOption(
        CommandLineReading reading, string longName, string reportedName) =>
        longName switch
        {
            ShareLongName => CountItemOption(reading.ShareOptionNames, reportedName),
            ShareAccessLongName => CountItemOption(reading.ShareAccessOptionNames, reportedName),
            BucketLongName => CountItemOption(reading.BucketOptionNames, reportedName),
            _ => null,
        };

    /// <summary>
    /// Refuses, once the whole line is read, the first share, bucket or <c>--share-access</c> share given
    /// twice, a bucket named as a share, then a <c>--share-access</c> naming no <c>--share</c>
    /// (<see cref="ConfiguredContentConflicts.Find"/>, ADR-0101 decision 5 rows 14 and 15); null when none.
    /// </summary>
    private static CommandLineParseResult? RefuseContentCombination(CommandLineReading reading)
    {
        var line = reading.CommandLine;
        var refusal = ConfiguredContentConflicts.Find(
            [.. line.Shares.Select((share, index) => (reading.ShareOptionNames[index], share))],
            [.. line.ShareAccess.Select((access, index) => (reading.ShareAccessOptionNames[index], access))],
            [.. line.Buckets.Select((bucket, index) => (reading.BucketOptionNames[index], bucket))]);
        return refusal is null ? null : RefusedOption(refusal.OptionName, refusal.Reason);
    }

    /// <summary>
    /// What an accepted <c>--topic</c>, <c>--queue</c>, <c>--topic-map</c> or <c>--topic-access</c> does besides
    /// setting its value: keeps the name it was written with and refuses the one past
    /// <see cref="ConfigurationItemReader.MaxOccurrences"/>; null to read on, and for every other option.
    /// </summary>
    private static CommandLineParseResult? ActOnAcceptedMessagingOption(
        CommandLineReading reading, string longName, string reportedName) =>
        longName switch
        {
            TopicLongName => CountItemOption(reading.TopicOptionNames, reportedName),
            QueueLongName => CountItemOption(reading.QueueOptionNames, reportedName),
            TopicMapLongName => CountItemOption(reading.TopicMapOptionNames, reportedName),
            TopicAccessLongName => CountItemOption(reading.TopicAccessOptionNames, reportedName),
            _ => null,
        };

    /// <summary>
    /// Refuses, once the whole line is read, the first topic filter, queue, topic-map source or
    /// <c>--topic-access</c> filter given twice (<see cref="ConfiguredMessagingConflicts.Find"/>, ADR-0101
    /// decision 5 row 14); null when none.
    /// </summary>
    private static CommandLineParseResult? RefuseMessagingCombination(CommandLineReading reading)
    {
        var line = reading.CommandLine;
        var refusal = ConfiguredMessagingConflicts.Find(
            [.. line.Topics.Select((topic, index) => (reading.TopicOptionNames[index], topic))],
            [.. line.Queues.Select((queue, index) => (reading.QueueOptionNames[index], queue))],
            [.. line.TopicMaps.Select((map, index) => (reading.TopicMapOptionNames[index], map))],
            [.. line.TopicAccess.Select((access, index) => (reading.TopicAccessOptionNames[index], access))]);
        return refusal is null ? null : RefusedOption(refusal.OptionName, refusal.Reason);
    }

    /// <summary>
    /// Keeps the name a <c>--group</c>, <c>--mail-domain</c> or <c>--mailbox</c> was written with, in
    /// <paramref name="optionNames"/>, and refuses the one past
    /// <see cref="ConfigurationItemReader.MaxOccurrences"/> (ADR-0101 decision 5 row 13); null otherwise.
    /// </summary>
    private static CommandLineParseResult? CountItemOption(List<string> optionNames, string reportedName)
    {
        optionNames.Add(reportedName);
        return optionNames.Count > ConfigurationItemReader.MaxOccurrences
            ? RefusedOption(reportedName, ConfigurationItemReader.GivenTooOften)
            : null;
    }

    /// <summary>
    /// Refuses the <c>--fault</c> past <see cref="ConfiguredFaultReader.MaxRules"/> (ADR-0124 decision 4),
    /// counted across the command line and every config file; null otherwise.
    /// </summary>
    private static CommandLineParseResult? CountFault(CommandLineReading reading, string reportedName) =>
        reading.CommandLine.Faults.Count > ConfiguredFaultReader.MaxRules
            ? RefusedOption(reportedName, ConfiguredFaultReader.TooManyRules)
            : null;

    /// <summary>
    /// Refuses a second <c>--user-ldap</c> (ADR-0149 decision 3: one directory is the account source) and one whose
    /// <c>protocols=</c> names a scheme no server this start runs speaks, so an extended scheme under
    /// <c>SURL_EXTENSIONS=0</c> is refused; null otherwise.
    /// </summary>
    private static CommandLineParseResult? ReadUserLdap(CommandLineReading reading, string reportedName)
    {
        reading.UserLdapOptionNames.Add(reportedName);
        if (reading.UserLdapOptionNames.Count > 1)
        {
            return RefusedOption(reportedName, ConfiguredLdapAccountSourceReader.GivenTwice);
        }

        var unknown = reading.CommandLine.UserLdap!.Protocols?
            .FirstOrDefault(scheme => !ConfiguredMailboxReader.IsServedScheme(scheme, reading.ExtendedSchemes));
        return unknown is null ? null : RefusedOption(reportedName, ConfiguredMailboxReader.UnknownScheme(unknown));
    }

    /// <summary>
    /// Counts the <c>--mailbox</c> just read, keeps its address as written for a later refusal,
    /// and refuses it when its <c>protocols=</c> names a scheme no server this start runs speaks,
    /// so an extended scheme under <c>SURL_EXTENSIONS=0</c> is refused (ADR-0101 decision 4); null otherwise.
    /// </summary>
    private static CommandLineParseResult? ReadMailbox(CommandLineReading reading, string reportedName, string argument)
    {
        ConfigurationItemReader.Read(ConfiguredMailboxReader.Shape, argument, out var item);
        reading.MailboxWrittenAddresses.Add(item.Name);
        var unknown = reading.CommandLine.Mailboxes[^1].Protocols?
            .FirstOrDefault(scheme => !ConfiguredMailboxReader.IsServedScheme(scheme, reading.ExtendedSchemes));
        return CountItemOption(reading.MailboxOptionNames, reportedName)
            ?? (unknown is null ? null : RefusedOption(reportedName, ConfiguredMailboxReader.UnknownScheme(unknown)));
    }

    /// <summary>
    /// Refuses, once the whole line is read, a mail domain given twice (compared as
    /// <see cref="MailDomainNames.ReadDomain"/> reads it), a mailbox address given twice (compared
    /// without regard to case), then the first mailbox with a conflict of ADR-0101 decision 5 row 15;
    /// null when none. Each refusal names the domain or address as written.
    /// </summary>
    private static CommandLineParseResult? RefuseMailCombination(CommandLineReading reading)
    {
        var domains = reading.CommandLine.MailDomains.Select(ComparedMailDomain).ToList();
        return RefuseMailDomainGivenTwice(reading, domains)
            ?? RefuseMailboxGivenTwice(reading)
            ?? RefuseMailboxConflict(reading, domains.ToHashSet(StringComparer.Ordinal))
            ?? RefuseMailRoutingCombination(reading);
    }

    /// <summary>
    /// Refuses the first <c>--mail-alias</c>, <c>--mail-catch-all</c> or <c>--shared-folder</c> given twice, then an
    /// alias or catch-all outside the mail domains, or an alias that is also a mailbox
    /// (<see cref="ConfiguredMailConflicts.Find"/>, ADR-0101 decision 5 rows 14 and 15); null when none.
    /// </summary>
    private static CommandLineParseResult? RefuseMailRoutingCombination(CommandLineReading reading)
    {
        var line = reading.CommandLine;
        var refusal = ConfiguredMailConflicts.Find(
            line.MailDomains,
            line.Mailboxes,
            [.. line.MailAliases.Select((alias, index) => (reading.MailAliasOptionNames[index], alias))],
            [.. line.MailCatchAlls.Select((catchAll, index) => (reading.MailCatchAllOptionNames[index], catchAll))],
            [.. line.SharedFolders.Select((folder, index) => (reading.SharedFolderOptionNames[index], folder))]);
        return refusal is null ? null : RefusedOption(refusal.OptionName, refusal.Reason);
    }

    /// <summary>Refuses the first <c>--mail-domain</c> an earlier one already gave; null when none.</summary>
    private static CommandLineParseResult? RefuseMailDomainGivenTwice(CommandLineReading reading, List<string> domains)
    {
        var repeated = FindRepeatedIndex(domains, StringComparer.Ordinal);
        return repeated < 0
            ? null
            : RefusedOption(reading.MailDomainOptionNames[repeated], MailDomainNames.MailDomainGivenTwice(reading.CommandLine.MailDomains[repeated]));
    }

    /// <summary>Refuses the first <c>--mailbox</c> whose address an earlier one gave, compared without regard to case; null when none.</summary>
    private static CommandLineParseResult? RefuseMailboxGivenTwice(CommandLineReading reading)
    {
        var repeated = FindRepeatedIndex(reading.CommandLine.Mailboxes.Select(mailbox => mailbox.Address).ToList(), StringComparer.OrdinalIgnoreCase);
        return repeated < 0
            ? null
            : RefusedOption(reading.MailboxOptionNames[repeated], ConfiguredMailboxReader.MailboxGivenTwice(reading.MailboxWrittenAddresses[repeated]));
    }

    /// <summary>Refuses the first <c>--mailbox</c> outside every mail domain, with none, or naming a <c>--user</c> account; null when none.</summary>
    private static CommandLineParseResult? RefuseMailboxConflict(CommandLineReading reading, HashSet<string> mailDomains)
    {
        var mailboxes = reading.CommandLine.Mailboxes;
        var userNames = reading.CommandLine.Accounts.Select(account => account.UserName).ToHashSet(StringComparer.Ordinal);
        for (var index = 0; index < mailboxes.Count; index++)
        {
            if (ConfiguredMailboxReader.FindConflict(reading.MailboxWrittenAddresses[index], mailboxes[index], mailDomains, userNames) is { } conflict)
            {
                return RefusedOption(reading.MailboxOptionNames[index], conflict);
            }
        }

        return null;
    }

    /// <summary>An accepted <c>--mail-domain</c> in the form domains are compared in (ADR-0102 decision 1.1).</summary>
    private static string ComparedMailDomain(string written)
    {
        MailDomainNames.ReadDomain(written, out var domain);
        return domain;
    }

    /// <summary>The index of the first name an earlier one already gave; -1 when none repeats.</summary>
    private static int FindRepeatedIndex(IReadOnlyList<string> names, StringComparer comparer)
    {
        var seen = new HashSet<string>(comparer);
        for (var index = 0; index < names.Count; index++)
        {
            if (!seen.Add(names[index]))
            {
                return index;
            }
        }

        return -1;
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

        var refusedControl = ControlEndpointOptionReader.RefuseCombination(reading.CommandLine);
        if (refusedControl is not null)
        {
            return CommandLineParseResult.Refused(refusedControl);
        }

        if (reading.ListenUrls.Count == 0 && reading.CommandLine.ProxyListenUrls.Count == 0)
        {
            return CommandLineParseResult.Refused(
                new CommandLineFailure(SurlExitCode.FailedInit, NoUrlSpecified, FollowedByTryHelpLine: true));
        }

        var refusedUnixSocket = UnixSocketCombination.RefuseProxy(reading.CommandLine)
            ?? UnixSocketCombination.Refuse(reading.CommandLine, reading.ListenUrls);
        if (refusedUnixSocket is not null)
        {
            return CommandLineParseResult.Refused(refusedUnixSocket);
        }

        return RefuseTlsServingCombination(reading)
            ?? CommandLineParseResult.Serve(
                ResolveLogging(reading.CommandLine) with { ListenUrls = UnixSocketCombination.Apply(reading.CommandLine, reading.ListenUrls) });
    }

    /// <summary>
    /// Refuses the TLS-SRP options in a combination that cannot work (<see cref="TlsSrpCombination"/>,
    /// ADR-0122 decision 6), then <c>--http3</c> in one (<see cref="RefuseHttp3Combination"/>); null when neither is.
    /// </summary>
    private static CommandLineParseResult? RefuseTlsServingCombination(CommandLineReading reading) =>
        TlsSrpCombination.Refuse(reading.CommandLine, reading.ListenUrls) is { } refusedTlsSrp
            ? CommandLineParseResult.Refused(refusedTlsSrp)
            : RefuseHttp3Combination(reading);

    /// <summary>
    /// Refuses <c>--http3</c> with no <c>https</c> listen URL, or with <c>--tls-max</c> below 1.3
    /// (QUIC is TLS 1.3 only), in that order (ADR-0082 decision 6 and its amendment 4); null
    /// otherwise. With <c>--cacert</c> the QUIC handshake requires a client certificate as the TCP
    /// listener does (ADR-0082 amendment 5).
    /// </summary>
    private static CommandLineParseResult? RefuseHttp3Combination(CommandLineReading reading)
    {
        var commandLine = reading.CommandLine;
        if (!commandLine.Http3)
        {
            return null;
        }

        if (!reading.ListenUrls.Exists(listenUrl => listenUrl.Scheme == HttpsScheme))
        {
            return RefusedOption(Http3Option, NeedsAnHttpsListenUrl);
        }

        return commandLine.HighestTlsVersion != System.Security.Authentication.SslProtocols.Tls13
            ? RefusedOption(Http3Option, NeedsTls13)
            : null;
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
    /// repeated <c>--authorized-keys</c> user, then a repeated <c>--group</c> name, then the mail
    /// options (<see cref="RefuseMailCombination"/>), then the content options (<see cref="RefuseContentCombination"/>); null when none is.
    /// </summary>
    private static CommandLineParseResult? RefuseCombination(CommandLineReading reading) =>
        RefuseTlsCombination(reading)
        ?? RefuseThrowawayHostKeyWithHostKey(reading)
        ?? RefuseRepeatedName(reading.CommandLine.Accounts.Select(account => account.UserName), reading.AccountOptionNames, name => UserGivenTwice(name))
        ?? RefuseRepeatedName(reading.CommandLine.AuthorizedKeys.Select(keys => keys.UserName), reading.AuthorizedKeysOptionNames, name => UserGivenTwice(name))
        ?? RefuseRepeatedName(reading.CommandLine.Groups.Select(group => group.Name), reading.GroupOptionNames, name => ConfiguredGroupReader.GroupGivenTwice(name))
        ?? RefuseMailCombination(reading)
        ?? RefuseContentCombination(reading)
        ?? RefuseMessagingCombination(reading);

    private static string UserGivenTwice(string userName) => $"user {userName} is given twice";

    /// <summary>
    /// Refuses <c>--throwaway-hostkey</c> left on with any <c>--hostkey</c> (ADR-0051 decision 4);
    /// null otherwise.
    /// </summary>
    private static CommandLineParseResult? RefuseThrowawayHostKeyWithHostKey(CommandLineReading reading) =>
        reading.CommandLine.ThrowawayHostKey && reading.GivenOptions.Contains(HostKeyLongName)
            ? RefusedOption(ThrowawayHostKeyOption, CannotBeUsedWithHostKey)
            : null;

    /// <summary>
    /// Refuses the first name an earlier one of the same option already gave, the empty name
    /// included, naming the option that gave it: <c>-u</c>/<c>--user</c> accounts (ADR-0032
    /// section 1), <c>--authorized-keys</c> users (ADR-0051 decision 6) and <c>--group</c> names
    /// (ADR-0101 decision 5 row 14), compared ordinally; null when none repeats.
    /// </summary>
    /// <param name="names">The names, in command-line order.</param>
    /// <param name="optionNames">The name each one's option was written with, in the same order.</param>
    /// <param name="givenTwice">The refusal reason for a repeated name.</param>
    private static CommandLineParseResult? RefuseRepeatedName(IEnumerable<string> names, List<string> optionNames, Func<string, string> givenTwice)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var name in names)
        {
            if (!seen.Add(name))
            {
                return RefusedOption(optionNames[index], givenTwice(name));
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
    private sealed class CommandLineReading(
        IReadOnlyList<string> arguments, IReadOnlyCollection<string> extendedSchemes, IConfigFileSource configFileSource)
    {
        private int _next;

        /// <summary>Reads the <c>-K</c> files.</summary>
        public IConfigFileSource ConfigFileSource => configFileSource;

        /// <summary>The identities of the config files being read, outermost first.</summary>
        public List<string> OpenConfigFiles { get; } = [];

        /// <summary><see langword="true"/> once a <c>-K -</c> has read standard input.</summary>
        public bool StandardInputRead { get; set; }

        /// <summary><see langword="true"/> once a refusal carries its <c>&lt;file&gt;:&lt;line&gt;: </c>.</summary>
        public bool FailureLocated { get; set; }

        /// <summary>
        /// The values of the config lines being read, innermost on top: while one is, an option
        /// takes its value from the line alone, never from the command line or a later line.
        /// </summary>
        public Stack<Queue<string>> LineWords { get; } = new();

        /// <summary>The command line so far; a field so an option's argument can replace it by reference.</summary>
        public SurlCommandLine CommandLine = new();

        public List<ListenUrl> ListenUrls { get; } = [];

        /// <summary>The schemes the registered extended protocol servers claim.</summary>
        public IReadOnlyCollection<string> ExtendedSchemes => extendedSchemes;

        /// <summary>The long names of the options whose argument was read and accepted.</summary>
        public HashSet<string> GivenOptions { get; } = new(StringComparer.Ordinal);

        /// <summary>The name each accepted <c>-u</c>/<c>--user</c> was written with, one per account, in order.</summary>
        public List<string> AccountOptionNames { get; } = [];

        /// <summary>The name each accepted <c>--authorized-keys</c> was written with, one per user, in order.</summary>
        public List<string> AuthorizedKeysOptionNames { get; } = [];

        /// <summary>The name each accepted <c>--group</c> was written with, one per group, in order.</summary>
        public List<string> GroupOptionNames { get; } = [];

        /// <summary>The name each accepted <c>--mail-domain</c> was written with, one per domain, in order.</summary>
        public List<string> MailDomainOptionNames { get; } = [];

        /// <summary>The name each accepted <c>--user-ldap</c> was written with, in order.</summary>
        public List<string> UserLdapOptionNames { get; } = [];

        /// <summary>The name each accepted <c>--mailbox</c> was written with, one per mailbox, in order.</summary>
        public List<string> MailboxOptionNames { get; } = [];

        /// <summary>The name each accepted <c>--mail-alias</c> was written with, one per alias, in order.</summary>
        public List<string> MailAliasOptionNames { get; } = [];

        /// <summary>The name each accepted <c>--mail-catch-all</c> was written with, one per catch-all, in order.</summary>
        public List<string> MailCatchAllOptionNames { get; } = [];

        /// <summary>The name each accepted <c>--shared-folder</c> was written with, one per folder, in order.</summary>
        public List<string> SharedFolderOptionNames { get; } = [];

        /// <summary>The address each accepted <c>--mailbox</c> was written with, decoded, one per mailbox, in order.</summary>
        public List<string> MailboxWrittenAddresses { get; } = [];

        /// <summary>The name each accepted <c>--share</c> was written with, one per share, in order.</summary>
        public List<string> ShareOptionNames { get; } = [];

        /// <summary>The name each accepted <c>--share-access</c> was written with, one per item, in order.</summary>
        public List<string> ShareAccessOptionNames { get; } = [];

        /// <summary>The name each accepted <c>--bucket</c> was written with, one per bucket, in order.</summary>
        public List<string> BucketOptionNames { get; } = [];

        /// <summary>The name each accepted <c>--topic</c> was written with, one per topic, in order.</summary>
        public List<string> TopicOptionNames { get; } = [];

        /// <summary>The name each accepted <c>--queue</c> was written with, one per queue, in order.</summary>
        public List<string> QueueOptionNames { get; } = [];

        /// <summary>The name each accepted <c>--topic-map</c> was written with, one per map, in order.</summary>
        public List<string> TopicMapOptionNames { get; } = [];

        /// <summary>The name each accepted <c>--topic-access</c> was written with, one per item, in order.</summary>
        public List<string> TopicAccessOptionNames { get; } = [];

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

        public string? TakeArgumentOrNull()
        {
            if (LineWords.TryPeek(out var line))
            {
                return line.TryDequeue(out var word) ? word : null;
            }

            return _next < arguments.Count ? arguments[_next++] : null;
        }
    }
}
