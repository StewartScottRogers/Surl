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

    private SurlServer(
        ComposedServing composedServing,
        CancellationTokenSource stop,
        Task<SurlServingOutcome> serving,
        IReadOnlyList<Uri> boundUrls,
        ExchangeRecordSubscriber? record)
    {
        this.composedServing = composedServing;
        this.stop = stop;
        this.serving = serving;
        this.record = record;
        BoundUrls = boundUrls;
        stopping = new Lazy<Task<SurlExitCode>>(StopServingAsync);
    }

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

    /// <summary>
    /// Each listen URL in the order given, as the status line writes it, with port 0 replaced by the
    /// port bound (<c>http://127.0.0.1:49731/</c>).
    /// </summary>
    public IReadOnlyList<Uri> BoundUrls { get; }

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
            options, SurlServerComposition.ComposeExtendedSchemes(areExtensionsTurnedOn, null, timeProvider));
        var seams = new SurlServerCompositionSeams(
            host.CreateListenerFactory, host.CanOpenDataDirectory, host.TakeDataDirectoryLock, timeProvider, areExtensionsTurnedOn)
        {
            DataDirectoryFileSystem = host.DataDirectoryFileSystem,
            ReadStartFile = host.ReadStartFile,
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
            ? new ExchangeRecordSubscriber(start.Serving!.Events, options.MaxRecordedExchanges, options.MaxRecordedBytesPerExchange)
            : null;
        return await ServeUntilBoundAsync(commandLine, start.Serving!, options.Log, record, timeProvider, cancellationToken);
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
        TextWriter? optionsLog,
        ExchangeRecordSubscriber? record,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var log = optionsLog is null ? TextWriter.Null : TextWriter.Synchronized(optionsLog);
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
            return new SurlServer(composedServing, stop, serving, [.. bound.Task.Result.Select(ToUri)], record);
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

    private static Uri ToUri(ListenUrl boundUrl) => new(ListenerStatusLine.FormatBoundListenUrl(boundUrl));

    private async Task<SurlExitCode> StopServingAsync()
    {
        await stop.CancelAsync();
        var outcome = await serving;
        await StopRecordingAsync(record);
        await composedServing.DisposeAsync();
        stop.Dispose();
        return outcome.ExitCode;
    }
}
