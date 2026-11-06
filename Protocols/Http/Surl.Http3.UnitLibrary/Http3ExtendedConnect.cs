using Surl.Protocol.Abstractions;
using Surl.Quic;

namespace Surl.Http3;

/// <summary>
/// One connection's extended <c>CONNECT</c> registrations and open sessions (ADR-0086 decision 3):
/// which handler serves a <c>:protocol</c>, a bidirectional stream signal or a unidirectional stream
/// type; which session an HTTP datagram or a claimed stream names; and the client's <c>SETTINGS</c>.
/// </summary>
/// <param name="handlers">The server's registrations; two for one <c>:protocol</c> throw <see cref="ArgumentException"/>.</param>
/// <param name="log">The exchange log.</param>
internal sealed class Http3ExtendedConnect(IReadOnlyList<IHttp3ExtendedConnectHandler> handlers, IExchangeLog log)
{
    /// <summary>The largest Quarter Stream ID (RFC 9297 section 2.1): 2^60 - 1.</summary>
    public const ulong MaxQuarterStreamId = (1UL << 60) - 1;

    private readonly Dictionary<string, IHttp3ExtendedConnectHandler> byProtocol = handlers.ToDictionary(handler => handler.Protocol, StringComparer.Ordinal);
    private readonly Dictionary<ulong, Http3ExtendedConnectSession> sessions = [];
    private readonly TaskCompletionSource<IReadOnlyDictionary<ulong, ulong>> clientSettings = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock gate = new();

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

    /// <summary>Serves <paramref name="session"/> with its handler, open for datagrams and streams until the handler returns.</summary>
    /// <param name="session">The session.</param>
    /// <param name="cancellationToken">Cancelled when the connection ends, fails or shuts down.</param>
    /// <returns>A task that completes when the handler has returned.</returns>
    public async Task ServeAsync(Http3ExtendedConnectSession session, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            sessions[session.Id] = session;
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
    /// <param name="cancellationToken">Cancelled when the connection ends, fails or shuts down.</param>
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
    /// 2.1), or drops it when none is open.
    /// </summary>
    /// <param name="datagram">A QUIC <c>DATAGRAM</c> frame's data.</param>
    /// <exception cref="Http3ConnectionException">The quarter stream ID is cut short or past 2^60 - 1 (<c>H3_DATAGRAM_ERROR</c>).</exception>
    public void Deliver(byte[] datagram)
    {
        if (!QuicVariableLengthInteger.TryRead(datagram, out var quarterStreamId, out var length) || quarterStreamId > MaxQuarterStreamId)
        {
            throw new Http3ConnectionException(Http3ErrorCode.DatagramError, "an HTTP datagram's quarter stream ID is cut short or past 2^60 - 1");
        }

        if (OpenSession(quarterStreamId * 4) is not { } session)
        {
            log.Note($"An HTTP datagram for stream {quarterStreamId * 4}, which is no open session, was dropped.");
            return;
        }

        session.Deliver(datagram[length..]);
    }

    private Http3ExtendedConnectSession? OpenSession(ulong sessionId)
    {
        lock (gate)
        {
            return sessions.GetValueOrDefault(sessionId);
        }
    }
}
