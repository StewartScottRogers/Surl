using System.Globalization;
using System.Text;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// One hub connection over the WebSocket transport, from the <c>101</c> to its close
/// (TransportProtocols.md "WebSockets"; HubProtocol.md "Overview" to "Ping"; ADR-0119, decisions 1,
/// 3, 4 and 7).
/// </summary>
/// <remarks>
/// <para>
/// Four kinds of work run at once, their writes one at a time and in the order they were queued: the
/// read loop (WebSocket frames, then hub records cut at <c>0x1E</c>, then messages, <c>Collect</c>
/// uploads included), the clock (the handshake deadline, the keep-alive Ping and the client timeout,
/// all on <see cref="ExchangeContext.TimeProvider"/>), one task per <c>Count</c> stream, and one per
/// <c>Broadcast</c> Invocation another connection queued. Each record is sent as one text message.
/// Once the handshake is accepted the session joins the hub's broadcast receivers.
/// </para>
/// <para>
/// The first of them to decide the connection's end wins: the others stop, then the end's hub Close,
/// if any, and its WebSocket Close are written, and the server lingers for the client's close. A
/// client that vanished ends with nothing written; a cancelled exchange, at shutdown or at a limit,
/// gets the farewell on its own deadline.
/// </para>
/// </remarks>
internal sealed class SignalRHubSession : IDisposable, ISignalRBroadcastReceiver
{
    /// <summary>
    /// The most bytes of records queued for a connection and not yet written (ADR-0119, decision 7).
    /// </summary>
    public const long MaxUnsentBytes = 1048576;

    /// <summary>
    /// How long the farewell to a cancelled exchange may take to write (ADR-0006 section 5's limit-reply deadline).
    /// </summary>
    public static readonly TimeSpan FarewellWriteDeadline = TimeSpan.FromSeconds(1);

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
    private readonly SignalRHubConnections hub;

    // The uploads being read, by stream ID; only the read loop touches them.
    private readonly Dictionary<string, SignalRUpload> uploads = new(StringComparer.Ordinal);
    private DateTimeOffset lastSentAt;
    private DateTimeOffset lastReceivedAt;
    private long unsentBytes;
    private bool isHandshakeDone;
    private bool isMessagePack;
    private bool takesNoMoreWork;

    /// <summary>
    /// Creates the session that runs a hub over one transport. Disposing it disposes the transport.
    /// </summary>
    /// <param name="transport">What carries the connection's bytes.</param>
    /// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
    /// <param name="invocationFaults">Asks the exchange's fault plan for a chosen Completion error per invocation (ADR-0144).</param>
    /// <param name="hub">The hub's connections, which <c>Broadcast</c> reaches.</param>
    /// <param name="reconnect">The connection's numbering when it negotiated stateful reconnect (ADR-0119, decision 6), else <see langword="null"/>.</param>
    public SignalRHubSession(ISignalRHubTransport transport, ExchangeContext context, SignalRInvocationFaults invocationFaults, SignalRHubConnections hub, SignalRReconnectState? reconnect = null)
    {
        this.reconnect = reconnect;
        this.transport = transport;
        writeGate = transport.WriteGate;
        this.context = context;
        this.invocationFaults = invocationFaults;
        this.hub = hub;
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
        using (context.CancellationToken.Register(() => Decide(SignalRSessionEnd.Farewell("the exchange was cancelled, at shutdown or at a limit"))))
        {
            var readLoop = GuardAsync(ReadLoopAsync);
            var clock = GuardAsync(RunClockAsync);
            end = await ended.Task;
            hub.Leave(this);
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

    /// <inheritdoc/>
    public void ReceiveBroadcast(HubInvocation invocation)
    {
        var record = Encode(invocation);
        lock (gate)
        {
            if (takesNoMoreWork)
            {
                return;
            }

            if (unsentBytes + record.Length <= MaxUnsentBytes)
            {
                unsentBytes += record.Length;

                // Started under the gate so the write queues before the session's next record,
                // and before the session stops taking work.
                TrackLocked(GuardAsync(token => WriteCountedAsync(record, token)));

                return;
            }
        }

        Decide(SignalRSessionEnd.HubClose(SignalRSessionEnd.ProtocolErrorText, SignalRSessionEnd.PolicyViolation, $"a broadcast would have left more than {MaxUnsentBytes} bytes unsent"));
    }

    // Writes a record already counted as unsent, and counts it sent once written or given up.
    private async Task WriteCountedAsync(byte[] record, CancellationToken token)
    {
        try
        {
            await SendNumberedAsync(record, token);
        }
        finally
        {
            lock (gate)
            {
                unsentBytes -= record.Length;
            }
        }
    }

    // The tasks still to wait for; from now on no broadcast is taken.
    private Task[] StreamTasks()
    {
        lock (gate)
        {
            takesNoMoreWork = true;

            return [.. streamTasks];
        }
    }

    private void TrackLocked(Task task)
    {
        streamTasks.RemoveAll(tracked => tracked.IsCompleted);
        streamTasks.Add(task);
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
        hub.Join(this);

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
                await CancelInvocationAsync(cancel.InvocationId, token);
                return null;
            case HubPing:
                return null;
            case HubStreamItem item:
                return await AcceptUploadItemAsync(item, token);
            case HubCompletion completion:
                return await AcceptUploadCompletionAsync(completion, token);
            case HubClose:
                return SignalRSessionEnd.WebSocketClose(SignalRSessionEnd.NormalClosure, "the client sent a hub Close");
            default:
                // An Ack or Sequence on a connection that did not ask for stateful reconnect.
                return SignalRSessionEnd.HubProtocolError($"the client sent a {message.Type} the connection did not negotiate");
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

        return await AnswerAsync(invocation, call, token);
    }

    private async Task<SignalRSessionEnd?> AnswerAsync(HubInvocation invocation, SignalRHubCall call, CancellationToken token)
    {
        if (call.Broadcasts)
        {
            hub.Broadcast(new HubInvocation(false, null, "Broadcast", invocation.Arguments, []));
        }

        if (call.UploadStreamId is { } streamId)
        {
            return await StartUploadAsync(invocation, streamId, token);
        }

        if (call.StreamItemCount is { } count)
        {
            // Only a StreamInvocation streams, and it always has an invocationId.
            await StartStreamAsync(invocation.InvocationId!, count, token);
        }
        else
        {
            await CompleteAsync(invocation.InvocationId, call.Result, call.Error, token);
        }

        return null;
    }

    // A non-blocking Invocation is completed with nothing at all.
    private async Task CompleteAsync(string? invocationId, HubJsonValue? result, string? error, CancellationToken token)
    {
        if (invocationId is not null)
        {
            await SendNumberedAsync(Encode(new HubCompletion(invocationId, result, error)), token);
        }
    }

    private bool IsInProgress(string id)
    {
        lock (gate)
        {
            return streams.ContainsKey(id) || FindUpload(id) is not null;
        }
    }

    // The upload a Collect still to be completed reads for invocationId.
    private SignalRUpload? FindUpload(string invocationId) =>
        uploads.Values.FirstOrDefault(upload => !upload.IsAbandoned && upload.InvocationId == invocationId);

    private int InvocationsInProgress()
    {
        lock (gate)
        {
            return streams.Count + uploads.Count;
        }
    }

    // Collect: the stream ID is read from now on, until the client's Completion of it.
    private async Task<SignalRSessionEnd?> StartUploadAsync(HubInvocation invocation, string streamId, CancellationToken token)
    {
        if (uploads.ContainsKey(streamId))
        {
            return SignalRSessionEnd.HubProtocolError("a stream ID already being uploaded was used again");
        }

        if (InvocationsInProgress() >= MaxInvocationsInProgress)
        {
            await CompleteAsync(invocation.InvocationId, null, TooManyInvocationsText, token);

            return null;
        }

        lock (gate)
        {
            uploads.Add(streamId, new SignalRUpload(invocation.InvocationId, invocation.IsStreaming));
        }

        return null;
    }

    private async Task<SignalRSessionEnd?> AcceptUploadItemAsync(HubStreamItem item, CancellationToken token)
    {
        if (!uploads.TryGetValue(item.InvocationId, out var upload))
        {
            return SignalRSessionEnd.HubProtocolError("the client sent a StreamItem for no upload stream the server reads");
        }

        if (upload.IsAbandoned)
        {
            return null;
        }

        if (!upload.TryAdd(item.Item, context.Limits.MaxUploadBytes))
        {
            await CompleteAsync(upload.InvocationId, null, SignalRHubMethods.UploadTooLargeText, token);
        }
        else if (upload.IsStreaming)
        {
            // A streaming Collect always has an invocationId.
            await SendNumberedAsync(Encode(new HubStreamItem(upload.InvocationId!, item.Item)), token);
        }

        return null;
    }

    // The client's Completion ends its upload, error or not; the Collect is completed unless it already was.
    private async Task<SignalRSessionEnd?> AcceptUploadCompletionAsync(HubCompletion completion, CancellationToken token)
    {
        SignalRUpload? upload;
        lock (gate)
        {
            uploads.Remove(completion.InvocationId, out upload);
        }

        if (upload is null)
        {
            return SignalRSessionEnd.HubProtocolError("the client sent a Completion for no upload stream the server reads");
        }

        if (!upload.IsAbandoned)
        {
            await CompleteAsync(upload.InvocationId, upload.IsStreaming ? null : upload.ItemsAsArray(isMessagePack), null, token);
        }

        return null;
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
            TrackLocked(stream);
        }
    }

    private CancellationTokenSource? TryRegisterStream(string id, CancellationToken token)
    {
        lock (gate)
        {
            if (streams.Count + uploads.Count >= MaxInvocationsInProgress)
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

    // A Count stops and completes; a Collect, streaming or waiting for its upload, completes with
    // no result and drops the rest of its upload; one naming nothing in progress is ignored
    // (HubProtocol.md "Cancellation"; ADR-0119, decision 1).
    private async Task CancelInvocationAsync(string id, CancellationToken token)
    {
        CancellationTokenSource? cancellation;
        SignalRUpload? upload;
        lock (gate)
        {
            streams.TryGetValue(id, out cancellation);
            upload = FindUpload(id);
        }

        if (cancellation is not null)
        {
            await cancellation.CancelAsync();
        }
        else if (upload is not null)
        {
            upload.Abandon();
            await CompleteAsync(id, null, null, token);
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
