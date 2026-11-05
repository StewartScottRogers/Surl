using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The message layer of one flow (RFC 7252 sections 4 and 5.2; ADR-0097 decision 2): every
/// datagram the client endpoint sends, read, rejected, recognised as a duplicate, or handed to the
/// request answering, and every answer sent the way its request asks.
/// </summary>
/// <remarks>
/// <para>
/// A CON request is answered piggybacked in an ACK when its answer is ready within
/// <see cref="CoapTransmissionParameters.SeparateResponseAfter"/>; otherwise an Empty ACK goes
/// then, and the answer follows as a separate CON response with the request's token and a new
/// Message ID, retransmitted from a random timeout between ACK_TIMEOUT and ACK_TIMEOUT x
/// ACK_RANDOM_FACTOR, doubled each time, until a matching ACK or RST arrives or MAX_RETRANSMIT
/// retransmissions have timed out. At most one CON of the server's is outstanding at a time
/// (NSTART 1). A NON request is answered with a NON.
/// </para>
/// <para>
/// A CON whose Message ID was seen within EXCHANGE_LIFETIME gets the same ACK or RST again and is
/// not served twice - an Empty ACK when the response went separately; a NON seen within
/// NON_LIFETIME is ignored. A NON with an unrecognised critical option is rejected silently: the
/// server never sends RST to a NON.
/// </para>
/// </remarks>
internal sealed class CoapFlowSession
{
    private static readonly HashSet<int> ResponseClasses = [2, 4, 5];

    private readonly IDatagramFlow flow;
    private readonly ExchangeContext context;
    private readonly Func<CoapMessage, CoapRequestOptions, CancellationToken, Task<CoapResponse>> answer;
    private readonly ICoapRandomSource random;
    private readonly CoapDuplicateCache duplicates = new(CoapTransmissionParameters.DuplicateCacheCapacity);
    private readonly SemaphoreSlim outstandingSlot = new(1, 1);
    private readonly Lock gate = new();
    private readonly List<Task> separateResponses = [];
    private ushort nextMessageId;
    private (ushort MessageId, TaskCompletionSource Acknowledged)? outstanding;

    /// <summary>
    /// Creates the message layer of <paramref name="flow"/>.
    /// </summary>
    /// <param name="flow">The flow.</param>
    /// <param name="context">The exchange: its clock, log and cancellation.</param>
    /// <param name="answer">Answers one request.</param>
    /// <param name="random">Draws the first Message ID and each retransmission's random factor.</param>
    public CoapFlowSession(
        IDatagramFlow flow,
        ExchangeContext context,
        Func<CoapMessage, CoapRequestOptions, CancellationToken, Task<CoapResponse>> answer,
        ICoapRandomSource random)
    {
        this.flow = flow;
        this.context = context;
        this.answer = answer;
        this.random = random;
        nextMessageId = random.DrawMessageId();
    }

    private CancellationToken Cancellation => context.CancellationToken;

    private DateTimeOffset Now => context.TimeProvider.GetUtcNow();

    /// <summary>
    /// Serves the flow's first datagram and every later one until the exchange is cancelled, then
    /// waits for the separate responses still under way to stop.
    /// </summary>
    /// <returns>A task that ends with <see cref="OperationCanceledException"/> when the exchange does.</returns>
    public Task ServeAsync() => StopAfterAsync(ServeDatagramsAsync()).Unwrap();

    private async Task ServeDatagramsAsync()
    {
        var datagram = flow.FirstDatagram;

        while (true)
        {
            await HandleAsync(datagram);
            datagram = await flow.ReceiveAsync(Cancellation);
        }
    }

    // Waits for serving to end however it ends, stops the separate responses under way, and hands
    // serving back so its outcome is the session's.
    private async Task<Task> StopAfterAsync(Task serving)
    {
        await Task.WhenAny(serving);
        await StopSeparateResponsesAsync();
        outstandingSlot.Dispose();

        return serving;
    }

    private async Task StopSeparateResponsesAsync()
    {
        Task[] underWay;

        lock (gate)
        {
            underWay = [.. separateResponses];
        }

        try
        {
            await Task.WhenAll(underWay);
        }
        catch (OperationCanceledException)
        {
            // Each separate response ends with the exchange's cancellation; the exchange is ending.
        }
    }

    private async Task HandleAsync(ReadOnlyMemory<byte> datagram)
    {
        var outcome = CoapMessageReader.Read(datagram.Span);

        switch (outcome.Kind)
        {
            case CoapReadOutcomeKind.NotCoap:
                context.Log.Note($"Ignored: {outcome.Fault}");
                break;
            case CoapReadOutcomeKind.FormatError:
                await RejectAsync(outcome.Type, outcome.MessageId, $"a message format error: {outcome.Fault}");
                break;
            default:
                await HandleMessageAsync(outcome.Message!);
                break;
        }
    }

    private async Task HandleMessageAsync(CoapMessage message)
    {
        if (message.Type is CoapMessageType.Acknowledgement or CoapMessageType.Reset)
        {
            MatchAcknowledgement(message);
        }
        else if (!message.Code.IsRequest)
        {
            await RejectAsync(message.Type, message.MessageId, $"code {message.Code}, which is no request.");
        }
        else if (duplicates.TryRecall(message.MessageId, Now, out var answered))
        {
            await AnswerDuplicateAsync(message, answered);
        }
        else if (message.Type == CoapMessageType.Confirmable)
        {
            await AnswerConfirmableAsync(message);
        }
        else
        {
            await AnswerNonConfirmableAsync(message);
        }
    }

    // Section 4.2: an Empty ACK or RST, or an ACK carrying a response, can end a retransmission;
    // an ACK carrying a request or a reserved class, and a RST that is not Empty, cannot.
    private static bool CanEndARetransmission(CoapMessage message) =>
        message.Code.IsEmpty || (message.Type == CoapMessageType.Acknowledgement && ResponseClasses.Contains(message.Code.Class));

    private TaskCompletionSource? TakeOutstanding(ushort messageId)
    {
        lock (gate)
        {
            if (outstanding is not { } pending || pending.MessageId != messageId)
            {
                return null;
            }

            outstanding = null;
            return pending.Acknowledged;
        }
    }

    private void MatchAcknowledgement(CoapMessage message)
    {
        var acknowledged = CanEndARetransmission(message) ? TakeOutstanding(message.MessageId) : null;

        if (acknowledged is null)
        {
            context.Log.Note($"Ignored: an unmatched {message.Type} with Message ID {message.MessageId}.");
            return;
        }

        context.Log.Note($"The client's {message.Type} with Message ID {message.MessageId} ends its retransmission.");
        acknowledged.TrySetResult();
    }

    private async Task AnswerDuplicateAsync(CoapMessage message, byte[]? answered)
    {
        context.Log.Note($"A duplicate {message.Type} with Message ID {message.MessageId}: not served again.");

        if (answered is not null)
        {
            await flow.SendAsync(answered, Cancellation);
        }
    }

    private async Task AnswerConfirmableAsync(CoapMessage request)
    {
        var answering = answer(request, CoapRequestOptions.Read(request), Cancellation);

        if (!answering.IsCompleted && !await IsDoneWithinAsync(answering, CoapTransmissionParameters.SeparateResponseAfter))
        {
            context.Log.Note($"The answer to Message ID {request.MessageId} is not ready: an Empty ACK now, the response separately.");
            await SendRememberedAsync(request.MessageId, new CoapMessage(CoapMessageType.Acknowledgement, CoapCode.Empty, request.MessageId));
            StartSeparateResponse(answering, request.Token);
            return;
        }

        var response = await answering;
        await SendRememberedAsync(
            request.MessageId,
            new CoapMessage(CoapMessageType.Acknowledgement, response.Code, request.MessageId, request.Token, response.Options, response.Payload));
    }

    private async Task AnswerNonConfirmableAsync(CoapMessage request)
    {
        duplicates.Remember(request.MessageId, null, Now + CoapTransmissionParameters.NonLifetime);
        var options = CoapRequestOptions.Read(request);

        if (options.UnrecognisedCriticalOption is { } number)
        {
            context.Log.Note($"Ignored: a NonConfirmable with Message ID {request.MessageId} and unrecognised critical option {number}.");
            return;
        }

        var response = await answer(request, options, Cancellation);
        var message = new CoapMessage(CoapMessageType.NonConfirmable, response.Code, DrawNextMessageId(), request.Token, response.Options, response.Payload);
        await flow.SendAsync(CoapMessageWriter.Write(message), Cancellation);
    }

    private async Task SendRememberedAsync(ushort messageId, CoapMessage message)
    {
        var bytes = CoapMessageWriter.Write(message);
        duplicates.Remember(messageId, bytes, Now + CoapTransmissionParameters.ExchangeLifetime);
        await flow.SendAsync(bytes, Cancellation);
    }

    private void StartSeparateResponse(Task<CoapResponse> answering, ReadOnlyMemory<byte> token)
    {
        lock (gate)
        {
            separateResponses.RemoveAll(response => response.IsCompletedSuccessfully);
            separateResponses.Add(SendSeparateResponseAsync(answering, token));
        }
    }

    private async Task SendSeparateResponseAsync(Task<CoapResponse> answering, ReadOnlyMemory<byte> token)
    {
        var response = await answering;
        await outstandingSlot.WaitAsync(Cancellation);

        try
        {
            var acknowledged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var messageId = DrawNextMessageId();
            var bytes = CoapMessageWriter.Write(
                new CoapMessage(CoapMessageType.Confirmable, response.Code, messageId, token, response.Options, response.Payload));

            lock (gate)
            {
                outstanding = (messageId, acknowledged);
            }

            await TransmitUntilAcknowledgedAsync(messageId, bytes, acknowledged.Task);
        }
        finally
        {
            lock (gate)
            {
                outstanding = null;
            }

            outstandingSlot.Release();
        }
    }

    private async Task TransmitUntilAcknowledgedAsync(ushort messageId, byte[] bytes, Task acknowledged)
    {
        var timeout = CoapTransmissionParameters.InitialTimeout(random.DrawFraction());

        for (var transmission = 0; transmission <= CoapTransmissionParameters.MaxRetransmit; transmission++)
        {
            await flow.SendAsync(bytes, Cancellation);

            if (await IsDoneWithinAsync(acknowledged, timeout))
            {
                return;
            }

            timeout *= 2;
        }

        context.Log.Note($"Gave up: the separate response with Message ID {messageId} was not acknowledged after {CoapTransmissionParameters.MaxRetransmit} retransmissions.");
    }

    private async Task<bool> IsDoneWithinAsync(Task task, TimeSpan timeout)
    {
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        var first = await Task.WhenAny(task, Task.Delay(timeout, context.TimeProvider, waiting.Token));
        await waiting.CancelAsync();
        Cancellation.ThrowIfCancellationRequested();

        return first == task;
    }

    private ushort DrawNextMessageId()
    {
        lock (gate)
        {
            return nextMessageId++;
        }
    }

    private async Task RejectAsync(CoapMessageType type, ushort messageId, string reason)
    {
        if (type == CoapMessageType.Confirmable)
        {
            context.Log.Note($"Rejected with RST: a CON with Message ID {messageId} and {reason}");
            await flow.SendAsync(CoapMessageWriter.Write(CoapMessage.EmptyReset(messageId)), Cancellation);
            return;
        }

        context.Log.Note($"Ignored: a {type} with Message ID {messageId} and {reason}");
    }
}
