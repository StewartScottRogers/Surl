using System.Globalization;
using System.Net;
using System.Runtime.ExceptionServices;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// The serving engine: starts one listener per listen URL through the listener seam, hands
/// each accepted connection or datagram flow, with a fresh <see cref="ExchangeContext"/>, to
/// the protocol server registered for the URL's scheme, and shuts down cleanly (ADR-0004;
/// exit codes from ADR-0005). It refuses a connection or flow past its
/// <see cref="ConnectionLimits"/> and cancels an exchange that idles or lasts too long
/// (ADR-0006, sections 1 and 5). A connection for a scheme that is TLS from the first byte
/// reaches its server only after the engine's handshake completed (ADR-0010, section 2).
/// </summary>
/// <remarks>
/// A listen URL whose scheme belongs to an <see cref="IConnectionProtocolServer"/> gets a
/// connection listener; one whose scheme belongs to an <see cref="IDatagramProtocolServer"/>
/// gets a datagram listener (ADR-0004, section 4). Datagram flows are served in
/// <c>ServingEngine.Datagrams.cs</c>.
/// </remarks>
public sealed partial class ServingEngine
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
    private readonly ConnectionLimits connectionLimits;
    private readonly IDataConnectionOpener dataConnectionOpener;
    private readonly ExchangeLimits exchangeLimits;
    private long lastExchangeId;

    /// <summary>
    /// How long a protocol server has to write its refusal to a connection past a connection
    /// limit before the engine closes the connection anyway (ADR-0006, section 5).
    /// </summary>
    public static readonly TimeSpan RefusalWriteDeadline = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Creates the engine with ADR-0006's default connection limits,
    /// <see cref="ConnectionLimits.Default"/>.
    /// </summary>
    /// <param name="listenerFactory">Starts the listeners.</param>
    /// <param name="protocolServers">Every protocol server, each answering the schemes it lists.</param>
    /// <param name="exchangeLogFactory">Hands out one log per exchange.</param>
    /// <param name="timeProvider">The one clock: the shutdown grace period and every exchange's time come from it.</param>
    /// <param name="shutdownGracePeriod">
    /// How long shutdown waits for exchanges in flight before cancelling them; zero or more.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Two protocol servers list the same scheme, or one takes both connections and datagram flows.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="shutdownGracePeriod"/> is negative.</exception>
    public ServingEngine(
        IListenerFactory listenerFactory,
        IReadOnlyList<IProtocolServer> protocolServers,
        IExchangeLogFactory exchangeLogFactory,
        TimeProvider timeProvider,
        TimeSpan shutdownGracePeriod)
        : this(listenerFactory, protocolServers, exchangeLogFactory, timeProvider, shutdownGracePeriod, ConnectionLimits.Default)
    {
    }

    /// <summary>
    /// Creates the engine with no data connections: every exchange's
    /// <see cref="ExchangeContext.DataConnections"/> refuses, through
    /// <see cref="RefusingDataConnectionOpener.Instance"/> (ADR-0052, decision 9).
    /// </summary>
    /// <param name="listenerFactory">Starts the listeners.</param>
    /// <param name="protocolServers">Every protocol server, each answering the schemes it lists.</param>
    /// <param name="exchangeLogFactory">Hands out one log per exchange.</param>
    /// <param name="timeProvider">The one clock: the shutdown grace period and every exchange's time come from it.</param>
    /// <param name="shutdownGracePeriod">
    /// How long shutdown waits for exchanges in flight before cancelling them; zero or more.
    /// </param>
    /// <param name="connectionLimits">
    /// How many connections the engine holds at once, and how long one exchange may idle or last.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Two protocol servers list the same scheme, or one takes both connections and datagram flows.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="shutdownGracePeriod"/> is negative.</exception>
    public ServingEngine(
        IListenerFactory listenerFactory,
        IReadOnlyList<IProtocolServer> protocolServers,
        IExchangeLogFactory exchangeLogFactory,
        TimeProvider timeProvider,
        TimeSpan shutdownGracePeriod,
        ConnectionLimits connectionLimits)
        : this(listenerFactory, protocolServers, exchangeLogFactory, timeProvider, shutdownGracePeriod, connectionLimits, RefusingDataConnectionOpener.Instance)
    {
    }

    /// <summary>
    /// Creates the engine with ADR-0006's default exchange limits,
    /// <see cref="ExchangeLimits.Default"/>, in every exchange's <see cref="ExchangeContext.Limits"/>.
    /// </summary>
    /// <param name="listenerFactory">Starts the listeners.</param>
    /// <param name="protocolServers">Every protocol server, each answering the schemes it lists.</param>
    /// <param name="exchangeLogFactory">Hands out one log per exchange.</param>
    /// <param name="timeProvider">The one clock: the shutdown grace period and every exchange's time come from it.</param>
    /// <param name="shutdownGracePeriod">
    /// How long shutdown waits for exchanges in flight before cancelling them; zero or more.
    /// </param>
    /// <param name="connectionLimits">
    /// How many connections the engine holds at once, and how long one exchange may idle or last.
    /// </param>
    /// <param name="dataConnectionOpener">
    /// Opens the FTP data connections each connection exchange asks for through its
    /// <see cref="ExchangeContext.DataConnections"/> (ADR-0052, decision 9). The engine wraps it
    /// per exchange: data bytes restart the exchange's idle clock and reach its log, and what
    /// the server leaves open is disposed when the exchange ends. Data connections do not count
    /// against <paramref name="connectionLimits"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Two protocol servers list the same scheme, or one takes both connections and datagram flows.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="shutdownGracePeriod"/> is negative.</exception>
    public ServingEngine(
        IListenerFactory listenerFactory,
        IReadOnlyList<IProtocolServer> protocolServers,
        IExchangeLogFactory exchangeLogFactory,
        TimeProvider timeProvider,
        TimeSpan shutdownGracePeriod,
        ConnectionLimits connectionLimits,
        IDataConnectionOpener dataConnectionOpener)
        : this(listenerFactory, protocolServers, exchangeLogFactory, timeProvider, shutdownGracePeriod, connectionLimits, dataConnectionOpener, ExchangeLimits.Default)
    {
    }

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
    /// <param name="connectionLimits">
    /// How many connections the engine holds at once, and how long one exchange may idle or last.
    /// </param>
    /// <param name="dataConnectionOpener">
    /// Opens the FTP data connections each connection exchange asks for through its
    /// <see cref="ExchangeContext.DataConnections"/> (ADR-0052, decision 9). The engine wraps it
    /// per exchange: data bytes restart the exchange's idle clock and reach its log, and what
    /// the server leaves open is disposed when the exchange ends. Data connections do not count
    /// against <paramref name="connectionLimits"/>.
    /// </param>
    /// <param name="exchangeLimits">
    /// The limits every exchange's <see cref="ExchangeContext.Limits"/> holds, as the command line
    /// gave them (ADR-0006, sections 1 and 6); the engine's own TLS handshake on a listen URL that
    /// is TLS from the first byte runs within their head timeout.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Two protocol servers list the same scheme, or one takes both connections and datagram flows.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="shutdownGracePeriod"/> is negative.</exception>
    public ServingEngine(
        IListenerFactory listenerFactory,
        IReadOnlyList<IProtocolServer> protocolServers,
        IExchangeLogFactory exchangeLogFactory,
        TimeProvider timeProvider,
        TimeSpan shutdownGracePeriod,
        ConnectionLimits connectionLimits,
        IDataConnectionOpener dataConnectionOpener,
        ExchangeLimits exchangeLimits)
    {
        ArgumentNullException.ThrowIfNull(listenerFactory);
        ArgumentNullException.ThrowIfNull(protocolServers);
        ArgumentNullException.ThrowIfNull(exchangeLogFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(connectionLimits);
        ArgumentNullException.ThrowIfNull(dataConnectionOpener);
        ArgumentNullException.ThrowIfNull(exchangeLimits);
        ArgumentOutOfRangeException.ThrowIfLessThan(shutdownGracePeriod, TimeSpan.Zero);

        this.listenerFactory = listenerFactory;
        serversByScheme = MapServersByScheme(protocolServers);
        this.exchangeLogFactory = exchangeLogFactory;
        this.timeProvider = timeProvider;
        this.shutdownGracePeriod = shutdownGracePeriod;
        this.connectionLimits = connectionLimits;
        this.dataConnectionOpener = dataConnectionOpener;
        this.exchangeLimits = exchangeLimits;
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
    /// URL's scheme has no connection or datagram server; <see cref="SurlExitCode.CouldNotResolveHost"/> or
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

        var servers = FindServers(listenUrls);

        if (servers is null)
        {
            return SurlExitCode.UnsupportedProtocol;
        }

        var started = new List<StartedListener>();
        var startupFailure = await CaptureFailureAsync(() => StartListenersAsync(listenUrls, servers, started, cancellationToken));

        if (startupFailure is not null)
        {
            // A listener that fails to stop does not hide why startup failed.
            _ = await StopListenersAsync(started);

            return MapStartupFailure(startupFailure.SourceException, cancellationToken) ?? await RethrowAsync(startupFailure);
        }

        await ServeUntilCancelledAsync(started, cancellationToken);

        return SurlExitCode.Ok;
    }

    private static Dictionary<string, IProtocolServer> MapServersByScheme(IReadOnlyList<IProtocolServer> protocolServers)
    {
        RefuseServersThatTakeBothTransports(protocolServers);

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

    // A server takes connections or flows, never both (ADR-0004, section 4).
    private static void RefuseServersThatTakeBothTransports(IReadOnlyList<IProtocolServer> protocolServers)
    {
        foreach (var server in protocolServers)
        {
            if (server is IConnectionProtocolServer and IDatagramProtocolServer)
            {
                throw new ArgumentException(
                    $"The protocol server for '{string.Join("', '", server.Schemes)}' takes both connections and datagram flows.",
                    nameof(protocolServers));
            }
        }
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
    private static async Task<ExceptionDispatchInfo?> StopListenersAsync(IEnumerable<StartedListener> listeners)
    {
        ExceptionDispatchInfo? firstFailure = null;

        foreach (var listener in listeners)
        {
            var failure = await CaptureFailureAsync(() => listener.Listener.DisposeAsync().AsTask());

            firstFailure ??= failure;
        }

        return firstFailure;
    }

    // An exchange the engine cancelled ends gracefully, whatever limit cancelled it (ADR-0006,
    // section 5), and so does one whose TLS handshake failed; a server that threw for any other
    // reason failed the exchange, and the caller aborts its connection.
    private bool NoteHowTheExchangeEnded(
        Exception? failure, IExchangeLog log, long exchangeId, ExchangeCancellation? cancellation)
    {
        var (note, failed) = DescribeHowTheExchangeEnded(failure, exchangeId, cancellation);

        if (note is not null)
        {
            log.Note(note);
        }

        return failed;
    }

    // A server's own STARTTLS or AUTH TLS upgrade that failed is the client's handshake
    // failing, not a server fault (ADR-0010, section 2), so its connection is only closed.
    private (string? Note, bool Failed) DescribeHowTheExchangeEnded(
        Exception? failure, long exchangeId, ExchangeCancellation? cancellation) =>
        (failure, cancellation) switch
        {
            (null or OperationCanceledException, { } reason) => (DescribeCancellation(exchangeId, reason), false),
            (null, null) => (null, false),
            (TlsHandshakeException handshakeFailure, _) => ($"TLS handshake failed: {handshakeFailure.Message}", false),
            ({ } thrown, _) =>
                ($"Exchange {exchangeId} ended because the protocol server threw {thrown.GetType().Name}: {thrown.Message}", true),
        };

    private string DescribeCancellation(long exchangeId, ExchangeCancellation cancellation) =>
        cancellation switch
        {
            ExchangeCancellation.IdleTimeout =>
                $"Exchange {exchangeId} cancelled: no byte moved for the idle timeout of {FormatSeconds(connectionLimits.IdleTimeout)} s.",
            ExchangeCancellation.MaxExchangeDuration =>
                $"Exchange {exchangeId} cancelled: it reached the maximum exchange duration of {FormatSeconds(connectionLimits.MaxExchangeDuration)} s.",
            _ => $"Exchange {exchangeId} cancelled at shutdown.",
        };

    private static string FormatSeconds(TimeSpan duration) =>
        duration.TotalSeconds.ToString(CultureInfo.InvariantCulture);

    // Each listen URL's server, or null when a scheme has none that takes connections or flows.
    private IProtocolServer[]? FindServers(IReadOnlyList<ListenUrl> listenUrls)
    {
        var servers = new IProtocolServer[listenUrls.Count];

        for (var index = 0; index < servers.Length; index++)
        {
            var server = serversByScheme.GetValueOrDefault(listenUrls[index].Scheme);

            if (server is not (IConnectionProtocolServer or IDatagramProtocolServer))
            {
                return null;
            }

            servers[index] = server;
        }

        return servers;
    }

    private async Task StartListenersAsync(
        IReadOnlyList<ListenUrl> listenUrls,
        IProtocolServer[] servers,
        List<StartedListener> started,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < servers.Length; index++)
        {
            started.Add(servers[index] is IDatagramProtocolServer datagramServer
                ? await StartDatagramListenerAsync(listenUrls[index], datagramServer, cancellationToken)
                : await StartConnectionListenerAsync(listenUrls[index], (IConnectionProtocolServer)servers[index], cancellationToken));
        }
    }

    private async Task<StartedListener> StartConnectionListenerAsync(
        ListenUrl listenUrl, IConnectionProtocolServer server, CancellationToken cancellationToken)
    {
        var listener = await listenerFactory.StartConnectionListenerAsync(listenUrl, cancellationToken);

        return new StartedListener(
            listener,
            state =>
            {
                var route = new AcceptedConnectionRoute(listener.ListenUrl, server, state.Admission, state.ShutdownToken);

                return AcceptUntilStoppedAsync(token => AcceptConnectionAsync(listener, route, token), state);
            });
    }

    private async Task ServeUntilCancelledAsync(List<StartedListener> listeners, CancellationToken cancellationToken)
    {
        var stopAccepting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var giveUpOnExchanges = new CancellationTokenSource();
        var inFlight = new InFlightExchanges();
        var state = new ServingState(
            new ConnectionAdmission(connectionLimits.MaxConnections, connectionLimits.MaxConnectionsPerAddress),
            inFlight,
            stopAccepting,
            giveUpOnExchanges.Token);
        var acceptLoops = listeners.Select(listener => listener.AcceptUntilStoppedAsync(state)).ToArray();

        var acceptFailure = await CaptureFailureAsync(() => Task.WhenAll(acceptLoops));

        var stopFailure = await StopListenersAsync(listeners);

        await inFlight.WaitForAllAsync(shutdownGracePeriod, timeProvider, giveUpOnExchanges);
        stopAccepting.Dispose();
        giveUpOnExchanges.Dispose();
        (acceptFailure ?? stopFailure)?.Throw();
    }

    // Accepts until stopAccepting is cancelled, starting each exchange or refusal the listener
    // hands over. A listener that fails to accept cancels it, so every other listener stops
    // too, and the failure reaches the caller of ServeAsync.
    private static async Task AcceptUntilStoppedAsync(
        Func<CancellationToken, Task<Func<Task>>> acceptNextAsync, ServingState state)
    {
        try
        {
            while (true)
            {
                state.InFlight.Start(await acceptNextAsync(state.StopAccepting.Token));
            }
        }
        catch (OperationCanceledException) when (state.StopAccepting.IsCancellationRequested)
        {
        }
        catch
        {
            state.StopAccepting.Cancel();
            throw;
        }
    }

    // Accepts one connection and hands back what to run for it. A connection past a connection
    // limit is refused, uncounted, and never becomes an exchange.
    private async Task<Func<Task>> AcceptConnectionAsync(
        IConnectionListener listener, AcceptedConnectionRoute route, CancellationToken cancellationToken)
    {
        var connection = await listener.AcceptAsync(cancellationToken);
        var remoteEndPoint = connection.RemoteEndPoint;

        return route.Admission.TryAdmit(remoteEndPoint) is { } refusal
            ? () => RefuseAsync(connection, route, refusal)
            : () => RunExchangeAsync(connection, remoteEndPoint, route);
    }

    // A plaintext connection gets the server's refusal, if it writes one, within the refusal
    // deadline; a secure one is closed before any handshake (ADR-0006, section 5). The
    // connection is aborted only when the refusal writer failed for a reason other than the
    // deadline or shutdown.
    private async Task RefuseAsync(IConnection connection, AcceptedConnectionRoute route, ConnectionRefusal refusal)
    {
        NoteRefusal("a connection", connection.RemoteEndPoint, refusal);

        if (route.Server is IConnectionRefusalWriter writer && !TlsSchemes.IsImplicitTls(route.ListenUrl.Scheme))
        {
            var failure = await CaptureFailureAsync(() => WriteRefusalWithinDeadlineAsync(
                token => writer.WriteRefusalAsync(connection, refusal, token), route.ShutdownToken));

            if (failure is { SourceException: not OperationCanceledException })
            {
                connection.Abort();
            }
        }

        await CaptureFailureAsync(() => connection.DisposeAsync().AsTask());
    }

    // A refusal has no exchange, so its note goes to the log outside any exchange (ADR-0028).
    // A log that fails to take it does not stop the refusal.
    private void NoteRefusal(string what, EndPoint remoteEndPoint, ConnectionRefusal refusal)
    {
        var limit = refusal == ConnectionRefusal.TooManyConnectionsFromAddress
            ? $"--max-connections-per-address {connectionLimits.MaxConnectionsPerAddress}"
            : $"--max-connections {connectionLimits.MaxConnections}";

        try
        {
            exchangeLogFactory.NoteOutsideExchange($"Refused {what} from {remoteEndPoint}: past {limit}.");
        }
        catch (Exception)
        {
        }
    }

    // The engine stops waiting at the deadline or shutdown even for a writer that ignores the
    // token, so a refused connection or flow is never held longer than that.
    private async Task WriteRefusalWithinDeadlineAsync(
        Func<CancellationToken, ValueTask> writeRefusalAsync, CancellationToken shutdownToken)
    {
        using var deadline = new CancellationTokenSource(RefusalWriteDeadline, timeProvider);
        using var deadlineOrShutdown = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, shutdownToken);

        await writeRefusalAsync(deadlineOrShutdown.Token).AsTask().WaitAsync(deadlineOrShutdown.Token);
    }

    // Whatever the server or the log throws, the connection is disposed, and aborted first when
    // the exchange failed; then, whatever happened, it stops counting against the connection
    // limits.
    private async Task RunExchangeAsync(IConnection connection, EndPoint remoteEndPoint, AcceptedConnectionRoute route)
    {
        try
        {
            using var deadlines = new ExchangeDeadlines(connectionLimits, timeProvider, route.ShutdownToken);
            var logFailure = await CaptureFailureAsync(
                () => ServeAndLogExchangeAsync(connection, route, deadlines));

            if (logFailure is not null)
            {
                connection.Abort();
            }

            await CaptureFailureAsync(() => connection.DisposeAsync().AsTask());
        }
        finally
        {
            route.Admission.Release(remoteEndPoint);
        }
    }

    private async Task ServeAndLogExchangeAsync(
        IConnection connection, AcceptedConnectionRoute route, ExchangeDeadlines deadlines)
    {
        var (exchangeId, log, context) = OpenExchange(route.ListenUrl, connection.LocalEndPoint, connection.RemoteEndPoint, deadlines);

        var recordingConnection = new RecordingConnection(connection, log);
        var dataConnections = new ExchangeDataConnections(dataConnectionOpener, log, deadlines);
        var failure = await CaptureFailureAsync(() => SecureThenServeAsync(
            recordingConnection, route, deadlines, context with { DataConnections = dataConnections }));

        // Never throws: a data connection that fails to dispose does not stop the exchange ending.
        await dataConnections.DisposeAsync();

        if (NoteHowTheExchangeEnded(failure?.SourceException, log, exchangeId, deadlines.Reason))
        {
            connection.Abort();
        }

        log.Note($"Exchange {exchangeId} ended; closing the connection.");
    }

    // A listen URL whose scheme is TLS from the first byte gets its handshake here, and the
    // server sees the connection only once it completed (ADR-0010, section 2).
    private async Task SecureThenServeAsync(
        RecordingConnection connection, AcceptedConnectionRoute route, ExchangeDeadlines deadlines, ExchangeContext context)
    {
        if (TlsSchemes.IsImplicitTls(route.ListenUrl.Scheme) && !await CompleteImplicitHandshakeAsync(connection, context))
        {
            return;
        }

        await route.Server.ServeAsync(new IdleClockRestartingConnection(connection, deadlines), context);
    }

    // Runs the handshake within the head timeout (ADR-0006, section 4) and notes how it went;
    // true when it completed. Cancellation of the exchange itself escapes, so the exchange's
    // own cancellation note follows.
    private async Task<bool> CompleteImplicitHandshakeAsync(IConnection connection, ExchangeContext context)
    {
        using var headTimeout = new CancellationTokenSource(context.Limits.HeadTimeout, timeProvider);
        using var headTimeoutOrExchangeEnd = CancellationTokenSource.CreateLinkedTokenSource(
            headTimeout.Token, context.CancellationToken);

        try
        {
            var session = await connection.UpgradeToTlsAsync(headTimeoutOrExchangeEnd.Token);

            context.Log.Note(
                $"TLS handshake completed: {session.Protocol}, {session.CipherSuite}, ALPN {session.ApplicationProtocol ?? "none"}");

            return true;
        }
        catch (IOException failure)
        {
            context.Log.Note($"TLS handshake failed: {failure.Message}");
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note(
                $"TLS handshake failed: no handshake within the head timeout of {FormatSeconds(context.Limits.HeadTimeout)} s");
        }

        return false;
    }

    // Numbers the exchange, creates its log and context, and notes that it opened.
    private (long ExchangeId, IExchangeLog Log, ExchangeContext Context) OpenExchange(
        ListenUrl listenUrl, EndPoint localEndPoint, EndPoint remoteEndPoint, ExchangeDeadlines deadlines)
    {
        var exchangeId = Interlocked.Increment(ref lastExchangeId);
        var log = exchangeLogFactory.Create(exchangeId, remoteEndPoint);
        var context = new ExchangeContext(
            exchangeId, listenUrl, localEndPoint, remoteEndPoint, log, timeProvider, deadlines.Token)
        {
            Limits = exchangeLimits,
            ShutdownToken = deadlines.ShutdownToken,
        };

        log.Note($"Exchange {exchangeId} opened: {listenUrl.Scheme} from {remoteEndPoint}.");

        return (exchangeId, log, context);
    }

    // A started listener, and the loop that accepts from it until the engine stops accepting.
    private sealed record StartedListener(IAsyncDisposable Listener, Func<ServingState, Task> AcceptUntilStoppedAsync);

    // What every listener's exchanges share while the engine serves: the counts they are
    // admitted against, the exchanges in flight, the source cancelled to stop accepting, and
    // the token cancelled when the engine gives up on the exchanges at shutdown.
    private sealed record ServingState(
        ConnectionAdmission Admission,
        InFlightExchanges InFlight,
        CancellationTokenSource StopAccepting,
        CancellationToken ShutdownToken);

    // Where one listener's accepted connections go: the listen URL and server they are served
    // under, the counts they are admitted against, and the token cancelled at shutdown.
    private sealed record AcceptedConnectionRoute(
        ListenUrl ListenUrl,
        IConnectionProtocolServer Server,
        ConnectionAdmission Admission,
        CancellationToken ShutdownToken);
}
