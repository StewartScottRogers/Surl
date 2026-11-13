using Surl.Cli;
using Surl.Control;
using Surl.Core;
using Surl.Networking;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// A start <see cref="SurlServerComposition.ComposeAsync"/> composed: the protocol servers, the
/// server TLS and the data-directory lock, ready to serve (ADR-0099, decision 2).
/// <see cref="ServeAsync"/> writes the start-up warnings, builds the <see cref="ServingEngine"/> and
/// serves until cancelled; disposing releases the TLS certificates and the data-directory lock.
/// </summary>
/// <remarks>
/// Public because more than one host calls it, but not a supported programming interface: it may
/// change in any release (ADR-0099, decision 1).
/// </remarks>
public sealed class ComposedServing : IAsyncDisposable
{
    private const string MessagePrefix = "surl: ";

    private readonly SurlCommandLine commandLine;
    private readonly SurlServerCompositionSeams seams;
    private readonly IProtocolServer[] servers;
    private readonly ComposedAuthentication authentication;
    private readonly EventStreamAndFaultPlan instruments;
    private readonly ServerTlsComposition tls;
    private readonly IDisposable? dataDirectoryLock;
    private readonly SeededIdentifierRandom? seededIdentifiers;

    internal ComposedServing(
        SurlCommandLine commandLine,
        SurlServerCompositionSeams seams,
        IProtocolServer[] servers,
        ComposedAuthentication authentication,
        EventStreamAndFaultPlan instruments,
        ComposedStores stores,
        ServerTlsComposition tls,
        IDisposable? dataDirectoryLock)
    {
        this.commandLine = commandLine;
        this.seams = seams;
        this.servers = servers;
        this.authentication = authentication;
        this.instruments = instruments;
        Stores = stores;
        this.tls = tls;
        this.dataDirectoryLock = dataDirectoryLock;
        seededIdentifiers = commandLine.RandomSeed is { } seed ? new SeededIdentifierRandom(seed) : null;
    }

    /// <summary>
    /// The source of non-secret generated identifiers for one exchange (ADR-0124, decision 6): the
    /// bytes <c>--random-seed</c> and <paramref name="exchangeId"/> give, the same every run, or, without
    /// a seed, the cryptographic source. This instance's own; another instance never shares it.
    /// </summary>
    /// <param name="exchangeId">The exchange's <see cref="ExchangeContext.ExchangeId"/>.</param>
    /// <returns>The source the serving engine stamps on that exchange's <see cref="ExchangeContext.Identifiers"/>.</returns>
    public IIdentifierRandom IdentifiersForExchange(long exchangeId) =>
        seededIdentifiers?.ForExchange(exchangeId) ?? CryptographicIdentifierRandom.Instance;

    /// <summary>
    /// The source of identifiers a server draws once, when it is created (ADR-0124, decision 6), such as
    /// the Kafka cluster ID or the NATS <c>server_id</c>: derived from <c>--random-seed</c> and
    /// <paramref name="serverName"/> alone, so adding a server never shifts another's; without a seed,
    /// the cryptographic source.
    /// </summary>
    /// <param name="serverName">Names the server, such as <c>nats</c>; compared ordinally.</param>
    /// <returns>A fresh source, not safe to share between threads.</returns>
    /// <exception cref="ArgumentException"><paramref name="serverName"/> is null or empty.</exception>
    public IIdentifierRandom IdentifiersForServer(string serverName)
    {
        ArgumentException.ThrowIfNullOrEmpty(serverName);
        return seededIdentifiers?.ForServer(serverName) ?? CryptographicIdentifierRandom.Instance;
    }

    /// <summary>
    /// The instance's one event stream (ADR-0124, decision 1): every exchange the serving engine
    /// serves, every change of the content store, the mail store, the message bus and the object
    /// metadata index, every judged login and every applied fault is published on it. Subscribe to
    /// see the events published after the subscription is made; another instance's are never on it.
    /// </summary>
    public SurlEventStream Events => instruments.Events;

    /// <summary>
    /// The instance's one fault plan (ADR-0124, decision 4), holding the <c>--fault</c> rules in
    /// command-line order as <c>f1</c>, <c>f2</c> ...; rules may be added, removed and cleared
    /// while serving.
    /// </summary>
    public FaultPlan Faults => instruments.Faults;

    /// <summary>
    /// The instance's stores - content, mail, message bus and accounts - as a test seeds, resets,
    /// snapshots and restores them, one at a time or all together (ADR-0124, decision 3). Every
    /// change they make is published on <see cref="Events"/>.
    /// </summary>
    public ComposedStores Stores { get; }

    /// <summary>
    /// The loopback control endpoint's bound listen URL once <see cref="ServeAsync"/> has bound it, port 0 replaced by
    /// the port bound; <see langword="null"/> without <c>--control</c> or before it binds (ADR-0124, decision 7).
    /// </summary>
    public ListenUrl? ControlListenUrl { get; private set; }

    /// <summary>
    /// The protocol servers the serving engine routes each listen URL to, original and extended, in
    /// registration order.
    /// </summary>
    internal IReadOnlyList<IProtocolServer> Servers => servers;

    /// <summary>
    /// The SHA-256 fingerprint of the throwaway certificate <c>--self-signed</c> made, or none when
    /// no certificate was made.
    /// </summary>
    public IReadOnlyList<string> ThrowawayCertificateFingerprints =>
        tls.ThrowawayCertificateFingerprint is { } fingerprint ? [fingerprint] : [];

    /// <summary>
    /// Writes the start-up warnings to <see cref="SurlServingOutputs.Log"/>, then serves every listen
    /// URL until <paramref name="cancellationToken"/> is cancelled, writing the status lines once
    /// every listener has bound.
    /// </summary>
    /// <param name="outputs">Where the warnings, the status lines and the exchange log go.</param>
    /// <param name="cancellationToken">Cancelled to stop serving.</param>
    /// <returns>The exit code, and the <c>(45)</c>, <c>(6)</c> or <c>(125)</c> failure when there was one.</returns>
    public async Task<SurlServingOutcome> ServeAsync(SurlServingOutputs outputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        var listenerFactory = seams.CreateListenerFactory(tls.Settings);
        if (authentication.ControlToken is { } controlToken)
        {
            return await ServeWithControlAsync(listenerFactory, controlToken, outputs, cancellationToken);
        }

        WriteStartUpLines(outputs.Log, null);
        return await ServeProtocolsAsync(listenerFactory, outputs, outputs.AllListenersBound, cancellationToken);
    }

    /// <summary>Releases the TLS certificates and then the data-directory lock.</summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        tls.Dispose();
        dataDirectoryLock?.Dispose();
        return ValueTask.CompletedTask;
    }

    private IDataConnectionOpener CreateDataConnectionOpener() =>
        seams.CreateDataConnectionOpener is { } create
            ? create(tls.Settings)
            : new SocketDataConnectionOpener(tls.Settings, seams.TimeProvider);

    // The status lines are written from the info level up (ADR-0033, section 1).
    private TextWriter StatusOutputOf(SurlServingOutputs outputs) =>
        commandLine.LogLevel >= LogLevel.Info ? outputs.StatusOutput : TextWriter.Null;

    private Task<SurlServingOutcome> ServeProtocolsAsync(
        IListenerFactory listenerFactory,
        SurlServingOutputs outputs,
        Action<IReadOnlyList<ListenUrl>> allListenersBound,
        CancellationToken cancellationToken)
    {
        var reporter = new ListenerStartReporter(
            listenerFactory,
            new ListenerStatusLine(StatusOutputOf(outputs)),
            commandLine.ListenUrls.Count,
            allListenersBound,
            listenUrl => WriteHttp3Note(listenUrl, outputs.Log));
        var engine = new ServingEngine(
            reporter,
            servers,
            outputs.ExchangeLogFactory,
            seams.TimeProvider,
            ServingEngine.DefaultShutdownGracePeriod,
            SurlServerComposition.ComposeConnectionLimits(commandLine),
            CreateDataConnectionOpener(),
            commandLine.Limits,
            instruments.Events,
            instruments.Faults)
        {
            IdentifiersForExchange = IdentifiersForExchange,
        };
        return RunEngineAsync(engine, reporter, commandLine.ListenUrls, cancellationToken);
    }

    // The control listener binds first, through the same listener factory, on its own serving engine: at most 8
    // connections of its own, never --max-connections, no fault plan, and nothing published or logged, so watching
    // the stream does not record the watching and no log holds the token (ADR-0124 decision 7). Its warning names
    // its bound URL, so the start-up lines follow its bind; its status line follows the Listening on lines.
    private async Task<SurlServingOutcome> ServeWithControlAsync(
        IListenerFactory listenerFactory, ControlToken controlToken, SurlServingOutputs outputs, CancellationToken cancellationToken)
    {
        using var stopControl = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var endpoint = new ControlEndpoint(new ComposedControlTarget(Events, Faults, Stores), controlToken);
        var feed = endpoint.FeedEventsAsync(stopControl.Token);
        var bound = new TaskCompletionSource<ListenUrl>(TaskCreationOptions.RunContinuationsAsynchronously);
        var controlReporter = new ListenerStartReporter(
            listenerFactory, new ListenerStatusLine(TextWriter.Null), 1, listenUrls => bound.TrySetResult(listenUrls[0]), _ => { });
        var controlServing = RunEngineAsync(
            ComposeControlEngine(controlReporter, endpoint), controlReporter, [commandLine.ControlListenUrl!], stopControl.Token);
        try
        {
            if (await Task.WhenAny(bound.Task, controlServing) != bound.Task)
            {
                // Not bound: the bind failure (45, 6), or a stop before it bound.
                return await controlServing;
            }

            ControlListenUrl = await bound.Task;
            WriteStartUpLines(outputs.Log, ControlListenUrl);
            var statusOutput = StatusOutputOf(outputs);
            return await ServeProtocolsAsync(
                listenerFactory,
                outputs,
                listenUrls =>
                {
                    statusOutput.WriteLine(ControlComposition.FormatStatusLine(ControlListenUrl));
                    outputs.AllListenersBound(listenUrls);
                },
                cancellationToken);
        }
        finally
        {
            await stopControl.CancelAsync();
            await controlServing;
            await feed;
        }
    }

    private ServingEngine ComposeControlEngine(ListenerStartReporter controlReporter, ControlEndpoint endpoint) =>
        new(
            controlReporter,
            [new ControlProtocolServer(endpoint)],
            new LevelledExchangeLogFactory(TextWriter.Null, LogLevel.None, false, seams.TimeProvider),
            seams.TimeProvider,
            ServingEngine.DefaultShutdownGracePeriod,
            ConnectionLimits.Default with
            {
                MaxConnections = ControlComposition.MaxConnections,
                MaxConnectionsPerAddress = ControlComposition.MaxConnections,
            },
            RefusingDataConnectionOpener.Instance,
            ExchangeLimits.Default,
            DiscardingSurlEventPublisher.Instance,
            EmptyFaultPlan.Instance);

    private static async Task<SurlServingOutcome> RunEngineAsync(
        ServingEngine engine, ListenerStartReporter reporter, IReadOnlyList<ListenUrl> listenUrls, CancellationToken cancellationToken)
    {
        try
        {
            var exitCode = await engine.ServeAsync(listenUrls, cancellationToken);
            return reporter.BindFailure is { } bindFailure
                ? new SurlServingOutcome(exitCode, new SurlStartFailure(exitCode, [SurlServerComposition.FormatBindFailure(bindFailure)]))
                : new SurlServingOutcome(exitCode, null);
        }
        catch (Exception exception)
        {
            // Any failure while serving ends surl with (125) (ADR-0007, section 5).
            return new SurlServingOutcome(
                SurlExitCode.InternalError,
                new SurlStartFailure(SurlExitCode.InternalError, [$"(125) Internal error: {exception.Message}"]));
        }
    }

    private void WriteStartUpLines(TextWriter log, ListenUrl? boundControlUrl)
    {
        // Each loosening option's warning (ADR-0032, section 9).
        AuthenticationComposition.WriteLooseningWarnings(commandLine, log);
        ProxyComposition.WriteLooseningWarning(commandLine, log);

        // The --fault warning, once, with the other loosening warnings (ADR-0124, decision 4).
        FaultPlanComposition.WriteWarning(commandLine, log);

        // The --control warning, naming the bound control URL (ADR-0124, decision 7).
        if (boundControlUrl is not null)
        {
            ControlComposition.WriteWarning(commandLine.LogLevel, boundControlUrl, log);
        }

        // Each share granting @everyone, with the other widening warnings (ADR-0102 decision 2.2).
        ContentShareComposition.WriteWarnings(commandLine.LogLevel, Stores.Content, log);

        // Each topic filter granting @everyone, with the other widening warnings (ADR-0102 decision 3.4).
        MessageBusComposition.WriteWarnings(commandLine.LogLevel, Stores.Bus.Configuration, log);

        // Each mail catch-all, with the other widening warnings (ADR-0102 decision 1.3, ADR-0101 decision 4).
        MailboxComposition.WriteWarnings(commandLine, log);

        // Each -K file, then the --user-file, readable by other users, at the warnings' level (ADR-0101, decision 5).
        ReadableByOthersWarning.WriteForFilesThatMayHoldPasswords(
            commandLine, seams.ReadStartFileUnixMode ?? ReadableByOthersWarning.ReadDiskMode, log);

        // Each skipped keytab entry's warning, then the unused --keytab one (ADR-0057, decision 1).
        KeytabComposition.WriteStartLines(commandLine, authentication.SkippedKeytabEntries, log);

        // The --self-signed warning from the info level up, the fingerprint note from verbose
        // up, both unstamped (ADR-0032, section 9; ADR-0033, section 7).
        if (tls.ThrowawayCertificateFingerprint is { } fingerprint)
        {
            WriteThrowawayCertificateLines(commandLine.LogLevel, fingerprint, log);
        }

        // Each --srp-groups group outside RFC 5054 Appendix A, once, from verbose up (ADR-0122 decision 4).
        tls.WriteStartLines(commandLine.LogLevel, log);

        // The --throwaway-hostkey and --allow-weak-ssh-algorithms warnings, then each SSH host key's note
        // (ADR-0051, decisions 8 and 11).
        authentication.SshHostKeys.WriteStartLines(commandLine, log);

        // The LDAP account source's note, and from here on its per-login notes (ADR-0149 decision 9).
        LdapAccountSourceComposition.WriteStartLine(commandLine, log);
        authentication.DirectoryNotes.Attach(log, commandLine.LogLevel);

        // Each group's note, after the warnings (ADR-0102 decision 5).
        authentication.AccountGroups.WriteStartLines(commandLine.LogLevel, log);

        // The mail domains' and each mailbox's note, after the groups' (ADR-0102 decision 5).
        authentication.Mailboxes.WriteStartLines(commandLine, log);

        // Each share's and bucket's note, after the mailboxes' (ADR-0102 decision 5).
        ContentShareComposition.WriteStartLines(commandLine.LogLevel, Stores.Content, log);

        // Each topic's, queue's and topic map's note and the topic access count, after the shares' (ADR-0102 decision 5).
        MessageBusComposition.WriteStartLines(commandLine.LogLevel, Stores.Bus.Configuration, log);
    }

    // From verbose up, unstamped, once per https listen URL served over HTTP/3 too (ADR-0082,
    // decision 6): the status line names the port both transports share.
    private void WriteHttp3Note(ListenUrl datagramListenUrl, TextWriter log)
    {
        // A dns listen URL binds UDP and TCP on one port too (ADR-0092), and is no HTTP/3.
        if (commandLine.LogLevel >= LogLevel.Verbose && datagramListenUrl.Scheme == "https")
        {
            log.WriteLine($"* Serving HTTP/3 over QUIC on UDP port {datagramListenUrl.BoundPort ?? datagramListenUrl.Port}");
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
}
