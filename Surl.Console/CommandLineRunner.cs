using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Surl.Authentication;
using Surl.Cli;
using Surl.Content;
using Surl.Core;
using Surl.Kerberos;
using Surl.MailStore;
using Surl.Networking;
using Surl.Output;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Dict;
using Surl.Protocol.Ftp;
using Surl.Protocol.Gopher;
using Surl.Protocol.Http;
using Surl.Protocol.Imap;
using Surl.Protocol.Mqtt;
using Surl.Protocol.Pop3;
using Surl.Protocol.Smtp;
using Surl.Protocol.Ssh;
using Surl.Protocol.Telnet;
using Surl.Protocol.Tftp;

namespace Surl.Console;

/// <summary>
/// Runs one <c>surl</c> command line: the composition root. It parses the command line,
/// answers <c>--help</c>, <c>--manual</c> and <c>--version</c>, checks the data directory when one is given and
/// the schemes, takes the data directory's lock, loads the MQTT retained messages and the mail store kept under it,
/// then constructs the TLS settings, the content store (on disk or in memory), the
/// protocol servers, the exchange log and the serving engine explicitly and serves until
/// cancelled, writing ADR-0007 section 5's texts and returning its exit codes.
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
internal sealed class CommandLineRunner(
    Func<ServerTlsSettings?, IListenerFactory> createListenerFactory,
    Func<string, bool> canOpenDataDirectory,
    Func<string, DataDirectoryLockOutcome> takeDataDirectoryLock,
    TimeProvider timeProvider,
    IContentFileSystem? dataDirectoryFileSystem = null,
    Func<string, FileMode, TextWriter>? openLogFile = null,
    Func<string, byte[]>? readStartFile = null,
    Func<ServerTlsSettings?, IDataConnectionOpener>? createDataConnectionOpener = null)
{
    private const string MessagePrefix = "surl: ";

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
        var parsed = CommandLineParser.Parse(args);

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

        return VersionText.Compose(informationalVersion, RuntimeInformation.RuntimeIdentifier, ComposeRegisteredSchemes());
    }

    /// <summary>
    /// Lists the scheme of every protocol server surl registers, in registration order: what
    /// <c>--version</c>'s <c>Protocols:</c> line lists, and what every <c>--aihelp</c> protocol
    /// topic's schemes must match (ADR-0046 decision 9).
    /// </summary>
    /// <returns>The schemes.</returns>
    internal IReadOnlyList<string> ComposeRegisteredSchemes() =>
        [.. ComposeUnservedProtocolServers(ComposeContentStore(new SurlCommandLine(), timeProvider))
            .SelectMany(server => server.Schemes)];

    /// <summary>
    /// Builds the one content store every protocol server reads, exposing what the command
    /// line's exposure options allow (ADR-0006): the data directory's full path on disk with
    /// <c>--directory</c>, and a new, empty in-memory file system at
    /// <see cref="InMemoryContentFileSystem.RootPath"/> without it (ADR-0031 decisions 1 and 4).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="timeProvider">The clock the in-memory file system stamps last-write times with.</param>
    /// <returns>The content store.</returns>
    internal static ContentStore ComposeContentStore(SurlCommandLine commandLine, TimeProvider timeProvider) =>
        ComposeContentStore(commandLine, ComposeContentFileSystem(commandLine, timeProvider, new DiskContentFileSystem()));

    private static ContentStore ComposeContentStore(SurlCommandLine commandLine, IContentFileSystem fileSystem) =>
        new(
            commandLine.DataDirectory is { } dataDirectory ? Path.GetFullPath(dataDirectory) : InMemoryContentFileSystem.RootPath,
            fileSystem,
            MapExposureOptions(commandLine));

    /// <summary>
    /// Chooses where the MQTT server keeps its retained messages across restarts: a
    /// <see cref="MqttRetainedMessageFile"/> in <c>&lt;data directory's full path&gt;/.surl/mqtt</c>,
    /// read and written through <paramref name="fileSystem"/>, with <c>--directory</c>; none
    /// without it, so they live in memory only (ADR-0031, decision 6).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="fileSystem">The file system the content store serves.</param>
    /// <returns>The file, or <see langword="null"/> without <c>--directory</c>.</returns>
    internal static MqttRetainedMessageFile? ComposeRetainedMessageFile(SurlCommandLine commandLine, IContentFileSystem fileSystem) =>
        commandLine.DataDirectory is { } dataDirectory
            ? new MqttRetainedMessageFile(fileSystem, Path.Join(Path.GetFullPath(dataDirectory), ".surl", "mqtt"))
            : null;

    /// <summary>
    /// Builds the MQTT server's retained messages: loaded from <paramref name="file"/> when there
    /// is one, empty and in memory only when not. A file that cannot be read or does not parse
    /// is not loaded, and gives the <c>(37)</c> message ADR-0031 decision 6 names instead.
    /// </summary>
    /// <param name="file">The retained-message file, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The store, or the message after the <c>surl: </c> prefix when it cannot be loaded.</returns>
    internal static async Task<(MqttRetainedMessages? RetainedMessages, string? FailureMessage)> LoadRetainedMessagesAsync(
        MqttRetainedMessageFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return (new MqttRetainedMessages(), null);
        }

        try
        {
            return (await MqttRetainedMessages.LoadAsync(file, cancellationToken: cancellationToken), null);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return (null, $"(37) Could not read {file.FilePath}: {failure.Message}");
        }
    }

    /// <summary>
    /// Chooses where the mail store the SMTP server delivers into persists: its
    /// <see cref="MailStoreFiles"/> in <c>&lt;data directory's full path&gt;/.surl/mail</c>, read and
    /// written through <paramref name="fileSystem"/>, with <c>--directory</c>; none without it, so
    /// the mail lives in memory only (ADR-0050, decision 7).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="fileSystem">The file system the content store serves.</param>
    /// <returns>The files, or <see langword="null"/> without <c>--directory</c>.</returns>
    internal static MailStoreFiles? ComposeMailStoreFiles(SurlCommandLine commandLine, IContentFileSystem fileSystem) =>
        commandLine.DataDirectory is { } dataDirectory
            ? new MailStoreFiles(fileSystem, Path.Join(Path.GetFullPath(dataDirectory), ".surl", "mail"))
            : null;

    /// <summary>
    /// Builds the one mail store the mail servers share: loaded from <paramref name="files"/> when
    /// there are some, empty and in memory only when not. Its owners are
    /// <paramref name="accountNames"/>, or the anonymous owner alone under <c>--allow-anonymous</c>,
    /// and one message is bounded by <c>--max-filesize</c> (ADR-0050, decisions 2, 6 and 7). A
    /// store that cannot be loaded gives ADR-0050 decision 7's <c>(37)</c> message instead.
    /// </summary>
    /// <param name="files">The mail store's files, or <see langword="null"/> for none.</param>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="accountNames">Every configured account's user name.</param>
    /// <param name="timeProvider">The clock for internal dates and <c>UIDVALIDITY</c>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The store, or the message after the <c>surl: </c> prefix when it cannot be loaded.</returns>
    internal static async Task<(MailboxStore? MailStore, string? FailureMessage)> LoadMailStoreAsync(
        MailStoreFiles? files,
        SurlCommandLine commandLine,
        IReadOnlyList<string> accountNames,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var maxMessageBytes = commandLine.Limits.MaxUploadBytes;
        if (files is null)
        {
            return (new MailboxStore(accountNames, commandLine.AllowAnonymous, timeProvider, maxMessageBytes), null);
        }

        try
        {
            return (await MailboxStore.LoadAsync(
                files, accountNames, commandLine.AllowAnonymous, timeProvider, maxMessageBytes, cancellationToken: cancellationToken), null);
        }
        catch (MailStoreLoadException failure)
        {
            return (null, $"(37) Could not read {failure.FilePath}: {failure.Message}");
        }
    }

    /// <summary>
    /// Chooses the file system the content store serves and service state is kept through:
    /// <paramref name="dataDirectoryFileSystem"/> with <c>--directory</c>, a new
    /// <see cref="InMemoryContentFileSystem"/> without it.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="timeProvider">The clock the in-memory file system stamps last-write times with.</param>
    /// <param name="dataDirectoryFileSystem">The file system the data directory is read and written
    /// through; <c>surl</c>'s is a <see cref="DiskContentFileSystem"/>.</param>
    /// <returns>The file system.</returns>
    internal static IContentFileSystem ComposeContentFileSystem(
        SurlCommandLine commandLine, TimeProvider timeProvider, IContentFileSystem dataDirectoryFileSystem) =>
        commandLine.DataDirectory is null ? new InMemoryContentFileSystem(timeProvider) : dataDirectoryFileSystem;

    private static ContentExposureOptions MapExposureOptions(SurlCommandLine commandLine) => new()
    {
        AllowUploads = commandLine.AllowUploads,
        ListDirectories = commandLine.ListDirectories,
        FollowSymbolicLinks = commandLine.FollowSymlinks,
        ServeDotFiles = commandLine.ServeDotFiles,
        MaxUploadBytes = commandLine.Limits.MaxUploadBytes,
    };

    /// <summary>
    /// Builds the connection limits the serving engine enforces from the command line's
    /// <c>--max-connections</c>, <c>--max-connections-per-address</c>, <c>--idle-timeout</c>
    /// and <c>-m</c>/<c>--max-time</c> (ADR-0006 section 1). 0 seconds, parsed as
    /// <see cref="Timeout.InfiniteTimeSpan"/>, becomes <see cref="TimeSpan.Zero"/>: no limit.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <returns>The connection limits.</returns>
    internal static ConnectionLimits ComposeConnectionLimits(SurlCommandLine commandLine) => new(
        commandLine.MaxConnections,
        commandLine.MaxConnectionsPerAddress,
        NoLimitAsZero(commandLine.IdleTimeout),
        NoLimitAsZero(commandLine.MaxTime));

    private static TimeSpan NoLimitAsZero(TimeSpan seconds) =>
        seconds == Timeout.InfiniteTimeSpan ? TimeSpan.Zero : seconds;

    /// <summary>
    /// Formats the message for a TLS option file that could not be loaded, after the
    /// <c>surl: </c> prefix, and the exit code it ends surl with (ADR-0010 section 3, ADR-0020).
    /// </summary>
    /// <param name="failure">The failure the loader threw.</param>
    /// <param name="caCertificateFile">The <c>--cacert</c> file as given, named by the "does not exist" lines.</param>
    /// <returns>The exit code and the message lines.</returns>
    internal static (SurlExitCode ExitCode, string[] Lines) DescribeTlsFileFailure(
        TlsFileLoadException failure, string? caCertificateFile) => failure.Failure switch
        {
            TlsFileLoadFailure.CaCertificateNotFound => (
                SurlExitCode.FailedInit,
                [$"The file '{caCertificateFile}' provided to --cacert does not exist", "option --cacert: is badly used here"]),
            TlsFileLoadFailure.CaCertificateUnreadable => (
                SurlExitCode.CaCertificateBadFile, [$"(77) Could not load the CA certificates: {failure.Message}"]),
            _ => (SurlExitCode.CertificateProblem, [$"(58) Could not load the server certificate: {failure.Message}"]),
        };

    // Every protocol server surl registers, over TCP or (TFTP) UDP; those that serve files serve
    // the one content store, the MQTT server keeps its retained messages in the store it is
    // given, the SMTP server delivers into the one mail store and the IMAP and POP3 servers
    // serve it. https, smtps, imaps and pop3s are the HTTP, SMTP, IMAP and POP3 servers
    // themselves, over a connection the engine has secured (ADR-0020, ADR-0053 decision 5,
    // ADR-0055 decision 11, ADR-0056 decision 8). The FTP server answers ftp and, TLS from the
    // first byte, ftps itself (ADR-0052 decision 5). The HTTP, MQTT, SMTP, IMAP, POP3, FTP and
    // SSH servers, the ones with a login, judge it by the one policy (ADR-0032); SMTP and IMAP
    // offer STARTTLS, POP3 STLS and FTP AUTH TLS only when a certificate is configured. The SSH server answers scp and sftp with its host keys and
    // offers ADR-0051 decision 2's default algorithms for them (ADR-0051 decision 13).
    private static IProtocolServer[] ComposeProtocolServers(
        ContentStore contentStore,
        ServiceState serviceState,
        AuthenticationPolicy authenticationPolicy,
        SshHostKeySet sshHostKeys,
        bool isTlsUpgradeAvailable)
    {
        var httpServer = new HttpProtocolServer(contentStore, authenticationPolicy);
        var smtpServer = new SmtpProtocolServer(authenticationPolicy, authenticationPolicy, serviceState.MailStore, isTlsUpgradeAvailable);
        var imapServer = new ImapProtocolServer(authenticationPolicy, authenticationPolicy, serviceState.MailStore, isTlsUpgradeAvailable);
        var pop3Server = new Pop3ProtocolServer(authenticationPolicy, authenticationPolicy, serviceState.MailStore, isTlsUpgradeAvailable);

        return
        [
            httpServer,
            new ImplicitTlsSchemeServer(httpServer, "https"),
            new DictProtocolServer(contentStore),
            new FtpProtocolServer(contentStore, authenticationPolicy, isTlsUpgradeAvailable),
            new GopherProtocolServer(contentStore),
            imapServer,
            new ImplicitTlsSchemeServer(imapServer, "imaps"),
            new MqttProtocolServer(serviceState.RetainedMessages, authenticationPolicy),
            pop3Server,
            new ImplicitTlsSchemeServer(pop3Server, "pop3s"),
            smtpServer,
            new ImplicitTlsSchemeServer(smtpServer, "smtps"),
            new SshProtocolServer(
                sshHostKeys,
                SshAlgorithmOffer.Default(sshHostKeys.SignatureAlgorithms, AesGcm.IsSupported),
                authenticationPolicy,
                new SshSystemRandomSource(),
                contentStore),
            new TelnetProtocolServer(),
            new TftpProtocolServer(contentStore),
        ];
    }

    // The servers composed only to ask for their schemes: nothing is served through them, so
    // they need no retained messages, no mail, no accounts and no host keys.
    private IProtocolServer[] ComposeUnservedProtocolServers(ContentStore contentStore) =>
        ComposeProtocolServers(
            contentStore,
            new ServiceState(new MqttRetainedMessages(), new MailboxStore([], allowAnonymous: false, timeProvider)),
            AuthenticationComposition.ComposeWithoutAccounts(timeProvider),
            new SshHostKeySet(),
            isTlsUpgradeAvailable: false);

    // What the servers keep across connections, loaded after the lock: the MQTT retained
    // messages and the mail store (ADR-0031 decision 6, ADR-0050 decision 7).
    private sealed record ServiceState(MqttRetainedMessages RetainedMessages, MailboxStore MailStore);

    private static SurlExitCode WriteTlsFileFailure(TextWriter error, TlsFileLoadException failure, string? caCertificateFile)
    {
        var (exitCode, lines) = DescribeTlsFileFailure(failure, caCertificateFile);
        foreach (var line in lines)
        {
            error.WriteLine(MessagePrefix + line);
        }

        return exitCode;
    }

    private static string? FindUnregisteredScheme(IReadOnlyList<ListenUrl> listenUrls, IProtocolServer[] servers)
    {
        var registeredSchemes = servers
            .SelectMany(server => server.Schemes)
            .ToHashSet(StringComparer.Ordinal);

        return listenUrls.Select(listenUrl => listenUrl.Scheme).FirstOrDefault(scheme => !registeredSchemes.Contains(scheme));
    }

    // A listen URL no registered server answers, then one TLS from the first byte with no
    // certificate to serve (ADR-0032, section 10), then an scp or sftp one with no host key to
    // serve (ADR-0051, decision 4): each refused before any listener binds.
    private static (SurlExitCode ExitCode, string Message)? FindListenUrlRefusal(
        SurlCommandLine commandLine, IProtocolServer[] servers)
    {
        if (FindUnregisteredScheme(commandLine.ListenUrls, servers) is { } scheme)
        {
            return (SurlExitCode.UnsupportedProtocol, $"(1) Protocol \"{scheme}\" not supported");
        }

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

    /// <summary>
    /// The first option given that names something this build does not serve yet, in option-table
    /// order: the <c>--auth</c> word <c>gssapi</c>, parsed but refused until its mechanism is
    /// built (ADR-0049 section 3), then <c>--hostcert</c>, refused until the SSH server serves host
    /// certificates (BL-222, ADR-0051 decision 5), and <c>--allow-weak-ssh-algorithms</c>, refused
    /// until the SSH server offers the weak algorithms (BL-221), after ADR-0032 section 1's precedent.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <returns>The option as <c>--&lt;name&gt;</c> (and the word, for <c>--auth</c>), or <see langword="null"/> when none is given.</returns>
    internal static string? FindUnavailableOption(SurlCommandLine commandLine) =>
        UnavailableOptions.FirstOrDefault(unavailable => unavailable.IsGiven(commandLine)).Option;

    private static readonly (string Option, Func<SurlCommandLine, bool> IsGiven)[] UnavailableOptions =
    [
        ("--auth gssapi", commandLine => commandLine.AcceptedAuthenticationMethods.Contains("gssapi")),
        ("--hostcert", commandLine => commandLine.HostCertificateFiles.Count > 0),
        ("--allow-weak-ssh-algorithms", commandLine => commandLine.AllowWeakSshAlgorithms),
    ];

    // --auth gssapi without --keytab (ADR-0057, decision 1), then an option this build does not
    // serve yet, is refused before anything else is checked.
    private Task<SurlExitCode> ServeAsync(
        SurlCommandLine commandLine, TextWriter output, TextWriter error, CancellationToken cancellationToken) =>
        FindOptionRefusal(commandLine) is { } refusal
            ? Task.FromResult(WriteFailure(error, SurlExitCode.FailedInit, refusal))
            : ServeAvailableAsync(commandLine, output, error, cancellationToken);

    // The first refusal of the options' consistency or availability, after the surl: prefix.
    private static string? FindOptionRefusal(SurlCommandLine commandLine)
    {
        if (KeytabComposition.IsGssapiWithoutKeytab(commandLine))
        {
            return "(2) --auth gssapi needs --keytab";
        }

        return FindUnavailableOption(commandLine) is { } unavailableOption
            ? $"(2) {unavailableOption} is not available in this build"
            : null;
    }

    private async Task<SurlExitCode> ServeAvailableAsync(
        SurlCommandLine commandLine, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (commandLine.DataDirectory is { } dataDirectory && !canOpenDataDirectory(dataDirectory))
        {
            return WriteFailure(error, SurlExitCode.CouldNotReadFile, $"(37) Could not open directory {dataDirectory}");
        }

        var fileSystem = ComposeContentFileSystem(commandLine, timeProvider, dataDirectoryFileSystem);
        var contentStore = ComposeContentStore(commandLine, fileSystem);
        if (FindListenUrlRefusal(commandLine, ComposeUnservedProtocolServers(contentStore)) is { } refusal)
        {
            return WriteFailure(error, refusal.ExitCode, refusal.Message);
        }

        var (authentication, exitCode, failureMessage) = ComposeAuthentication(commandLine);
        return authentication is null
            ? WriteFailure(error, exitCode, failureMessage!)
            : await LockThenServeAsync(commandLine, fileSystem, contentStore, authentication, output, error, cancellationToken);
    }

    // The --auth words, the --user-file, the --authorized-keys and --keytab files, then the --hostkey
    // files, are checked before the lock is taken and any listener binds (ADR-0032, sections 1 and 2;
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
            loaded = await LoadServiceStateAsync(commandLine, fileSystem, authentication.AccountNames, cancellationToken);
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
                ComposeProtocolServers(
                    contentStore,
                    loaded.State,
                    authentication.Policy,
                    authentication.SshHostKeys.HostKeys,
                    ServerTlsComposition.IsCertificateConfigured(commandLine)),
                authentication,
                output,
                error,
                cancellationToken);
    }

    // The retained messages, then the mail store; the first that cannot be loaded ends the start.
    private async Task<(ServiceState? State, string? FailureMessage)> LoadServiceStateAsync(
        SurlCommandLine commandLine, IContentFileSystem fileSystem, IReadOnlyList<string> accountNames, CancellationToken cancellationToken)
    {
        var (retainedMessages, retainedMessagesFailure) = await LoadRetainedMessagesAsync(
            ComposeRetainedMessageFile(commandLine, fileSystem), cancellationToken);
        if (retainedMessages is null)
        {
            return (null, retainedMessagesFailure);
        }

        var (mailStore, mailStoreFailure) = await LoadMailStoreAsync(
            ComposeMailStoreFiles(commandLine, fileSystem), commandLine, accountNames, timeProvider, cancellationToken);
        return mailStore is null ? (null, mailStoreFailure) : (new ServiceState(retainedMessages, mailStore), null);
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

            // The --throwaway-hostkey warning, then each SSH host key's note (ADR-0051, decisions 8 and 11).
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
            ComposeConnectionLimits(commandLine),
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
