using Surl.HttpMessage;
using Surl.Protocol.Abstractions;
using Surl.WebSocket;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// Carries a <see cref="SocketIoSession"/> over a WebSocket (EIO "WebSocket", "Upgrade"; RFC 6455;
/// ADR-0120, decision 2): one Engine.IO packet per message, text packets in text messages and binary
/// ones in binary messages.
/// </summary>
/// <remarks>
/// <para>
/// A session opened directly over the WebSocket is served from its open packet
/// (<see cref="ServeAsync"/>). A polling session's WebSocket is probed first
/// (<see cref="ProbeThenServeAsync"/>): its <c>2probe</c> is answered <c>3probe</c> and the polls are
/// answered <c>6</c>, then its <c>5</c> moves the session here. Anything else before the <c>5</c> ends
/// this WebSocket alone and leaves the session on polling.
/// </para>
/// <para>
/// While the session is carried, the read loop hands each packet to the session and the writer
/// writes the session's outbox in order. Once the end is decided, what is queued is written, then the
/// end's WebSocket Close, and the server lingers for the client's close. A client that vanished, or an
/// exchange cancelled, ends with nothing written, except that a limit's cancellation (ADR-0059) gets
/// each connected namespace a DISCONNECT, then Close <c>1001</c>.
/// </para>
/// </remarks>
internal sealed class SocketIoWebSocketTransport
{
    // The longest header a client frame can have; --max-message bounds the payload alone.
    private const int MaxClientFrameHeaderBytes = 14;

    private static readonly byte[] ProbeAnswer = "3probe"u8.ToArray();

    private readonly SocketIoSession session;
    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly WebSocketFrameReader frameReader;
    private readonly WebSocketMessageReassembler reassembler;
    private readonly WebSocketLingeringClose lingeringClose;
    private bool isCarrying;

    /// <summary>
    /// Creates the transport for one upgraded connection.
    /// </summary>
    /// <param name="session">The session it carries.</param>
    /// <param name="connection">Where frames are written.</param>
    /// <param name="reader">The reader that read the upgrade request, so bytes it already buffered come first.</param>
    /// <param name="context">The WebSocket's exchange: its limits, log and cancellation.</param>
    public SocketIoWebSocketTransport(SocketIoSession session, IConnection connection, HttpConnectionReader reader, ExchangeContext context)
    {
        this.session = session;
        this.connection = connection;
        this.context = context;
        var maxMessageBytes = context.Limits.MaxMessageBytes;
        frameReader = new WebSocketFrameReader(reader.ReadAsync, maxMessageBytes == 0 ? 0 : maxMessageBytes + MaxClientFrameHeaderBytes);
        reassembler = new WebSocketMessageReassembler(maxMessageBytes);
        lingeringClose = new WebSocketLingeringClose(reader, context);
    }

    /// <summary>
    /// Serves a session opened over this WebSocket: the open packet first, then the session to its end.
    /// </summary>
    public Task ServeAsync()
    {
        session.Deliver(SocketIoOutboxItem.Of(session.OpenPacket()));
        session.Start();

        return CarryAsync();
    }

    /// <summary>
    /// Probes this WebSocket for a polling session, then, on the client's <c>5</c>, carries the session.
    /// </summary>
    public async Task ProbeThenServeAsync()
    {
        SocketIoSessionEnd? failure;
        using (var probeEnds = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, session.EndingToken))
        {
            failure = await ProbeAsync(probeEnds.Token);
        }

        if (failure is not null)
        {
            session.CancelProbe();
            context.Log.Note($"Upgrade abandoned: {failure.Why}.");
            await WriteCloseAsync(failure);

            return;
        }

        session.CompleteUpgrade();
        await CarryAsync();
    }

    private static SocketIoSessionEnd FrameNotRead(WebSocketFrameReadOutcome outcome) => outcome switch
    {
        WebSocketFrameReadOutcome.ConnectionClosed => SocketIoSessionEnd.Silent("the client closed the connection without a WebSocket Close"),
        WebSocketFrameReadOutcome.FrameTooLarge => SocketIoSessionEnd.WebSocketClose(SocketIoSessionEnd.MessageTooBig, "a client frame is past --max-message"),
        WebSocketFrameReadOutcome.CloseReasonNotUtf8 => SocketIoSessionEnd.WebSocketClose(SocketIoSessionEnd.InvalidPayload, "a client Close's reason is not valid UTF-8"),
        _ => SocketIoSessionEnd.ProtocolErrorClose($"a client frame is malformed ({outcome})"),
    };

    private static SocketIoSessionEnd ReassemblyFailure(WebSocketReassemblyOutcome outcome) => outcome switch
    {
        WebSocketReassemblyOutcome.MessageTooLarge => SocketIoSessionEnd.WebSocketClose(SocketIoSessionEnd.MessageTooBig, "a client message is past --max-message"),
        WebSocketReassemblyOutcome.TextNotUtf8 => SocketIoSessionEnd.WebSocketClose(SocketIoSessionEnd.InvalidPayload, "a client text message is not valid UTF-8"),
        _ => SocketIoSessionEnd.ProtocolErrorClose($"a client frame is out of its message's order ({outcome})"),
    };

    // Every client frame is masked and sets no reserved bit, since no extension was agreed (RFC 6455 section 5).
    private static bool IsUnmaskedOrReserved(WebSocketFrame frame) => !frame.Masked || frame.Rsv1 || frame.Rsv2 || frame.Rsv3;

    private static byte[] Frames(SocketIoOutboxItem item)
    {
        if (item.WebSocketPong is { } pong)
        {
            return WebSocketFrameEncoder.EncodeFrame(true, WebSocketOpcode.Pong, pong);
        }

        IEnumerable<byte> frames = [];
        foreach (var packet in item.Packets)
        {
            var opcode = packet.IsBinary ? WebSocketOpcode.Binary : WebSocketOpcode.Text;
            frames = frames.Concat(WebSocketFrameEncoder.EncodeFrame(true, opcode, EngineIoPacketCodec.WriteWebSocketMessage(packet)));
        }

        return [.. frames];
    }

    private static bool IsText(EngineIoPacket packet, EngineIoPacketType type, string data) =>
        !packet.IsBinary && packet.Type == type && packet.Data.Span.SequenceEqual(System.Text.Encoding.ASCII.GetBytes(data));

    // EIO "Upgrade": 2probe answered 3probe (written here, since the polls still carry the outbox), then 5.
    private async Task<SocketIoSessionEnd?> ProbeAsync(CancellationToken token)
    {
        try
        {
            var (probe, end) = await ReadPacketAsync(token);
            if (probe is null || !IsText(probe, EngineIoPacketType.Ping, "probe"))
            {
                return end ?? SocketIoSessionEnd.ProtocolErrorClose("the probing WebSocket's first packet is not 2probe");
            }

            await connection.WriteAsync(WebSocketFrameEncoder.EncodeFrame(true, WebSocketOpcode.Text, ProbeAnswer), token);
            session.AnswerPollsWithNoop();
            var (upgrade, upgradeEnd) = await ReadPacketAsync(token);

            return upgrade is not null && IsText(upgrade, EngineIoPacketType.Upgrade, string.Empty)
                ? null
                : upgradeEnd ?? SocketIoSessionEnd.ProtocolErrorClose("the probing WebSocket's packet after 3probe is not 5");
        }
        catch (OperationCanceledException)
        {
            return SocketIoSessionEnd.Silent("the session or the exchange ended during the upgrade");
        }
        catch (IOException failure)
        {
            return SocketIoSessionEnd.Silent($"the connection failed ({failure.Message})");
        }
    }

    private async Task CarryAsync()
    {
        isCarrying = true;
        SocketIoSessionEnd end;
        using (context.CancellationToken.Register(() => session.Decide(SocketIoSessionEnd.Silent("the exchange was cancelled"))))
        {
            var writer = WriteOutboxAsync();
            var readLoop = session.GuardAsync(ReadLoopAsync);
            end = await session.Finished;
            await readLoop;
            await writer;
        }

        await WriteEndAsync(end);
    }

    // Runs until the outbox is completed, so what was queued before the end is written.
    private async Task WriteOutboxAsync()
    {
        try
        {
            await session.Outbox.WriteAllAsync((item, token) => connection.WriteAsync(Frames(item), token).AsTask(), context.CancellationToken);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
        }
        catch (IOException failure)
        {
            session.Decide(SocketIoSessionEnd.Silent($"the connection failed ({failure.Message})"));
        }
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        while (true)
        {
            var (packet, end) = await ReadPacketAsync(token);
            end ??= await session.AcceptPacketAsync(packet!, token);
            if (end is not null)
            {
                session.Decide(end);

                return;
            }
        }
    }

    // The next Engine.IO packet, or how the WebSocket ends; control frames are handled on the way.
    private async Task<(EngineIoPacket? Packet, SocketIoSessionEnd? End)> ReadPacketAsync(CancellationToken token)
    {
        while (true)
        {
            var result = await frameReader.ReadFrameAsync(token);
            if (result.Frame is not { } frame)
            {
                return (null, FrameNotRead(result.Outcome));
            }

            if (IsUnmaskedOrReserved(frame))
            {
                return (null, SocketIoSessionEnd.ProtocolErrorClose("a client frame is unmasked or sets a reserved bit"));
            }

            var step = reassembler.Accept(frame);
            switch (step.Outcome)
            {
                case WebSocketReassemblyOutcome.FragmentHeld:
                    continue;
                case WebSocketReassemblyOutcome.MessageComplete:
                    return Packet(step.Message!);
                case WebSocketReassemblyOutcome.ControlFrame:
                    if (await AcceptControlFrameAsync(step.ControlFrame!, token) is { } end)
                    {
                        return (null, end);
                    }

                    continue;
                default:
                    return (null, ReassemblyFailure(step.Outcome));
            }
        }
    }

    private static (EngineIoPacket? Packet, SocketIoSessionEnd? End) Packet(WebSocketMessage message)
    {
        if (message.Opcode != WebSocketOpcode.Text)
        {
            return (EngineIoPacketCodec.ReadBinary(message.Payload), null);
        }

        return EngineIoPacketCodec.TryReadText(message.Payload, out var packet)
            ? (packet, null)
            : (null, SocketIoSessionEnd.ProtocolErrorClose("a client text message is not an Engine.IO packet"));
    }

    // A WebSocket Ping is answered with a Pong and a Pong ignored; neither is Engine.IO's heartbeat.
    private async Task<SocketIoSessionEnd?> AcceptControlFrameAsync(WebSocketFrame frame, CancellationToken token)
    {
        if (frame.Opcode == WebSocketOpcode.Close)
        {
            return SocketIoSessionEnd.WebSocketClose(SocketIoSessionEnd.NormalClosure, "the client sent a WebSocket Close");
        }

        if (frame.Opcode == WebSocketOpcode.Ping)
        {
            await AnswerPingAsync(frame.Payload, token);
        }

        return null;
    }

    // While probing, the outbox is the polls'; the Pong is written straight away.
    private async Task AnswerPingAsync(byte[] payload, CancellationToken token)
    {
        if (isCarrying)
        {
            session.Deliver(SocketIoOutboxItem.Pong(payload));

            return;
        }

        await connection.WriteAsync(WebSocketFrameEncoder.EncodeFrame(true, WebSocketOpcode.Pong, payload), token);
    }

    private async Task WriteEndAsync(SocketIoSessionEnd end)
    {
        if (end.CloseCode is null && context.IsCancelledForALimit)
        {
            await SayFarewellAsync();

            return;
        }

        await WriteCloseAsync(end);
    }

    private async Task WriteCloseAsync(SocketIoSessionEnd end)
    {
        if (end.CloseCode is not { } code)
        {
            return;
        }

        var token = context.CancellationToken;
        await connection.WriteAsync(WebSocketFrameEncoder.EncodeClose(code, string.Empty), token);
        await connection.CompleteWritesAsync(token);
        await lingeringClose.LingerAsync(token);
    }

    // ADR-0120 decision 7 on ADR-0059's terms: a DISCONNECT for each connected namespace, then Close
    // 1001, within a one-second window that only shutdown cuts short.
    private async Task SayFarewellAsync()
    {
        using var deadline = new CancellationTokenSource(SocketIoSession.FarewellWriteDeadline, context.TimeProvider);
        using var deadlineOrShutdown = CancellationTokenSource.CreateLinkedTokenSource(context.ShutdownToken, deadline.Token);
        try
        {
            foreach (var item in session.Farewell())
            {
                await connection.WriteAsync(Frames(item), deadlineOrShutdown.Token);
            }

            await connection.WriteAsync(WebSocketFrameEncoder.EncodeClose(SocketIoSessionEnd.GoingAway, string.Empty), deadlineOrShutdown.Token);
            await connection.CompleteWritesAsync(deadlineOrShutdown.Token);
        }
        catch (OperationCanceledException) when (!context.ShutdownToken.IsCancellationRequested)
        {
            context.Log.Note($"The farewell was not written within its {SocketIoSession.FarewellWriteDeadline.TotalSeconds}-second write deadline.");
        }
    }
}
