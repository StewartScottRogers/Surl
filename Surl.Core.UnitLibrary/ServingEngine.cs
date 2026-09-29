using System.Runtime.ExceptionServices;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// The serving engine: starts one listener per listen URL through the listener seam, hands
/// each accepted connection, with a fresh <see cref="ExchangeContext"/>, to the protocol
/// server registered for the URL's scheme, and shuts down cleanly (ADR-0004; exit codes
/// from ADR-0005).
/// </summary>
/// <remarks>
/// This engine serves stream-oriented connections. A listen URL whose scheme belongs to an
/// <see cref="IDatagramProtocolServer"/> is refused as unsupported until datagram flows are
/// dispatched (BL-032).
/// </remarks>
public sealed class ServingEngine
{
    /// <summary>
    /// How long shutdown waits for exchanges in flight before cancelling them, when the
    /// caller has no reason to choose otherwise.
    /// </summary>
    public static readonly TimeSpan DefaultShutdownGracePeriod = TimeSpan.FromSeconds(5);

    private readonly IListenerFactory listenerFactory;
    private readonly Dictionary<string, IProtocolServer> serversByScheme;
    private readonly IExchangeLogFactory exchangeLogFactory;
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan shutdownGracePeriod;
    private long lastExchangeId;

    /// <summary>
    /// Creates the engine.
    /// </summary>
    /// <param name="listenerFactory">Starts the listeners.</param>
    /// <param name="protocolServers">Every protocol server, each answering the schemes it lists.</param>
    /// <param name="exchangeLogFactory">Hands out one log per exchange.</param>
    /// <param name="timeProvider">The one clock: the shutdown grace period and every exchange's time come from it.</param>
    /// <param name="shutdownGracePeriod">
    /// How long shutdown waits for exchanges in flight before cancelling them; zero or more.
    /// </param>
    /// <exception cref="ArgumentException">Two protocol servers list the same scheme.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="shutdownGracePeriod"/> is negative.</exception>
    public ServingEngine(
        IListenerFactory listenerFactory,
        IReadOnlyList<IProtocolServer> protocolServers,
        IExchangeLogFactory exchangeLogFactory,
        TimeProvider timeProvider,
        TimeSpan shutdownGracePeriod)
    {
        ArgumentNullException.ThrowIfNull(listenerFactory);
        ArgumentNullException.ThrowIfNull(protocolServers);
        ArgumentNullException.ThrowIfNull(exchangeLogFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThan(shutdownGracePeriod, TimeSpan.Zero);

        this.listenerFactory = listenerFactory;
        serversByScheme = MapServersByScheme(protocolServers);
        this.exchangeLogFactory = exchangeLogFactory;
        this.timeProvider = timeProvider;
        this.shutdownGracePeriod = shutdownGracePeriod;
    }

    /// <summary>
    /// Listens on every listen URL and serves until <paramref name="cancellationToken"/> is
    /// cancelled, then stops accepting, waits up to the shutdown grace period for the exchanges
    /// in flight, cancels any still running, and waits for them to end.
    /// </summary>
    /// <param name="listenUrls">What to listen on; at least one.</param>
    /// <param name="cancellationToken">Cancelled to stop serving, as by Ctrl+C or SIGTERM.</param>
    /// <returns>
    /// <see cref="SurlExitCode.Ok"/> once stopped, cancellation during startup included;
    /// <see cref="SurlExitCode.UnsupportedProtocol"/>, before any listener starts, when a listen
    /// URL's scheme has no connection server; <see cref="SurlExitCode.CouldNotResolveHost"/> or
    /// <see cref="SurlExitCode.BindFailed"/> when a listener cannot bind, after stopping every
    /// listener already started.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="listenUrls"/> is empty.</exception>
    public async Task<SurlExitCode> ServeAsync(IReadOnlyList<ListenUrl> listenUrls, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(listenUrls);

        if (listenUrls.Count == 0)
        {
            throw new ArgumentException("The engine needs at least one listen URL.", nameof(listenUrls));
        }

        var servers = FindConnectionServers(listenUrls);

        if (servers is null)
        {
            return SurlExitCode.UnsupportedProtocol;
        }

        var started = new List<IConnectionListener>();
        var startupFailure = await CaptureFailureAsync(() => StartListenersAsync(listenUrls, started, cancellationToken));

        if (startupFailure is not null)
        {
            // A listener that fails to stop does not hide why startup failed.
            _ = await StopListenersAsync(started);

            return MapStartupFailure(startupFailure.SourceException, cancellationToken) ?? await RethrowAsync(startupFailure);
        }

        await ServeUntilCancelledAsync(started, servers, cancellationToken);

        return SurlExitCode.Ok;
    }

    private static Dictionary<string, IProtocolServer> MapServersByScheme(IReadOnlyList<IProtocolServer> protocolServers)
    {
        var serversByScheme = new Dictionary<string, IProtocolServer>(StringComparer.Ordinal);

        foreach (var scheme in protocolServers.SelectMany(server => server.Schemes, (server, scheme) => (server, scheme)))
        {
            if (!serversByScheme.TryAdd(scheme.scheme, scheme.server))
            {
                throw new ArgumentException(
                    $"Two protocol servers list the scheme '{scheme.scheme}'.", nameof(protocolServers));
            }
        }

        return serversByScheme;
    }

    // Runs work and hands back what it threw instead of throwing it, so the caller cleans up
    // with an await outside any catch or finally block.
    private static async Task<ExceptionDispatchInfo?> CaptureFailureAsync(Func<Task> work)
    {
        try
        {
            await work();

            return null;
        }
        catch (Exception exception)
        {
            return ExceptionDispatchInfo.Capture(exception);
        }
    }

    // A bind failure and the caller's own cancellation are outcomes with an exit code (ADR-0005);
    // anything else has none.
    private static SurlExitCode? MapStartupFailure(Exception failure, CancellationToken cancellationToken) =>
        failure switch
        {
            ListenerBindException bindFailure => MapBindFailure(bindFailure.Failure),
            OperationCanceledException when cancellationToken.IsCancellationRequested => SurlExitCode.Ok,
            _ => null,
        };

    // Awaiting the returned task rethrows the failure with its original stack trace.
    private static Task<SurlExitCode> RethrowAsync(ExceptionDispatchInfo failure) =>
        Task.FromException<SurlExitCode>(failure.SourceException);

    private static SurlExitCode MapBindFailure(ListenerBindFailure failure) =>
        failure == ListenerBindFailure.HostNotFound ? SurlExitCode.CouldNotResolveHost : SurlExitCode.BindFailed;

    // Disposes every listener, even after one fails to dispose, and hands back the first failure.
    private static async Task<ExceptionDispatchInfo?> StopListenersAsync(IEnumerable<IConnectionListener> listeners)
    {
        ExceptionDispatchInfo? firstFailure = null;

        foreach (var listener in listeners)
        {
            var failure = await CaptureFailureAsync(() => listener.DisposeAsync().AsTask());

            firstFailure ??= failure;
        }

        return firstFailure;
    }

    private static void NoteHowTheExchangeEnded(
        Exception? failure, IConnection connection, IExchangeLog log, long exchangeId, CancellationToken cancellationToken)
    {
        if (failure is null)
        {
            return;
        }

        if (failure is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            log.Note($"Exchange {exchangeId} cancelled at shutdown.");

            return;
        }

        connection.Abort();
        log.Note($"Exchange {exchangeId} ended because the protocol server threw {failure.GetType().Name}: {failure.Message}");
    }

    private IConnectionProtocolServer[]? FindConnectionServers(IReadOnlyList<ListenUrl> listenUrls)
    {
        var servers = new IConnectionProtocolServer[listenUrls.Count];

        for (var index = 0; index < servers.Length; index++)
        {
            if (serversByScheme.GetValueOrDefault(listenUrls[index].Scheme) is not IConnectionProtocolServer server)
            {
                return null;
            }

            servers[index] = server;
        }

        return servers;
    }

    private async Task StartListenersAsync(
        IReadOnlyList<ListenUrl> listenUrls, List<IConnectionListener> started, CancellationToken cancellationToken)
    {
        foreach (var listenUrl in listenUrls)
        {
            started.Add(await listenerFactory.StartConnectionListenerAsync(listenUrl, cancellationToken));
        }
    }

    private async Task ServeUntilCancelledAsync(
        List<IConnectionListener> listeners, IConnectionProtocolServer[] servers, CancellationToken cancellationToken)
    {
        var stopAccepting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var giveUpOnExchanges = new CancellationTokenSource();
        var inFlight = new InFlightExchanges();
        var acceptLoops = listeners
            .Select((listener, index) => AcceptUntilStoppedAsync(
                listener, servers[index], inFlight, stopAccepting, giveUpOnExchanges.Token))
            .ToArray();

        var acceptFailure = await CaptureFailureAsync(() => Task.WhenAll(acceptLoops));

        var stopFailure = await StopListenersAsync(listeners);

        await inFlight.WaitForAllAsync(shutdownGracePeriod, timeProvider, giveUpOnExchanges);
        stopAccepting.Dispose();
        giveUpOnExchanges.Dispose();
        (acceptFailure ?? stopFailure)?.Throw();
    }

    // Accepts until stopAccepting is cancelled. A listener that fails to accept cancels it, so
    // every other listener stops too, and the failure reaches the caller of ServeAsync.
    private async Task AcceptUntilStoppedAsync(
        IConnectionListener listener,
        IConnectionProtocolServer server,
        InFlightExchanges inFlight,
        CancellationTokenSource stopAccepting,
        CancellationToken exchangeCancellationToken)
    {
        try
        {
            while (true)
            {
                var connection = await listener.AcceptAsync(stopAccepting.Token);

                inFlight.Start(() => RunExchangeAsync(connection, listener.ListenUrl, server, exchangeCancellationToken));
            }
        }
        catch (OperationCanceledException) when (stopAccepting.IsCancellationRequested)
        {
        }
        catch
        {
            stopAccepting.Cancel();
            throw;
        }
    }

    // Whatever the server or the log throws, the connection is disposed, and aborted first when
    // the exchange failed.
    private async Task RunExchangeAsync(
        IConnection connection, ListenUrl listenUrl, IConnectionProtocolServer server, CancellationToken cancellationToken)
    {
        var logFailure = await CaptureFailureAsync(
            () => ServeAndLogExchangeAsync(connection, listenUrl, server, cancellationToken));

        if (logFailure is not null)
        {
            connection.Abort();
        }

        await CaptureFailureAsync(() => connection.DisposeAsync().AsTask());
    }

    private async Task ServeAndLogExchangeAsync(
        IConnection connection, ListenUrl listenUrl, IConnectionProtocolServer server, CancellationToken cancellationToken)
    {
        var exchangeId = Interlocked.Increment(ref lastExchangeId);
        var log = exchangeLogFactory.Create(exchangeId, connection.RemoteEndPoint);
        var context = new ExchangeContext(
            exchangeId, listenUrl, connection.LocalEndPoint, connection.RemoteEndPoint, log, timeProvider, cancellationToken);

        log.Note($"Exchange {exchangeId} opened: {listenUrl.Scheme} from {connection.RemoteEndPoint}.");

        var failure = await CaptureFailureAsync(() => server.ServeAsync(new RecordingConnection(connection, log), context));

        NoteHowTheExchangeEnded(failure?.SourceException, connection, log, exchangeId, cancellationToken);
        log.Note($"Exchange {exchangeId} ended; closing the connection.");
    }
}
