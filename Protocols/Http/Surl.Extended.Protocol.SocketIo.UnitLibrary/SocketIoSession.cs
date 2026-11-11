using System.Security.Cryptography;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// One Engine.IO session, whichever transport carries it, from its open packet to its end (EIO
/// "Handshake", "Heartbeat", "Protocol", "Upgrade"; SIO "Connection to a namespace" to
/// "Disconnection from a namespace"; ADR-0120, decisions 2 to 7).
/// </summary>
/// <remarks>
/// <para>
/// The session reads Engine.IO packets (<see cref="AcceptPacketAsync"/>) and queues what it sends in
/// its <see cref="Outbox"/>; a <see cref="SocketIoWebSocketTransport"/> or HTTP long-polling
/// (<see cref="PollAsync"/>, <see cref="TryBeginPost"/>) carries both. Its clock (the ping every
/// <see cref="PingInterval"/>, the pong deadline and the connect timeout, on the
/// <see cref="ExchangeContext.TimeProvider"/> of the exchange that opened it) runs from
/// <see cref="Start"/> until the end is decided; replies and broadcasts from other sessions are
/// queued, so a slow client never holds up its sender.
/// </para>
/// <para>
/// A session opened by polling may move to a WebSocket (EIO "Upgrade"): <see cref="TryBeginProbe"/>
/// when the WebSocket is upgraded, <see cref="AnswerPollsWithNoop"/> once its <c>2probe</c> is
/// answered, then <see cref="CompleteUpgrade"/> on its <c>5</c>, or <see cref="CancelProbe"/> when the
/// WebSocket ends first.
/// </para>
/// </remarks>
internal sealed class SocketIoSession
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

    /// <summary>
    /// The noop packet <c>6</c> a held poll is answered with during an upgrade (EIO "Upgrade").
    /// </summary>
    public static readonly EngineIoPacket NoopPacket = EngineIoPacket.Text(EngineIoPacketType.Noop, string.Empty);

    /// <summary>
    /// The close packet <c>1</c> a poll is answered with once the session has ended.
    /// </summary>
    public static readonly EngineIoPacket ClosePacket = EngineIoPacket.Text(EngineIoPacketType.Close, string.Empty);

    private static readonly Task<SocketIoSessionEnd?> Continues = Task.FromResult<SocketIoSessionEnd?>(null);

    private readonly string sessionId;
    private readonly SocketIoListener listener;
    private readonly ExchangeContext context;
    private readonly TlsSession? tlsSession;
    private readonly CancellationTokenSource ending = new();
    private readonly TaskCompletionSource<SocketIoSessionEnd> ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock gate = new();
    private readonly List<string> connectedNamespaces = [];
    private SocketIoTransportState transport;
    private TaskCompletionSource? heldPollWake;
    private bool isPosting;
    private SocketIoPendingBinaryPacket? pendingBinary;
    private bool hasReadSocketIoPacket;
    private DateTimeOffset heartbeatFrom;
    private DateTimeOffset? pingSentAt;
    private DateTimeOffset? noNamespaceSince;
    private Task<SocketIoSessionEnd>? finished;

    /// <summary>
    /// Creates a session; its clock starts with <see cref="Start"/>.
    /// </summary>
    /// <param name="sessionId">The Engine.IO <c>sid</c> reserved for it.</param>
    /// <param name="listener">What the listener's sessions share.</param>
    /// <param name="context">The exchange that opened it: its clock, limits and log.</param>
    /// <param name="tlsSession">The TLS session of the connection that opened it, handed to the policy with each login.</param>
    /// <param name="transport">The transport it opens on: <see cref="SocketIoTransportState.Polling"/> or <see cref="SocketIoTransportState.WebSocket"/>.</param>
    public SocketIoSession(string sessionId, SocketIoListener listener, ExchangeContext context, TlsSession? tlsSession, SocketIoTransportState transport)
    {
        this.sessionId = sessionId;
        this.listener = listener;
        this.context = context;
        this.tlsSession = tlsSession;
        this.transport = transport;
        var openedAt = context.TimeProvider.GetUtcNow();
        heartbeatFrom = openedAt;
        noNamespaceSince = openedAt;
    }

    /// <summary>
    /// The packets waiting to be sent, in order.
    /// </summary>
    public SocketIoOutbox Outbox { get; } = new(SocketIoOutbox.MaxUnsentBytes);

    /// <summary>
    /// Cancelled once the session's end is decided, so every loop serving it stops.
    /// </summary>
    public CancellationToken EndingToken => ending.Token;

    /// <summary>
    /// Completes with the session's end once its clock has stopped and its outbox takes no more.
    /// </summary>
    public Task<SocketIoSessionEnd> Finished => finished!;

    /// <summary>
    /// The open packet: <c>upgrades</c> is <c>["websocket"]</c> on a polling session and <c>[]</c> on
    /// a WebSocket one (ADR-0120, decisions 2 and 3).
    /// </summary>
    public EngineIoPacket OpenPacket()
    {
        string[] upgrades = transport == SocketIoTransportState.Polling ? ["websocket"] : [];

        return EngineIoOpenPacket.Write(sessionId, upgrades, EngineIoOpenPacket.PingIntervalMilliseconds, EngineIoOpenPacket.PingTimeoutMilliseconds, context.Limits.MaxMessageBytes);
    }

    /// <summary>
    /// Starts the clock; <see cref="Finished"/> completes once the end is decided.
    /// </summary>
    public void Start() => finished = RunAsync();

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
    /// Queues an item for the client; past <see cref="SocketIoOutbox.MaxUnsentBytes"/> unsent, it is
    /// dropped and the session is closed <c>1008</c>.
    /// </summary>
    public void Deliver(SocketIoOutboxItem item)
    {
        if (!Outbox.TryQueue(item))
        {
            context.Log.Note($"Frames dropped: more than {SocketIoOutbox.MaxUnsentBytes} bytes would be unsent.");
            Decide(SocketIoSessionEnd.WebSocketClose(SocketIoSessionEnd.PolicyViolation, $"more than {SocketIoOutbox.MaxUnsentBytes} bytes would be unsent"));
        }
    }

    /// <summary>
    /// Decides the session's end, unless it is already decided.
    /// </summary>
    public void Decide(SocketIoSessionEnd end) => ended.TrySetResult(end);

    /// <summary>
    /// Runs <paramref name="loop"/> with <see cref="EndingToken"/>: it stops quietly once the end is
    /// decided, and a connection failure ends the session with nothing more written.
    /// </summary>
    public async Task GuardAsync(Func<CancellationToken, Task> loop)
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

    /// <summary>
    /// The item that sends <paramref name="packet"/> as an Engine.IO message, then its attachments as
    /// binary messages.
    /// </summary>
    public static SocketIoOutboxItem Item(SocketIoPacket packet, IReadOnlyList<byte[]> attachments) =>
        new([new EngineIoPacket(EngineIoPacketType.Message, SocketIoPacketWriter.Write(packet), IsBinary: false), .. attachments.Select(attachment => EngineIoPacket.Binary(attachment))]);

    /// <summary>
    /// The DISCONNECT for each connected namespace, the farewell a limit's cancellation writes.
    /// </summary>
    public SocketIoOutboxItem[] Farewell()
    {
        lock (gate)
        {
            return [.. connectedNamespaces.Select(name => Item(TextPacket(SocketIoPacketType.Disconnect, name, string.Empty), []))];
        }
    }

    /// <summary>
    /// Reads one Engine.IO packet from the client (EIO "Protocol"): close ends the session, pong
    /// answers the heartbeat, message carries Socket.IO or an attachment, ping and noop are ignored;
    /// a client open, an upgrade outside an upgrade, or a binary packet no binary Socket.IO packet
    /// awaits, is a protocol error.
    /// </summary>
    /// <returns>The session's end, or <see langword="null"/> while it goes on.</returns>
    public Task<SocketIoSessionEnd?> AcceptPacketAsync(EngineIoPacket packet, CancellationToken token)
    {
        if (pendingBinary is not null)
        {
            return AcceptAttachmentAsync(pendingBinary, packet, token);
        }

        if (packet.IsBinary)
        {
            return Ends(SocketIoSessionEnd.ProtocolErrorClose("a binary message arrived with no binary packet awaiting it"));
        }

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

    /// <summary>
    /// Answers one HTTP long-polling <c>GET</c> (EIO "Receiving data"): held until a packet is queued,
    /// then every queued packet; <c>6</c> (noop) during an upgrade; <c>1</c> (close) once the session
    /// has ended. A <c>GET</c> while another is held closes the session.
    /// </summary>
    /// <returns>The packets to answer with, or <see langword="null"/> for code 3.</returns>
    public async Task<IReadOnlyList<EngineIoPacket>?> PollAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource wake;
        lock (gate)
        {
            if (transport == SocketIoTransportState.WebSocket)
            {
                return null;
            }

            if (heldPollWake is not null)
            {
                Decide(SocketIoSessionEnd.ProtocolErrorClose("a second concurrent poll arrived"));

                return null;
            }

            if (transport == SocketIoTransportState.Probed)
            {
                return [NoopPacket];
            }

            heldPollWake = wake = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        try
        {
            var queued = Outbox.WaitToTakeAsync(cancellationToken);
            if (await Task.WhenAny(queued, wake.Task) == wake.Task)
            {
                return [NoopPacket];
            }

            return await queued ? Outbox.TakeAll() : [ClosePacket];
        }
        finally
        {
            lock (gate)
            {
                heldPollWake = null;
            }
        }
    }

    /// <summary>
    /// Begins one HTTP long-polling <c>POST</c> (EIO "Sending data"). A <c>POST</c> while another is
    /// read closes the session; one after the move to a WebSocket does not.
    /// </summary>
    /// <returns><see langword="false"/> for code 3.</returns>
    public bool TryBeginPost()
    {
        lock (gate)
        {
            if (transport == SocketIoTransportState.WebSocket)
            {
                return false;
            }

            if (isPosting)
            {
                Decide(SocketIoSessionEnd.ProtocolErrorClose("a second concurrent post arrived"));

                return false;
            }

            isPosting = true;

            return true;
        }
    }

    /// <summary>
    /// Ends the <c>POST</c> <see cref="TryBeginPost"/> began.
    /// </summary>
    public void EndPost()
    {
        lock (gate)
        {
            isPosting = false;
        }
    }

    /// <summary>
    /// Begins the upgrade of a polling session to a WebSocket; refused when the session is not on
    /// polling alone (a WebSocket session, or one already probing).
    /// </summary>
    public bool TryBeginProbe()
    {
        lock (gate)
        {
            if (transport != SocketIoTransportState.Polling)
            {
                return false;
            }

            transport = SocketIoTransportState.Probing;

            return true;
        }
    }

    /// <summary>
    /// Once <c>2probe</c> is answered: the held poll, and every poll until the upgrade completes, is
    /// answered <c>6</c> (noop) so the client can pause polling (EIO "Upgrade").
    /// </summary>
    public void AnswerPollsWithNoop()
    {
        TaskCompletionSource? wake;
        lock (gate)
        {
            transport = SocketIoTransportState.Probed;
            wake = heldPollWake;
        }

        wake?.TrySetResult();
    }

    /// <summary>
    /// The probing WebSocket ended before <c>5</c>: the session stays on polling.
    /// </summary>
    public void CancelProbe()
    {
        lock (gate)
        {
            transport = SocketIoTransportState.Polling;
        }
    }

    /// <summary>
    /// The client's <c>5</c>: the session moves to the WebSocket.
    /// </summary>
    public void CompleteUpgrade()
    {
        lock (gate)
        {
            transport = SocketIoTransportState.WebSocket;
        }
    }

    private static SocketIoPacket TextPacket(SocketIoPacketType type, string name, string payload) =>
        new(type, name, 0, null, Encoding.ASCII.GetBytes(payload));

    private static Task<SocketIoSessionEnd?> Ends(SocketIoSessionEnd end) => Task.FromResult<SocketIoSessionEnd?>(end);

    private static DateTimeOffset Earliest(DateTimeOffset first, DateTimeOffset second, DateTimeOffset third)
    {
        var earlier = first < second ? first : second;

        return earlier < third ? earlier : third;
    }

    // Runs the clock until the end is decided, then takes nothing more.
    private async Task<SocketIoSessionEnd> RunAsync()
    {
        var clock = GuardAsync(RunClockAsync);
        var end = await ended.Task;
        await ending.CancelAsync();
        await clock;
        Outbox.Complete();
        context.Log.Note($"Engine.IO session ended: {end.Why}.");

        return end;
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

    private Task<SocketIoSessionEnd?> AcceptAttachmentAsync(SocketIoPendingBinaryPacket pending, EngineIoPacket packet, CancellationToken token)
    {
        if (!packet.IsBinary)
        {
            return Ends(SocketIoSessionEnd.ProtocolErrorClose("a text message arrived before every attachment"));
        }

        if (!pending.TryAdd(packet.Data.ToArray()))
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

        var login = SocketIoConnectCredentials.Read(packet.Payload, context.Scheme, tlsSession);
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
        Deliver(Item(TextPacket(SocketIoPacketType.Connect, name, "{\"sid\":\"" + socketId + "\"}"), []));
    }

    private void SendConnectError(string name, string message) =>
        Deliver(Item(TextPacket(SocketIoPacketType.ConnectError, name, "{\"message\":\"" + message + "\"}"), []));

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

        var reply = Item(packet with { AckId = null }, attachments);
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

    private void Broadcast(string name, SocketIoOutboxItem reply)
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
        Deliver(Item(ack, attachments));
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

        Deliver(SocketIoOutboxItem.Of(EngineIoPacket.Text(EngineIoPacketType.Ping, string.Empty)));
    }
}
