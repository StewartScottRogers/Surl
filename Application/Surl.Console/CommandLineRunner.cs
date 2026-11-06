using System.Reflection;
using System.Runtime.InteropServices;
using Surl.Cli;
using Surl.Composition;
using Surl.Content;
using Surl.Networking;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// Runs one <c>surl</c> command line: the composition root. It parses the command line, answers
/// <c>--help</c>, <c>--aihelp</c>, <c>--manual</c> and <c>--version</c>, has
/// <see cref="SurlServerComposition.ComposeAsync"/> run the ordered start, opens the log streams,
/// and has the <see cref="ComposedServing"/> serve until cancelled, writing ADR-0007 section 5's
/// texts and returning its exit codes.
/// </summary>
/// <param name="createListenerFactory">
/// Creates the factory that starts the listeners, given the process's TLS settings
/// (<see langword="null"/> when no listen URL is TLS from the first byte); <c>surl</c> passes
/// one that creates a <see cref="SocketListenerFactory"/>.
/// </param>
/// <param name="canOpenDataDirectory">
/// Tells whether the data directory, as given with <c>--directory</c>, can be opened; never
/// called without <c>--directory</c>. <c>surl</c> passes <see cref="DataDirectoryProbe.CanOpen"/>.
/// </param>
/// <param name="takeDataDirectoryLock">
/// Takes the data directory's <c>.surl/lock</c> (ADR-0031, decision 7), given the data
/// directory as given with <c>--directory</c>; never called without <c>--directory</c>.
/// <c>surl</c> passes <see cref="DataDirectoryLock.Take"/>.
/// </param>
/// <param name="timeProvider">The one clock every exchange runs on.</param>
/// <param name="dataDirectoryFileSystem">
/// The file system the data directory is served and its service state kept through when
/// serving with <c>--directory</c>; a <see cref="DiskContentFileSystem"/> when
/// <see langword="null"/>, which is what <c>surl</c> passes.
/// </param>
/// <param name="openLogFile">
/// Opens a <c>--log-file</c> (<see cref="FileMode.Append"/>) or trace file
/// (<see cref="FileMode.Create"/>) for writing, before any listener binds (ADR-0033,
/// section 6); <see cref="LogFile.Open"/> when <see langword="null"/>, which is what
/// <c>surl</c> passes.
/// </param>
/// <param name="readStartFile">
/// Reads the bytes of a file surl reads at start, given its path as given, before any listener
/// binds: the <c>--user-file</c> (ADR-0032, section 2), each <c>--authorized-keys</c> file, the
/// <c>--keytab</c> file (ADR-0057, decision 1) and each
/// <c>--hostkey</c> file (ADR-0051, decisions 4 and 6); never called when none is given.
/// <see cref="File.ReadAllBytes(string)"/> when <see langword="null"/>, which is what <c>surl</c> passes.
/// </param>
/// <param name="createDataConnectionOpener">
/// Creates what opens the FTP server's data connections, given the process's TLS settings, the
/// ones the listeners secure connections with (ADR-0052, decision 9); a
/// <see cref="SocketDataConnectionOpener"/> on the one clock when <see langword="null"/>, which
/// is what <c>surl</c> passes.
/// </param>
/// <param name="environmentVariables">
/// Reads the <c>SURL_EXTENSIONS</c> switch, once, at construction (ADR-0083, decision 6); a
/// <see cref="ProcessEnvironmentVariableReader"/> when <see langword="null"/>, which is what
/// <c>surl</c> passes.
/// </param>
/// <param name="composeExtendedProtocolServers">
/// Builds the extended protocol servers while the switch leaves them on, and is never called
/// while <c>SURL_EXTENSIONS=0</c>; <see cref="ExtendedProtocolServers.Compose"/> when
/// <see langword="null"/>, which is what <c>surl</c> passes.
/// </param>
/// <param name="configFiles">
/// Reads each <c>-K</c>/<c>--config</c> file while the command line is parsed (ADR-0101 decision 1);
/// a <see cref="DiskConfigFileSource"/> over the disk and standard input when <see langword="null"/>,
/// which is what <c>surl</c> passes.
/// </param>
internal sealed class CommandLineRunner(
    Func<ServerTlsSettings?, IListenerFactory> createListenerFactory,
    Func<string, bool> canOpenDataDirectory,
    Func<string, DataDirectoryLockOutcome> takeDataDirectoryLock,
    TimeProvider timeProvider,
    IContentFileSystem? dataDirectoryFileSystem = null,
    Func<string, FileMode, TextWriter>? openLogFile = null,
    Func<string, byte[]>? readStartFile = null,
    Func<ServerTlsSettings?, IDataConnectionOpener>? createDataConnectionOpener = null,
    IEnvironmentVariableReader? environmentVariables = null,
    Func<ExtendedProtocolServerServices, IReadOnlyList<IProtocolServer>>? composeExtendedProtocolServers = null,
    IConfigFileSource? configFiles = null)
{
    // -K/--config files are read while parsing (ADR-0101 decision 1).
    private readonly IConfigFileSource configFiles = configFiles ?? new DiskConfigFileSource(System.Console.OpenStandardInput, fileNamesIgnoreCase: !OperatingSystem.IsLinux());

    private const string MessagePrefix = "surl: ";

    // SURL_EXTENSIONS is read once, here, by the host (ADR-0083, decision 6; ADR-0099, decision 2).
    private readonly bool areExtensionsTurnedOn = AreExtensionsTurnedOn(environmentVariables);

    private static bool AreExtensionsTurnedOn(IEnvironmentVariableReader? environmentVariables) =>
        ExtendedProtocolServers.AreTurnedOn(environmentVariables ?? new ProcessEnvironmentVariableReader());

    private readonly Func<string, FileMode, TextWriter> openLogFile = openLogFile ?? LogFile.Open;

    /// <summary>
    /// Runs <paramref name="args"/>.
    /// </summary>
    /// <param name="args">The command-line arguments, without the program name.</param>
    /// <param name="output">
    /// stdout: where the help, the manual, the version and, from the info level up, the status lines go;
    /// and a <c>--trace -</c> dump or <c>--log-file -</c> log.
    /// </param>
    /// <param name="error">
    /// stderr: where every command-line refusal and, above the <c>-s</c> level, every
    /// <c>surl: </c> failure message goes, and the log stream without <c>--log-file</c>
    /// (ADR-0033, section 1).
    /// </param>
    /// <param name="cancellationToken">Cancelled to stop serving.</param>
    /// <returns>The exit code, as ADR-0007 section 5 gives it.</returns>
    public async Task<SurlExitCode> RunAsync(
        IReadOnlyList<string> args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var parsed = CommandLineParser.Parse(args, ComposeExtendedSchemes(), configFiles);

        return parsed.Outcome switch
        {
            CommandLineOutcome.ShowHelp => WriteHelp(output, error, HelpText.Answer(parsed.HelpSubject)),
            CommandLineOutcome.ShowAiHelp => WriteHelp(output, error, AiHelpText.Answer(parsed.AiHelpTopic)),
            CommandLineOutcome.ShowVersion => WriteText(output, ComposeVersionText()),
            CommandLineOutcome.ShowManual => WriteText(output, ManualText.Text),
            CommandLineOutcome.Refused => WriteRefusal(error, parsed.Failure!),
            _ => await ServeAsync(parsed.CommandLine!, output, HideAtLevelNone(parsed.CommandLine!, error), cancellationToken),
        };
    }

    // -s writes nothing, the surl: failure messages and the log alike (ADR-0033, section 1); a
    // refused command line has no level yet, so its refusal is always written.
    private static TextWriter HideAtLevelNone(SurlCommandLine commandLine, TextWriter error) =>
        commandLine.LogLevel == LogLevel.None ? TextWriter.Null : error;

    /// <summary>
    /// Formats the <c>(45)</c> or <c>(6)</c> message for a listener that could not bind, after
    /// the <c>surl: </c> prefix (ADR-0007 section 5), as
    /// <see cref="SurlServerComposition.FormatBindFailure"/> does.
    /// </summary>
    /// <param name="bindFailure">The failure the listener factory threw.</param>
    /// <returns>The message.</returns>
    internal static string FormatBindFailure(ListenerBindException bindFailure) =>
        SurlServerComposition.FormatBindFailure(bindFailure);

    /// <summary>
    /// Formats the <c>(58)</c> message for a listen URL that is TLS from the first byte with
    /// neither <c>--cert</c> nor <c>--self-signed</c>, after the <c>surl: </c> prefix, as
    /// <see cref="SurlServerComposition.FormatMissingCertificate"/> does.
    /// </summary>
    /// <param name="listenUrl">The first such listen URL.</param>
    /// <returns>The message.</returns>
    internal static string FormatMissingCertificate(ListenUrl listenUrl) =>
        SurlServerComposition.FormatMissingCertificate(listenUrl);

    /// <summary>Writes each stream's part of a help answer; every help answer exits Ok (ADR-0034 decision 3).</summary>
    private static SurlExitCode WriteHelp(TextWriter output, TextWriter error, HelpAnswer answer)
    {
        output.Write(answer.Output);
        error.Write(answer.Error);
        return SurlExitCode.Ok;
    }

    private static SurlExitCode WriteText(TextWriter output, string text)
    {
        output.Write(text);
        return SurlExitCode.Ok;
    }

    private static SurlExitCode WriteRefusal(TextWriter error, CommandLineFailure failure)
    {
        error.WriteLine(MessagePrefix + failure.Message);
        if (failure.FollowedByTryHelpLine)
        {
            error.WriteLine(MessagePrefix + CommandLineFailure.TryHelpLine);
        }

        return failure.ExitCode;
    }

    private static SurlExitCode WriteFailure(TextWriter error, SurlStartFailure failure)
    {
        foreach (var line in failure.Lines)
        {
            error.WriteLine(MessagePrefix + line);
        }

        return failure.ExitCode;
    }

    private string ComposeVersionText()
    {
        var informationalVersion = typeof(CommandLineRunner).Assembly
            .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
            .Select(attribute => attribute.InformationalVersion)
            .FirstOrDefault();

        return VersionText.Compose(
            informationalVersion, RuntimeInformation.RuntimeIdentifier, ComposeRegisteredSchemes(), ComposeExtendedSchemes());
    }

    /// <summary>
    /// Lists the scheme of every original protocol server surl registers, in registration order,
    /// as <see cref="SurlServerComposition.ComposeRegisteredSchemes"/> composes them on the one clock.
    /// </summary>
    /// <returns>The schemes.</returns>
    internal IReadOnlyList<string> ComposeRegisteredSchemes() => SurlServerComposition.ComposeRegisteredSchemes(timeProvider);

    /// <summary>
    /// Lists the scheme of every extended protocol server surl registers, none while
    /// <c>SURL_EXTENSIONS=0</c>, as <see cref="SurlServerComposition.ComposeExtendedSchemes"/>
    /// composes them through this runner's switch and seam.
    /// </summary>
    /// <returns>The schemes.</returns>
    internal IReadOnlyList<string> ComposeExtendedSchemes() =>
        SurlServerComposition.ComposeExtendedSchemes(areExtensionsTurnedOn, composeExtendedProtocolServers, timeProvider);

    // The runner's seams, as the composition takes them (ADR-0099, decision 2).
    private SurlServerCompositionSeams ComposeSeams() =>
        new(createListenerFactory, canOpenDataDirectory, takeDataDirectoryLock, timeProvider, areExtensionsTurnedOn)
        {
            DataDirectoryFileSystem = dataDirectoryFileSystem,
            ReadStartFile = readStartFile,
            CreateDataConnectionOpener = createDataConnectionOpener,
            ComposeExtendedProtocolServers = composeExtendedProtocolServers,
        };

    // The ordered start runs in Surl.Composition up to server TLS; then the log streams open, and a
    // file that cannot be opened ends surl with 23 and releases the lock; then the serving writes the
    // start-up warnings and serves (ADR-0099, decision 2).
    private async Task<SurlExitCode> ServeAsync(
        SurlCommandLine commandLine, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var start = await SurlServerComposition.ComposeAsync(commandLine, ComposeSeams(), cancellationToken);
        if (start.Failure is { } failure)
        {
            return WriteFailure(error, failure);
        }

        await using var serving = start.Serving!;
        var (logStreams, openFailure) = LogStreams.Open(commandLine, output, error, openLogFile);
        if (logStreams is null)
        {
            return WriteFailure(error, new SurlStartFailure(SurlExitCode.CouldNotWriteFile, [openFailure!]));
        }

        using (logStreams)
        {
            var outcome = await serving.ServeAsync(
                new SurlServingOutputs(
                    logStreams.Log,
                    output,
                    logStreams.CreateExchangeLogFactory(commandLine, timeProvider),
                    AllListenersBound: static _ => { }),
                cancellationToken);
            return outcome.Failure is { } servingFailure ? WriteFailure(error, servingFailure) : outcome.ExitCode;
        }
    }
}
