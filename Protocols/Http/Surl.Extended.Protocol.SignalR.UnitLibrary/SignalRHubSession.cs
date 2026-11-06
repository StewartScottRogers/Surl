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

    // The longest header a client frame can have; --max-message bounds the payload alone.
    private const int MaxClientFrameHeaderBytes = 14;

    private static readonly TimeSpan MaxTimerDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1.0);

    private static readonly byte[] PingRecord = HubMessageJsonWriter.Write(new HubPing());

    // The answer to a frame after which the connection goes on.
    private static readonly Task<SignalRSessionEnd?> Continues = Task.FromResult<SignalRSessionEnd?>(null);

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly WebSocketFrameReader frameReader;
    private readonly WebSocketMessageReassembler reassembler;
    private readonly HubRecordReader recordReader;
    private readonly WebSocketLingeringClose lingeringClose;
    private readonly SemaphoreSlim writeGate = new(1, 1);
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

    /// <summary>
    /// Creates the session that follows one <c>101</c>.
    /// </summary>
    /// <param name="connection">Where frames are written.</param>
    /// <param name="reader">The reader that read the upgrade request, so bytes it already buffered come first.</param>
    /// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
    public SignalRHubSession(IConnection connection, HttpConnectionReader reader, ExchangeContext context)
    {
        this.connection = connection;
        this.context = context;
        var maxMessageBytes = context.Limits.MaxMessageBytes;
        frameReader = new WebSocketFrameReader(reader.ReadAsync, maxMessageBytes == 0 ? 0 : maxMessageBytes + MaxClientFrameHeaderBytes);
        reassembler = new WebSocketMessageReassembler(maxMessageBytes);
        recordReader = new HubRecordReader(maxMessageBytes);
        lingeringClose = new WebSocketLingeringClose(reader, context);
    }

    /// <summary>
    /// Serves the connection until its end is decided, then writes that end.
    /// </summary>
    /// <returns>A task that completes when the connection is done.</returns>
    public async Task RunAsync()
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
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var cancellation in streamCancellations)
        {
            cancellation.Dispose();
        }

        ending.Dispose();
        writeGate.Dispose();
    }

    private static SignalRSessionEnd FrameNotRead(WebSocketFrameReadOutcome outcome) => outcome switch
    {
        WebSocketFrameReadOutcome.ConnectionClosed => SignalRSessionEnd.Silent("the client closed the connection without a WebSocket Close"),
        WebSocketFrameReadOutcome.FrameTooLarge => SignalRSessionEnd.WebSocketClose(SignalRSessionEnd.MessageTooBig, "a client frame is past --max-message"),
        WebSocketFrameReadOutcome.CloseReasonNotUtf8 => SignalRSessionEnd.WebSocketClose(SignalRSessionEnd.InvalidPayload, "a client Close's reason is not valid UTF-8"),
        _ => SignalRSessionEnd.WebSocketClose(SignalRSessionEnd.ProtocolError, $"a client frame is malformed ({outcome})"),
    };

    // Every client frame is masked and sets no reserved bit, since no extension was agreed (RFC 6455 section 5).
    private static bool IsUnmaskedOrReserved(WebSocketFrame frame) => !frame.Masked || frame.Rsv1 || frame.Rsv2 || frame.Rsv3;

    private void Decide(SignalRSessionEnd end) => ended.TrySetResult(end);

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
            var result = await frameReader.ReadFrameAsync(token);
            var end = result.Frame is { } frame ? await AcceptFrameAsync(frame, token) : FrameNotRead(result.Outcome);
            if (end is not null)
            {
                Decide(end);

                return;
            }
        }
    }

    private Task<SignalRSessionEnd?> AcceptFrameAsync(WebSocketFrame frame, CancellationToken token)
    {
        if (IsUnmaskedOrReserved(frame))
        {
            return Ends(SignalRSessionEnd.WebSocketClose(SignalRSessionEnd.ProtocolError, "a client frame is unmasked or sets a reserved bit"));
        }

        var step = reassembler.Accept(frame);
        if (step.Outcome == WebSocketReassemblyOutcome.FragmentHeld)
        {
            return Continues;
        }

        if (step.Outcome == WebSocketReassemblyOutcome.MessageComplete)
        {
            return AcceptPayloadAsync(step.Message!.Payload, token);
        }

        return step.Outcome == WebSocketReassemblyOutcome.ControlFrame
            ? AcceptControlFrameAsync(step.ControlFrame!, token)
            : Ends(ReassemblyFailure(step.Outcome));
    }

    private static SignalRSessionEnd ReassemblyFailure(WebSocketReassemblyOutcome outcome) => outcome switch
    {
        WebSocketReassemblyOutcome.MessageTooLarge => SignalRSessionEnd.WebSocketClose(SignalRSessionEnd.MessageTooBig, "a client message is past --max-message"),
        WebSocketReassemblyOutcome.TextNotUtf8 => SignalRSessionEnd.WebSocketClose(SignalRSessionEnd.InvalidPayload, "a client text message is not valid UTF-8"),
        _ => SignalRSessionEnd.WebSocketClose(SignalRSessionEnd.ProtocolError, $"a client frame is out of its message's order ({outcome})"),
    };

    private static Task<SignalRSessionEnd?> Ends(SignalRSessionEnd end) => Task.FromResult<SignalRSessionEnd?>(end);

    // A WebSocket Ping is answered with a Pong and a Pong ignored; neither is a hub record, so
    // neither restarts the client timeout (ADR-0119, decision 4).
    private async Task<SignalRSessionEnd?> AcceptControlFrameAsync(WebSocketFrame frame, CancellationToken token)
    {
        if (frame.Opcode == WebSocketOpcode.Close)
        {
            return SignalRSessionEnd.WebSocketClose(SignalRSessionEnd.NormalClosure, "the client sent a WebSocket Close");
        }

        if (frame.Opcode == WebSocketOpcode.Ping)
        {
            await WriteFrameAsync(WebSocketFrameEncoder.EncodeFrame(true, WebSocketOpcode.Pong, frame.Payload), token);
        }

        return null;
    }

    private async Task<SignalRSessionEnd?> AcceptPayloadAsync(byte[] payload, CancellationToken token)
    {
        var records = new List<ReadOnlyMemory<byte>>();
        var isWithinLimit = recordReader.TryAppend(payload, records);
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

        return HubMessageJsonReader.TryRead(record, out var message)
            ? await AcceptMessageAsync(message!, token)
            : SignalRSessionEnd.HubProtocolError("a client record is not a message the server reads");
    }

    private async Task<SignalRSessionEnd?> AcceptHandshakeAsync(ReadOnlyMemory<byte> record, CancellationToken token)
    {
        var outcome = HubHandshake.Judge(record);
        await SendRecordAsync(HubHandshake.Response(outcome), token);
        if (outcome != HubHandshakeOutcome.Accepted)
        {
            return SignalRSessionEnd.WebSocketClose(SignalRSessionEnd.NormalClosure, $"the handshake was refused ({outcome})");
        }

        isHandshakeDone = true;
        handshakeAccepted.TrySetResult();

        return null;
    }

    private async Task<SignalRSessionEnd?> AcceptMessageAsync(HubMessage message, CancellationToken token)
    {
        switch (message)
        {
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

        var call = SignalRHubMethods.Call(invocation);
        if (call.StreamItemCount is { } count)
        {
            // Only a StreamInvocation streams, and it always has an invocationId.
            await StartStreamAsync(invocation.InvocationId!, count, token);
        }
        else if (invocation.InvocationId is { } completed)
        {
            await SendRecordAsync(HubMessageJsonWriter.Write(new HubCompletion(completed, call.Result, call.Error)), token);
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
            await SendRecordAsync(HubMessageJsonWriter.Write(new HubCompletion(id, null, TooManyInvocationsText)), token);

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

                var itemJson = Encoding.ASCII.GetBytes(item.ToString(CultureInfo.InvariantCulture));
                await SendRecordAsync(HubMessageJsonWriter.Write(new HubStreamItem(id, new HubJsonValue(itemJson))), ending.Token);
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

        await SendRecordAsync(HubMessageJsonWriter.Write(new HubCompletion(id, null, null)), ending.Token);
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
                await SendRecordAsync(PingRecord, token);

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
        await WriteFrameAsync(WebSocketFrameEncoder.EncodeFrame(true, WebSocketOpcode.Text, record), token);
        lock (gate)
        {
            lastSentAt = context.TimeProvider.GetUtcNow();
        }
    }

    private async Task WriteFrameAsync(byte[] frame, CancellationToken token)
    {
        await writeGate.WaitAsync(token);
        try
        {
            await connection.WriteAsync(frame, token);
        }
        finally
        {
            writeGate.Release();
        }
    }

    private async Task WriteEndAsync(SignalRSessionEnd end)
    {
        if (end.CloseCode is not { } code)
        {
            return;
        }

        var token = context.CancellationToken;
        if (end.HubCloseRecord is { } record)
        {
            await connection.WriteAsync(WebSocketFrameEncoder.EncodeFrame(true, WebSocketOpcode.Text, record), token);
        }

        await connection.WriteAsync(WebSocketFrameEncoder.EncodeClose(code, string.Empty), token);
        await connection.CompleteWritesAsync(token);
        await lingeringClose.LingerAsync(token);
    }
}
