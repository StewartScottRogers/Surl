using System.Globalization;
using System.Text;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;
using Surl.WebSocket;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// One hub connection over the WebSocket transport, from the <c>101</c> to its close
/// (TransportProtocols.md "WebSockets"; HubProtocol.md "Overview" to "Ping"; ADR-0119, decisions 1,
/// 3, 4 and 7).
/// </summary>
/// <remarks>
/// <para>
/// Three kinds of work run at once, their writes one at a time: the read loop (WebSocket frames, then
/// hub records cut at <c>0x1E</c>, then messages), the clock (the handshake deadline, the keep-alive
/// Ping and the client timeout, all on <see cref="ExchangeContext.TimeProvider"/>), and one task per
/// <c>Count</c> stream. Each record is sent as one text message.
/// </para>
/// <para>
/// The first of them to decide the connection's end wins: the others stop, then the end's hub Close,
/// if any, and its WebSocket Close are written, and the server lingers for the client's close. A
/// client that vanished, or an exchange cancelled, ends with nothing written.
/// </para>
/// </remarks>
internal sealed class SignalRHubSession : IDisposable
{
    /// <summary>
    /// The most streams in progress on one connection (ADR-0119, decision 7).
    /// </summary>
    public const int MaxInvocationsInProgress = 100;

    /// <summary>
    /// The Completion <c>error</c> for an invocation past <see cref="MaxInvocationsInProgress"/>.
    /// </summary>
    public const string TooManyInvocationsText = "Too many invocations in progress.";

    /// <summary>
    /// How long the server may send no record before it sends a Ping (ADR-0119, decision 4).
    /// </summary>
    public static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// How long the client may send no record before the server closes it (ADR-0119, decision 4).
    /// </summary>
    public static readonly TimeSpan ClientTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The time between two of <c>Count</c>'s StreamItems (ADR-0119, decision 1).
    /// </summary>
    public static readonly TimeSpan CountItemInterval = TimeSpan.FromMilliseconds(100);

    private static readonly TimeSpan MaxTimerDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1.0);

    private readonly ISignalRHubTransport transport;
    private readonly ExchangeContext context;
    private readonly SignalRInvocationFaults invocationFaults;
    private readonly SignalRReconnectState? reconnect;
    private readonly HubRecordReader recordReader;
    private readonly HubMessagePackRecordReader messagePackRecordReader;
    private readonly SemaphoreSlim writeGate;
    private readonly CancellationTokenSource ending = new();
    private readonly TaskCompletionSource<SignalRSessionEnd> ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource handshakeAccepted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock gate = new();
    private readonly Dictionary<string, CancellationTokenSource> streams = new(StringComparer.Ordinal);
    private readonly List<CancellationTokenSource> streamCancellations = [];
    private readonly List<Task> streamTasks = [];
    private DateTimeOffset lastSentAt;
    private DateTimeOffset lastReceivedAt;
    private bool isHandshakeDone;
    private bool isMessagePack;

    /// <summary>
    /// Creates the session that runs a hub over one transport. Disposing it disposes the transport.
    /// </summary>
    /// <param name="transport">What carries the connection's bytes.</param>
    /// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
    /// <param name="invocationFaults">Asks the exchange's fault plan for a chosen Completion error per invocation (ADR-0144).</param>
    /// <param name="reconnect">The connection's numbering when it negotiated stateful reconnect (ADR-0119, decision 6), else <see langword="null"/>.</param>
    public SignalRHubSession(ISignalRHubTransport transport, ExchangeContext context, SignalRInvocationFaults invocationFaults, SignalRReconnectState? reconnect = null)
    {
        this.reconnect = reconnect;
        this.transport = transport;
        writeGate = transport.WriteGate;
        this.context = context;
        this.invocationFaults = invocationFaults;
        var maxMessageBytes = context.Limits.MaxMessageBytes;
        recordReader = new HubRecordReader(maxMessageBytes);
        messagePackRecordReader = new HubMessagePackRecordReader(maxMessageBytes);
    }

    /// <summary>
    /// Serves the connection until its end is decided, then writes that end.
    /// </summary>
    /// <returns>How the connection ended.</returns>
    public async Task<SignalRSessionEnd> RunAsync()
    {
        SignalRSessionEnd end;
        using (context.CancellationToken.Register(() => Decide(SignalRSessionEnd.Silent("the exchange was cancelled"))))
        {
            var readLoop = GuardAsync(ReadLoopAsync);
            var clock = GuardAsync(RunClockAsync);
            end = await ended.Task;
            await ending.CancelAsync();
            await Task.WhenAll(readLoop, clock);
            await Task.WhenAll(StreamTasks());
        }

        context.Log.Note($"SignalR connection ended: {end.Why}.");
        await WriteEndAsync(end);

        return end;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var cancellation in streamCancellations)
        {
            cancellation.Dispose();
        }

        ending.Dispose();
        transport.Dispose();
    }

    private void Decide(SignalRSessionEnd end) => ended.TrySetResult(end);

    private byte[] Encode(HubMessage message) =>
        isMessagePack ? HubMessagePackWriter.Write(message) : HubMessageJsonWriter.Write(message);

    private bool TryReadMessage(ReadOnlyMemory<byte> record, out HubMessage? message) =>
        isMessagePack ? HubMessagePackReader.TryRead(record, out message) : HubMessageJsonReader.TryRead(record, out message);

    // One of Count's items: the integer as JSON text, or as a MessagePack integer.
    private byte[] CountItem(int item)
    {
        if (!isMessagePack)
        {
            return Encoding.ASCII.GetBytes(item.ToString(CultureInfo.InvariantCulture));
        }

        var writer = new MessagePackWriter();
        writer.WriteUnsigned(item);

        return writer.Written.ToArray();
    }

    private Task[] StreamTasks()
    {
        lock (gate)
        {
            return [.. streamTasks];
        }
    }

    // Stops a loop quietly once the end is decided, and ends the connection when it fails.
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
            Decide(SignalRSessionEnd.Silent($"the connection failed ({failure.Message})"));
        }
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        while (true)
        {
            var read = await transport.ReadAsync(token);
            var end = read.Payload is { } payload ? await AcceptPayloadAsync(payload, token) : read.End;
            if (end is not null)
            {
                Decide(end);

                return;
            }
        }
    }

    private async Task<SignalRSessionEnd?> AcceptPayloadAsync(byte[] payload, CancellationToken token)
    {
        var records = new List<ReadOnlyMemory<byte>>();
        var isWithinLimit = isMessagePack ? messagePackRecordReader.TryAppend(payload, records) : recordReader.TryAppend(payload, records);
        foreach (var record in records)
        {
            if (await AcceptRecordAsync(record, token) is { } end)
            {
                return end;
            }
        }

        return isWithinLimit
            ? null
            : SignalRSessionEnd.HubClose(SignalRSessionEnd.ProtocolErrorText, SignalRSessionEnd.MessageTooBig, "a hub record is past --max-message");
    }

    private async Task<SignalRSessionEnd?> AcceptRecordAsync(ReadOnlyMemory<byte> record, CancellationToken token)
    {
        lock (gate)
        {
            lastReceivedAt = context.TimeProvider.GetUtcNow();
        }

        if (!isHandshakeDone)
        {
            return await AcceptHandshakeAsync(record, token);
        }

        return TryReadMessage(record, out var message)
            ? await AcceptMessageAsync(message!, token)
            : SignalRSessionEnd.HubProtocolError("a client record is not a message the server reads");
    }

    private async Task<SignalRSessionEnd?> AcceptHandshakeAsync(ReadOnlyMemory<byte> record, CancellationToken token)
    {
        var outcome = HubHandshake.Judge(record);
        if (outcome == HubHandshakeOutcome.AcceptedMessagePack && !transport.CarriesBinary)
        {
            // MessagePack is a Binary transfer format; Server-Sent Events carries Text only.
            outcome = HubHandshakeOutcome.ProtocolNotAvailable;
        }

        await SendRecordAsync(HubHandshake.Response(outcome), token);
        if (outcome is not (HubHandshakeOutcome.Accepted or HubHandshakeOutcome.AcceptedMessagePack))
        {
            return SignalRSessionEnd.WebSocketClose(SignalRSessionEnd.NormalClosure, $"the handshake was refused ({outcome})");
        }

        // The response above is JSON; every message after it is in the encoding the client chose.
        isMessagePack = outcome == HubHandshakeOutcome.AcceptedMessagePack;
        isHandshakeDone = true;
        handshakeAccepted.TrySetResult();
        if (reconnect is { IsResumption: true })
        {
            await ResendAsync(reconnect, token);
        }

        return null;
    }

    // A resumed connection's Sequence names the first record kept, then every kept record follows in order.
    private async Task ResendAsync(SignalRReconnectState state, CancellationToken token)
    {
        var (firstSequenceId, records) = state.Resend();
        await SendRecordAsync(Encode(new HubSequence(firstSequenceId)), token);
        foreach (var record in records)
        {
            await SendRecordAsync(record, token);
        }
    }

    // On a stateful-reconnect connection each Invocation, StreamItem and Completion the client sends is
    // numbered and acknowledged at once; one the client sends again after a reconnect is dropped.
    private async Task<bool> IsNewAfterAcknowledgingAsync(HubMessage message, CancellationToken token)
    {
        if (reconnect is null || message is not (HubInvocation or HubStreamItem or HubCompletion))
        {
            return true;
        }

        if (reconnect.CountReceived() is not { } received)
        {
            return false;
        }

        await SendRecordAsync(Encode(new HubAck(received)), token);

        return true;
    }

    private static SignalRSessionEnd? AcceptReconnectMessage(SignalRReconnectState state, HubMessage message)
    {
        if (message is HubSequence sequence)
        {
            state.AcceptClientSequence(sequence.SequenceId);

            return null;
        }

        return state.TryAcknowledge(((HubAck)message).SequenceId)
            ? null
            : SignalRSessionEnd.HubProtocolError("a client Ack is above the last record the server sent");
    }

    private async Task<SignalRSessionEnd?> AcceptMessageAsync(HubMessage message, CancellationToken token)
    {
        if (!await IsNewAfterAcknowledgingAsync(message, token))
        {
            return null;
        }

        switch (message)
        {
            case HubAck or HubSequence when reconnect is not null:
                return AcceptReconnectMessage(reconnect, message);
            case HubInvocation invocation:
                return await InvokeAsync(invocation, token);
            case HubCancelInvocation cancel:
                await CancelStreamAsync(cancel.InvocationId);
                return null;
            case HubPing:
                return null;
            case HubClose:
                return SignalRSessionEnd.WebSocketClose(SignalRSessionEnd.NormalClosure, "the client sent a hub Close");
            default:
                return SignalRSessionEnd.HubProtocolError($"the client sent a {message.Type} for no upload stream the server reads");
        }
    }

    private async Task<SignalRSessionEnd?> InvokeAsync(HubInvocation invocation, CancellationToken token)
    {
        if (invocation.InvocationId is { } id && IsInProgress(id))
        {
            return SignalRSessionEnd.HubProtocolError("an invocationId already in progress was used again");
        }

        // ADR-0144: a chosen-error fault rule completes the invocation in place of the hub method.
        var call = invocation.InvocationId is not null && await invocationFaults.FindErrorAsync(invocation) is { } chosenError
            ? SignalRHubCall.Failed(chosenError)
            : SignalRHubMethods.Call(invocation, isMessagePack);
        if (call.StreamItemCount is { } count)
        {
            // Only a StreamInvocation streams, and it always has an invocationId.
            await StartStreamAsync(invocation.InvocationId!, count, token);
        }
        else if (invocation.InvocationId is { } completed)
        {
            await SendNumberedAsync(Encode(new HubCompletion(completed, call.Result, call.Error)), token);
        }

        return null;
    }

    private bool IsInProgress(string id)
    {
        lock (gate)
        {
            return streams.ContainsKey(id);
        }
    }

    private async Task StartStreamAsync(string id, int count, CancellationToken token)
    {
        if (TryRegisterStream(id, token) is not { } cancellation)
        {
            await SendNumberedAsync(Encode(new HubCompletion(id, null, TooManyInvocationsText)), token);

            return;
        }

        var stream = GuardAsync(_ => StreamAsync(id, count, cancellation.Token));
        lock (gate)
        {
            streamTasks.Add(stream);
        }
    }

    private CancellationTokenSource? TryRegisterStream(string id, CancellationToken token)
    {
        lock (gate)
        {
            if (streams.Count >= MaxInvocationsInProgress)
            {
                return null;
            }

            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            streams.Add(id, cancellation);
            streamCancellations.Add(cancellation);

            return cancellation;
        }
    }

    // Count's items, the first at once and each next CountItemInterval later, then its Completion;
    // a CancelInvocation stops the items and sends the Completion at once.
    private async Task StreamAsync(string id, int count, CancellationToken cancelled)
    {
        try
        {
            for (var item = 1; item <= count; item++)
            {
                if (item > 1)
                {
                    await Task.Delay(CountItemInterval, context.TimeProvider, cancelled);
                }

                await SendNumberedAsync(Encode(new HubStreamItem(id, new HubJsonValue(CountItem(item)))), ending.Token);
            }
        }
        catch (OperationCanceledException) when (!ending.IsCancellationRequested)
        {
            context.Log.Note("A stream was cancelled by the client.");
        }

        lock (gate)
        {
            streams.Remove(id);
        }

        await SendNumberedAsync(Encode(new HubCompletion(id, null, null)), ending.Token);
    }

    // A CancelInvocation naming no stream in progress is ignored (HubProtocol.md "Cancellation").
    private async Task CancelStreamAsync(string id)
    {
        CancellationTokenSource? cancellation;
        lock (gate)
        {
            streams.TryGetValue(id, out cancellation);
        }

        if (cancellation is not null)
        {
            await cancellation.CancelAsync();
        }
    }

    private async Task RunClockAsync(CancellationToken token)
    {
        if (!await WaitForHandshakeAsync(token))
        {
            Decide(SignalRSessionEnd.WebSocketClose(SignalRSessionEnd.PolicyViolation, "no handshake request arrived within --head-timeout"));

            return;
        }

        while (true)
        {
            var now = context.TimeProvider.GetUtcNow();
            var (sendDue, receiveDue) = Deadlines();
            if (now >= receiveDue)
            {
                Decide(SignalRSessionEnd.HubClose(SignalRSessionEnd.ClientTimeoutText, SignalRSessionEnd.NormalClosure, "the client sent nothing within the client timeout"));

                return;
            }

            if (now >= sendDue)
            {
                await SendRecordAsync(Encode(new HubPing()), token);

                continue;
            }

            await Task.Delay((sendDue < receiveDue ? sendDue : receiveDue) - now, context.TimeProvider, token);
        }
    }

    private (DateTimeOffset SendDue, DateTimeOffset ReceiveDue) Deadlines()
    {
        lock (gate)
        {
            return (lastSentAt + KeepAliveInterval, lastReceivedAt + ClientTimeout);
        }
    }

    // True once the handshake is accepted; false when --head-timeout passes first.
    private async Task<bool> WaitForHandshakeAsync(CancellationToken token)
    {
        using var stopWaiting = CancellationTokenSource.CreateLinkedTokenSource(token);
        var headTimeout = context.Limits.HeadTimeout;
        var deadline = Task.Delay(headTimeout > MaxTimerDelay ? Timeout.InfiniteTimeSpan : headTimeout, context.TimeProvider, stopWaiting.Token);
        var first = await Task.WhenAny(handshakeAccepted.Task, deadline);
        await stopWaiting.CancelAsync();
        if (first == handshakeAccepted.Task)
        {
            return true;
        }

        // Throws when the connection's end cancelled the wait.
        await deadline;

        return false;
    }

    private async Task SendRecordAsync(byte[] record, CancellationToken token)
    {
        await WriteLockedAsync(record, token);
        NoteSent();
    }

    // An Invocation, StreamItem or Completion: on a stateful-reconnect connection it is numbered and kept
    // until an Ack covers it, in the order it is written; past the bound the connection ends.
    private async Task SendNumberedAsync(byte[] record, CancellationToken token)
    {
        if (reconnect is null)
        {
            await SendRecordAsync(record, token);

            return;
        }

        await writeGate.WaitAsync(token);
        try
        {
            if (!reconnect.TryKeep(record))
            {
                Decide(SignalRSessionEnd.HubProtocolError("the unacknowledged records passed the reconnect buffer's bound"));

                return;
            }

            await transport.WriteRecordAsync(record, isMessagePack, token);
        }
        finally
        {
            writeGate.Release();
        }

        NoteSent();
    }

    private void NoteSent()
    {
        lock (gate)
        {
            lastSentAt = context.TimeProvider.GetUtcNow();
        }
    }

    private async Task WriteLockedAsync(byte[] record, CancellationToken token)
    {
        await writeGate.WaitAsync(token);
        try
        {
            await transport.WriteRecordAsync(record, isMessagePack, token);
        }
        finally
        {
            writeGate.Release();
        }
    }

    private async Task WriteEndAsync(SignalRSessionEnd end)
    {
        var hubClose = end.HubCloseMessage is { } close ? Encode(close) : null;
        await transport.WriteEndAsync(end, hubClose, isMessagePack, context.CancellationToken);
    }
}
