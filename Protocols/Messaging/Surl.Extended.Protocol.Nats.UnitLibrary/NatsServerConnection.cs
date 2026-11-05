using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Nats;

/// <summary>
/// One NATS connection (ADR-0113 decisions 0 to 5 and 7): INFO, CONNECT within the head timeout,
/// then operations until the client closes, a protocol error, a stale connection or a slow
/// consumer. Every byte it sends goes through one <see cref="NatsOutbox"/>, in order.
/// </summary>
internal sealed class NatsServerConnection : IDisposable
{
    private static readonly byte[] OkLine = "+OK\r\n"u8.ToArray();
    private static readonly byte[] PingLine = "PING\r\n"u8.ToArray();
    private static readonly byte[] PongLine = "PONG\r\n"u8.ToArray();

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly IAuthenticationPolicy policy;
    private readonly Surl.MessageBus.MessageBus bus;
    private readonly NatsServerOptions options;
    private readonly NatsInfo info;
    private readonly NatsControlLineReader reader;
    private readonly NatsOutbox outbox;
    private readonly ConcurrentDictionary<string, NatsClientSubscription> subscriptions = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource stopReading = new();
    private readonly CancellationTokenSource stopPinging = new();
    private BusConnection? busConnection;
    private NatsConnectOptions? connected;
    private int unansweredPings;

    public NatsServerConnection(
        IConnection connection, ExchangeContext context, IAuthenticationPolicy policy, Surl.MessageBus.MessageBus bus, NatsServerOptions options, NatsInfo info)
    {
        this.connection = connection;
        this.context = context;
        this.policy = policy;
        this.bus = bus;
        this.options = options;
        this.info = info;
        reader = new NatsControlLineReader(connection, context.Limits.MaxLineBytes);
        outbox = new NatsOutbox(options.PendingBytesLimit);
    }

    private NatsConnectOptions Connected => connected!;

    private BusConnection OnBus => busConnection!;

    public async Task ServeAsync()
    {
        var writing = WriteThenStopAsync();
        var failure = await CaptureFailureAsync(ServeOperationsAsync);
        busConnection?.Dispose();
        outbox.Complete();
        var writeFailure = await CaptureFailureAsync(() => writing);
        (failure ?? writeFailure)?.Throw();
        if (outbox.HasOverflowed)
        {
            await CutOffSlowConsumerAsync();
        }
    }

    public void Dispose()
    {
        outbox.Dispose();
        stopReading.Dispose();
        stopPinging.Dispose();
    }

    // However the writing ends - the outbox drained, a slow consumer or a failed write - reading
    // and pinging stop with it.
    private async Task WriteThenStopAsync()
    {
        try
        {
            await outbox.WriteAllAsync(connection, context.CancellationToken);
        }
        finally
        {
            stopReading.Cancel();
            stopPinging.Cancel();
        }
    }

    private async Task ServeOperationsAsync()
    {
        try
        {
            outbox.Enqueue(info.Encode());
            await ServeAfterInfoAsync();
        }
        catch (NatsProtocolViolationException violation)
        {
            context.Log.Note($"{violation.Message}; answered -ERR '{violation.ErrorText}' and closed.");
            outbox.Enqueue(NatsErrors.Line(violation.ErrorText));
        }
    }

    // ADR-0113 decision 7: best effort and bounded; whatever happens, the connection then closes.
    private async Task CutOffSlowConsumerAsync()
    {
        context.Log.Note($"More than {options.PendingBytesLimit} bytes waited unwritten; answered -ERR '{NatsErrors.SlowConsumer}' and closed.");
        using var timeout = new CancellationTokenSource(NatsServerOptions.SlowConsumerErrorTimeout, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, timeout.Token);
        try
        {
            await connection.WriteAsync(NatsErrors.Line(NatsErrors.SlowConsumer), cancellation.Token);
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException)
        {
            context.Log.Note($"The slow consumer's -ERR was not written ({exception.GetType().Name}).");
        }
    }

    private async Task ServeAfterInfoAsync()
    {
        var connect = await ReadConnectWithinHeadTimeoutAsync();
        if (connect is not null)
        {
            await AcceptConnectAsync(connect.Value.Arguments);
            await ServeConnectedAsync();
        }
    }

    // ADR-0113 decision 2: the head timeout runs from accept to a complete CONNECT line. A write
    // that failed or overflowed stops the reading without a word.
    private async ValueTask<NatsControlLine?> ReadConnectWithinHeadTimeoutAsync()
    {
        using var headTimeout = new CancellationTokenSource(context.Limits.HeadTimeout, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, headTimeout.Token, stopReading.Token);
        try
        {
            return await ReadConnectAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (stopReading.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            throw new NatsProtocolViolationException(NatsErrors.AuthorizationTimeout, "No CONNECT arrived within the head timeout");
        }
    }

    private async ValueTask<NatsControlLine?> ReadConnectAsync(CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } bytes)
        {
            var line = NatsControlLine.Parse(bytes);
            if (line.Operation == "CONNECT")
            {
                return line;
            }

            if (line.Operation.Length > 0)
            {
                throw new NatsProtocolViolationException(
                    NatsErrors.AuthorizationViolation, $"{line.Operation} arrived before CONNECT");
            }
        }

        return null;
    }

    private async Task AcceptConnectAsync(string json)
    {
        var connect = NatsConnectOptions.Parse(json)
            ?? throw new NatsProtocolViolationException(
                NatsErrors.ParserError, "CONNECT's argument was not a JSON object with the protocol's field types");
        var account = await LogInAsync(connect);
        RequireServedOptions(connect);
        context.Log.Note(
            $"CONNECT accepted: name {connect.Name ?? "(none)"}, lang {connect.Lang ?? "(none)"}, version {connect.Version ?? "(none)"}, verbose {connect.Verbose}, pedantic {connect.Pedantic}.");
        connected = connect;
        busConnection = bus.Connect(account);
        WriteOkIfVerbose();
    }

    private static void RequireServedOptions(NatsConnectOptions connect)
    {
        if (connect.Protocol is not (0 or 1))
        {
            throw new NatsProtocolViolationException(
                NatsErrors.InvalidClientProtocol, $"CONNECT asked for protocol {connect.Protocol}");
        }

        if (connect.NoResponders && !connect.Headers)
        {
            throw new NatsProtocolViolationException(
                NatsErrors.NoRespondersRequiresHeaders, "CONNECT set no_responders without headers");
        }
    }

    // Returns the account the bus judges the connection's publishes and subscribes for (ADR-0113
    // decision 2): the login's name, the empty name for a token, null for none.
    private async Task<string?> LogInAsync(NatsConnectOptions connect)
    {
        if (connect.UsesKeys)
        {
            throw new NatsProtocolViolationException(
                NatsErrors.AuthorizationViolation, "CONNECT asked for an NKey or JWT login, which surl does not serve");
        }

        var login = connect.ToPasswordLogin(context.Scheme, connection.TlsSession);
        var verdict = await policy.CheckPasswordLoginAsync(login, context.CancellationToken);
        if (verdict is not (PasswordLoginVerdict.Accepted or PasswordLoginVerdict.AcceptedUnchecked))
        {
            throw new NatsProtocolViolationException(
                NatsErrors.AuthorizationViolation, $"CONNECT's login was refused ({verdict})");
        }

        return login.UserName;
    }

    private async Task ServeConnectedAsync()
    {
        using var pingCancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, stopPinging.Token);
        var pinging = PingAsync(pingCancellation.Token);
        var failure = await CaptureFailureAsync(ReadOperationsAsync);
        await stopPinging.CancelAsync();
        var pingFailure = await CaptureFailureAsync(() => pinging);
        (failure ?? pingFailure)?.Throw();
    }

    private async Task ReadOperationsAsync()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, stopReading.Token);
        try
        {
            while (await reader.ReadLineAsync(cancellation.Token) is { } bytes)
            {
                await AnswerAsync(NatsControlLine.Parse(bytes), cancellation.Token);
            }
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
        }
    }

    private async ValueTask AnswerAsync(NatsControlLine line, CancellationToken cancellationToken)
    {
        switch (line.Operation)
        {
            case "":
                break;
            case "PING":
                RequireNoArguments(line);
                outbox.Enqueue(PongLine);
                break;
            case "PONG":
                RequireNoArguments(line);
                Interlocked.Exchange(ref unansweredPings, 0);
                break;
            case "PUB":
                await PublishAsync(NatsPublishArguments.Parse(line.Arguments, withHeaders: false), cancellationToken);
                break;
            case "HPUB" when Connected.Headers:
                await PublishAsync(NatsPublishArguments.Parse(line.Arguments, withHeaders: true), cancellationToken);
                break;
            case "SUB":
                Subscribe(NatsSubscribeArguments.Parse(line.Arguments));
                break;
            case "UNSUB":
                Unsubscribe(NatsUnsubscribeArguments.Parse(line.Arguments));
                break;
            default:
                throw new NatsProtocolViolationException(
                    NatsErrors.UnknownProtocolOperation, $"The operation {line.Operation} is not served after CONNECT");
        }
    }

    private static void RequireNoArguments(NatsControlLine line)
    {
        if (line.Arguments.Length > 0)
        {
            throw new NatsProtocolViolationException(NatsErrors.ParserError, $"{line.Operation} carried arguments");
        }
    }

    // ADR-0113 decisions 4 and 7: the size is judged before the payload is read, the subjects after.
    private async ValueTask PublishAsync(NatsPublishArguments publish, CancellationToken cancellationToken)
    {
        if (publish.TotalBytes > info.MaxPayload)
        {
            throw new NatsProtocolViolationException(
                NatsErrors.MaximumPayloadViolation, $"A publish of {publish.TotalBytes} bytes was over max_payload {info.MaxPayload}");
        }

        var body = await reader.ReadPayloadAsync((int)publish.TotalBytes, cancellationToken);
        if (body is not null)
        {
            PublishOnBus(publish, body);
        }
    }

    private void PublishOnBus(NatsPublishArguments publish, byte[] body)
    {
        var topic = bus.Names.ToCanonicalTopic(context.Scheme, publish.Subject);
        var replyTo = MapReplyTo(publish.ReplyTo);
        if (!topic.IsMapped || replyTo is { IsMapped: false })
        {
            outbox.Enqueue(NatsErrors.Line(NatsErrors.InvalidPublishSubject));
            return;
        }

        AnswerPublished(publish, OnBus.Publish(NewBusMessage(topic.Name, replyTo?.Name, body, (int)publish.HeaderBytes)));
    }

    private TopicNameMapping? MapReplyTo(string? replyTo) =>
        replyTo is null ? null : bus.Names.ToCanonicalTopic(context.Scheme, replyTo);

    // The header block, if any, is the message's native form byte for byte and its parsed headers
    // (ADR-0113 decision 4).
    private static BusMessage NewBusMessage(string topic, string? responseTopic, byte[] body, int headerBytes)
    {
        var headerBlock = body.AsMemory(0, headerBytes);
        return new BusMessage(topic, body.AsMemory(headerBytes))
        {
            ResponseTopic = responseTopic,
            Headers = NatsHeaderBlock.Parse(headerBlock.Span),
            NativeForm = headerBlock.IsEmpty ? null : new BusNativeForm(NatsHeaderBlock.NativeFormat, headerBlock),
        };
    }

    private void AnswerPublished(NatsPublishArguments publish, BusPublishResult result)
    {
        switch (result.Outcome)
        {
            case MessageBusOutcome.Succeeded:
                AnswerNoRespondersIfNoneTook(publish.ReplyTo, result);
                WriteOkIfVerbose();
                break;
            case MessageBusOutcome.AccessDenied:
                outbox.Enqueue(NatsErrors.Line(NatsErrors.PublishPermissionsViolation(publish.Subject)));
                break;
            default:
                throw new NatsProtocolViolationException(
                    NatsErrors.ResourceLimitExceeded, $"The bus refused a publish ({result.Outcome})");
        }
    }

    // The headers ADR: a request no live subscription took gets the 503 status message on each of
    // the requester's own subscriptions matching its reply-to.
    private void AnswerNoRespondersIfNoneTook(string? replyTo, BusPublishResult result)
    {
        if (replyTo is not null && Connected.NoResponders && result.DeliveredSubscriptionCount == 0)
        {
            SendNoResponders(replyTo);
        }
    }

    private void SendNoResponders(string replyTo)
    {
        var matching = subscriptions.Values
            .Where(subscription => NatsSubjects.Matches(subscription.Subject, replyTo))
            .OrderBy(subscription => subscription.Sid, StringComparer.Ordinal);
        foreach (var subscription in matching)
        {
            if (subscription.TryCountDelivery())
            {
                outbox.Enqueue(NatsMessageLine.Encode(replyTo, subscription.Sid, null, NatsHeaderBlock.NoResponders.Span, withHeaders: true, []));
            }
        }
    }

    private void Subscribe(NatsSubscribeArguments subscribe)
    {
        if (subscriptions.TryGetValue(subscribe.Sid, out var live) && !live.HasEnded)
        {
            WriteOkIfVerbose();
            return;
        }

        var filter = bus.Names.ToCanonicalFilter(context.Scheme, subscribe.Subject);
        if (!filter.IsMapped || !NatsSubjects.HasOnlyWholeTokenWildcards(subscribe.Subject))
        {
            outbox.Enqueue(NatsErrors.Line(NatsErrors.InvalidSubject));
            return;
        }

        SubscribeOnBus(subscribe, filter.Name);
    }

    private void SubscribeOnBus(NatsSubscribeArguments subscribe, string filter)
    {
        var subscription = new NatsClientSubscription(subscribe.Sid, subscribe.Subject);
        var result = OnBus.Subscribe(filter, message => Deliver(subscription, message), subscribe.QueueGroup, noLocal: !Connected.Echo);
        switch (result.Outcome)
        {
            case MessageBusOutcome.Succeeded:
                subscription.Attach(result.Subscription!);
                subscriptions[subscribe.Sid] = subscription;
                WriteOkIfVerbose();
                break;
            case MessageBusOutcome.AccessDenied:
                outbox.Enqueue(NatsErrors.Line(NatsErrors.SubscriptionPermissionsViolation(subscribe.Subject)));
                break;
            default:
                outbox.Enqueue(NatsErrors.Line(NatsErrors.MaximumSubscriptionsExceeded));
                break;
        }
    }

    private void Unsubscribe(NatsUnsubscribeArguments unsubscribe)
    {
        if (subscriptions.TryGetValue(unsubscribe.Sid, out var subscription) && subscription.EndAfter(unsubscribe.MaxMessages))
        {
            subscriptions.TryRemove(new KeyValuePair<string, NatsClientSubscription>(unsubscribe.Sid, subscription));
        }

        WriteOkIfVerbose();
    }

    // Called by the bus and never blocks: the message is queued in the outbox (ADR-0113 decision 4).
    private void Deliver(NatsClientSubscription subscription, BusMessage message)
    {
        var subject = bus.Names.FromCanonicalTopic(context.Scheme, message.Topic);
        if (!subject.IsMapped || !subscription.TryCountDelivery())
        {
            return;
        }

        if (subscription.HasEnded)
        {
            subscriptions.TryRemove(new KeyValuePair<string, NatsClientSubscription>(subscription.Sid, subscription));
        }

        var headerBlock = HeaderBlockFor(message);
        outbox.Enqueue(NatsMessageLine.Encode(
            subject.Name, subscription.Sid, ReplyToOf(message), headerBlock, headerBlock is not null, message.Payload.Span));
    }

    private byte[]? HeaderBlockFor(BusMessage message) =>
        !Connected.Headers ? null
        : message.NativeForm is { Format: NatsHeaderBlock.NativeFormat } native ? native.Bytes.ToArray()
        : NatsHeaderBlock.Encode(message.Headers);

    private string? ReplyToOf(BusMessage message)
    {
        var replyTo = message.ResponseTopic is null ? null : bus.Names.FromCanonicalTopic(context.Scheme, message.ResponseTopic);
        return replyTo is { IsMapped: true } ? replyTo.Name : null;
    }

    private void WriteOkIfVerbose()
    {
        if (Connected.Verbose)
        {
            outbox.Enqueue(OkLine);
        }
    }

    // ADR-0113 decision 5. However the pinging ends - stale, stopped or cancelled - reading stops
    // too, so a connection is never left reading with nobody keeping it alive.
    private async Task PingAsync(CancellationToken cancellationToken)
    {
        try
        {
            await PingUntilStaleAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Stopped or cancelled: the only token the pinging waits on.
        }
        finally
        {
            stopReading.Cancel();
        }
    }

    private async Task PingUntilStaleAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(options.PingInterval, context.TimeProvider, cancellationToken);
        while (Interlocked.Increment(ref unansweredPings) <= NatsServerOptions.MaxUnansweredPings)
        {
            outbox.Enqueue(PingLine);
            await Task.Delay(options.PingInterval, context.TimeProvider, cancellationToken);
        }

        context.Log.Note($"{NatsServerOptions.MaxUnansweredPings} PINGs went unanswered; answered -ERR '{NatsErrors.StaleConnection}' and closed.");
        outbox.Enqueue(NatsErrors.Line(NatsErrors.StaleConnection));
    }

    // Runs work and hands back what it threw instead of throwing it, so what runs beside it is
    // stopped and awaited before any failure leaves the connection.
    private static async Task<ExceptionDispatchInfo?> CaptureFailureAsync(Func<Task> work)
    {
        try
        {
            await work();
            return null;
        }
        catch (Exception exception)
        {
            return ExceptionDispatchInfo.Capture(exception);
        }
    }
}
