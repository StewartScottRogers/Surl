using Surl.Protocol.Abstractions;
using Surl.Quic;

namespace Surl.Http3;

/// <summary>
/// One connection's extended <c>CONNECT</c> registrations and open sessions (ADR-0086 decision 3):
/// which handler serves a <c>:protocol</c>, a bidirectional stream signal or a unidirectional stream
/// type; which session an HTTP datagram or a claimed stream names; the HTTP datagrams held for a
/// session not open yet (ADR-0117 decision 3); the client's <c>SETTINGS</c>; a handler's request to
/// close the connection; and the start of shutdown.
/// </summary>
/// <param name="handlers">The server's registrations; two for one <c>:protocol</c> throw <see cref="ArgumentException"/>.</param>
/// <param name="log">The exchange log.</param>
/// <param name="time">The exchange's clock, which times how long a datagram is held.</param>
/// <param name="holdTime">How long a datagram for no open session is held: the head timeout, <see cref="Timeout.InfiniteTimeSpan"/> for no limit.</param>
internal sealed class Http3ExtendedConnect(IReadOnlyList<IHttp3ExtendedConnectHandler> handlers, IExchangeLog log, TimeProvider time, TimeSpan holdTime)
{
    /// <summary>The largest Quarter Stream ID (RFC 9297 section 2.1): 2^60 - 1.</summary>
    public const ulong MaxQuarterStreamId = (1UL << 60) - 1;

    /// <summary>How many HTTP datagrams a connection holds for sessions not open yet (ADR-0117 decision 3); one past it is dropped.</summary>
    public const int HeldDatagramCapacity = 64;

    private readonly Dictionary<string, IHttp3ExtendedConnectHandler> byProtocol = handlers.ToDictionary(handler => handler.Protocol, StringComparer.Ordinal);
    private readonly Dictionary<ulong, Http3ExtendedConnectSession> sessions = [];
    private readonly List<(ulong SessionId, byte[] Payload, DateTimeOffset Arrival)> held = [];
    private readonly TaskCompletionSource<IReadOnlyDictionary<ulong, ulong>> clientSettings = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<Http3ConnectionException> closeRequest = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TimeSpan holdTime = Http3HeadTimeoutDelay.For(holdTime);
    private readonly HashSet<ulong> undecided = [];
    private readonly Lock gate = new();
    private bool isShuttingDown;
    private ulong nextUnacceptedStreamId;

    /// <summary>Whether any handler is registered: the server then accepts <c>:protocol</c> and HTTP datagrams.</summary>
    public bool IsEnabled => byProtocol.Count > 0;

    /// <summary>The handler registered for <paramref name="protocol"/>, or <see langword="null"/>.</summary>
    /// <param name="protocol">A <c>:protocol</c> value.</param>
    /// <returns>The handler, or <see langword="null"/>.</returns>
    public IHttp3ExtendedConnectHandler? HandlerForProtocol(string protocol) => byProtocol.GetValueOrDefault(protocol);

    /// <summary>The first handler that registered <paramref name="signal"/> as its bidirectional stream signal, or <see langword="null"/>.</summary>
    /// <param name="signal">The first integer of a client bidirectional stream.</param>
    /// <returns>The handler, or <see langword="null"/>.</returns>
    public IHttp3ExtendedConnectHandler? HandlerForSignal(ulong signal) => handlers.FirstOrDefault(handler => handler.BidirectionalStreamSignal == signal);

    /// <summary>The first handler that registered <paramref name="type"/> as its unidirectional stream type, or <see langword="null"/>.</summary>
    /// <param name="type">A client unidirectional stream's type.</param>
    /// <returns>The handler, or <see langword="null"/>.</returns>
    public IHttp3ExtendedConnectHandler? HandlerForStreamType(ulong type) => handlers.FirstOrDefault(handler => handler.UnidirectionalStreamType == type);

    /// <summary>The <c>SETTINGS</c> the registrations add (RFC 9220 section 3, RFC 9297 section 2.1.1): none, or <c>ENABLE_CONNECT_PROTOCOL</c> and <c>H3_DATAGRAM</c> both 1.</summary>
    /// <returns>The identifier and value pairs.</returns>
    public KeyValuePair<ulong, ulong>[] Settings() =>
        IsEnabled ? [new(Http3FrameWriter.EnableConnectProtocolSetting, 1), new(Http3FrameWriter.H3DatagramSetting, 1)] : [];

    /// <summary>Records the client's <c>SETTINGS</c>, once its control stream delivered them.</summary>
    /// <param name="settings">Identifier to value.</param>
    public void ReceiveClientSettings(IReadOnlyDictionary<ulong, ulong> settings) => clientSettings.TrySetResult(settings);

    /// <summary>Waits for the client's <c>SETTINGS</c>.</summary>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>Identifier to value.</returns>
    public Task<IReadOnlyDictionary<ulong, ulong>> ReadClientSettingsAsync(CancellationToken cancellationToken) =>
        clientSettings.Task.WaitAsync(cancellationToken);

    /// <summary>Asks the connection to close with <paramref name="code"/>; the first request wins.</summary>
    /// <param name="code">The code <c>CONNECTION_CLOSE</c> carries.</param>
    /// <param name="reason">Why, for the exchange log.</param>
    public void RequestConnectionClose(Http3ErrorCode code, string reason) =>
        closeRequest.TrySetResult(new Http3ConnectionException(code, reason));

    /// <summary>Waits for a handler's request to close the connection, then throws it for the connection to close with.</summary>
    /// <param name="cancellationToken">Cancelled when the connection ends or fails.</param>
    /// <returns>A task that never completes successfully.</returns>
    /// <exception cref="Http3ConnectionException">A handler asked to close the connection.</exception>
    public async Task ThrowWhenConnectionCloseRequestedAsync(CancellationToken cancellationToken) =>
        throw await closeRequest.Task.WaitAsync(cancellationToken);

    /// <summary>
    /// Starts shutdown: every open session is marked shutting down and its shutdown writes run, and
    /// every session that opens later starts marked; returns once the writes are done.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the connection ends or fails.</param>
    /// <returns>A task that completes when every open session's shutdown writes are done.</returns>
    public async Task ShutDownAsync(CancellationToken cancellationToken)
    {
        Http3ExtendedConnectSession[] open;
        lock (gate)
        {
            isShuttingDown = true;
            open = [.. sessions.Values];
        }

        await Task.WhenAll(open.Select(session => ShutDownSessionAsync(session, cancellationToken)));
    }

    /// <summary>
    /// Records that the client opened request stream <paramref name="streamId"/>: until it is decided
    /// (<see cref="Decide"/>, or its session opens), datagrams naming it are held; after, they are
    /// dropped unless its session is open.
    /// </summary>
    /// <param name="streamId">A client bidirectional stream ID.</param>
    public void Accept(ulong streamId)
    {
        lock (gate)
        {
            undecided.Add(streamId);
            nextUnacceptedStreamId = Math.Max(nextUnacceptedStreamId, streamId + 4);
        }
    }

    /// <summary>
    /// Records that request stream <paramref name="streamId"/> is not, or is no longer, an open
    /// session: the datagrams held for it are dropped, and so are later ones.
    /// </summary>
    /// <param name="streamId">A client bidirectional stream ID.</param>
    public void Decide(ulong streamId)
    {
        lock (gate)
        {
            if (undecided.Remove(streamId) && held.RemoveAll(datagram => datagram.SessionId == streamId) is var dropped and > 0)
            {
                log.Note($"{dropped} HTTP datagram(s) held for stream {streamId} were dropped: it is no session.");
            }
        }
    }

    /// <summary>
    /// Serves <paramref name="session"/> with its handler, open for datagrams and streams until the
    /// handler returns. The datagrams held for it are handed over first, in the order they arrived.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="cancellationToken">Cancelled when the connection ends, fails, or its shutdown grace has passed.</param>
    /// <returns>A task that completes when the handler has returned.</returns>
    public async Task ServeAsync(Http3ExtendedConnectSession session, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            sessions[session.Id] = session;
            undecided.Remove(session.Id);
            if (isShuttingDown)
            {
                session.MarkShuttingDown();
            }

            DropExpiredDatagrams();
            foreach (var datagram in held.Where(datagram => datagram.SessionId == session.Id))
            {
                session.Deliver(datagram.Payload);
            }

            held.RemoveAll(datagram => datagram.SessionId == session.Id);
        }

        try
        {
            await session.Handler.ServeSessionAsync(session, cancellationToken);
        }
        finally
        {
            lock (gate)
            {
                sessions.Remove(session.Id);
            }

            session.End();
        }
    }

    /// <summary>
    /// Hands a claimed stream to the open session of <paramref name="handler"/> its header names, or
    /// else to <paramref name="handler"/>'s <see cref="IHttp3ExtendedConnectHandler.ServeStreamWithoutSessionAsync"/>.
    /// </summary>
    /// <param name="handler">The handler whose signal or stream type opened the stream.</param>
    /// <param name="stream">The stream, read past its header.</param>
    /// <param name="cancellationToken">Cancelled when the connection ends, fails, or its shutdown grace has passed.</param>
    /// <returns>A task that completes once the stream is handed over.</returns>
    /// <exception cref="Http3ConnectionException">The session ID is not a client-initiated bidirectional stream ID (<c>H3_ID_ERROR</c>).</exception>
    public async Task HandOverAsync(IHttp3ExtendedConnectHandler handler, Http3SessionStream stream, CancellationToken cancellationToken)
    {
        if (stream.SessionId % 4 != 0)
        {
            throw new Http3ConnectionException(Http3ErrorCode.IdError, $"stream {stream.Id} names session {stream.SessionId}, which is not a client-initiated bidirectional stream");
        }

        if (OpenSession(stream.SessionId) is { } session && session.Handler == handler)
        {
            session.Deliver(stream);
            return;
        }

        await handler.ServeStreamWithoutSessionAsync(stream, cancellationToken);
    }

    /// <summary>
    /// Hands an HTTP datagram's payload to the session its quarter stream ID names (RFC 9297 section
    /// 2.1). While none is open there and the stream is not accepted yet, or accepted and not yet
    /// decided, holds it for that session (ADR-0117 decision 3): up to
    /// <see cref="HeldDatagramCapacity"/> a connection, each for at most the head timeout, and drops
    /// one past either. One for a decided stream - an ordinary request, a refused or ended session -
    /// is dropped at once.
    /// </summary>
    /// <param name="datagram">A QUIC <c>DATAGRAM</c> frame's data.</param>
    /// <exception cref="Http3ConnectionException">The quarter stream ID is cut short or past 2^60 - 1 (<c>H3_DATAGRAM_ERROR</c>).</exception>
    public void Deliver(byte[] datagram)
    {
        if (!QuicVariableLengthInteger.TryRead(datagram, out var quarterStreamId, out var length) || quarterStreamId > MaxQuarterStreamId)
        {
            throw new Http3ConnectionException(Http3ErrorCode.DatagramError, "an HTTP datagram's quarter stream ID is cut short or past 2^60 - 1");
        }

        var sessionId = quarterStreamId * 4;
        lock (gate)
        {
            DropExpiredDatagrams();
            if (sessions.TryGetValue(sessionId, out var session))
            {
                session.Deliver(datagram[length..]);
            }
            else
            {
                HoldOrDrop(sessionId, datagram[length..]);
            }
        }
    }

    // Called with the gate held, for a datagram whose session is not open.
    private void HoldOrDrop(ulong sessionId, byte[] payload)
    {
        if (sessionId < nextUnacceptedStreamId && !undecided.Contains(sessionId))
        {
            log.Note($"An HTTP datagram for stream {sessionId}, which is no open session, was dropped.");
        }
        else if (held.Count < HeldDatagramCapacity)
        {
            held.Add((sessionId, payload, time.GetUtcNow()));
            log.Note($"An HTTP datagram for stream {sessionId}, which is no open session yet, is held for it.");
        }
        else
        {
            log.Note($"An HTTP datagram for stream {sessionId}, which is no open session, was dropped: {HeldDatagramCapacity} are held already.");
        }
    }

    // A session whose CONNECT stream has already ended cannot take a write; that is its own end, not the connection's.
    private async Task ShutDownSessionAsync(Http3ExtendedConnectSession session, CancellationToken cancellationToken)
    {
        try
        {
            await session.ShutDownAsync(cancellationToken);
        }
        catch (InvalidOperationException ended)
        {
            log.Note($"A shutdown write of session {session.Id} failed: {ended.Message}");
        }
    }

    private Http3ExtendedConnectSession? OpenSession(ulong sessionId)
    {
        lock (gate)
        {
            return sessions.GetValueOrDefault(sessionId);
        }
    }

    // Called with the gate held. With no head timeout, a held datagram waits as long as the connection lasts.
    private void DropExpiredDatagrams()
    {
        var now = time.GetUtcNow();
        var expired = held.FindAll(datagram => holdTime != Timeout.InfiniteTimeSpan && now - datagram.Arrival >= holdTime);
        foreach (var datagram in expired)
        {
            log.Note($"An HTTP datagram held for stream {datagram.SessionId} was dropped: no session opened there within the head timeout.");
            held.Remove(datagram);
        }
    }
}
