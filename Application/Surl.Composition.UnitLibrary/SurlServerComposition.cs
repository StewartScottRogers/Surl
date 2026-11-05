using System.Security.Cryptography;
using Surl.Authentication;
using Surl.Cli;
using Surl.Content;
using Surl.Core;
using Surl.MailStore;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Dict;
using Surl.Protocol.Ftp;
using Surl.Protocol.Gopher;
using Surl.Protocol.Http;
using Surl.Protocol.Imap;
using Surl.Protocol.Ldap;
using Surl.Protocol.Mqtt;
using Surl.Protocol.Pop3;
using Surl.Protocol.Rtsp;
using Surl.Protocol.Smb;
using Surl.Protocol.Smtp;
using Surl.Protocol.Ssh;
using Surl.Protocol.Telnet;
using Surl.Protocol.Tftp;
using Surl.Protocol.Ws;

namespace Surl.Composition;

/// <summary>
/// The one place protocol servers are composed (ADR-0099, decision 1): every original server
/// <c>surl</c> registers, with its <see cref="ImplicitTlsSchemeServer"/> second registrations, the
/// extended servers behind <c>SURL_EXTENSIONS</c>, the content store they serve, the service state
/// they keep across connections and the connection limits the serving engine enforces. Each host
/// (<c>surl</c> today) calls it instead of composing them itself.
/// </summary>
/// <remarks>
/// Public because more than one host calls it, but not a supported programming interface: it may
/// change in any release (ADR-0099, decision 1).
/// </remarks>
public static partial class SurlServerComposition
{
    /// <summary>
    /// Lists the scheme of every original protocol server surl registers, in registration order:
    /// what <c>--version</c>'s <c>Protocols:</c> line lists, and, with
    /// <see cref="ComposeExtendedSchemes"/>, what every <c>--aihelp</c> protocol topic's schemes
    /// must match (ADR-0046 decision 9).
    /// </summary>
    /// <param name="timeProvider">The clock the servers composed to ask for their schemes run on.</param>
    /// <returns>The schemes.</returns>
    public static IReadOnlyList<string> ComposeRegisteredSchemes(TimeProvider timeProvider) =>
        [.. ComposeUnservedProtocolServers(ComposeContentStore(new SurlCommandLine(), timeProvider), timeProvider)
            .SelectMany(server => server.Schemes)];

    /// <summary>
    /// Lists the scheme of every extended protocol server surl registers, in registration order;
    /// none while <c>SURL_EXTENSIONS=0</c>: what <c>--version</c>'s <c>Extended:</c> line lists
    /// and the listen-URL parser accepts besides upstream curl's schemes (ADR-0083, decisions 6
    /// and 8).
    /// </summary>
    /// <param name="areExtensionsTurnedOn">Whether <see cref="ExtendedProtocolServers.AreTurnedOn"/> leaves them on.</param>
    /// <param name="composeExtendedProtocolServers">
    /// Builds the extended servers; <see cref="ExtendedProtocolServers.Compose"/> when <see langword="null"/>.
    /// </param>
    /// <param name="timeProvider">The clock the servers composed to ask for their schemes run on.</param>
    /// <returns>The schemes.</returns>
    public static IReadOnlyList<string> ComposeExtendedSchemes(
        bool areExtensionsTurnedOn,
        Func<ExtendedProtocolServerServices, IReadOnlyList<IProtocolServer>>? composeExtendedProtocolServers,
        TimeProvider timeProvider) =>
        [.. ComposeExtendedProtocolServers(
                areExtensionsTurnedOn,
                composeExtendedProtocolServers,
                new ExtendedProtocolServerServices(
                    ComposeContentStore(new SurlCommandLine(), timeProvider),
                    ComposePolicyWithoutAccounts(timeProvider),
                    IsTlsUpgradeAvailable: false))
            .SelectMany(server => server.Schemes)];

    /// <summary>
    /// Builds the extended protocol servers while the switch leaves them on, and none, never
    /// composed, while <c>SURL_EXTENSIONS=0</c> (ADR-0083, decision 6).
    /// </summary>
    /// <param name="areExtensionsTurnedOn">Whether <see cref="ExtendedProtocolServers.AreTurnedOn"/> leaves them on.</param>
    /// <param name="composeExtendedProtocolServers">
    /// Builds the extended servers; <see cref="ExtendedProtocolServers.Compose"/> when <see langword="null"/>.
    /// </param>
    /// <param name="services">What the servers are built from.</param>
    /// <returns>The servers.</returns>
    public static IReadOnlyList<IProtocolServer> ComposeExtendedProtocolServers(
        bool areExtensionsTurnedOn,
        Func<ExtendedProtocolServerServices, IReadOnlyList<IProtocolServer>>? composeExtendedProtocolServers,
        ExtendedProtocolServerServices services) =>
        areExtensionsTurnedOn ? (composeExtendedProtocolServers ?? ExtendedProtocolServers.Compose)(services) : [];

    /// <summary>
    /// Builds a policy with no accounts, no loosening option and no HTTP method: what the servers
    /// are composed with when only their schemes are wanted, since nothing is served through them.
    /// </summary>
    /// <param name="timeProvider">The clock the policy runs on.</param>
    /// <returns>The policy.</returns>
    public static AuthenticationPolicy ComposePolicyWithoutAccounts(TimeProvider timeProvider) =>
        new(
            new AuthenticationSettings(new AccountBook([]), AllowAnonymous: false, AllowPlaintextAuthentication: false, AuthenticationMethods.DefaultAccepted),
            [],
            timeProvider);

    /// <summary>
    /// Builds the one content store every protocol server reads, exposing what the command
    /// line's exposure options allow (ADR-0006): the data directory's full path on disk with
    /// <c>--directory</c>, and a new, empty in-memory file system at
    /// <see cref="InMemoryContentFileSystem.RootPath"/> without it (ADR-0031 decisions 1 and 4).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="timeProvider">The clock the in-memory file system stamps last-write times with.</param>
    /// <returns>The content store.</returns>
    public static ContentStore ComposeContentStore(SurlCommandLine commandLine, TimeProvider timeProvider) =>
        ComposeContentStore(commandLine, ComposeContentFileSystem(commandLine, timeProvider, new DiskContentFileSystem()));

    /// <summary>
    /// Builds the one content store every protocol server reads over <paramref name="fileSystem"/>,
    /// rooted at the data directory's full path with <c>--directory</c> and at
    /// <see cref="InMemoryContentFileSystem.RootPath"/> without it, exposing what the command line's
    /// exposure options allow (ADR-0006, ADR-0031 decisions 1 and 4).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="fileSystem">The file system <see cref="ComposeContentFileSystem"/> chose.</param>
    /// <returns>The content store.</returns>
    public static ContentStore ComposeContentStore(SurlCommandLine commandLine, IContentFileSystem fileSystem) =>
        new(
            commandLine.DataDirectory is { } dataDirectory ? Path.GetFullPath(dataDirectory) : InMemoryContentFileSystem.RootPath,
            fileSystem,
            MapExposureOptions(commandLine));

    /// <summary>
    /// Chooses the file system the content store serves and service state is kept through:
    /// <paramref name="dataDirectoryFileSystem"/> with <c>--directory</c>;
    /// <paramref name="contentWithoutDataDirectory"/>, or a new <see cref="InMemoryContentFileSystem"/>
    /// when that is <see langword="null"/>, without it.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="timeProvider">The clock the in-memory file system stamps last-write times with.</param>
    /// <param name="dataDirectoryFileSystem">The file system the data directory is read and written
    /// through; <c>surl</c>'s is a <see cref="DiskContentFileSystem"/>.</param>
    /// <param name="contentWithoutDataDirectory">The file system a host serves without
    /// <c>--directory</c>, such as files its caller supplies in memory; <c>surl</c> gives none.</param>
    /// <returns>The file system.</returns>
    public static IContentFileSystem ComposeContentFileSystem(
        SurlCommandLine commandLine,
        TimeProvider timeProvider,
        IContentFileSystem dataDirectoryFileSystem,
        IContentFileSystem? contentWithoutDataDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        return commandLine.DataDirectory is null
            ? contentWithoutDataDirectory ?? new InMemoryContentFileSystem(timeProvider)
            : dataDirectoryFileSystem;
    }

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
    public static ConnectionLimits ComposeConnectionLimits(SurlCommandLine commandLine) => new(
        commandLine.MaxConnections,
        commandLine.MaxConnectionsPerAddress,
        NoLimitAsZero(commandLine.IdleTimeout),
        NoLimitAsZero(commandLine.MaxTime));

    private static TimeSpan NoLimitAsZero(TimeSpan seconds) =>
        seconds == Timeout.InfiniteTimeSpan ? TimeSpan.Zero : seconds;

    /// <summary>
    /// Loads what the servers keep across connections: the retained messages, then the mail store,
    /// then the LDAP directory; the first that cannot be loaded ends the start with its
    /// <c>(37)</c> message.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="fileSystem">The file system the content store serves.</param>
    /// <param name="authenticationPolicy">Judges every LDAP bind.</param>
    /// <param name="accountNames">Every configured account's user name, the mail store's owners.</param>
    /// <param name="isTlsUpgradeAvailable">Whether a certificate is configured, so the LDAP server offers <c>StartTLS</c>.</param>
    /// <param name="timeProvider">The one clock.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The state, or the message after the <c>surl: </c> prefix when part of it cannot be loaded.</returns>
    public static async Task<(ServiceState? State, string? FailureMessage)> LoadServiceStateAsync(
        SurlCommandLine commandLine,
        IContentFileSystem fileSystem,
        AuthenticationPolicy authenticationPolicy,
        IReadOnlyList<string> accountNames,
        bool isTlsUpgradeAvailable,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var (retainedMessages, retainedMessagesFailure) = await LoadRetainedMessagesAsync(
            ComposeRetainedMessageFile(commandLine, fileSystem), cancellationToken);
        if (retainedMessages is null)
        {
            return (null, retainedMessagesFailure);
        }

        var (mailStore, mailStoreFailure) = await LoadMailStoreAsync(
            ComposeMailStoreFiles(commandLine, fileSystem), commandLine, accountNames, timeProvider, cancellationToken);
        if (mailStore is null)
        {
            return (null, mailStoreFailure);
        }

        var (ldapServer, ldapFailure) = await LoadLdapServerAsync(
            ComposeLdapDirectoryFile(commandLine, fileSystem),
            authenticationPolicy,
            isTlsUpgradeAvailable,
            timeProvider,
            cancellationToken);
        return ldapServer is null ? (null, ldapFailure) : (new ServiceState(retainedMessages, mailStore, ldapServer), null);
    }

    /// <summary>
    /// Chooses where the MQTT server keeps its retained messages across restarts: a
    /// <see cref="MqttRetainedMessageFile"/> in <c>&lt;data directory's full path&gt;/.surl/mqtt</c>,
    /// read and written through <paramref name="fileSystem"/>, with <c>--directory</c>; none
    /// without it, so they live in memory only (ADR-0031, decision 6).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="fileSystem">The file system the content store serves.</param>
    /// <returns>The file, or <see langword="null"/> without <c>--directory</c>.</returns>
    public static MqttRetainedMessageFile? ComposeRetainedMessageFile(SurlCommandLine commandLine, IContentFileSystem fileSystem) =>
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
    public static async Task<(MqttRetainedMessages? RetainedMessages, string? FailureMessage)> LoadRetainedMessagesAsync(
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
    public static MailStoreFiles? ComposeMailStoreFiles(SurlCommandLine commandLine, IContentFileSystem fileSystem) =>
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
    public static async Task<(MailboxStore? MailStore, string? FailureMessage)> LoadMailStoreAsync(
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
    /// Chooses where the LDAP server's directory is read from: its <see cref="LdapDirectoryFile"/>
    /// in <c>&lt;data directory's full path&gt;/.surl/ldap</c>, read through
    /// <paramref name="fileSystem"/>, with <c>--directory</c>; none without it, so the directory is
    /// empty (ADR-0072, decision 1).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="fileSystem">The file system the content store serves.</param>
    /// <returns>The file, or <see langword="null"/> without <c>--directory</c>.</returns>
    public static LdapDirectoryFile? ComposeLdapDirectoryFile(SurlCommandLine commandLine, IContentFileSystem fileSystem) =>
        commandLine.DataDirectory is { } dataDirectory
            ? new LdapDirectoryFile(fileSystem, Path.Join(Path.GetFullPath(dataDirectory), ".surl", "ldap"))
            : null;

    /// <summary>
    /// Builds the LDAP server over its directory: loaded from <paramref name="file"/> when there is
    /// one, empty when not, its binds judged by <paramref name="authenticationPolicy"/> and
    /// <c>StartTLS</c> offered when a certificate is configured (ADR-0072, decisions 1 and 5). A
    /// directory that cannot be loaded gives ADR-0072 decision 1's <c>(37)</c> message instead.
    /// </summary>
    /// <param name="file">The directory's file, or <see langword="null"/> for none.</param>
    /// <param name="authenticationPolicy">Judges every bind.</param>
    /// <param name="isTlsUpgradeAvailable">Whether a certificate is configured.</param>
    /// <param name="timeProvider">The clock a search's time limit is measured by.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The server, or the message after the <c>surl: </c> prefix when its directory cannot be loaded.</returns>
    public static async Task<(LdapProtocolServer? LdapServer, string? FailureMessage)> LoadLdapServerAsync(
        LdapDirectoryFile? file,
        AuthenticationPolicy authenticationPolicy,
        bool isTlsUpgradeAvailable,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return (new LdapProtocolServer(authenticationPolicy, authenticationPolicy, isTlsUpgradeAvailable), null);
        }

        try
        {
            return (await LdapProtocolServer.LoadAsync(
                file, authenticationPolicy, authenticationPolicy, timeProvider, isTlsUpgradeAvailable, cancellationToken), null);
        }
        catch (LdapDirectoryLoadException failure)
        {
            return (null, $"(37) Could not read {failure.FilePath}: {failure.Message}");
        }
    }

    /// <summary>
    /// Builds every original protocol server surl registers, over TCP or (TFTP) UDP, in
    /// registration order.
    /// </summary>
    /// <remarks>
    /// Those that serve files serve the one content store, the MQTT server keeps its retained
    /// messages in the store it is given, the SMTP server delivers into the one mail store and the
    /// IMAP and POP3 servers serve it. https, smtps, imaps, pop3s, ldaps and wss are the HTTP,
    /// SMTP, IMAP, POP3, LDAP and WebSocket servers themselves, registered again through
    /// <see cref="ImplicitTlsSchemeServer"/> over a connection the engine has secured (ADR-0020,
    /// ADR-0053 decision 5, ADR-0055 decision 11, ADR-0056 decision 8, ADR-0071 decision 8,
    /// ADR-0072 decision 5). The FTP server answers ftp and, TLS from the first byte, ftps itself
    /// (ADR-0052 decision 5); the SMB server smb and smbs (ADR-0073 decision 6). The servers with a
    /// login judge it by the one policy (ADR-0032); SMTP and IMAP offer STARTTLS, POP3 STLS, FTP
    /// AUTH TLS and LDAP StartTLS only when a certificate is configured. The SSH server answers scp
    /// and sftp with its host keys and offers the algorithms given (ADR-0051, ADR-0066). The
    /// WebSocket server echoes every client message instead with <c>--ws-echo</c> (ADR-0071
    /// decision 4). The RTSP server has no rtsps, as curl has none (ADR-0074 decision 7).
    /// </remarks>
    /// <param name="contentStore">The one content store.</param>
    /// <param name="serviceState">The retained messages, the mail store and the LDAP server.</param>
    /// <param name="authenticationPolicy">The one policy every login is judged by.</param>
    /// <param name="sshHostKeys">The SSH server's host keys.</param>
    /// <param name="sshAlgorithms">The SSH algorithms offered.</param>
    /// <param name="isTlsUpgradeAvailable">Whether a certificate is configured, so a TLS upgrade can be offered.</param>
    /// <param name="echoesWebSocketMessages">Whether <c>--ws-echo</c> is given.</param>
    /// <returns>The servers.</returns>
    public static IProtocolServer[] ComposeProtocolServers(
        ContentStore contentStore,
        ServiceState serviceState,
        AuthenticationPolicy authenticationPolicy,
        SshHostKeySet sshHostKeys,
        SshAlgorithmOffer sshAlgorithms,
        bool isTlsUpgradeAvailable,
        bool echoesWebSocketMessages)
    {
        ArgumentNullException.ThrowIfNull(serviceState);

        var httpServer = new HttpProtocolServer(contentStore, authenticationPolicy);
        var smtpServer = new SmtpProtocolServer(authenticationPolicy, authenticationPolicy, serviceState.MailStore, isTlsUpgradeAvailable);
        var imapServer = new ImapProtocolServer(authenticationPolicy, authenticationPolicy, serviceState.MailStore, isTlsUpgradeAvailable);
        var pop3Server = new Pop3ProtocolServer(authenticationPolicy, authenticationPolicy, serviceState.MailStore, isTlsUpgradeAvailable);
        var wsServer = new WsProtocolServer(contentStore, authenticationPolicy, echoesWebSocketMessages);

        return
        [
            httpServer,
            new ImplicitTlsSchemeServer(httpServer, "https"),
            new DictProtocolServer(contentStore),
            new FtpProtocolServer(contentStore, authenticationPolicy, isTlsUpgradeAvailable),
            new GopherProtocolServer(contentStore),
            imapServer,
            new ImplicitTlsSchemeServer(imapServer, "imaps"),
            serviceState.LdapServer,
            new ImplicitTlsSchemeServer(serviceState.LdapServer, "ldaps"),
            new MqttProtocolServer(serviceState.RetainedMessages, authenticationPolicy),
            pop3Server,
            new ImplicitTlsSchemeServer(pop3Server, "pop3s"),
            new RtspProtocolServer(contentStore, authenticationPolicy),
            new SmbProtocolServer(contentStore, authenticationPolicy),
            smtpServer,
            new ImplicitTlsSchemeServer(smtpServer, "smtps"),
            new SshProtocolServer(
                sshHostKeys,
                sshAlgorithms,
                authenticationPolicy,
                new SshSystemRandomSource(),
                contentStore),
            new TelnetProtocolServer(),
            new TftpProtocolServer(contentStore),
            wsServer,
            new ImplicitTlsSchemeServer(wsServer, "wss"),
        ];
    }

    /// <summary>
    /// Builds the servers only to ask for their schemes: nothing is served through them, so they
    /// need no retained messages, no mail, no directory, no accounts and no host keys.
    /// </summary>
    /// <param name="contentStore">The content store they are built over.</param>
    /// <param name="timeProvider">The clock they run on.</param>
    /// <returns>The servers, in registration order.</returns>
    public static IProtocolServer[] ComposeUnservedProtocolServers(ContentStore contentStore, TimeProvider timeProvider)
    {
        var authenticationPolicy = ComposePolicyWithoutAccounts(timeProvider);
        return ComposeProtocolServers(
            contentStore,
            new ServiceState(
                new MqttRetainedMessages(),
                new MailboxStore([], allowAnonymous: false, timeProvider),
                new LdapProtocolServer(authenticationPolicy, authenticationPolicy)),
            authenticationPolicy,
            new SshHostKeySet(),
            SshAlgorithmOffer.Default([], AesGcm.IsSupported),
            isTlsUpgradeAvailable: false,
            echoesWebSocketMessages: false);
    }
}
