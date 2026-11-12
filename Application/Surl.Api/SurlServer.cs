using Surl.Cli;
using Surl.Composition;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// A Surl server running in this process (ADR-0099, decision 4): started from
/// <see cref="SurlServerOptions"/> through the same composition as <c>surl</c>, so it serves,
/// validates and refuses exactly as <c>surl</c> does with the same options, and stopped with
/// <see cref="StopAsync"/> or by disposing it.
/// </summary>
/// <example>
/// <code>
/// var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
/// await using var server = await SurlServer.StartAsync(options);
/// // server.BoundUrl is http://127.0.0.1:&lt;port&gt;/
/// </code>
/// </example>
public sealed class SurlServer : IAsyncDisposable
{
    private readonly ComposedServing composedServing;
    private readonly CancellationTokenSource stop;
    private readonly Task<SurlServingOutcome> serving;
    private readonly Lazy<Task<SurlExitCode>> stopping;
    private readonly ExchangeRecordSubscriber? record;
    private readonly List<EventCallbackHook> callbacks = [];
    private int lastCallbackNumber;

    private SurlServer(
        ComposedServing composedServing,
        CancellationTokenSource stop,
        Task<SurlServingOutcome> serving,
        IReadOnlyList<Uri> boundUrls,
        ExchangeRecordSubscriber? record,
        TimeProvider timeProvider,
        TimeSpan hookTimeLimit)
    {
        this.composedServing = composedServing;
        this.stop = stop;
        this.serving = serving;
        this.record = record;
        BoundUrls = boundUrls;
        Faults = new SurlServerFaults(composedServing.Faults, composedServing.Events, timeProvider, hookTimeLimit);
        Mail = new SurlServerMail(composedServing.Stores);
        Files = new SurlServerFiles(composedServing.Stores);
        Messages = new SurlServerMessages(composedServing.Stores);
        Accounts = new SurlServerAccounts(composedServing.Stores);
        stopping = new Lazy<Task<SurlExitCode>>(StopServingAsync);
    }

    /// <summary>The source of the non-secret identifiers exchange <paramref name="exchangeId"/> draws, seeded by <see cref="SurlServerOptions.RandomSeed"/>.</summary>
    /// <param name="exchangeId">The exchange.</param>
    /// <returns>The source.</returns>
    internal IIdentifierRandom IdentifiersForExchange(long exchangeId) => composedServing.IdentifiersForExchange(exchangeId);

    /// <summary>
    /// Every exchange recorded so far, oldest first, at most <see cref="SurlServerOptions.MaxRecordedExchanges"/>
    /// of them (ADR-0099, decision 7): a snapshot that later exchanges and bytes do not change. Safe to
    /// read while the server serves.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="SurlServerOptions.RecordExchanges"/> is off, so nothing was recorded.</exception>
    public IReadOnlyList<SurlExchangeRecord> Exchanges => RecordOrThrow().Snapshot();

    /// <summary>How many recorded exchanges were dropped, oldest first, past <see cref="SurlServerOptions.MaxRecordedExchanges"/>.</summary>
    /// <exception cref="InvalidOperationException"><see cref="SurlServerOptions.RecordExchanges"/> is off, so nothing was recorded.</exception>
    public long DroppedExchangeCount => RecordOrThrow().DroppedExchangeCount;

    /// <summary>How many kept events were dropped, oldest first, past <see cref="SurlServerOptions.MaxRecordedEvents"/>.</summary>
    /// <exception cref="InvalidOperationException"><see cref="SurlServerOptions.RecordExchanges"/> is off, so nothing was recorded.</exception>
    public long DroppedEventCount => RecordOrThrow().DroppedEventCount;

    /// <summary>
    /// The recorded exchanges <paramref name="query"/> asks for, oldest first (ADR-0124 decision 1, ADR-0143): a
    /// snapshot like <see cref="Exchanges"/>, of the exchanges <see cref="Exchanges"/> still holds.
    /// </summary>
    /// <param name="query">Which exchanges: by scheme, listen URL, account judged, event kind seen, store changed, start time and sequence.</param>
    /// <returns>The matching exchanges.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><see cref="SurlServerOptions.RecordExchanges"/> is off, so nothing was recorded.</exception>
    public IReadOnlyList<SurlExchangeRecord> FindExchanges(SurlEventQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return RecordOrThrow().FindExchanges(query);
    }

    /// <summary>
    /// The kept events <paramref name="query"/> asks for, oldest first: the instance's event stream's own records,
    /// every kind but exchange bytes, at most <see cref="SurlServerOptions.MaxRecordedEvents"/> of them.
    /// </summary>
    /// <param name="query">Which events: by scheme, listen URL, account, kind, store, time and sequence.</param>
    /// <returns>The matching events.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><see cref="SurlServerOptions.RecordExchanges"/> is off, so nothing was recorded.</exception>
    public IReadOnlyList<SurlEvent> FindEvents(SurlEventQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return RecordOrThrow().FindEvents(query);
    }

    /// <summary>
    /// The first recorded exchange <paramref name="query"/> asks for: one already recorded is returned at once,
    /// else the wait ends when one comes to match (a later login, store change or event of it included).
    /// </summary>
    /// <param name="query">Which exchange, as <see cref="FindExchanges"/> takes it.</param>
    /// <param name="timeout">How long to wait on <see cref="SurlServerOptions.TimeProvider"/>, or <see cref="Timeout.InfiniteTimeSpan"/>.</param>
    /// <param name="cancellationToken">Gives up the wait.</param>
    /// <returns>A snapshot of the exchange as it stood when it matched; it may not have ended.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><see cref="SurlServerOptions.RecordExchanges"/> is off, so nothing was recorded.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative and not infinite.</exception>
    /// <exception cref="TimeoutException">None matched in time; the message says what was asked and how many events were recorded meanwhile.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<SurlExchangeRecord> WaitForExchangeAsync(
        SurlEventQuery query, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return RecordOrThrow().WaitForExchangeAsync(query, timeout, cancellationToken);
    }

    /// <summary>
    /// The first kept event <paramref name="query"/> asks for: one already kept is returned at once, so a test
    /// cannot miss an event by awaiting too late; else the wait ends when one is published.
    /// </summary>
    /// <param name="query">Which event, as <see cref="FindEvents"/> takes it.</param>
    /// <param name="timeout">How long to wait on <see cref="SurlServerOptions.TimeProvider"/>, or <see cref="Timeout.InfiniteTimeSpan"/>.</param>
    /// <param name="cancellationToken">Gives up the wait.</param>
    /// <returns>The event.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><see cref="SurlServerOptions.RecordExchanges"/> is off, so nothing was recorded.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative and not infinite.</exception>
    /// <exception cref="TimeoutException">None matched in time; the message says what was asked and how many events were recorded meanwhile.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<SurlEvent> WaitForEventAsync(SurlEventQuery query, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return RecordOrThrow().WaitForEventAsync(query, timeout, cancellationToken);
    }

    /// <summary>
    /// Runs <paramref name="callback"/> for each event <paramref name="query"/> matches from now on (ADR-0124,
    /// decision 5), whether or not <see cref="SurlServerOptions.RecordExchanges"/> is on, until the registration is
    /// disposed or the server stops. The callback runs on its own subscription of the server's event stream, one event
    /// at a time and off the serving path, so it never blocks a server: a callback slower than the events loses the
    /// oldest, as any slow subscriber does. A callback that throws is published as a <see cref="HookFailedEvent"/>
    /// naming it (<c>h1</c>, <c>h2</c> ...) and runs again for the next event; exchange bytes never reach it.
    /// </summary>
    /// <param name="query">Which events, as <see cref="FindEvents"/> takes it.</param>
    /// <param name="callback">What to run; its token is cancelled when the registration is disposed.</param>
    /// <returns>The registration; disposing it stops the callback being run.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> or <paramref name="callback"/> is null.</exception>
    public IDisposable OnEvent(SurlEventQuery query, Func<SurlEvent, CancellationToken, ValueTask> callback)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(callback);
        lock (callbacks)
        {
            var hookId = "h" + (++lastCallbackNumber).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var hook = new EventCallbackHook(hookId, query, callback, composedServing.Events);
            callbacks.Add(hook);
            return hook;
        }
    }

    /// <summary>
    /// Each listen URL in the order given, as the status line writes it, with port 0 replaced by the
    /// port bound (<c>http://127.0.0.1:49731/</c>).
    /// </summary>
    public IReadOnlyList<Uri> BoundUrls { get; }

    /// <summary>
    /// The server's fault plan (ADR-0124, decision 4): the rules <see cref="SurlServerOptions.Faults"/> gave at start,
    /// and adding, removing and clearing rules while it serves.
    /// </summary>
    public SurlServerFaults Faults { get; }

    /// <summary>The server's mail store, to seed messages into mailboxes (ADR-0124, decision 3).</summary>
    public SurlServerMail Mail { get; }

    /// <summary>The server's content store, to seed files (ADR-0124, decision 3).</summary>
    public SurlServerFiles Files { get; }

    /// <summary>The server's message bus, to seed messages onto topics and queues (ADR-0124, decision 3).</summary>
    public SurlServerMessages Messages { get; }

    /// <summary>The server's accounts, to add and remove them while it serves (ADR-0124, decision 3).</summary>
    public SurlServerAccounts Accounts { get; }

    /// <summary>
    /// Returns the content store, the mail store, the message bus and the accounts, in that order, to their state
    /// at start (ADR-0124, decision 3): the files <see cref="SurlServerOptions.Files"/> gave and no others, the
    /// configured mailboxes holding no messages (each with a new <c>UIDVALIDITY</c>), the configured topics and
    /// queues holding no messages, and exactly the accounts given at start. Open sessions keep their connections
    /// and see the new contents on their next command. Publishes one <see cref="StoreChangedEvent"/> with
    /// <see cref="StoreChange.Reset"/> per store.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait for the content store.</param>
    /// <returns>A task that completes once every store is reset.</returns>
    /// <exception cref="InvalidOperationException">A store refused, such as one on disk under
    /// <see cref="SurlServerOptions.DataDirectory"/>; the message names it, and the stores before it are reset.</exception>
    public Task ResetAsync(CancellationToken cancellationToken = default) =>
        composedServing.Stores.ResetAsync(cancellationToken);

    /// <summary>
    /// Takes an in-memory copy of every store - files, mail with its UIDs and flags, retained and queued messages,
    /// and accounts - that <see cref="RestoreAsync"/> gives back to this server (ADR-0124, decision 3). Changes
    /// nothing.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait for the content store.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="InvalidOperationException">A store refused, such as one on disk, or the stores together
    /// hold more than 256 MiB.</exception>
    public async Task<SurlSnapshot> SnapshotAsync(CancellationToken cancellationToken = default) =>
        new(this, await composedServing.Stores.TakeSnapshotAsync(cancellationToken));

    /// <summary>
    /// Replaces every store's contents with <paramref name="snapshot"/>'s, in the order <see cref="ResetAsync"/>
    /// takes them; every mailbox gets a new <c>UIDVALIDITY</c>. Publishes one <see cref="StoreChangedEvent"/> with
    /// <see cref="StoreChange.Restored"/> per store.
    /// </summary>
    /// <param name="snapshot">A snapshot this server took.</param>
    /// <param name="cancellationToken">Cancels the wait for the content store.</param>
    /// <returns>A task that completes once every store is restored.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="snapshot"/> was taken by another server.</exception>
    /// <exception cref="InvalidOperationException">A store refused; the message names it.</exception>
    public Task RestoreAsync(SurlSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!ReferenceEquals(snapshot.Owner, this))
        {
            throw new ArgumentException("The snapshot was taken by another server; restore it into the server that took it.", nameof(snapshot));
        }

        return composedServing.Stores.RestoreAsync(snapshot.Stores, cancellationToken);
    }

    /// <summary>The first of <see cref="BoundUrls"/>.</summary>
    public Uri BoundUrl => BoundUrls[0];

    /// <summary>
    /// Starts a server on every listen URL in <paramref name="options"/> and returns once every
    /// listener has bound.
    /// </summary>
    /// <param name="options">What to serve.</param>
    /// <param name="cancellationToken">Cancels the start only; a started server ignores it.</param>
    /// <returns>The running server.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">The options are misused (<see cref="SurlServerOptions"/> says how).</exception>
    /// <exception cref="SurlServerStartException"><c>surl</c> would refuse the start; nothing is left bound or locked.</exception>
    /// <exception cref="OperationCanceledException">The start was cancelled before every listener bound.</exception>
    public static Task<SurlServer> StartAsync(SurlServerOptions options, CancellationToken cancellationToken = default) =>
        StartAsync(options, SurlServerHostSeams.Production, cancellationToken);

    /// <summary>
    /// Stops serving, after the same shutdown grace period as <c>surl</c> after Ctrl+C, and releases
    /// the data directory's lock. Stopping again returns the same exit code.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait, not the stop.</param>
    /// <returns>The exit code <c>surl</c> would have exited with: <see cref="SurlExitCode.Ok"/>, or <see cref="SurlExitCode.InternalError"/> when serving failed.</returns>
    public Task<SurlExitCode> StopAsync(CancellationToken cancellationToken = default) =>
        stopping.Value.WaitAsync(cancellationToken);

    /// <summary>Stops the server as <see cref="StopAsync"/> does, discarding its exit code; disposing again does nothing more.</summary>
    /// <returns>A task that completes once the server has stopped.</returns>
    public async ValueTask DisposeAsync() => await stopping.Value;

    /// <summary>Starts a server through <paramref name="host"/>'s seams.</summary>
    /// <param name="options">What to serve.</param>
    /// <param name="host">How listeners start, how the data directory is locked and where <c>SURL_EXTENSIONS</c> is read.</param>
    /// <param name="cancellationToken">Cancels the start only.</param>
    /// <returns>The running server.</returns>
    internal static async Task<SurlServer> StartAsync(
        SurlServerOptions options, SurlServerHostSeams host, CancellationToken cancellationToken)
    {
        SurlServerArguments.ThrowIfMisused(options);
        var timeProvider = options.TimeProvider;
        var areExtensionsTurnedOn = options.AreExtensionsTurnedOn ?? ExtendedProtocolServers.AreTurnedOn(host.EnvironmentVariables);
        var commandLine = SurlServerArguments.Parse(
            options, SurlServerComposition.ComposeExtendedSchemes(areExtensionsTurnedOn, null, timeProvider), host.ReadStartFile);
        var seams = new SurlServerCompositionSeams(
            tlsSettings => host.CreateListenerFactory(tlsSettings, timeProvider), host.CanOpenDataDirectory, host.TakeDataDirectoryLock, timeProvider, areExtensionsTurnedOn)
        {
            DataDirectoryFileSystem = host.DataDirectoryFileSystem,
            ReadStartFile = host.ReadStartFile,
            CanOpenShareDirectory = host.CanOpenDataDirectory,
            ContentWithoutDataDirectory = await InMemoryFiles.ComposeAsync(options.Files, timeProvider, cancellationToken),
        };

        var start = await SurlServerComposition.ComposeAsync(commandLine, seams, cancellationToken);
        if (start.Failure is { } failure)
        {
            // A start cancelled while loading the service state is refused with Ok and no lines.
            cancellationToken.ThrowIfCancellationRequested();
            throw new SurlServerStartException(failure);
        }

        var record = options.RecordExchanges
            ? new ExchangeRecordSubscriber(start.Serving!.Events, timeProvider, ExchangeRecordBounds.Of(options))
            : null;
        return await ServeUntilBoundAsync(commandLine, start.Serving!, options, record, cancellationToken);
    }

    /// <summary>The exchange log <c>surl</c> would write at the command line's level, to <paramref name="log"/>.</summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="log">Where the log goes.</param>
    /// <param name="timeProvider">The clock that stamps the lines.</param>
    /// <returns>The exchange log factory.</returns>
    internal static IExchangeLogFactory CreateExchangeLogFactory(SurlCommandLine commandLine, TextWriter log, TimeProvider timeProvider) =>
        commandLine.LogLevel == LogLevel.Trace
            ? new TraceExchangeLogFactory(log, commandLine.TraceLayout, commandLine.TraceTime, timeProvider)
            : new LevelledExchangeLogFactory(log, commandLine.LogLevel, commandLine.TraceTime, timeProvider);

    private static async Task<SurlServer> ServeUntilBoundAsync(
        SurlCommandLine commandLine,
        ComposedServing composedServing,
        SurlServerOptions options,
        ExchangeRecordSubscriber? record,
        CancellationToken cancellationToken)
    {
        var timeProvider = options.TimeProvider;
        var log = options.Log is null ? TextWriter.Null : TextWriter.Synchronized(options.Log);
        var bound = new TaskCompletionSource<IReadOnlyList<ListenUrl>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stop = new CancellationTokenSource();
        var exchangeLogFactory = CreateExchangeLogFactory(commandLine, log, timeProvider);
        var outputs = new SurlServingOutputs(
            log,
            log,
            exchangeLogFactory,
            boundUrls => bound.TrySetResult(boundUrls));
        var serving = composedServing.ServeAsync(outputs, stop.Token);
        using (cancellationToken.Register(stop.Cancel))
        {
            await Task.WhenAny(bound.Task, serving);
        }

        if (bound.Task.IsCompletedSuccessfully)
        {
            return new SurlServer(composedServing, stop, serving, [.. bound.Task.Result.Select(ToUri)], record, timeProvider, options.HookTimeLimit);
        }

        // Serving ended before every listener bound: cancelled, or a bind failure (45 or 6).
        var outcome = await serving;
        await StopRecordingAsync(record);
        await composedServing.DisposeAsync();
        stop.Dispose();
        cancellationToken.ThrowIfCancellationRequested();
        throw new SurlServerStartException(outcome.Failure!);
    }

    private ExchangeRecordSubscriber RecordOrThrow() =>
        record ?? throw new InvalidOperationException(
            "RecordExchanges is off, so no exchange was recorded; set SurlServerOptions.RecordExchanges to read Exchanges.");

    // The record stays readable once stopped: the pump has applied every event the subscription held.
    private static async ValueTask StopRecordingAsync(ExchangeRecordSubscriber? record)
    {
        if (record is not null)
        {
            await record.DisposeAsync();
        }
    }

    private void StopCallbacks()
    {
        lock (callbacks)
        {
            callbacks.ForEach(callback => callback.Dispose());
        }
    }

    private static Uri ToUri(ListenUrl boundUrl) => new(ListenerStatusLine.FormatBoundListenUrl(boundUrl));

    private async Task<SurlExitCode> StopServingAsync()
    {
        await stop.CancelAsync();
        var outcome = await serving;
        await StopRecordingAsync(record);
        StopCallbacks();
        await composedServing.DisposeAsync();
        stop.Dispose();
        return outcome.ExitCode;
    }
}
