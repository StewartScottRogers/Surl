using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using Surl.Authentication;
using Surl.Cli;
using Surl.Composition;
using Surl.Content;
using Surl.Core;
using Surl.Kerberos;
using Surl.Networking;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// Runs one <c>surl</c> command line: the composition root. It parses the command line,
/// answers <c>--help</c>, <c>--manual</c> and <c>--version</c>, checks the data directory when one is given and
/// the listen URLs, takes the data directory's lock, loads the MQTT retained messages, the mail store and the LDAP directory kept under it,
/// then constructs the TLS settings, the exchange log and the serving engine explicitly, with the
/// content store (on disk or in memory), the service state and the protocol servers composed by
/// <see cref="SurlServerComposition"/>, and serves until cancelled, writing ADR-0007 section 5's
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
    Func<ExtendedProtocolServerServices, IReadOnlyList<IProtocolServer>>? composeExtendedProtocolServers = null)
{
    private const string MessagePrefix = "surl: ";

    // SURL_EXTENSIONS is read once, here, at composition (ADR-0083, decision 6).
    private readonly bool areExtensionsTurnedOn = AreExtensionsTurnedOn(environmentVariables);

    private static bool AreExtensionsTurnedOn(IEnvironmentVariableReader? environmentVariables) =>
        ExtendedProtocolServers.AreTurnedOn(environmentVariables ?? new ProcessEnvironmentVariableReader());

    private readonly IContentFileSystem dataDirectoryFileSystem = dataDirectoryFileSystem ?? new DiskContentFileSystem();

    private readonly Func<string, FileMode, TextWriter> openLogFile = openLogFile ?? LogFile.Open;

    private readonly Func<string, byte[]> readStartFile = readStartFile ?? File.ReadAllBytes;

    // The opener createDataConnectionOpener makes, or surl's own over sockets on the one clock.
    private IDataConnectionOpener CreateDataConnectionOpener(ServerTlsSettings? tlsSettings) =>
        createDataConnectionOpener is null
            ? new SocketDataConnectionOpener(tlsSettings, timeProvider)
            : createDataConnectionOpener(tlsSettings);

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
        var parsed = CommandLineParser.Parse(args, ComposeExtendedSchemes());

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
    /// the <c>surl: </c> prefix (ADR-0007 section 5).
    /// </summary>
    /// <param name="bindFailure">The failure the listener factory threw.</param>
    /// <returns>The message.</returns>
    internal static string FormatBindFailure(ListenerBindException bindFailure)
    {
        var listenUrl = bindFailure.ListenUrl;
        if (bindFailure.Failure == ListenerBindFailure.HostNotFound)
        {
            return $"(6) Could not resolve host: {listenUrl.Host}";
        }

        var address = bindFailure.EndPoint is IPEndPoint endPoint ? endPoint.Address.ToString() : listenUrl.Host;
        var bracketed = address.Contains(':', StringComparison.Ordinal) ? $"[{address}]" : address;
        return $"(45) Could not bind {listenUrl.Scheme}://{bracketed}:{listenUrl.Port}/: {DescribeBindFailure(bindFailure.Failure)}";
    }

    /// <summary>
    /// Formats the <c>(58)</c> message for a listen URL that is TLS from the first byte with
    /// neither <c>--cert</c> nor <c>--self-signed</c>, after the <c>surl: </c> prefix, the URL
    /// written as the status line writes it, with the port as given (ADR-0032, section 10).
    /// </summary>
    /// <param name="listenUrl">The first such listen URL.</param>
    /// <returns>The message.</returns>
    internal static string FormatMissingCertificate(ListenUrl listenUrl) =>
        $"(58) {ListenerStatusLine.FormatBoundListenUrl(listenUrl with { BoundPort = listenUrl.Port })} needs a certificate: "
        + "give --cert <file>, or --self-signed for a throwaway one";

    private static string DescribeBindFailure(ListenerBindFailure failure) => failure switch
    {
        ListenerBindFailure.AddressInUse => "Address already in use",
        ListenerBindFailure.AddressNotAvailable => "Address not available",
        ListenerBindFailure.PermissionDenied => "Permission denied",
        _ => "Bind failed",
    };

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

    private static SurlExitCode WriteFailure(TextWriter error, SurlExitCode exitCode, string message)
    {
        error.WriteLine(MessagePrefix + message);
        return exitCode;
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

    private static SurlExitCode WriteTlsFileFailure(TextWriter error, TlsFileLoadException failure, string? caCertificateFile)
    {
        var (exitCode, lines) = ServerTlsComposition.DescribeTlsFileFailure(failure, caCertificateFile);
        foreach (var line in lines)
        {
            error.WriteLine(MessagePrefix + line);
        }

        return exitCode;
    }

    // A listen URL TLS from the first byte with no certificate to serve (ADR-0032, section 10),
    // then an scp or sftp one with no host key to serve (ADR-0051, decision 4): each refused
    // before any listener binds. A scheme no server answers never gets here: every scheme
    // ListenUrlParser accepts is registered, and it refuses every other one with (1).
    private static (SurlExitCode ExitCode, string Message)? FindListenUrlRefusal(SurlCommandLine commandLine)
    {
        if (ServerTlsComposition.FindListenUrlWithoutCertificate(commandLine) is { } uncertified)
        {
            return (SurlExitCode.CertificateProblem, FormatMissingCertificate(uncertified));
        }

        return SshHostKeyComposition.FindListenUrlWithoutHostKey(commandLine) is { } keyless
            ? (SurlExitCode.FailedInit, SshHostKeyComposition.FormatMissingHostKey(keyless))
            : null;
    }

    private DataDirectoryLockOutcome TakeDataDirectoryLockWhenGiven(SurlCommandLine commandLine) =>
        commandLine.DataDirectory is { } dataDirectory ? takeDataDirectoryLock(dataDirectory) : DataDirectoryLockOutcome.NoLock;

    // --auth gssapi without --keytab (ADR-0057, decision 1), then an --ssh-ciphers or --ssh-macs
    // name surl cannot offer (ADR-0066), is refused before anything else is checked.
    private Task<SurlExitCode> ServeAsync(
        SurlCommandLine commandLine, TextWriter output, TextWriter error, CancellationToken cancellationToken) =>
        FindOptionRefusal(commandLine) is { } refusal
            ? Task.FromResult(WriteFailure(error, SurlExitCode.FailedInit, refusal))
            : ServeAvailableAsync(commandLine, output, error, cancellationToken);

    // The first refusal of the options' consistency, after the surl: prefix.
    private static string? FindOptionRefusal(SurlCommandLine commandLine) =>
        KeytabComposition.IsGssapiWithoutKeytab(commandLine)
            ? "(2) --auth gssapi needs --keytab"
            : SshAlgorithmComposition.FindRefusal(commandLine);

    private async Task<SurlExitCode> ServeAvailableAsync(
        SurlCommandLine commandLine, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (commandLine.DataDirectory is { } dataDirectory && !canOpenDataDirectory(dataDirectory))
        {
            return WriteFailure(error, SurlExitCode.CouldNotReadFile, $"(37) Could not open directory {dataDirectory}");
        }

        var fileSystem = SurlServerComposition.ComposeContentFileSystem(commandLine, timeProvider, dataDirectoryFileSystem);
        var contentStore = SurlServerComposition.ComposeContentStore(commandLine, fileSystem);
        if (FindListenUrlRefusal(commandLine) is { } refusal)
        {
            return WriteFailure(error, refusal.ExitCode, refusal.Message);
        }

        var (authentication, exitCode, failureMessage) = ComposeAuthentication(commandLine);
        return authentication is null
            ? WriteFailure(error, exitCode, failureMessage!)
            : await LockThenServeAsync(commandLine, fileSystem, contentStore, authentication, output, error, cancellationToken);
    }

    // The --auth words, the --user-file, the --authorized-keys and --keytab files, then the --hostkey
    // and --hostcert files, are checked before the lock is taken and any listener binds (ADR-0032, sections 1 and 2;
    // ADR-0051, decisions 4 and 6; ADR-0057, decision 1).
    private (ComposedAuthentication? Authentication, SurlExitCode ExitCode, string? FailureMessage) ComposeAuthentication(
        SurlCommandLine commandLine)
    {
        var (policy, accountNames, skippedKeytabEntries, exitCode, failureMessage) =
            AuthenticationComposition.Compose(commandLine, readStartFile, timeProvider);
        if (policy is null)
        {
            return (null, exitCode, failureMessage);
        }

        var (sshHostKeys, hostKeyExitCode, hostKeyFailureMessage) = SshHostKeyComposition.Compose(commandLine, readStartFile);
        return sshHostKeys is null
            ? (null, hostKeyExitCode, hostKeyFailureMessage)
            : (new ComposedAuthentication(policy, accountNames, skippedKeytabEntries, sshHostKeys), SurlExitCode.Ok, null);
    }

    // What the servers judge logins by, the keytab entries skipped, and what the SSH server proves itself with.
    private sealed record ComposedAuthentication(
        AuthenticationPolicy Policy,
        IReadOnlyList<string> AccountNames,
        IReadOnlyList<KerberosKeytabSkippedEntry> SkippedKeytabEntries,
        SshHostKeyComposition SshHostKeys);

    private async Task<SurlExitCode> LockThenServeAsync(
        SurlCommandLine commandLine,
        IContentFileSystem fileSystem,
        ContentStore contentStore,
        ComposedAuthentication authentication,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var dataDirectoryLock = TakeDataDirectoryLockWhenGiven(commandLine);
        if (dataDirectoryLock.FailureMessage is { } lockFailure)
        {
            return WriteFailure(error, dataDirectoryLock.ExitCode, lockFailure);
        }

        using (dataDirectoryLock.Holder)
        {
            return await LoadServiceStateAndServeAsync(
                commandLine, fileSystem, contentStore, authentication, output, error, cancellationToken);
        }
    }

    private async Task<SurlExitCode> LoadServiceStateAndServeAsync(
        SurlCommandLine commandLine,
        IContentFileSystem fileSystem,
        ContentStore contentStore,
        ComposedAuthentication authentication,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        (ServiceState? State, string? FailureMessage) loaded;
        try
        {
            loaded = await SurlServerComposition.LoadServiceStateAsync(
                commandLine,
                fileSystem,
                authentication.Policy,
                authentication.AccountNames,
                ServerTlsComposition.IsCertificateConfigured(commandLine),
                timeProvider,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stopped before serving began: the same clean stop as one while serving.
            return SurlExitCode.Ok;
        }

        return loaded.State is null
            ? WriteFailure(error, SurlExitCode.CouldNotReadFile, loaded.FailureMessage!)
            : await ServeUnderTheLockAsync(
                commandLine,
                [
                    .. SurlServerComposition.ComposeProtocolServers(
                        contentStore,
                        loaded.State,
                        authentication.Policy,
                        authentication.SshHostKeys.HostKeys,
                        SshAlgorithmComposition.Compose(authentication.SshHostKeys.HostKeys.SignatureAlgorithms, commandLine),
                        ServerTlsComposition.IsCertificateConfigured(commandLine),
                        commandLine.WsEcho),
                    .. SurlServerComposition.ComposeExtendedProtocolServers(areExtensionsTurnedOn, composeExtendedProtocolServers, new ExtendedProtocolServerServices(
                        contentStore, authentication.Policy, ServerTlsComposition.IsCertificateConfigured(commandLine))),
                ],
                authentication,
                output,
                error,
                cancellationToken);
    }

    private async Task<SurlExitCode> ServeUnderTheLockAsync(
        SurlCommandLine commandLine,
        IProtocolServer[] servers,
        ComposedAuthentication authentication,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        ServerTlsComposition tls;
        try
        {
            tls = ServerTlsComposition.Compose(commandLine, timeProvider);
        }
        catch (TlsFileLoadException failure)
        {
            return WriteTlsFileFailure(error, failure, commandLine.CaCertificateFile);
        }

        using (tls)
        {
            return await ServeSecuredAsAskedAsync(commandLine, servers, authentication, tls, output, error, cancellationToken);
        }
    }

    private async Task<SurlExitCode> ServeSecuredAsAskedAsync(
        SurlCommandLine commandLine,
        IProtocolServer[] servers,
        ComposedAuthentication authentication,
        ServerTlsComposition tls,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var (logStreams, openFailure) = LogStreams.Open(commandLine, output, error, openLogFile);
        if (logStreams is null)
        {
            return WriteFailure(error, SurlExitCode.CouldNotWriteFile, openFailure!);
        }

        using (logStreams)
        {
            // Each loosening option's warning, then the --self-signed one (ADR-0032, section 9).
            AuthenticationComposition.WriteLooseningWarnings(commandLine, logStreams.Log);

            // Each skipped keytab entry's warning, then the unused --keytab one (ADR-0057, decision 1).
            KeytabComposition.WriteStartLines(commandLine, authentication.SkippedKeytabEntries, logStreams.Log);

            // The --self-signed warning from the info level up, the fingerprint note from verbose
            // up, both unstamped (ADR-0032, section 9; ADR-0033, section 7).
            if (tls.ThrowawayCertificateFingerprint is { } fingerprint)
            {
                WriteThrowawayCertificateLines(commandLine.LogLevel, fingerprint, logStreams.Log);
            }

            // The --throwaway-hostkey and --allow-weak-ssh-algorithms warnings, then each SSH host key's note
            // (ADR-0051, decisions 8 and 11).
            authentication.SshHostKeys.WriteStartLines(commandLine, logStreams.Log);

            return await ServeLoggedAsync(commandLine, servers, tls, logStreams, output, error, cancellationToken);
        }
    }

    private static void WriteThrowawayCertificateLines(LogLevel logLevel, string fingerprint, TextWriter log)
    {
        if (logLevel >= LogLevel.Info)
        {
            log.WriteLine(MessagePrefix + "warning: --self-signed: serving a throwaway certificate; clients must skip verification (curl -k)");
        }

        if (logLevel >= LogLevel.Verbose)
        {
            log.WriteLine($"* Serving a throwaway certificate, SHA-256 {fingerprint}");
        }
    }

    private async Task<SurlExitCode> ServeLoggedAsync(
        SurlCommandLine commandLine,
        IProtocolServer[] servers,
        ServerTlsComposition tls,
        LogStreams logStreams,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        // The status lines are written from the info level up (ADR-0033, section 1).
        var statusOutput = commandLine.LogLevel >= LogLevel.Info ? output : TextWriter.Null;
        var reporter = new ListenerStartReporter(
            createListenerFactory(tls.Settings), new ListenerStatusLine(statusOutput), commandLine.ListenUrls.Count);
        var engine = new ServingEngine(
            reporter,
            servers,
            logStreams.CreateExchangeLogFactory(commandLine, timeProvider),
            timeProvider,
            ServingEngine.DefaultShutdownGracePeriod,
            SurlServerComposition.ComposeConnectionLimits(commandLine),
            CreateDataConnectionOpener(tls.Settings),
            commandLine.Limits);

        try
        {
            var exitCode = await engine.ServeAsync(commandLine.ListenUrls, cancellationToken);
            return reporter.BindFailure is { } bindFailure
                ? WriteFailure(error, exitCode, FormatBindFailure(bindFailure))
                : exitCode;
        }
        catch (Exception exception)
        {
            return WriteFailure(error, SurlExitCode.InternalError, $"(125) Internal error: {exception.Message}");
        }
    }
}
