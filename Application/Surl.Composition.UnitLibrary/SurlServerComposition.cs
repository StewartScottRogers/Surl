using System.Security.Cryptography;
using Surl.Authentication;
using Surl.Cli;
using Surl.Content;
using Surl.Core;
using Surl.Extended.Protocol.DnsQuery;
using Surl.MailStore;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Ldap;
using Surl.Protocol.Ssh;

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
    /// Lists the scheme of every extended protocol server surl registers, each once (<c>dns</c> has a UDP and a TCP server), in registration order;
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
                ComposeUnservedExtendedServices(timeProvider))
            .SelectMany(server => server.Schemes)
            .Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// Builds what the extended servers are built from when only their schemes are wanted: the
    /// services <see cref="ComposeUnservedProtocolServices"/> composes over an empty in-memory content
    /// store, its in-memory object metadata index among them, since nothing is served through them.
    /// </summary>
    /// <param name="timeProvider">The clock they run on.</param>
    /// <returns>The services.</returns>
    public static ExtendedProtocolServerServices ComposeUnservedExtendedServices(TimeProvider timeProvider) =>
        new(ComposeUnservedProtocolServices(ComposeContentStore(new SurlCommandLine(), timeProvider), timeProvider));

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
        ComposeContentStore(commandLine, fileSystem, DiscardingSurlEventPublisher.Instance, EmptyFaultPlan.Instance);

    /// <summary>
    /// Builds the one content store over <paramref name="fileSystem"/>, as
    /// <see cref="ComposeContentStore(SurlCommandLine, IContentFileSystem)"/> does, publishing each
    /// change on <paramref name="events"/> and applying <paramref name="faults"/>' out-of-space rules
    /// (ADR-0124, decisions 1 and 4).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="fileSystem">The file system <see cref="ComposeContentFileSystem"/> chose.</param>
    /// <param name="events">The instance's event stream.</param>
    /// <param name="faults">The instance's fault plan.</param>
    /// <returns>The content store.</returns>
    public static ContentStore ComposeContentStore(
        SurlCommandLine commandLine, IContentFileSystem fileSystem, ISurlEventPublisher events, IFaultPlan faults) =>
        ComposeContentStore(commandLine, fileSystem, events, faults, [], []);

    /// <summary>
    /// Builds the one content store over <paramref name="fileSystem"/>, as
    /// <see cref="ComposeContentStore(SurlCommandLine, IContentFileSystem, ISurlEventPublisher, IFaultPlan)"/>
    /// does, serving <paramref name="shares"/> beside the root and mapping <paramref name="buckets"/>
    /// (ADR-0102 decisions 2.1 and 2.4), as <see cref="ContentShareComposition.Compose"/> composed them.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="fileSystem">The file system <see cref="ComposeContentFileSystem"/> chose.</param>
    /// <param name="events">The instance's event stream.</param>
    /// <param name="faults">The instance's fault plan.</param>
    /// <param name="shares">The composed shares.</param>
    /// <param name="buckets">The composed buckets.</param>
    /// <returns>The content store.</returns>
    public static ContentStore ComposeContentStore(
        SurlCommandLine commandLine,
        IContentFileSystem fileSystem,
        ISurlEventPublisher events,
        IFaultPlan faults,
        IReadOnlyList<ContentShare> shares,
        IReadOnlyList<ContentBucket> buckets) =>
        new(ComposeServedRoot(commandLine), fileSystem, MapExposureOptions(commandLine), shares, buckets, events, faults);

    private static string ComposeServedRoot(SurlCommandLine commandLine) =>
        commandLine.DataDirectory is { } dataDirectory ? Path.GetFullPath(dataDirectory) : InMemoryContentFileSystem.RootPath;

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
    /// Loads what the servers keep across connections: the message bus, then the mail store,
    /// then the LDAP directory, then the object metadata index; the first that cannot be loaded ends the start with its
    /// <c>(37)</c> message.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="fileSystem">The file system the content store serves.</param>
    /// <param name="authenticationPolicy">Judges every LDAP bind.</param>
    /// <param name="accountNames">Every configured account's user name, the mail store's owners.</param>
    /// <param name="isTlsUpgradeAvailable">Whether a certificate is configured, so the LDAP server offers <c>StartTLS</c>.</param>
    /// <param name="timeProvider">The one clock.</param>
    /// <param name="events">The instance's event stream, which the bus, the mail store and the index publish their changes on.</param>
    /// <param name="faults">The instance's fault plan, whose out-of-space rules the bus and the mail store apply.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <param name="mailDomains">The mail domains and mailboxes the mail store resolves recipients against (ADR-0102, decision 1); none without <c>--mail-domain</c>.</param>
    /// <returns>The state, or the message after the <c>surl: </c> prefix when part of it cannot be loaded.</returns>
    public static async Task<(ServiceState? State, string? FailureMessage)> LoadServiceStateAsync(
        SurlCommandLine commandLine,
        IContentFileSystem fileSystem,
        AuthenticationPolicy authenticationPolicy,
        IReadOnlyList<string> accountNames,
        bool isTlsUpgradeAvailable,
        TimeProvider timeProvider,
        ISurlEventPublisher events,
        IFaultPlan faults,
        CancellationToken cancellationToken,
        MailDomainConfiguration? mailDomains = null)
    {
        var (messageBus, messageBusFailure) = await LoadMessageBusAsync(
            ComposeMessageBusFiles(commandLine, fileSystem), commandLine, timeProvider, events, faults, cancellationToken);
        if (messageBus is null)
        {
            return (null, messageBusFailure);
        }

        var (mailStore, mailStoreFailure) = await LoadMailStoreAsync(
            ComposeMailStoreFiles(commandLine, fileSystem), commandLine, accountNames, timeProvider, events, faults, cancellationToken, mailDomains);
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
        if (ldapServer is null)
        {
            return (null, ldapFailure);
        }

        var (index, indexFailure) = await LoadObjectMetadataIndexAsync(
            ComposeObjectMetadataIndex(commandLine, fileSystem, events), cancellationToken);
        if (index is null)
        {
            return (null, indexFailure);
        }

        var (zones, zoneFailure) = await LoadDnsZonesAsync(ComposeDnsZoneFolder(commandLine, fileSystem), cancellationToken);
        return zones is null
            ? (null, zoneFailure)
            : (new ServiceState(messageBus, mailStore, ldapServer, index) { DnsZones = zones }, null);
    }

    /// <summary>
    /// Chooses the folder the DNS server's zones are read from (ADR-0092 decision 1):
    /// <c>&lt;data directory's full path&gt;/.surl/dns</c>, read through <paramref name="fileSystem"/>,
    /// with <c>--directory</c>; none without it, so no zone is served.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="fileSystem">The file system the content store serves.</param>
    /// <returns>The folder, or <see langword="null"/> without a data directory.</returns>
    public static DnsZoneFolder? ComposeDnsZoneFolder(SurlCommandLine commandLine, IContentFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        return commandLine.DataDirectory is { } dataDirectory
            ? new DnsZoneFolder(fileSystem, Path.Join(Path.GetFullPath(dataDirectory), ".surl", "dns"))
            : null;
    }

    /// <summary>
    /// Loads the DNS server's zones from <paramref name="folder"/>, or none when there is no folder;
    /// a zone file that cannot be loaded gives ADR-0092 decision 1's <c>(37)</c> message instead.
    /// </summary>
    /// <param name="folder">The zones' folder, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The zones, or the message after the <c>surl: </c> prefix when a file cannot be loaded.</returns>
    public static async Task<(DnsZoneSet? Zones, string? FailureMessage)> LoadDnsZonesAsync(
        DnsZoneFolder? folder, CancellationToken cancellationToken)
    {
        if (folder is null)
        {
            return (new DnsZoneSet(), null);
        }

        try
        {
            return (await folder.LoadAsync(new DnsZoneSet(), cancellationToken), null);
        }
        catch (DnsZoneLoadException failure)
        {
            return (null, $"(37) Could not read {failure.FilePath}: {failure.Message}");
        }
    }

    /// <summary>
    /// Builds the one object metadata index of the content store's served root (ADR-0100, decision
    /// 2.3): kept in <c>&lt;data directory's full path&gt;/.surl/objects/index</c>, read and written
    /// through <paramref name="fileSystem"/>, with <c>--directory</c>; held in memory only without it.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="fileSystem">The file system the content store serves.</param>
    /// <returns>The index, not yet loaded.</returns>
    /// <param name="events">The instance's event stream, which the index publishes each stored metadata on; none when <see langword="null"/>.</param>
    public static ObjectMetadataIndex ComposeObjectMetadataIndex(
        SurlCommandLine commandLine, IContentFileSystem fileSystem, ISurlEventPublisher? events = null)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        return new ObjectMetadataIndex(
            fileSystem, ComposeServedRoot(commandLine), persists: commandLine.DataDirectory is not null, events: events);
    }

    /// <summary>
    /// Loads <paramref name="index"/> from its file, when it has one. A file that cannot be read or
    /// does not parse gives <c>(37) Could not read &lt;file&gt;: &lt;reason&gt;</c> instead, as the
    /// other service state does.
    /// </summary>
    /// <param name="index">The index <see cref="ComposeObjectMetadataIndex"/> built.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The index, or the message after the <c>surl: </c> prefix when it cannot be loaded.</returns>
    public static async Task<(ObjectMetadataIndex? Index, string? FailureMessage)> LoadObjectMetadataIndexAsync(
        ObjectMetadataIndex index, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(index);
        try
        {
            await index.LoadAsync(cancellationToken);
            return (index, null);
        }
        catch (ObjectMetadataIndexLoadException failure)
        {
            return (null, $"(37) Could not read {failure.FilePath}: {failure.Message}");
        }
    }

    /// <summary>
    /// Chooses where the one message bus persists: its <see cref="MessageBusFiles"/> over
    /// <c>&lt;data directory's full path&gt;/.surl</c> (the bus's own files in <c>.surl/bus</c>, and
    /// an old MQTT 3.1.1 <c>.surl/mqtt/retained-messages</c> read once), read and written through
    /// <paramref name="fileSystem"/>, with <c>--directory</c>; none without it, so the bus lives in
    /// memory only (ADR-0100, decision 3.7).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="fileSystem">The file system the content store serves.</param>
    /// <returns>The files, or <see langword="null"/> without <c>--directory</c>.</returns>
    public static MessageBusFiles? ComposeMessageBusFiles(SurlCommandLine commandLine, IContentFileSystem fileSystem) =>
        commandLine.DataDirectory is { } dataDirectory
            ? new MessageBusFiles(fileSystem, Path.Join(Path.GetFullPath(dataDirectory), ".surl"))
            : null;

    /// <summary>
    /// Builds the one message bus the messaging servers share - the MQTT server keeps its retained
    /// messages on it - with one message bounded by <c>--max-filesize</c> (no bound when it is 0):
    /// loaded from <paramref name="files"/> when there are some, empty and in memory only when not.
    /// Files that cannot be loaded give <c>(37) Could not read &lt;file&gt;: &lt;reason&gt;</c>
    /// instead (ADR-0100, decision 3.7). A failure to write what the load changed (an old MQTT
    /// file's messages copied into <c>.surl/bus/retained</c>) ends nothing: the bus keeps the
    /// change and its next save writes it.
    /// </summary>
    /// <param name="files">The bus's files, or <see langword="null"/> for none.</param>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="timeProvider">The clock the bus stamps and expires messages by.</param>
    /// <param name="events">The instance's event stream, which the bus publishes its changes on.</param>
    /// <param name="faults">The instance's fault plan, whose out-of-space rules the bus applies.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bus, or the message after the <c>surl: </c> prefix when it cannot be loaded.</returns>
    public static async Task<(MessageBus.MessageBus? MessageBus, string? FailureMessage)> LoadMessageBusAsync(
        MessageBusFiles? files,
        SurlCommandLine commandLine,
        TimeProvider timeProvider,
        ISurlEventPublisher events,
        IFaultPlan faults,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        var maxUploadBytes = commandLine.Limits.MaxUploadBytes;
        var bus = new MessageBus.MessageBus(
            timeProvider, maxMessageBytes: maxUploadBytes > 0 ? maxUploadBytes : long.MaxValue, events: events, faults: faults);
        if (files is null)
        {
            return (bus, null);
        }

        try
        {
            await bus.LoadAsync(files, cancellationToken);
            return (bus, null);
        }
        catch (MessageBusLoadException failure)
        {
            return (null, $"(37) Could not read {failure.FilePath}: {failure.Message}");
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
    public static Task<(MailboxStore? MailStore, string? FailureMessage)> LoadMailStoreAsync(
        MailStoreFiles? files,
        SurlCommandLine commandLine,
        IReadOnlyList<string> accountNames,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        LoadMailStoreAsync(
            files, commandLine, accountNames, timeProvider, DiscardingSurlEventPublisher.Instance, EmptyFaultPlan.Instance, cancellationToken);

    /// <summary>
    /// Builds the one mail store the mail servers share, as
    /// <see cref="LoadMailStoreAsync(MailStoreFiles?, SurlCommandLine, IReadOnlyList{string}, TimeProvider, CancellationToken)"/>
    /// does, publishing each change on <paramref name="events"/> and applying
    /// <paramref name="faults"/>' out-of-space rules (ADR-0124, decisions 1 and 4).
    /// </summary>
    /// <param name="files">The mail store's files, or <see langword="null"/> for none.</param>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="accountNames">Every configured account's user name.</param>
    /// <param name="timeProvider">The clock for internal dates and <c>UIDVALIDITY</c>.</param>
    /// <param name="events">The instance's event stream.</param>
    /// <param name="faults">The instance's fault plan.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <param name="mailDomains">The mail domains and mailboxes recipients are resolved against (ADR-0102, decision 1); none without <c>--mail-domain</c>.</param>
    /// <returns>The store, or the message after the <c>surl: </c> prefix when it cannot be loaded.</returns>
    public static async Task<(MailboxStore? MailStore, string? FailureMessage)> LoadMailStoreAsync(
        MailStoreFiles? files,
        SurlCommandLine commandLine,
        IReadOnlyList<string> accountNames,
        TimeProvider timeProvider,
        ISurlEventPublisher events,
        IFaultPlan faults,
        CancellationToken cancellationToken,
        MailDomainConfiguration? mailDomains = null)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        var maxMessageBytes = commandLine.Limits.MaxUploadBytes;
        if (files is null)
        {
            return (new MailboxStore(
                accountNames, commandLine.AllowAnonymous, timeProvider, maxMessageBytes, mailDomains: mailDomains, events: events, faults: faults), null);
        }

        try
        {
            return (await MailboxStore.LoadAsync(
                files,
                accountNames,
                commandLine.AllowAnonymous,
                timeProvider,
                maxMessageBytes,
                mailDomains: mailDomains,
                events: events,
                faults: faults,
                cancellationToken: cancellationToken), null);
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
            return (new LdapProtocolServer(authenticationPolicy, authenticationPolicy, timeProvider, isTlsUpgradeAvailable), null);
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
    /// registration order: <see cref="ProtocolServerRegistrations.Original"/>, each entry built
    /// over the services given (ADR-0124, decision 2). The SOCKS server it builds reaches no origin: this overload composes no outbound connector.
    /// </summary>
    /// <param name="contentStore">The one content store.</param>
    /// <param name="serviceState">The message bus, the mail store and the LDAP server.</param>
    /// <param name="authenticationPolicy">The one policy every login is judged by.</param>
    /// <param name="sshHostKeys">The SSH server's host keys.</param>
    /// <param name="sshAlgorithms">The SSH algorithms offered.</param>
    /// <param name="isTlsUpgradeAvailable">Whether a certificate is configured, so a TLS upgrade can be offered.</param>
    /// <param name="echoesWebSocketMessages">Whether <c>--ws-echo</c> is given.</param>
    /// <param name="advertisesHttp3">Whether <c>--http3</c> is given, so the HTTP server adds <c>Alt-Svc</c> on https (ADR-0082, decision 6).</param>
    /// <param name="timeProvider">The one clock; <see cref="TimeProvider.System"/> when <see langword="null"/>.</param>
    /// <param name="authenticationPolicyForScheme">
    /// The policy each server with a login judges it by, given the scheme it answers (the plaintext
    /// one, which admits its TLS twin), over the accounts allowed on that scheme (ADR-0102, decision
    /// 4.3; <see cref="SchemeAuthenticationPolicies.For"/>); <paramref name="authenticationPolicy"/>
    /// for every scheme when <see langword="null"/>.
    /// </param>
    /// <returns>The servers.</returns>
    public static IProtocolServer[] ComposeProtocolServers(
        ContentStore contentStore,
        ServiceState serviceState,
        AuthenticationPolicy authenticationPolicy,
        SshHostKeySet sshHostKeys,
        SshAlgorithmOffer sshAlgorithms,
        bool isTlsUpgradeAvailable,
        bool echoesWebSocketMessages,
        bool advertisesHttp3 = false,
        TimeProvider? timeProvider = null,
        Func<string, AuthenticationPolicy>? authenticationPolicyForScheme = null)
    {
        ArgumentNullException.ThrowIfNull(serviceState);
        return ComposeProtocolServers(
            ProtocolServerRegistrations.Original,
            new ComposedProtocolServices(
                contentStore,
                serviceState,
                authenticationPolicy,
                timeProvider ?? TimeProvider.System,
                sshHostKeys,
                sshAlgorithms,
                isTlsUpgradeAvailable,
                echoesWebSocketMessages,
                advertisesHttp3)
            {
                AuthenticationPolicyForScheme = authenticationPolicyForScheme,
                OriginConnector = new InMemoryOriginConnector(),
            });
    }

    /// <summary>
    /// Builds the server of every entry of <paramref name="registrations"/>, in its order, over
    /// <paramref name="services"/>; each entry may share a server an earlier entry over the same
    /// services built, through <see cref="ComposedProtocolServices.ComposedServerFor"/> (ADR-0124,
    /// decision 2).
    /// </summary>
    /// <param name="registrations">The ordered registration list.</param>
    /// <param name="services">What the servers are built from.</param>
    /// <returns>The servers, one per entry.</returns>
    public static IProtocolServer[] ComposeProtocolServers(
        IReadOnlyList<ProtocolServerRegistration> registrations, ComposedProtocolServices services)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(services);
        var servers = new IProtocolServer[registrations.Count];
        for (var index = 0; index < servers.Length; index++)
        {
            var registration = registrations[index];
            servers[index] = registration.Create(services);
            services.RecordComposed(registration, servers[index]);
        }

        return servers;
    }

    /// <summary>
    /// Builds the servers only to ask for their schemes: nothing is served through them, so they
    /// need no persisted bus, no mail, no directory, no accounts and no host keys.
    /// </summary>
    /// <param name="contentStore">The content store they are built over.</param>
    /// <param name="timeProvider">The clock they run on.</param>
    /// <returns>The servers, in registration order.</returns>
    public static IProtocolServer[] ComposeUnservedProtocolServers(ContentStore contentStore, TimeProvider timeProvider) =>
        ComposeProtocolServers(ProtocolServerRegistrations.Original, ComposeUnservedProtocolServices(contentStore, timeProvider));

    /// <summary>
    /// Composes the services servers are built over only to ask for their schemes: an empty
    /// in-memory bus, mail store and directory, a policy with no accounts, no host keys and no
    /// certificate.
    /// </summary>
    /// <param name="contentStore">The content store they are built over.</param>
    /// <param name="timeProvider">The clock they run on.</param>
    /// <returns>The services.</returns>
    public static ComposedProtocolServices ComposeUnservedProtocolServices(ContentStore contentStore, TimeProvider timeProvider)
    {
        var authenticationPolicy = ComposePolicyWithoutAccounts(timeProvider);
        return new ComposedProtocolServices(
            contentStore,
            new ServiceState(
                new MessageBus.MessageBus(timeProvider),
                new MailboxStore([], allowAnonymous: false, timeProvider),
                new LdapProtocolServer(authenticationPolicy, authenticationPolicy, timeProvider),
                new ObjectMetadataIndex(new InMemoryContentFileSystem(timeProvider), contentStore.ServedRoot, persists: false)),
            authenticationPolicy,
            timeProvider,
            new SshHostKeySet(),
            SshAlgorithmOffer.Default([], AesGcm.IsSupported),
            isTlsUpgradeAvailable: false,
            echoesWebSocketMessages: false,
            advertisesHttp3: false)
        {
            // Nothing is served through these services, so the SOCKS server is built over a connector that connects nowhere.
            OriginConnector = new InMemoryOriginConnector(),
        };
    }
}
