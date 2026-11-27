using System.Net;
using System.Security.Cryptography.X509Certificates;
using Surl.Cli;
using Surl.Content;
using Surl.Core;
using Surl.Extended.Protocol.Amqp0;
using Surl.Extended.Protocol.AzureStorage;
using Surl.Extended.Protocol.DnsQuery;
using Surl.Extended.Protocol.GoogleCloudStorage;
using Surl.Extended.Protocol.Kafka;
using Surl.Extended.Protocol.McpServer;
using Surl.Extended.Protocol.Mqtt5;
using Surl.Extended.Protocol.OciRegistry;
using Surl.Extended.Protocol.Redis;
using Surl.Extended.Protocol.S3;
using Surl.Kerberos;
using Surl.MessageBus;
using Surl.Networking;
using Surl.Output;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Http;

namespace Surl.Composition;

/// <summary>
/// The ordered start (ADR-0099, decision 2): the option refusals, the data directory, the content
/// store, the listen-URL refusals, authentication and host keys, the lock, the service state and
/// server TLS, each refusal returned rather than written.
/// </summary>
public static partial class SurlServerComposition
{
    /// <summary>
    /// Runs the ordered start of <paramref name="commandLine"/> up to and including server TLS and
    /// the service state, opening no log or trace file: <c>--auth gssapi</c> without
    /// <c>--keytab</c> and an <c>--ssh-ciphers</c> or <c>--ssh-macs</c> name surl cannot offer (2),
    /// a data directory that cannot be opened (37), a TLS-first listen URL with no certificate (58)
    /// or an <c>scp</c> or <c>sftp</c> one with no host key (2), the start files read before the
    /// lock (37, 2, or 77 for the <c>--user-ldap</c> <c>ca-file</c>), the shares and
    /// buckets (2, or 37 for a share or bucket directory that cannot be opened), the data
    /// directory's lock (124 or 23), the service state (37, or 2) and the TLS files (58, 2 or 77,
    /// or 37 for a TLS-SRP file), in that order.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="seams">What the host hands the composition.</param>
    /// <param name="cancellationToken">
    /// Cancels the start; a start cancelled while loading the service state is refused with
    /// <see cref="SurlExitCode.Ok"/> and no lines, the same clean stop as one while serving.
    /// </param>
    /// <returns>The refusal, or the composed serving, which holds the lock until disposed.</returns>
    public static async Task<SurlServerStart> ComposeAsync(
        SurlCommandLine commandLine, SurlServerCompositionSeams seams, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(seams);

        // Every --proxy listen URL is listened on and checked like any other (a certificate for an https
        // one, ADR-0088 decision 6); its IsProxy mark sends its exchanges to the proxy server.
        commandLine = commandLine with { ListenUrls = [.. commandLine.ListenUrls, .. commandLine.ProxyListenUrls] };

        // --s3-plaintext marks every s3 listen URL plain, so it needs no certificate and the engine
        // serves it without a handshake (ADR-0108 decision 5).
        commandLine = S3PlaintextComposition.MarkS3ListenUrlsPlain(commandLine);

        // --oci-plaintext does the same for every oci listen URL (ADR-0115 decision 6).
        commandLine = OciPlaintextComposition.MarkOciListenUrlsPlain(commandLine);

        // --az-plaintext does the same for every az listen URL (ADR-0109 decision 6).
        commandLine = AzPlaintextComposition.MarkAzListenUrlsPlain(commandLine);

        // --gs-plaintext does the same for every gs listen URL (ADR-0110 decision 5).
        commandLine = GsPlaintextComposition.MarkGsListenUrlsPlain(commandLine);

        if ((FindOptionRefusal(commandLine) ?? DnsTransportComposition.FindRefusal(commandLine)) is { } optionRefusal)
        {
            return Refuse(SurlExitCode.FailedInit, optionRefusal);
        }

        // --dns-transport turns each dns listen URL into one per transport (ADR-0092 decision 5).
        commandLine = DnsTransportComposition.ExpandDnsListenUrls(commandLine);

        if (commandLine.DataDirectory is { } dataDirectory && !seams.CanOpenDataDirectory(dataDirectory))
        {
            return Refuse(SurlExitCode.CouldNotReadFile, $"(37) Could not open directory {dataDirectory}");
        }

        return await ComposeAvailableAsync(commandLine, seams, cancellationToken);
    }

    /// <summary>
    /// Formats the <c>(45)</c> or <c>(6)</c> message for a listener that could not bind, after
    /// the <c>surl: </c> prefix (ADR-0007 section 5).
    /// </summary>
    /// <param name="bindFailure">The failure the listener factory threw.</param>
    /// <returns>The message.</returns>
    public static string FormatBindFailure(ListenerBindException bindFailure)
    {
        ArgumentNullException.ThrowIfNull(bindFailure);
        var listenUrl = bindFailure.ListenUrl;
        if (bindFailure.Failure == ListenerBindFailure.HostNotFound)
        {
            return $"(6) Could not resolve host: {listenUrl.Host}";
        }

        if (listenUrl.UnixSocket is { } unixSocket)
        {
            var unixMessage = FormatUnixSocketBindFailure(bindFailure.Failure, unixSocket);
            if (unixMessage is not null)
            {
                return unixMessage;
            }
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
    public static string FormatMissingCertificate(ListenUrl listenUrl)
    {
        ArgumentNullException.ThrowIfNull(listenUrl);
        var url = ListenerStatusLine.FormatBoundListenUrl(listenUrl with { BoundPort = listenUrl.Port });
        if (string.Equals(listenUrl.Scheme, "coaps", StringComparison.Ordinal))
        {
            return $"(58) {url} needs a certificate or an account: "
                + "give --cert <file>, --self-signed for a throwaway one, or --user <name:key> for a pre-shared key";
        }

        return $"(58) {url} needs a certificate: "
            + "give --cert <file>, or --self-signed for a throwaway one";
    }

    // ADR-0094, decisions 3 and 5: the three refusals made before a Unix socket is bound.
    private static string? FormatUnixSocketBindFailure(ListenerBindFailure failure, UnixSocketAddress unixSocket) => failure switch
    {
        ListenerBindFailure.UnixSocketPathTooLong =>
            $"(45) Unix socket path too long: '{unixSocket.Name}' is {System.Text.Encoding.UTF8.GetByteCount(unixSocket.Name)} bytes"
            + $" and this platform allows {UnixSocketBindRules.MaximumPathBytes(OperatingSystem.IsMacOS())}",
        ListenerBindFailure.AddressInUse => $"(45) Could not bind {unixSocket.Name}: the path exists already",
        ListenerBindFailure.AbstractUnixSocketUnsupported => "(45) Abstract Unix socket names exist only on Linux",
        _ => null,
    };

    private static string DescribeBindFailure(ListenerBindFailure failure) => failure switch
    {
        ListenerBindFailure.AddressInUse => "Address already in use",
        ListenerBindFailure.AddressNotAvailable => "Address not available",
        ListenerBindFailure.PermissionDenied => "Permission denied",
        _ => "Bind failed",
    };

    private static SurlServerStart Refuse(SurlExitCode exitCode, params string[] lines) =>
        new(new SurlStartFailure(exitCode, lines), null);

    // --auth gssapi without --keytab (ADR-0057, decision 1), then an --ssh-ciphers or --ssh-macs
    // name surl cannot offer (ADR-0066): the first refusal of the options' consistency.
    private static string? FindOptionRefusal(SurlCommandLine commandLine) =>
        KeytabComposition.IsGssapiWithoutKeytab(commandLine)
            ? "(2) --auth gssapi needs --keytab"
            : SshAlgorithmComposition.FindRefusal(commandLine);

    // A listen URL TLS from the first byte with no certificate to serve (ADR-0032, section 10),
    // then an scp or sftp one with no host key to serve (ADR-0051, decision 4): each refused
    // before any listener binds. A scheme no server answers never gets here: every scheme
    // ListenUrlParser accepts is registered, and it refuses every other one with (1).
    private static SurlServerStart? FindListenUrlRefusal(SurlCommandLine commandLine)
    {
        if (ServerTlsComposition.FindListenUrlWithoutCertificate(commandLine) is { } uncertified)
        {
            return Refuse(SurlExitCode.CertificateProblem, FormatMissingCertificate(uncertified));
        }

        return SshHostKeyComposition.FindListenUrlWithoutHostKey(commandLine) is { } keyless
            ? Refuse(SurlExitCode.FailedInit, SshHostKeyComposition.FormatMissingHostKey(keyless))
            : null;
    }

    private static async Task<SurlServerStart> ComposeAvailableAsync(
        SurlCommandLine commandLine, SurlServerCompositionSeams seams, CancellationToken cancellationToken)
    {
        var fileSystem = ComposeContentFileSystem(
            commandLine,
            seams.TimeProvider,
            seams.DataDirectoryFileSystem ?? new DiskContentFileSystem(),
            seams.ContentWithoutDataDirectory);
        // One event stream and one fault plan per instance, never static (ADR-0124, decisions 1 and 4).
        var instruments = new EventStreamAndFaultPlan(
            new SurlEventStream(seams.TimeProvider), FaultPlanComposition.Compose(commandLine.Faults));
        if (FindListenUrlRefusal(commandLine) is { } refusal)
        {
            return refusal;
        }

        var (authentication, failure) = ComposeAuthentication(commandLine, seams, instruments);
        if (authentication is null)
        {
            return new SurlServerStart(failure, null);
        }

        // A protocols= list naming a scheme whose server makes no login (ADR-0102 decision 4.3, BL-1483).
        if (SchemeLoginCheck.FindRefusal(
                authentication.Mailboxes.MailboxProtocols,
                commandLine,
                AllRegistrations()) is { } noLogin)
        {
            return Refuse(SurlExitCode.FailedInit, noLogin);
        }

        // The shares, their access and the buckets, once the groups are expanded (ADR-0102 decision 5).
        var (shares, buckets, shareExitCode, shareFailureMessage) = ContentShareComposition.Compose(
            commandLine,
            authentication.AccountGroups,
            seams.CanOpenShareDirectory ?? DataDirectoryProbe.CanOpen,
            seams.ShareFileSystem ?? new DiskContentFileSystem());
        if (shares is null)
        {
            return Refuse(shareExitCode, shareFailureMessage!);
        }

        // The topics, queues, topic maps and topic access, once the groups are expanded (ADR-0102 decision 5).
        var (busConfiguration, busFailureMessage) = MessageBusComposition.Compose(commandLine, authentication.AccountGroups);
        if (busConfiguration is null)
        {
            return Refuse(SurlExitCode.FailedInit, busFailureMessage!);
        }

        var contentStore = ComposeContentStore(commandLine, fileSystem, instruments.Events, instruments.Faults, shares, buckets!, seams.TimeProvider);
        if (ContentShareComposition.PrepareBucketFolders(commandLine, contentStore, fileSystem) is { } bucketFailure)
        {
            return Refuse(SurlExitCode.CouldNotReadFile, bucketFailure);
        }

        return await LockThenComposeAsync(
            commandLine, seams, fileSystem, contentStore, authentication, busConfiguration, instruments, cancellationToken);
    }

    private static ProtocolServerRegistration[] AllRegistrations() =>
        [.. ProtocolServerRegistrations.Original, .. ExtendedProtocolServers.Registrations];

    // The --auth words, the --user-file, the --authorized-keys and --keytab files, then the --hostkey
    // and --hostcert files, are checked before the lock is taken and any listener binds (ADR-0032, sections 1 and 2;
    // ADR-0051, decisions 4 and 6; ADR-0057, decision 1).
    private static (ComposedAuthentication? Authentication, SurlStartFailure? Failure) ComposeAuthentication(
        SurlCommandLine commandLine, SurlServerCompositionSeams seams, EventStreamAndFaultPlan instruments)
    {
        var readStartFile = seams.ReadStartFile ?? File.ReadAllBytes;

        // The --mailbox-files, read before the accounts their mailboxes join (ADR-0102 decision 5); the
        // extended schemes their protocols= may name are listed only when there is a file to read.
        var (mailboxes, mailboxExitCode, mailboxFailureMessage) = MailboxComposition.Compose(
            commandLine,
            readStartFile,
            commandLine.MailboxFiles.Count == 0
                ? []
                : ComposeExtendedSchemes(seams.AreExtensionsTurnedOn, seams.ComposeExtendedProtocolServers, seams.TimeProvider),
            SchemeLoginCheck.SchemesWithoutLogin(AllRegistrations()));
        if (mailboxes is null)
        {
            return (null, new SurlStartFailure(mailboxExitCode, [mailboxFailureMessage!]));
        }

        var directoryNotes = new DirectoryNoteSink();
        var (policy, accountNames, skippedKeytabEntries, exitCode, failureMessage) = AuthenticationComposition.Compose(
            commandLine, readStartFile, seams.TimeProvider, instruments.Events, instruments.Faults, mailboxes.Accounts,
            seams.LdapDirectoryConnector, directoryNotes.Write);
        if (policy is null)
        {
            return (null, new SurlStartFailure(exitCode, [failureMessage!]));
        }

        // The groups, expanded over every account once --user-file is read (ADR-0102 decisions 4.1 and 5).
        var (accountGroups, groupFailureMessage) = AccountGroupExpansion.Compose(
            commandLine.Groups, accountNames, takesUnknownNamesAsDirectoryAccounts: commandLine.UserLdap is not null);
        if (accountGroups is null)
        {
            return (null, new SurlStartFailure(SurlExitCode.FailedInit, [groupFailureMessage!]));
        }

        // The shared folders' members, expanded now the groups are (ADR-0102 decision 1.6).
        if (mailboxes.ComposeSharedFolders(commandLine, accountGroups) is { } sharedFolderFailure)
        {
            return (null, new SurlStartFailure(SurlExitCode.FailedInit, [sharedFolderFailure]));
        }

        // The proxy's own accounts, never the origin's (ADR-0088 decision 5).
        var (proxyPolicy, proxyExitCode, proxyFailureMessage) = AuthenticationComposition.ComposeProxyPolicy(
            commandLine, readStartFile, seams.TimeProvider, instruments.Events, instruments.Faults);
        if (proxyPolicy is null)
        {
            return (null, new SurlStartFailure(proxyExitCode, [proxyFailureMessage!]));
        }

        var (sshHostKeys, hostKeyExitCode, hostKeyFailureMessage) = SshHostKeyComposition.Compose(commandLine, readStartFile);
        if (sshHostKeys is null)
        {
            return (null, new SurlStartFailure(hostKeyExitCode, [hostKeyFailureMessage!]));
        }

        // The control token file, the last start file read before the data-directory lock (ADR-0124 decision 7).
        // After the lock, ComposeServing reads the server TLS files.
        var (controlToken, controlExitCode, controlFailureMessage) = ControlComposition.ReadToken(commandLine, readStartFile);
        return controlFailureMessage is not null
            ? (null, new SurlStartFailure(controlExitCode, [controlFailureMessage]))
            : (new ComposedAuthentication(
                policy,
                accountNames,
                accountGroups,
                skippedKeytabEntries,
                sshHostKeys,
                mailboxes,
                new SchemeAuthenticationPolicies(
                    policy, seams.TimeProvider, instruments.Events, instruments.Faults, commandLine.GivenAuthenticationMethods is not null))
            {
                ProxyPolicy = proxyPolicy,
                DirectoryNotes = directoryNotes,
                ControlToken = controlToken,
            }, null);
    }

    private static async Task<SurlServerStart> LockThenComposeAsync(
        SurlCommandLine commandLine,
        SurlServerCompositionSeams seams,
        IContentFileSystem fileSystem,
        ContentStore contentStore,
        ComposedAuthentication authentication,
        MessageBusConfiguration busConfiguration,
        EventStreamAndFaultPlan instruments,
        CancellationToken cancellationToken)
    {
        var dataDirectoryLock = TakeEveryLock(commandLine, seams);
        if (dataDirectoryLock.FailureMessage is { } lockFailure)
        {
            return Refuse(dataDirectoryLock.ExitCode, lockFailure);
        }

        // The lock is handed to the composed serving, or released when the start ends any other way.
        SurlServerStart? start = null;
        try
        {
            start = await ComposeUnderTheLockAsync(
                commandLine, seams, fileSystem, contentStore, authentication, busConfiguration, instruments, dataDirectoryLock.Holder, cancellationToken);
            return start;
        }
        finally
        {
            if (start?.Serving is null)
            {
                dataDirectoryLock.Holder?.Dispose();
            }
        }
    }

    // The --directory lock, then the lock of every share that is not read-only, in command-line order (ADR-0031 decision 7, BL-1380).
    // The first refusal releases every lock taken before it; otherwise one holder releases them all.
    private static DataDirectoryLockOutcome TakeEveryLock(
        SurlCommandLine commandLine, SurlServerCompositionSeams seams)
    {
        var paths = new List<string>();
        if (commandLine.DataDirectory is { } dataDirectory)
        {
            paths.Add(dataDirectory);
        }

        paths.AddRange(commandLine.Shares.Where(share => !share.IsReadOnly).Select(share => share.Path));
        if (paths.Count == 0)
        {
            return DataDirectoryLockOutcome.NoLock;
        }

        var held = new List<IDisposable>();
        foreach (var path in paths)
        {
            var outcome = seams.TakeDataDirectoryLock(path);
            if (outcome.Holder is null)
            {
                ReleaseAll(held);
                return outcome;
            }

            held.Add(outcome.Holder);
        }

        return DataDirectoryLockOutcome.Taken(new HeldLocks(held));
    }

    private static void ReleaseAll(List<IDisposable> held)
    {
        foreach (var holder in held)
        {
            holder.Dispose();
        }
    }

    private sealed class HeldLocks(List<IDisposable> held) : IDisposable
    {
        public void Dispose() => ReleaseAll(held);
    }

    // The SOCKS5 server's GSS-API login (BL-1066): over the authentication policy's --keytab acceptor, so the
    // keytab is read once and the instance has one replay cache (ADR-0057 amendment 5), and the service name
    // rcmd that curl's --socks5-gssapi-service defaults to. None without a keytab.
    private static Surl.Protocol.Socks.SocksGssApiLogin? ComposeSocksGssApiLogin(KerberosAcceptor? acceptor) =>
        acceptor is null ? null : new Surl.Protocol.Socks.SocksGssApiLogin(acceptor, Surl.Protocol.Socks.SocksGssApiLogin.DefaultService);

    private static async Task<SurlServerStart> ComposeUnderTheLockAsync(
        SurlCommandLine commandLine,
        SurlServerCompositionSeams seams,
        IContentFileSystem fileSystem,
        ContentStore contentStore,
        ComposedAuthentication authentication,
        MessageBusConfiguration busConfiguration,
        EventStreamAndFaultPlan instruments,
        IDisposable? dataDirectoryLock,
        CancellationToken cancellationToken)
    {
        var isTlsUpgradeAvailable = ServerTlsComposition.IsTlsUpgradeAvailable(commandLine);
        (ServiceState? State, string? FailureMessage) loaded;
        try
        {
            loaded = await LoadServiceStateAsync(
                commandLine,
                fileSystem,
                authentication.SchemePolicies.For("ldap"),
                authentication.AccountNames,
                isTlsUpgradeAvailable,
                seams.TimeProvider,
                instruments.Events,
                instruments.Faults,
                cancellationToken,
                authentication.Mailboxes.MailDomains,
                busConfiguration);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stopped before serving began: the same clean stop as one while serving.
            return Refuse(SurlExitCode.Ok);
        }

        if (loaded.State is null)
        {
            // A route the mail store refuses is a mistake in the options (2), not a file it could not read (37).
            return Refuse(
                loaded.FailureMessage!.StartsWith("(2)", StringComparison.Ordinal) ? SurlExitCode.FailedInit : SurlExitCode.CouldNotReadFile,
                loaded.FailureMessage);
        }

        var boundProxyEndPoints = new BoundProxyEndPoints();
        var originConnector = ComposeOriginConnector(commandLine, seams, boundProxyEndPoints);
        var services = new ComposedProtocolServices(
            contentStore,
            loaded.State,
            authentication.Policy,
            seams.TimeProvider,
            authentication.SshHostKeys.HostKeys,
            SshAlgorithmComposition.Compose(authentication.SshHostKeys.HostKeys.SignatureAlgorithms, commandLine),
            isTlsUpgradeAvailable,
            commandLine.WsEcho,
            commandLine.Http3)
        {
            Events = instruments.Events,
            Faults = instruments.Faults,
            AuthenticationPolicyForScheme = authentication.SchemePolicies.For,
            IdleTimeout = ComposeConnectionLimits(commandLine).IdleTimeout,
            AllowsZoneTransfer = commandLine.AllowZoneTransfer,
            MqttHoldSubscriptionCount = commandLine.MqttHoldSubscriptionCount,
            Mqtt5DisconnectsAfterRetained = commandLine.Mqtt5DisconnectAfterRetained,
            Cookies = commandLine.Cookies,
            KafkaFetchCompression = Enum.Parse<KafkaCompressionCodec>(commandLine.KafkaFetchCompression, ignoreCase: true),
            OriginConnector = originConnector,
            ProxyAuthenticationPolicy = authentication.ProxyPolicy,
            SocksGssApiLogin = ComposeSocksGssApiLogin(authentication.Policy.Settings.KerberosAcceptor),
            MailDomains = commandLine.MailDomains,
            IdentifiersForServer = commandLine.RandomSeed is { } seed
                ? new SeededIdentifierRandom(seed).ForServer
                : _ => CryptographicIdentifierRandom.Instance,
        };
        IProtocolServer[] servers =
        [
            .. ProxyComposition.RouteProxyListeners(
                ComposeProtocolServers(ProtocolServerRegistrations.Original, services), commandLine, originConnector, authentication.ProxyPolicy),
            .. ComposeExtendedProtocolServers(
                seams.AreExtensionsTurnedOn,
                seams.ComposeExtendedProtocolServers,
                new ExtendedProtocolServerServices(services)),
            .. ComposeProtocolServers([.. seams.AdditionalProtocolServers.Select(additional => additional.Registration)], services),
        ];
        if (await LoadAmqp0ExchangesAsync(servers, commandLine, fileSystem, cancellationToken) is { } exchangeFailure)
        {
            return Refuse(SurlExitCode.CouldNotReadFile, exchangeFailure);
        }

        var served = commandLine with
        {
            ListenUrls = [.. commandLine.ListenUrls, .. seams.AdditionalProtocolServers.SelectMany(additional => additional.ListenUrls)],
        };
        var stores = ComposeStores(served, servers, loaded.State, contentStore, authentication, services, instruments);
        return ComposeServing(served, seams, servers, stores, authentication, instruments, services.QuicCertificate, dataDirectoryLock, boundProxyEndPoints);
    }


    // Every store a test seeds, resets, snapshots and restores, behind one surface (ADR-0124, decision 3). The keyspace, the
    // zones and the directory exist only when a listen URL serves them (ADR-0158 Amendment 1, BL-1410).
    private static ComposedStores ComposeStores(
        SurlCommandLine served,
        IProtocolServer[] servers,
        ServiceState state,
        ContentStore contentStore,
        ComposedAuthentication authentication,
        ComposedProtocolServices services,
        EventStreamAndFaultPlan instruments)
    {
        var redis = ControlledRedisKeyspace(servers, ServesScheme(served, "redis", "rediss"), services);
        var dns = ControlledDnsZones(servers, ServesScheme(served, "dns"), state.DnsZones);
        var ldap = ServesScheme(served, "ldap", "ldaps") ? state.LdapServer : null;
        return new ComposedStores(
            contentStore,
            state.ObjectMetadataIndex,
            state.MailStore,
            state.MessageBus,
            authentication.Policy.Settings.Accounts,
            additionalRegistrations: [.. redis, .. (ldap is null ? new List<ComposedStoreRegistration>() : [LdapDirectoryStoreRegistration.For(ldap)]), .. ControlledBrokerAndSessionStores(servers, instruments.Events), .. dns, .. ControlledUploads(servers, contentStore, instruments.Events)],
            ldapServer: ldap,
            redisKeyspace: redis.Count > 0 ? services.RedisKeyspace : null,
            dnsZones: dns.Count > 0 ? state.DnsZones : null,
            proxyAccounts: (authentication.ProxyPolicy as Surl.Authentication.AuthenticationPolicy)?.Settings.Accounts,
            events: instruments.Events);
    }
    // The Redis keyspace joins the order after the accounts when a redis server is composed and a redis or rediss
    // listen URL is served (ADR-0158 decision 5); with SURL_EXTENSIONS=0 there is no server and so no store.
    private static List<ComposedStoreRegistration> ControlledRedisKeyspace(IProtocolServer[] servers, bool isServed, ComposedProtocolServices services) =>
        isServed && servers.OfType<RedisServer>().Any() ? [RedisKeyspaceStoreRegistration.For(services.RedisKeyspace)] : [];

    // The DNS zones join the order after the broker and session stores (BL-1165: the exchanges already follow the directory) and before the uploads, when a dns server is composed and a dns listen URL is served (ADR-0158 decision 5).
    private static List<ComposedStoreRegistration> ControlledDnsZones(IProtocolServer[] servers, bool isServed, DnsZoneSet zones) =>
        isServed && IsDnsComposed(servers) ? [DnsZoneStoreRegistration.For(zones)] : [];

    // The open uploads the composed object servers hold (ADR-0167 decision 3), after the content their temporary files live in.
    private static List<ComposedStoreRegistration> ControlledUploads(IProtocolServer[] servers, ContentStore content, SurlEventStream events) =>
        [.. new[]
        {
            SameKindStoreRegistrations.Combine(
                StoreKind.Uploads,
                S3UploadStoreRegistration.For([.. servers.OfType<S3ProtocolServer>()], content, events),
                AzureStagedUploadStoreRegistration.For([.. servers.OfType<AzureStorageProtocolServer>()], content, events),
                GcsStagedUploadStoreRegistration.For([.. servers.OfType<GoogleCloudStorageProtocolServer>()], content, events),
                OciUploadStoreRegistration.For([.. servers.OfType<OciRegistryProtocolServer>()], content, events)),
        }.OfType<ComposedStoreRegistration>()];

    private static bool ServesScheme(SurlCommandLine commandLine, params string[] schemes) =>
        commandLine.ListenUrls.Any(url => schemes.Contains(url.Scheme));

    // ExtendedProtocolServers registers the datagram and the connection DNS server together under the one `dns` scheme, so
    // every composition that has a DNS server has the datagram one; testing for it alone is the same answer, reachable both ways.
    private static bool IsDnsComposed(IProtocolServer[] servers) =>
        servers.OfType<DnsDatagramRoutingProtocolServer>().Any();

    // The broker and session stores the composed servers hold beside the bus (ADR-0159 decision 3, ADR-0167), each only when
    // its server is composed: the AMQP 0-9-1 exchanges, the Kafka producers, then the one sessions entry (MQTT 5 sessions,
    // OCI tokens, MCP OAuth state and sessions, in that order).
    private static List<ComposedStoreRegistration> ControlledBrokerAndSessionStores(IProtocolServer[] servers, SurlEventStream events) =>
        [.. new[]
        {
            Amqp0ExchangeStoreRegistration.For([.. servers.OfType<Amqp0Broker>()], events),
            KafkaBrokerStoreRegistration.For([.. servers.OfType<KafkaBroker>()], events),
            SameKindStoreRegistrations.Combine(
                StoreKind.Sessions,
                Mqtt5SessionStoreRegistration.For([.. servers.OfType<Mqtt5Broker>()], events),
                OciTokenStoreRegistration.For([.. servers.OfType<OciRegistryProtocolServer>()], events),
                McpSessionStoreRegistration.For([.. servers.OfType<McpProtocolServer>()], events)),
        }.OfType<ComposedStoreRegistration>()];

    // How the proxies reach origins, over the rule --proxy-allow-origin and the proxy listen URLs make
    // (ADR-0088 decision 2); the host's seam when it gives one.
    private static IOriginConnector ComposeOriginConnector(
        SurlCommandLine commandLine, SurlServerCompositionSeams seams, BoundProxyEndPoints boundProxyEndPoints)
    {
        var rule = ProxyComposition.ComposeAllowedOriginRule(commandLine, boundProxyEndPoints);
        return seams.CreateOriginConnector is { } create ? create(rule) : new SocketOriginConnector(rule, seams.TimeProvider);
    }

    private static SurlServerStart ComposeServing(
        SurlCommandLine commandLine,
        SurlServerCompositionSeams seams,
        IProtocolServer[] servers,
        ComposedStores stores,
        ComposedAuthentication authentication,
        EventStreamAndFaultPlan instruments,
        QuicCertificateSource quicCertificateSource,
        IDisposable? dataDirectoryLock,
        BoundProxyEndPoints boundProxyEndPoints)
    {
        ServerTlsComposition tls;
        try
        {
            tls = ServerTlsComposition.Compose(commandLine, seams.TimeProvider, seams.ReadStartFile);
        }
        catch (TlsFileLoadException failure)
        {
            var (exitCode, lines) = ServerTlsComposition.DescribeTlsFileFailure(failure, commandLine.CaCertificateFile);
            return Refuse(exitCode, lines);
        }
        catch (TlsSrpAccountsException failure)
        {
            return Refuse(failure.ExitCode, failure.Message);
        }

        if (CoapsDtlsCertificate.FindRefusal(commandLine, tls.QuicCertificate) is { } certificateRefusal)
        {
            tls.Dispose();
            return Refuse(SurlExitCode.CertificateProblem, certificateRefusal);
        }

        if (tls.Settings?.TlsSrpAccounts is { } tlsSrpAccounts)
        {
            stores.JoinTlsSrpAccounts(
                tlsSrpAccounts,
                commandLine.RandomSeed is { } seed ? new SeededIdentifierRandom(seed).ForServer("tls-srp") : CryptographicIdentifierRandom.Instance);
        }

        quicCertificateSource.Certificate = tls.QuicCertificate;
        quicCertificateSource.ClientTrustAnchors = tls.ClientTrustAnchors;
        IProtocolServer[] served = commandLine.Http3 && tls.QuicCertificate is { } quicCertificate
            ? [.. servers, ComposeHttp3ProtocolServer(commandLine, stores.Content, authentication.SchemePolicies.For("https"), quicCertificate, tls.ClientTrustAnchors, seams.TimeProvider)]
            : servers;
        return new SurlServerStart(
            null, new ComposedServing(commandLine, seams, served, authentication, instruments, stores, tls, dataDirectoryLock, boundProxyEndPoints));
    }

    // --http3: the HTTP/3 server on every https listen URL's UDP side, sharing the scheme with the
    // HTTP server's https registration, so the engine binds both on one port (ADR-0082, decision 6).
    // With --cacert its handshakes require a client certificate chaining to those anchors (BL-741).
    private static Http3ProtocolServer ComposeHttp3ProtocolServer(
        SurlCommandLine commandLine,
        ContentStore contentStore,
        IAuthenticationPolicy policy,
        QuicServerCertificate quicCertificate,
        IReadOnlyList<X509Certificate2> clientTrustAnchors,
        TimeProvider timeProvider) =>
        new(
            contentStore,
            policy,
            quicCertificate.Chain,
            quicCertificate.Key,
            ComposeConnectionLimits(commandLine).IdleTimeout,
            clientTrustAnchors.Count == 0 ? null : new QuicClientCertificateTrust(clientTrustAnchors, timeProvider).Trusts,
            commandLine.Cookies);
}
