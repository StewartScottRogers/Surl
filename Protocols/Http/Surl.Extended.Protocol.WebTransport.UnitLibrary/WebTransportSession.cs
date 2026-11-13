using Surl.Http3;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// One accepted WebTransport session, served as the echo service ADR-0117 decisions 3, 4, 6 and 8
/// describe: each client stream's bytes come back (a bidirectional stream on itself, a
/// unidirectional one on a server unidirectional stream of the session), each HTTP datagram and
/// <c>DATAGRAM</c> capsule comes back unchanged, and the session ends with the client's
/// <c>WT_CLOSE_SESSION</c>, its FIN or reset of the CONNECT stream, a limit, or shutdown.
/// </summary>
/// <remarks>
/// <para>
/// When the session ends, every stream still open is ended with <c>WT_SESSION_GONE</c>: the server's
/// half by <c>RESET_STREAM_AT</c> keeping its stream header, the client's by <c>STOP_SENDING</c>.
/// Shutdown (the exchange cancelled) sends <c>WT_DRAIN_SESSION</c>, then <c>WT_CLOSE_SESSION</c>
/// code 0 <c>server shutting down</c> and FIN, before <c>Surl.Http3</c> sends <c>GOAWAY</c>.
/// </para>
/// <para>
/// A client reset of a stream it sends on is mirrored on the half the server writes for it, with the
/// same code, after the bytes already echoed; a client <c>STOP_SENDING</c> on the server's half
/// (which <c>Surl.Quic</c> answers with a reset) is answered with <c>STOP_SENDING</c> of the same code
/// on the client's half. Unknown capsule types are ignored; a capsule that breaks its layout resets
/// the CONNECT stream <c>H3_MESSAGE_ERROR</c>, as does any capsule after <c>WT_CLOSE_SESSION</c>.
/// </para>
/// <para>
/// With flow control off every flow-control capsule is ignored (WTH3 5.1). With it on (decision 4)
/// the client is held to <see cref="WebTransportReceiveLimits"/>, whose raised limits the session
/// sends as it reads, and the echo is held to the client's limits, sending <c>WT_DATA_BLOCKED</c> or
/// <c>WT_STREAMS_BLOCKED</c> when they stop it. A client past a limit, or one not raising its own
/// when it sends one, ends the session: the CONNECT stream is reset <c>WT_FLOW_CONTROL_ERROR</c>.
/// <c>WT_MAX_STREAM_DATA</c> and <c>WT_STREAM_DATA_BLOCKED</c>, which HTTP/3 prohibits, reset it
/// <c>H3_MESSAGE_ERROR</c>. Either way, once the payload the session received on its streams and as
/// HTTP datagrams reaches <c>--max-filesize</c>, the server sends <c>WT_CLOSE_SESSION</c> code 1
/// <c>session data limit reached</c> and FIN (decision 8).
/// </para>
/// </remarks>
internal sealed class WebTransportSession : IDisposable
{
    /// <summary>The most bytes one read of a client stream takes before they are echoed.</summary>
    public const int EchoChunkLength = 65536;

    /// <summary>The message of the server's own <c>WT_CLOSE_SESSION</c> at shutdown (ADR-0117 decision 6).</summary>
    public const string ShutdownMessage = "server shutting down";

    /// <summary>The code of the server's own <c>WT_CLOSE_SESSION</c> when the session's payload reaches <c>--max-filesize</c> (ADR-0117 decision 8).</summary>
    public const uint DataLimitCode = 1;

    /// <summary>The message that goes with <see cref="DataLimitCode"/>.</summary>
    public const string DataLimitMessage = "session data limit reached";

    private readonly Http3ExtendedConnectSession session;
    private readonly ExchangeContext context;
    private readonly WebTransportFlowControl? flowControl;
    private readonly CancellationTokenSource ending = new();
    private readonly CancellationToken endingToken;
    private readonly CancellationTokenSource endingByServer = new();
    private readonly Lock gate = new();
    private readonly List<Task> echoes = [];
    private Func<Task>? serverEnd;
    private long received;
    private int drainSent;
    private readonly TaskCompletionSource answered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Creates the session's server.</summary>
    /// <param name="session">The session <c>Surl.Http3</c> opened, answered <c>200</c> already.</param>
    /// <param name="context">The exchange: its limits and log.</param>
    /// <param name="flowControl">The session's flow control, or <see langword="null"/> when it is off.</param>
    public WebTransportSession(Http3ExtendedConnectSession session, ExchangeContext context, WebTransportFlowControl? flowControl)
    {
        this.session = session;
        this.context = context;
        this.flowControl = flowControl;
        endingToken = ending.Token;

        // Registered here, before the 200 goes out, so a shutdown that begins the moment the client sees the 200 still drains this session.
        _ = session.AddShutdownWrite(SendDrainAsync);
    }

    /// <summary>Says the <c>200</c> has been written, which lets a shutdown's <c>WT_DRAIN_SESSION</c> follow it.</summary>
    public void MarkAnswered() => answered.TrySetResult();

    /// <summary>Serves the session until it ends.</summary>
    /// <param name="cancellationToken">Cancelled when the connection ends, fails or shuts down.</param>
    /// <returns>A task that completes when the session and its streams are over.</returns>
    public async Task ServeAsync(CancellationToken cancellationToken)
    {
        using var linked = cancellationToken.Register(ending.Cancel);
        using var readingToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, endingByServer.Token);
        var accepting = AcceptStreamsAsync();
        var datagrams = EchoDatagramsAsync();
        var reading = ReadCapsulesAsync(readingToken.Token);
        await reading.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        await EndStreamsAsync();
        await Task.WhenAll(accepting, datagrams);
        await reading;
    }

    /// <summary>Echoes one client stream of the session until it ends or the session does.</summary>
    /// <param name="stream">The client stream, read past its header.</param>
    /// <param name="cancellationToken">Cancelled when the connection ends, fails or shuts down.</param>
    /// <returns>A task that completes when the stream is over.</returns>
    public async Task ServeStreamAsync(Http3SessionStream stream, CancellationToken cancellationToken)
    {
        using var token = CancellationTokenSource.CreateLinkedTokenSource(endingToken, cancellationToken);
        var isBidirectional = stream.Stream.IsBidirectional;
        var answer = isBidirectional ? stream : null;
        try
        {
            await CountOpenedStreamAsync(isBidirectional);
            answer ??= await OpenAnswerAsync(token.Token);
            await (answer is null ? RefuseUnansweredAsync(stream, token.Token) : EchoAsync(stream, answer, token.Token));
            await SendCapsuleAsync(flowControl?.Receive.CloseStream(isBidirectional));
        }
        catch (OperationCanceledException)
        {
            await EndForSessionAsync(stream, answer);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ending.Dispose();
        endingByServer.Dispose();
    }

    // The bytes of a server unidirectional stream's header (0x54 and the session ID), which a reset keeps (WTH3 4.4).
    private ulong ReliableHeaderLength(Http3SessionStream answer, Http3SessionStream stream) =>
        answer == stream ? 0 : (ulong)new WebTransportStreamHeader(WebTransportStreamKind.Unidirectional, session.Id).Encode().Length;

    private async Task AcceptStreamsAsync()
    {
        try
        {
            // AcceptStreamAsync returns null only after ServeSessionAsync returns, by which time the
            // session has been ended and this loop cancelled.
            while (true)
            {
                var stream = await session.AcceptStreamAsync(endingToken);
                lock (gate)
                {
                    echoes.Add(ServeStreamAsync(stream!, CancellationToken.None));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The session has ended.
        }
    }

    // ADR-0117 decision 8: at most WebTransportDatagramEchoQueue.Capacity datagrams wait for their echo, the newest dropped.
    private async Task EchoDatagramsAsync()
    {
        var waiting = new WebTransportDatagramEchoQueue();
        using var echoingToken = CancellationTokenSource.CreateLinkedTokenSource(endingToken, endingByServer.Token);
        var receiving = QueueDatagramsAsync(waiting);
        try
        {
            await foreach (var payload in waiting.TakeAsync(echoingToken.Token))
            {
                await SendDatagramAsync(payload);
            }
        }
        catch (OperationCanceledException)
        {
            // The session has ended.
        }

        await receiving;
    }

    private async Task QueueDatagramsAsync(WebTransportDatagramEchoQueue waiting)
    {
        try
        {
            // ReceiveDatagramAsync returns null only once the session has ended, and the send then throws.
            while (true)
            {
                ReadOnlyMemory<byte> payload = await session.ReceiveDatagramAsync(endingToken);
                CountReceived(payload.Length);
                waiting.Enqueue(payload, context.Log, session.Id);
            }
        }
        catch (OperationCanceledException)
        {
            // The session has ended.
        }
        finally
        {
            waiting.Complete();
        }
    }

    // RFC 9221 section 5: a datagram the client cannot take is dropped.
    private async Task SendDatagramAsync(ReadOnlyMemory<byte> payload)
    {
        try
        {
            await session.SendDatagramAsync(payload, endingToken);
        }
        catch (InvalidOperationException)
        {
            context.Log.Note($"A datagram of session {session.Id} was dropped: the client takes no DATAGRAM frames.");
        }
        catch (ArgumentOutOfRangeException)
        {
            context.Log.Note($"A datagram of {payload.Length} bytes of session {session.Id} was dropped: it is larger than the client takes.");
        }
    }

    private async Task ReadCapsulesAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await session.ReadCapsuleAsync(cancellationToken) is { } capsule)
            {
                if (!await TakeCapsuleAsync(capsule, cancellationToken))
                {
                    return;
                }
            }

            await EndByClientAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await (serverEnd ?? CloseForShutdownAsync)();
        }
    }

    // False once the capsule has ended the session.
    private async Task<bool> TakeCapsuleAsync(Http3Capsule capsule, CancellationToken cancellationToken)
    {
        switch (WebTransportCapsuleReader.Decode((WebTransportCapsuleType)capsule.Type, capsule.Value))
        {
            case null:
                await ResetConnectStreamAsync(Http3ErrorCode.MessageError, $"a capsule of type 0x{capsule.Type:x} broke its layout", cancellationToken);
                return false;
            case WebTransportCloseSessionCapsule close:
                await CloseByClientAsync(close, cancellationToken);
                return false;
            case WebTransportDrainSessionCapsule:
                context.Log.Note($"The client sent WT_DRAIN_SESSION on session {session.Id}; it is still served.");
                return true;
            case WebTransportDatagramCapsule:
                await session.WriteCapsuleAsync(capsule, cancellationToken);
                return true;
            case WebTransportLimitCapsule limit when flowControl is not null:
                return await TakeClientLimitAsync(flowControl, limit, cancellationToken);
            case WebTransportUnparsedCapsule { Type: WebTransportCapsuleType.MaxStreamData or WebTransportCapsuleType.StreamDataBlocked } when flowControl is not null:
                await ResetConnectStreamAsync(Http3ErrorCode.MessageError, $"a capsule of type 0x{capsule.Type:x} is prohibited over HTTP/3", cancellationToken);
                return false;
            default:
                return true;
        }
    }

    // WTH3 5.6: a WT_MAX_* must raise the client's last limit; a *_BLOCKED is only logged.
    private async Task<bool> TakeClientLimitAsync(WebTransportFlowControl control, WebTransportLimitCapsule limit, CancellationToken cancellationToken)
    {
        if (!control.TryApplyClientLimit(limit))
        {
            await ResetConnectStreamAsync((Http3ErrorCode)WebTransportHttp3Codes.FlowControlError, $"the client's capsule 0x{(ulong)limit.Type:x} with {limit.Limit} does not raise its limit", cancellationToken);
            return false;
        }

        context.Log.Note($"The client sent capsule 0x{(ulong)limit.Type:x} with {limit.Limit} on session {session.Id}.");
        return true;
    }

    // WTH3 6: the session's streams end, then the CONNECT stream - by FIN, unless more followed the capsule.
    private async Task CloseByClientAsync(WebTransportCloseSessionCapsule close, CancellationToken cancellationToken)
    {
        context.Log.Note($"The client closed session {session.Id} with code {close.ErrorCode}: {close.Message}");
        await EndStreamsAsync();
        if (await session.ReadCapsuleAsync(cancellationToken) is not null)
        {
            await ResetConnectStreamAsync(Http3ErrorCode.MessageError, "a capsule followed WT_CLOSE_SESSION", cancellationToken);
            return;
        }

        await FinishConnectStreamAsync(cancellationToken);
    }

    // WTH3 6: the client's FIN is WT_CLOSE_SESSION code 0; its reset ends the session too.
    private async Task EndByClientAsync(CancellationToken cancellationToken)
    {
        context.Log.Note($"The client ended session {session.Id}'s CONNECT stream.");
        await EndStreamsAsync();
        if (session.RequestStream.HasRequestBodyEndedEarly)
        {
            await session.RequestStream.ResetResponseAsync((Http3ErrorCode)WebTransportHttp3Codes.SessionGone, cancellationToken);
            return;
        }

        await FinishConnectStreamAsync(cancellationToken);
    }

    // WTH3 4.7: WT_DRAIN_SESSION goes out once, before GOAWAY (Surl.Http3 awaits this write), or at the close when shutdown left no time for it.
    private async Task SendDrainAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref drainSent, 1) == 0)
        {
            await answered.Task.WaitAsync(cancellationToken);
            await session.WriteCapsuleAsync(ToHttp3Capsule(new WebTransportDrainSessionCapsule()), cancellationToken);
        }
    }

    // WTH3 4.7 and 6: once the shutdown grace has passed, WT_CLOSE_SESSION 0 and FIN (WT_DRAIN_SESSION first if it has not gone).
    private async Task CloseForShutdownAsync()
    {
        context.Log.Note($"Session {session.Id} was drained and closed for shutdown.");
        await SendDrainAsync(CancellationToken.None);
        await session.WriteCapsuleAsync(ToHttp3Capsule(new WebTransportCloseSessionCapsule(0, ShutdownMessage)), CancellationToken.None);
        await session.RequestStream.WriteDataAsync(ReadOnlyMemory<byte>.Empty, endStream: true, CancellationToken.None);
    }

    // ADR-0117 decision 8: WT_CLOSE_SESSION code 1, then FIN (WTH3 6: "MUST immediately send a FIN").
    private async Task CloseForDataLimitAsync()
    {
        context.Log.Note($"Session {session.Id} was closed: its payload reached --max-filesize ({context.Limits.MaxUploadBytes} bytes).");
        await EndStreamsAsync();
        await session.WriteCapsuleAsync(ToHttp3Capsule(new WebTransportCloseSessionCapsule(DataLimitCode, DataLimitMessage)), CancellationToken.None);
        await FinishConnectStreamAsync(CancellationToken.None);
    }

    private async Task ResetConnectStreamAsync(Http3ErrorCode code, string reason, CancellationToken cancellationToken)
    {
        await EndStreamsAsync();
        await session.RequestStream.ResetAsync(code, reason, cancellationToken);
    }

    // A CONNECT stream Surl.Http3 has reset already (a capsule past --max-message, or cut short) takes no FIN.
    private async Task FinishConnectStreamAsync(CancellationToken cancellationToken)
    {
        try
        {
            await session.RequestStream.WriteDataAsync(ReadOnlyMemory<byte>.Empty, endStream: true, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            context.Log.Note($"Session {session.Id}'s CONNECT stream was reset already.");
        }
    }

    // A stream or the payload ended the session: the capsule reader stops and runs end; the stream's own echo is cancelled.
    private OperationCanceledException EndByServer(Func<Task> end)
    {
        if (Interlocked.CompareExchange(ref serverEnd, end, null) is null)
        {
            endingByServer.Cancel();
        }

        return new OperationCanceledException();
    }

    private Func<Task> ResetForFlowControl(string reason) =>
        () => ResetConnectStreamAsync((Http3ErrorCode)WebTransportHttp3Codes.FlowControlError, reason, CancellationToken.None);

    // ADR-0117 decision 8: streams and HTTP datagrams count towards --max-filesize alike.
    private void CountReceived(int count)
    {
        if (Interlocked.Add(ref received, count) >= context.Limits.MaxUploadBytes)
        {
            throw EndByServer(CloseForDataLimitAsync);
        }
    }

    // WTH3 5.6.3: a client stream past WT_MAX_STREAMS is a flow-control error.
    private async Task CountOpenedStreamAsync(bool isBidirectional)
    {
        if (flowControl is null)
        {
            return;
        }

        if (!flowControl.Receive.TryOpenStream(isBidirectional, out var raise))
        {
            throw EndByServer(ResetForFlowControl("a client stream past WT_MAX_STREAMS"));
        }

        await SendCapsuleAsync(raise);
    }

    // WTH3 5.6.2: stream payload past WT_MAX_DATA is a flow-control error.
    private async Task CountStreamPayloadAsync(int count)
    {
        CountReceived(count);
        if (flowControl is null)
        {
            return;
        }

        if (!flowControl.Receive.TryConsume((ulong)count, out var raise))
        {
            throw EndByServer(ResetForFlowControl("stream payload past WT_MAX_DATA"));
        }

        await SendCapsuleAsync(raise);
    }

    // A flow-control capsule of the server's own. Only the session's streams send them, and the session
    // cancels and waits for every one of them before it finishes the CONNECT stream, so none follows the FIN.
    private async Task SendCapsuleAsync(WebTransportCapsule? capsule)
    {
        if (capsule is not null)
        {
            await session.WriteCapsuleAsync(ToHttp3Capsule(capsule), endingToken);
        }
    }

    // A server unidirectional stream to answer on; with flow control, within the client's WT_MAX_STREAMS.
    private async Task<Http3SessionStream?> OpenAnswerAsync(CancellationToken cancellationToken)
    {
        if (flowControl is not null)
        {
            await flowControl.SendUnidirectionalStreams.TakeAsync(1, ReportBlockedAsync(WebTransportCapsuleType.StreamsBlockedUnidirectional), cancellationToken);
        }

        return await session.OpenUnidirectionalStreamAsync(cancellationToken);
    }

    private Func<ulong, Task> ReportBlockedAsync(WebTransportCapsuleType type) =>
        limit => SendCapsuleAsync(new WebTransportLimitCapsule(type, limit));

    // Cancels every echo, which then ends its stream with WT_SESSION_GONE, and waits for them.
    private async Task EndStreamsAsync()
    {
        await ending.CancelAsync();
        Task[] running;
        lock (gate)
        {
            running = [.. echoes];
        }

        await Task.WhenAll(running);
    }

    private async Task EchoAsync(Http3SessionStream stream, Http3SessionStream answer, CancellationToken cancellationToken)
    {
        var buffer = new byte[EchoChunkLength];
        var written = ReliableHeaderLength(answer, stream);
        int count;
        while ((count = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await CountStreamPayloadAsync(count);
            if (await IsStoppedAsync(stream, answer, cancellationToken))
            {
                return;
            }

            await WriteEchoAsync(answer, buffer.AsMemory(0, count), cancellationToken);
            written += (ulong)count;
        }

        await FinishEchoAsync(stream, answer, written, cancellationToken);
    }

    // With flow control, the echo goes out as the client's WT_MAX_DATA allows; the client's stream is not read meanwhile.
    private async Task WriteEchoAsync(Http3SessionStream answer, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        while (!bytes.IsEmpty)
        {
            var allowed = flowControl is null
                ? bytes.Length
                : (int)await flowControl.SendData.TakeAsync((ulong)bytes.Length, ReportBlockedAsync(WebTransportCapsuleType.DataBlocked), cancellationToken);
            await answer.WriteAsync(bytes[..allowed], fin: false, cancellationToken);
            bytes = bytes[allowed..];
        }
    }

    // The client's FIN is answered by FIN, its reset by a reset with the same code after what was echoed.
    private static async Task FinishEchoAsync(Http3SessionStream stream, Http3SessionStream answer, ulong written, CancellationToken cancellationToken)
    {
        if (stream.Stream.ResetErrorCode is { } code)
        {
            await answer.ResetAtAsync(code, written, cancellationToken);
            return;
        }

        if (!await IsStoppedAsync(stream, answer, cancellationToken))
        {
            await answer.WriteAsync(ReadOnlyMemory<byte>.Empty, fin: true, cancellationToken);
        }
    }

    // Whether the client stopped the server's half; its code is sent back on the client's half.
    private static async Task<bool> IsStoppedAsync(Http3SessionStream stream, Http3SessionStream answer, CancellationToken cancellationToken)
    {
        if (answer.Stream.StopSendingErrorCode is not { } code)
        {
            return false;
        }

        await stream.StopSendingAsync(code, cancellationToken);
        return true;
    }

    // No server unidirectional stream is free to answer on (the client's MAX_STREAMS): the client's stream is refused.
    private async Task RefuseUnansweredAsync(Http3SessionStream stream, CancellationToken cancellationToken)
    {
        context.Log.Note($"Stream {stream.Id} of session {session.Id} was stopped with H3_REQUEST_REJECTED: the client allows no server stream to answer it on.");
        await stream.StopSendingAsync((ulong)Http3ErrorCode.RequestRejected, cancellationToken);
    }

    // WTH3 6: the server's half is reset WT_SESSION_GONE (keeping its header), the client's stopped.
    private async Task EndForSessionAsync(Http3SessionStream stream, Http3SessionStream? answer)
    {
        if (answer is not null)
        {
            await answer.ResetAtAsync(WebTransportHttp3Codes.SessionGone, ReliableHeaderLength(answer, stream), CancellationToken.None);
        }

        await stream.StopSendingAsync(WebTransportHttp3Codes.SessionGone, CancellationToken.None);
    }

    private static Http3Capsule ToHttp3Capsule(WebTransportCapsule capsule) => new((ulong)capsule.Type, capsule.EncodeValue());
}
