using System.Security.Cryptography;
using System.Text;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;
using Surl.WebSocket;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// One Engine.IO session over the WebSocket transport, from the <c>101</c> to its close (EIO
/// "Handshake", "Heartbeat", "WebSocket"; SIO "Connection to a namespace" to "Disconnection from a
/// namespace"; ADR-0120, decisions 2 to 7).
/// </summary>
/// <remarks>
/// <para>
/// The open packet is the first message. Three loops run at once: the read loop (WebSocket frames,
/// then Engine.IO packets, then Socket.IO packets and their attachments), the clock (the ping every
/// <see cref="PingInterval"/>, the pong deadline and the connect timeout, on
/// <see cref="ExchangeContext.TimeProvider"/>), and the writer, which writes the session's
/// <see cref="SocketIoOutbox"/> in order. Replies and broadcasts from other sessions are queued
/// there, so a slow client never holds up its sender.
/// </para>
/// <para>
/// The first loop to decide the session's end wins: the others stop, what is queued is written,
/// then the end's WebSocket Close, and the server lingers for the client's close. A client that
/// vanished, or an exchange cancelled, ends with nothing written, except that a limit's cancellation
/// (ADR-0059) gets each connected namespace a DISCONNECT, then Close <c>1001</c>.
/// </para>
/// </remarks>
internal sealed class SocketIoSession : IDisposable
{
    /// <summary>
    /// How long after the open packet, or after a pong, the server sends a ping (ADR-0120, decision 3).
    /// </summary>
    public static readonly TimeSpan PingInterval = TimeSpan.FromMilliseconds(EngineIoOpenPacket.PingIntervalMilliseconds);

    /// <summary>
    /// How long the client has to answer a ping (ADR-0120, decision 3).
    /// </summary>
    public static readonly TimeSpan PingTimeout = TimeSpan.FromMilliseconds(EngineIoOpenPacket.PingTimeoutMilliseconds);

    /// <summary>
    /// The window a limit's farewell is written in (ADR-0059, decision 3).
    /// </summary>
    public static readonly TimeSpan FarewellWriteDeadline = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The CONNECT_ERROR <c>message</c> for a namespace not served.
    /// </summary>
    public const string InvalidNamespaceText = "Invalid namespace";

    /// <summary>
    /// The CONNECT_ERROR <c>message</c> for any refused login (SIO's own example).
    /// </summary>
    public const string NotAuthorizedText = "Not authorized";

    // The longest header a client frame can have; --max-message bounds the payload alone.
    private const int MaxClientFrameHeaderBytes = 14;

    private static readonly Task<SocketIoSessionEnd?> Continues = Task.FromResult<SocketIoSessionEnd?>(null);

    private readonly string sessionId;
    private readonly SocketIoListener listener;
    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly WebSocketFrameReader frameReader;
    private readonly WebSocketMessageReassembler reassembler;
    private readonly WebSocketLingeringClose lingeringClose;
    private readonly SocketIoOutbox outbox = new(SocketIoOutbox.MaxUnsentBytes);
    private readonly CancellationTokenSource ending = new();
    private readonly TaskCompletionSource<SocketIoSessionEnd> ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock gate = new();
    private readonly List<string> connectedNamespaces = [];
    private SocketIoPendingBinaryPacket? pendingBinary;
    private bool hasReadSocketIoPacket;
    private DateTimeOffset heartbeatFrom;
    private DateTimeOffset? pingSentAt;
    private DateTimeOffset? noNamespaceSince;

    /// <summary>
    /// Creates the session that follows one <c>101</c>.
    /// </summary>
    /// <param name="sessionId">The Engine.IO <c>sid</c> reserved for it.</param>
    /// <param name="listener">What the listener's sessions share.</param>
    /// <param name="connection">Where frames are written.</param>
    /// <param name="reader">The reader that read the upgrade request, so bytes it already buffered come first.</param>
    /// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
    public SocketIoSession(string sessionId, SocketIoListener listener, IConnection connection, HttpConnectionReader reader, ExchangeContext context)
    {
        this.sessionId = sessionId;
        this.listener = listener;
        this.connection = connection;
        this.context = context;
        var maxMessageBytes = context.Limits.MaxMessageBytes;
        frameReader = new WebSocketFrameReader(reader.ReadAsync, maxMessageBytes == 0 ? 0 : maxMessageBytes + MaxClientFrameHeaderBytes);
        reassembler = new WebSocketMessageReassembler(maxMessageBytes);
        lingeringClose = new WebSocketLingeringClose(reader, context);
    }

    /// <summary>
    /// Whether <paramref name="name"/> is connected on this session.
    /// </summary>
    public bool IsConnectedTo(string name)
    {
        lock (gate)
        {
            return connectedNamespaces.Contains(name);
        }
    }

    /// <summary>
    /// Queues frames for the client; past <see cref="SocketIoOutbox.MaxUnsentBytes"/> unsent, they are
    /// dropped and the session is closed <c>1008</c>.
    /// </summary>
    public void Deliver(byte[] frames)
    {
        if (!outbox.TryQueue(frames))
        {
            context.Log.Note($"Frames dropped: more than {SocketIoOutbox.MaxUnsentBytes} bytes would be unsent.");
            Decide(SocketIoSessionEnd.WebSocketClose(SocketIoSessionEnd.PolicyViolation, $"more than {SocketIoOutbox.MaxUnsentBytes} bytes would be unsent"));
        }
    }

    /// <summary>
    /// Serves the session until its end is decided, then writes that end.
    /// </summary>
    /// <returns>A task that completes when the connection is done.</returns>
    public async Task RunAsync()
    {
        var openedAt = context.TimeProvider.GetUtcNow();
        heartbeatFrom = openedAt;
        noNamespaceSince = openedAt;
        var open = EngineIoOpenPacket.Write(sessionId, [], EngineIoOpenPacket.PingIntervalMilliseconds, EngineIoOpenPacket.PingTimeoutMilliseconds, context.Limits.MaxMessageBytes);
        Deliver(TextFrame(EngineIoPacketCodec.WriteWebSocketMessage(open)));

        SocketIoSessionEnd end;
        using (context.CancellationToken.Register(() => Decide(SocketIoSessionEnd.Silent("the exchange was cancelled"))))
        {
            var writer = WriteOutboxAsync();
            var readLoop = GuardAsync(ReadLoopAsync);
            var clock = GuardAsync(RunClockAsync);
            end = await ended.Task;
            await ending.CancelAsync();
            await Task.WhenAll(readLoop, clock);
            outbox.Complete();
            await writer;
        }

        context.Log.Note($"Engine.IO session ended: {end.Why}.");
        await WriteEndAsync(end);
    }

    /// <inheritdoc/>
    public void Dispose() => ending.Dispose();

    private static byte[] TextFrame(byte[] payload) => WebSocketFrameEncoder.EncodeFrame(true, WebSocketOpcode.Text, payload);

    // One Socket.IO packet as an Engine.IO message, then its attachments as binary messages.
    private static byte[] Frames(SocketIoPacket packet, IReadOnlyList<byte[]> attachments)
    {
        var message = new EngineIoPacket(EngineIoPacketType.Message, SocketIoPacketWriter.Write(packet), IsBinary: false);
        IEnumerable<byte> frames = TextFrame(EngineIoPacketCodec.WriteWebSocketMessage(message));
        foreach (var attachment in attachments)
        {
            frames = frames.Concat(WebSocketFrameEncoder.EncodeFrame(true, WebSocketOpcode.Binary, attachment));
        }

        return [.. frames];
    }

    private static SocketIoPacket TextPacket(SocketIoPacketType type, string name, string payload) =>
        new(type, name, 0, null, Encoding.ASCII.GetBytes(payload));

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

    private static Task<SocketIoSessionEnd?> Ends(SocketIoSessionEnd end) => Task.FromResult<SocketIoSessionEnd?>(end);

    private static DateTimeOffset Earliest(DateTimeOffset first, DateTimeOffset second, DateTimeOffset third)
    {
        var earlier = first < second ? first : second;

        return earlier < third ? earlier : third;
    }

    private void Decide(SocketIoSessionEnd end) => ended.TrySetResult(end);

    // Stops a loop quietly once the end is decided, and ends the session when the connection fails.
    private async Task GuardAsync(Func<CancellationToken, Task> loop)
    {
        try
        {
            await loop(ending.Token);
        }
        catch (OperationCanceledException) when (ending.IsCancellationRequested)
        {
        }
        catch (IOException failure)
        {
            Decide(SocketIoSessionEnd.Silent($"the connection failed ({failure.Message})"));
        }
    }

    // Runs until the outbox is completed, so what was queued before the end is written.
    private async Task WriteOutboxAsync()
    {
        try
        {
            await outbox.WriteAllAsync(connection, context.CancellationToken);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
        }
        catch (IOException failure)
        {
            Decide(SocketIoSessionEnd.Silent($"the connection failed ({failure.Message})"));
        }
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        while (true)
        {
            var result = await frameReader.ReadFrameAsync(token);
            var end = result.Frame is { } frame ? await AcceptFrameAsync(frame, token) : FrameNotRead(result.Outcome);
            if (end is not null)
            {
                Decide(end);

                return;
            }
        }
    }

    private Task<SocketIoSessionEnd?> AcceptFrameAsync(WebSocketFrame frame, CancellationToken token)
    {
        if (IsUnmaskedOrReserved(frame))
        {
            return Ends(SocketIoSessionEnd.ProtocolErrorClose("a client frame is unmasked or sets a reserved bit"));
        }

        var step = reassembler.Accept(frame);
        if (step.Outcome == WebSocketReassemblyOutcome.FragmentHeld)
        {
            return Continues;
        }

        if (step.Outcome == WebSocketReassemblyOutcome.MessageComplete)
        {
            return AcceptMessageAsync(step.Message!, token);
        }

        return step.Outcome == WebSocketReassemblyOutcome.ControlFrame
            ? AcceptControlFrame(step.ControlFrame!)
            : Ends(ReassemblyFailure(step.Outcome));
    }

    // A WebSocket Ping is answered with a Pong and a Pong ignored; neither is Engine.IO's heartbeat.
    private Task<SocketIoSessionEnd?> AcceptControlFrame(WebSocketFrame frame)
    {
        if (frame.Opcode == WebSocketOpcode.Close)
        {
            return Ends(SocketIoSessionEnd.WebSocketClose(SocketIoSessionEnd.NormalClosure, "the client sent a WebSocket Close"));
        }

        if (frame.Opcode == WebSocketOpcode.Ping)
        {
            Deliver(WebSocketFrameEncoder.EncodeFrame(true, WebSocketOpcode.Pong, frame.Payload));
        }

        return Continues;
    }

    private Task<SocketIoSessionEnd?> AcceptMessageAsync(WebSocketMessage message, CancellationToken token)
    {
        if (pendingBinary is not null)
        {
            return AcceptAttachmentAsync(pendingBinary, message, token);
        }

        if (message.Opcode != WebSocketOpcode.Text)
        {
            return Ends(SocketIoSessionEnd.ProtocolErrorClose("a binary message arrived with no binary packet awaiting it"));
        }

        return EngineIoPacketCodec.TryReadText(message.Payload, out var packet)
            ? AcceptEngineIoPacketAsync(packet!, token)
            : Ends(SocketIoSessionEnd.ProtocolErrorClose("a client text message is not an Engine.IO packet"));
    }

    // EIO "Protocol": close ends the session, pong answers the heartbeat, message carries Socket.IO;
    // ping and noop are ignored; a client open, or an upgrade outside BL-762's upgrade, is a protocol error.
    private Task<SocketIoSessionEnd?> AcceptEngineIoPacketAsync(EngineIoPacket packet, CancellationToken token)
    {
        switch (packet.Type)
        {
            case EngineIoPacketType.Close:
                return Ends(SocketIoSessionEnd.WebSocketClose(SocketIoSessionEnd.NormalClosure, "the client sent an Engine.IO close"));
            case EngineIoPacketType.Pong:
                AcceptPong();
                return Continues;
            case EngineIoPacketType.Ping:
            case EngineIoPacketType.Noop:
                return Continues;
            case EngineIoPacketType.Message:
                return AcceptSocketIoPacketAsync(packet.Data, token);
            default:
                return Ends(SocketIoSessionEnd.ProtocolErrorClose($"the client sent an Engine.IO {packet.Type}"));
        }
    }

    private void AcceptPong()
    {
        lock (gate)
        {
            if (pingSentAt is not null)
            {
                pingSentAt = null;
                heartbeatFrom = context.TimeProvider.GetUtcNow();
            }
        }
    }

    private Task<SocketIoSessionEnd?> AcceptSocketIoPacketAsync(ReadOnlyMemory<byte> data, CancellationToken token)
    {
        if (!SocketIoPacketReader.TryReadFromClient(data, out var packet))
        {
            return Ends(SocketIoSessionEnd.ProtocolErrorClose("a client Socket.IO packet is malformed"));
        }

        if (!hasReadSocketIoPacket && packet!.Type != SocketIoPacketType.Connect)
        {
            return Ends(SocketIoSessionEnd.ProtocolErrorClose("the client's first Socket.IO packet is not a CONNECT"));
        }

        hasReadSocketIoPacket = true;
        if (SocketIoPacket.CarriesAttachments(packet!.Type))
        {
            pendingBinary = new SocketIoPendingBinaryPacket(packet, data.Length, context.Limits.MaxMessageBytes);

            return Continues;
        }

        return AnswerAsync(packet, [], token);
    }

    private Task<SocketIoSessionEnd?> AcceptAttachmentAsync(SocketIoPendingBinaryPacket pending, WebSocketMessage message, CancellationToken token)
    {
        if (message.Opcode != WebSocketOpcode.Binary)
        {
            return Ends(SocketIoSessionEnd.ProtocolErrorClose("a text message arrived before every attachment"));
        }

        if (!pending.TryAdd(message.Payload))
        {
            return Ends(SocketIoSessionEnd.WebSocketClose(SocketIoSessionEnd.MessageTooBig, "a binary packet and its attachments are past --max-message"));
        }

        if (!pending.IsComplete)
        {
            return Continues;
        }

        pendingBinary = null;

        return AnswerAsync(pending.Packet, pending.Attachments, token);
    }

    // A client ACK or BINARY_ACK answers nothing the server asked, so it is dropped (ADR-0120, decision 5).
    private async Task<SocketIoSessionEnd?> AnswerAsync(SocketIoPacket packet, IReadOnlyList<byte[]> attachments, CancellationToken token)
    {
        switch (packet.Type)
        {
            case SocketIoPacketType.Connect:
                await ConnectAsync(packet, token);
                break;
            case SocketIoPacketType.Disconnect:
                Leave(packet.Namespace);
                break;
            case SocketIoPacketType.Event:
            case SocketIoPacketType.BinaryEvent:
                AnswerEvent(packet, attachments);
                break;
        }

        return null;
    }

    private async Task ConnectAsync(SocketIoPacket packet, CancellationToken token)
    {
        var name = packet.Namespace;
        if (!listener.Namespaces.Serves(name))
        {
            SendConnectError(name, InvalidNamespaceText);

            return;
        }

        if (IsConnectedTo(name))
        {
            return;
        }

        var login = SocketIoConnectCredentials.Read(packet.Payload, context.Scheme, connection.TlsSession);
        var verdict = await listener.AuthenticationPolicy.CheckPasswordLoginAsync(login, token);
        if (verdict is not (PasswordLoginVerdict.Accepted or PasswordLoginVerdict.AcceptedUnchecked))
        {
            context.Log.Note($"CONNECT to {name} refused: {verdict}.");
            SendConnectError(name, NotAuthorizedText);

            return;
        }

        lock (gate)
        {
            connectedNamespaces.Add(name);
            noNamespaceSince = null;
        }

        using var random = RandomNumberGenerator.Create();
        var socketId = SocketIoRandomId.Create(random);
        Deliver(Frames(TextPacket(SocketIoPacketType.Connect, name, "{\"sid\":\"" + socketId + "\"}"), []));
    }

    private void SendConnectError(string name, string message) =>
        Deliver(Frames(TextPacket(SocketIoPacketType.ConnectError, name, "{\"message\":\"" + message + "\"}"), []));

    private void Leave(string name)
    {
        lock (gate)
        {
            if (connectedNamespaces.Remove(name) && connectedNamespaces.Count == 0)
            {
                noNamespaceSince = context.TimeProvider.GetUtcNow();
            }
        }
    }

    // Echo, or broadcast to every session on the namespace; an ack id is answered by an ACK
    // carrying the arguments instead of an echo (ADR-0120, decisions 5 and 6).
    private void AnswerEvent(SocketIoPacket packet, IReadOnlyList<byte[]> attachments)
    {
        var name = packet.Namespace;
        if (!IsConnectedTo(name))
        {
            return;
        }

        var reply = Frames(packet with { AckId = null }, attachments);
        if (SocketIoEventPayload.IsBroadcast(packet.Payload))
        {
            Broadcast(name, reply);
        }
        else if (packet.AckId is null)
        {
            Deliver(reply);
        }

        if (packet.AckId is { } ackId)
        {
            Acknowledge(packet, ackId, attachments);
        }
    }

    private void Broadcast(string name, byte[] reply)
    {
        foreach (var session in listener.Sessions.Attached())
        {
            if (session.IsConnectedTo(name))
            {
                session.Deliver(reply);
            }
        }
    }

    private void Acknowledge(SocketIoPacket packet, long ackId, IReadOnlyList<byte[]> attachments)
    {
        var ackType = packet.Type == SocketIoPacketType.BinaryEvent ? SocketIoPacketType.BinaryAck : SocketIoPacketType.Ack;
        var ack = new SocketIoPacket(ackType, packet.Namespace, packet.AttachmentCount, ackId, SocketIoEventPayload.Arguments(packet.Payload));
        Deliver(Frames(ack, attachments));
    }

    private async Task RunClockAsync(CancellationToken token)
    {
        while (true)
        {
            var now = context.TimeProvider.GetUtcNow();
            var (pingDue, pongDue, connectDue) = Deadlines();
            if (now >= pongDue)
            {
                Decide(SocketIoSessionEnd.WebSocketClose(SocketIoSessionEnd.NormalClosure, "no pong arrived within pingTimeout"));

                return;
            }

            if (now >= connectDue)
            {
                Decide(SocketIoSessionEnd.WebSocketClose(SocketIoSessionEnd.NormalClosure, "no namespace was connected within --head-timeout"));

                return;
            }

            if (now >= pingDue)
            {
                SendPing(now);

                continue;
            }

            await Task.Delay(Earliest(pingDue, pongDue, connectDue) - now, context.TimeProvider, token);
        }
    }

    private (DateTimeOffset PingDue, DateTimeOffset PongDue, DateTimeOffset ConnectDue) Deadlines()
    {
        var headTimeout = context.Limits.HeadTimeout;
        lock (gate)
        {
            var connectDue = noNamespaceSince is { } since && headTimeout != Timeout.InfiniteTimeSpan ? since + headTimeout : DateTimeOffset.MaxValue;

            return pingSentAt is { } sentAt
                ? (DateTimeOffset.MaxValue, sentAt + PingTimeout, connectDue)
                : (heartbeatFrom + PingInterval, DateTimeOffset.MaxValue, connectDue);
        }
    }

    private void SendPing(DateTimeOffset now)
    {
        lock (gate)
        {
            pingSentAt = now;
        }

        Deliver(TextFrame([(byte)'2']));
    }

    private async Task WriteEndAsync(SocketIoSessionEnd end)
    {
        if (end.CloseCode is not { } code)
        {
            if (context.IsCancelledForALimit)
            {
                await SayFarewellAsync();
            }

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
        using var deadline = new CancellationTokenSource(FarewellWriteDeadline, context.TimeProvider);
        using var deadlineOrShutdown = CancellationTokenSource.CreateLinkedTokenSource(context.ShutdownToken, deadline.Token);
        try
        {
            foreach (var name in ConnectedNamespaces())
            {
                await connection.WriteAsync(Frames(TextPacket(SocketIoPacketType.Disconnect, name, string.Empty), []), deadlineOrShutdown.Token);
            }

            await connection.WriteAsync(WebSocketFrameEncoder.EncodeClose(SocketIoSessionEnd.GoingAway, string.Empty), deadlineOrShutdown.Token);
            await connection.CompleteWritesAsync(deadlineOrShutdown.Token);
        }
        catch (OperationCanceledException) when (!context.ShutdownToken.IsCancellationRequested)
        {
            context.Log.Note($"The farewell was not written within its {FarewellWriteDeadline.TotalSeconds}-second write deadline.");
        }
    }

    private string[] ConnectedNamespaces()
    {
        lock (gate)
        {
            return [.. connectedNamespaces];
        }
    }
}
