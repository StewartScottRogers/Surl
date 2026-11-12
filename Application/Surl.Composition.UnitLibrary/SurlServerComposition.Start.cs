using System.Net;
using System.Security.Cryptography.X509Certificates;
using Surl.Cli;
using Surl.Content;
using Surl.Core;
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
    /// the service state, opening no file for writing: <c>--auth gssapi</c> without
    /// <c>--keytab</c> and an <c>--ssh-ciphers</c> or <c>--ssh-macs</c> name surl cannot offer (2),
    /// a data directory that cannot be opened (37), a TLS-first listen URL with no certificate (58)
    /// or an <c>scp</c> or <c>sftp</c> one with no host key (2), the start files, the shares and
    /// buckets (2, or 37 for a share directory that cannot be opened), the data
    /// directory's lock (124 or 23), the service state (37) and the TLS files (58, 2 or 77), in
    /// that order.
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

        if (FindOptionRefusal(commandLine) is { } optionRefusal)
        {
            return Refuse(SurlExitCode.FailedInit, optionRefusal);
        }

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
        return $"(58) {ListenerStatusLine.FormatBoundListenUrl(listenUrl with { BoundPort = listenUrl.Port })} needs a certificate: "
            + "give --cert <file>, or --self-signed for a throwaway one";
    }

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

        var contentStore = ComposeContentStore(commandLine, fileSystem, instruments.Events, instruments.Faults, shares, buckets!);
        return await LockThenComposeAsync(commandLine, seams, fileSystem, contentStore, authentication, instruments, cancellationToken);
    }

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
                : ComposeExtendedSchemes(seams.AreExtensionsTurnedOn, seams.ComposeExtendedProtocolServers, seams.TimeProvider));
        if (mailboxes is null)
        {
            return (null, new SurlStartFailure(mailboxExitCode, [mailboxFailureMessage!]));
        }

        var (policy, accountNames, skippedKeytabEntries, exitCode, failureMessage) = AuthenticationComposition.Compose(
            commandLine, readStartFile, seams.TimeProvider, instruments.Events, instruments.Faults, mailboxes.Accounts);
        if (policy is null)
        {
            return (null, new SurlStartFailure(exitCode, [failureMessage!]));
        }

        // The groups, expanded over every account once --user-file is read (ADR-0102 decisions 4.1 and 5).
        var (accountGroups, groupFailureMessage) = AccountGroupExpansion.Compose(commandLine.Groups, accountNames);
        if (accountGroups is null)
        {
            return (null, new SurlStartFailure(SurlExitCode.FailedInit, [groupFailureMessage!]));
        }

        // The proxy's own accounts, never the origin's (ADR-0088 decision 5).
        var (proxyPolicy, proxyExitCode, proxyFailureMessage) = AuthenticationComposition.ComposeProxyPolicy(
            commandLine, readStartFile, seams.TimeProvider, instruments.Events, instruments.Faults);
        if (proxyPolicy is null)
        {
            return (null, new SurlStartFailure(proxyExitCode, [proxyFailureMessage!]));
        }

        var (sshHostKeys, hostKeyExitCode, hostKeyFailureMessage) = SshHostKeyComposition.Compose(commandLine, readStartFile);
        return sshHostKeys is null
            ? (null, new SurlStartFailure(hostKeyExitCode, [hostKeyFailureMessage!]))
            : (new ComposedAuthentication(
                policy,
                accountNames,
                accountGroups,
                skippedKeytabEntries,
                sshHostKeys,
                mailboxes,
                new SchemeAuthenticationPolicies(policy, seams.TimeProvider, instruments.Events, instruments.Faults))
            {
                ProxyPolicy = proxyPolicy,
                PreSharedKeyAccounts = AuthenticationComposition.ReadPreSharedKeyAccounts(commandLine, readStartFile),
            }, null);
    }

    private static async Task<SurlServerStart> LockThenComposeAsync(
        SurlCommandLine commandLine,
        SurlServerCompositionSeams seams,
        IContentFileSystem fileSystem,
        ContentStore contentStore,
        ComposedAuthentication authentication,
        EventStreamAndFaultPlan instruments,
        CancellationToken cancellationToken)
    {
        var dataDirectoryLock = commandLine.DataDirectory is { } dataDirectory
            ? seams.TakeDataDirectoryLock(dataDirectory)
            : DataDirectoryLockOutcome.NoLock;
        if (dataDirectoryLock.FailureMessage is { } lockFailure)
        {
            return Refuse(dataDirectoryLock.ExitCode, lockFailure);
        }

        // The lock is handed to the composed serving, or released when the start ends any other way.
        SurlServerStart? start = null;
        try
        {
            start = await ComposeUnderTheLockAsync(
                commandLine, seams, fileSystem, contentStore, authentication, instruments, dataDirectoryLock.Holder, cancellationToken);
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

    private static async Task<SurlServerStart> ComposeUnderTheLockAsync(
        SurlCommandLine commandLine,
        SurlServerCompositionSeams seams,
        IContentFileSystem fileSystem,
        ContentStore contentStore,
        ComposedAuthentication authentication,
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
                authentication.Mailboxes.MailDomains);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stopped before serving began: the same clean stop as one while serving.
            return Refuse(SurlExitCode.Ok);
        }

        if (loaded.State is null)
        {
            return Refuse(SurlExitCode.CouldNotReadFile, loaded.FailureMessage!);
        }

        var originConnector = ComposeOriginConnector(commandLine, seams);
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
            OriginConnector = originConnector,
            ProxyAuthenticationPolicy = authentication.ProxyPolicy,
            PreSharedKeyAccounts = authentication.PreSharedKeyAccounts,
            MailDomains = commandLine.MailDomains,
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
        var served = commandLine with
        {
            ListenUrls = [.. commandLine.ListenUrls, .. seams.AdditionalProtocolServers.SelectMany(additional => additional.ListenUrls)],
        };
        // Every store a test seeds, resets, snapshots and restores, behind one surface (ADR-0124, decision 3).
        var stores = new ComposedStores(
            contentStore,
            loaded.State.ObjectMetadataIndex,
            loaded.State.MailStore,
            loaded.State.MessageBus,
            authentication.Policy.Settings.Accounts);
        return ComposeServing(served, seams, servers, stores, authentication, instruments, services.QuicCertificate, dataDirectoryLock);
    }

    // How the proxies reach origins, over the rule --proxy-allow-origin and the proxy listen URLs make
    // (ADR-0088 decision 2); the host's seam when it gives one.
    private static IOriginConnector ComposeOriginConnector(SurlCommandLine commandLine, SurlServerCompositionSeams seams)
    {
        var rule = ProxyComposition.ComposeAllowedOriginRule(commandLine);
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
        IDisposable? dataDirectoryLock)
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

        quicCertificateSource.Certificate = tls.QuicCertificate;
        quicCertificateSource.ClientTrustAnchors = tls.ClientTrustAnchors;
        IProtocolServer[] served = commandLine.Http3 && tls.QuicCertificate is { } quicCertificate
            ? [.. servers, ComposeHttp3ProtocolServer(commandLine, stores.Content, authentication.SchemePolicies.For("https"), quicCertificate, tls.ClientTrustAnchors, seams.TimeProvider)]
            : servers;
        return new SurlServerStart(
            null, new ComposedServing(commandLine, seams, served, authentication, instruments, stores, tls, dataDirectoryLock));
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
            clientTrustAnchors.Count == 0 ? null : new QuicClientCertificateTrust(clientTrustAnchors, timeProvider).Trusts);
}
