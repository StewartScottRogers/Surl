using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// Answers one connection to the <see cref="Amqp0Broker"/>, from its protocol header to its end.
/// <see cref="Amqp0Broker"/> documents the answers.
/// </summary>
internal sealed partial class Amqp0BrokerConnection
{
    private const string TheVirtualHost = "/";

    /// <summary>The longest the farewell <c>connection.close</c> 320 may take to write.</summary>
    public static readonly TimeSpan FarewellWriteDeadline = TimeSpan.FromSeconds(1);

    private const string ForcedCloseText = "CONNECTION_FORCED - idle timeout, maximum time or shutdown";

    private static readonly TimeSpan CloseOkWait = TimeSpan.FromSeconds(1);

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly ISaslAuthenticationPolicy policy;
    private readonly Amqp0BrokerOptions options;
    private readonly MessageBus.MessageBus bus;
    private readonly Amqp0ExchangeTable exchanges;
    private readonly Amqp0ConsumerRegistry consumers;
    private readonly Amqp0FrameReader reader;
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private readonly CancellationTokenSource heartbeatsCancellation = new();
    private Task heartbeatsRunning = Task.CompletedTask;
    private Channel<byte[]>? outbound;
    private Task outboundPumping = Task.CompletedTask;
    private BusConnection? busConnection;
    private bool closeSent;
    private bool clientAcceptsConsumerCancel;
    private long lastWrittenUtcTicks;
    private int frameMax = Amqp0FrameLimits.FrameMinSize;
    private ushort channelMax = Amqp0Broker.ChannelMax;
    private TimeSpan silenceLimit = Timeout.InfiniteTimeSpan;

    /// <summary>Prepares to answer <paramref name="connection"/>.</summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">The exchange's context: its limits, log, clock and cancellation token.</param>
    /// <param name="policy">Offers the mechanisms and judges the login.</param>
    /// <param name="options">The idle timeout and whether <c>ANONYMOUS</c> is offered.</param>
    /// <param name="bus">The shared message bus: the queues and the topic space.</param>
    /// <param name="exchanges">The broker's exchanges, shared by its connections.</param>
    /// <param name="consumers">The broker's consumers by queue, shared by its connections.</param>
    public Amqp0BrokerConnection(
        IConnection connection, ExchangeContext context, ISaslAuthenticationPolicy policy, Amqp0BrokerOptions options, MessageBus.MessageBus bus, Amqp0ExchangeTable exchanges,
        Amqp0ConsumerRegistry consumers)
    {
        this.consumers = consumers;
        this.connection = connection;
        this.context = context;
        this.policy = policy;
        this.options = options;
        this.bus = bus;
        this.exchanges = exchanges;
        reader = new Amqp0FrameReader(connection);
    }

    private BusConnection Bus => busConnection!;

    /// <summary>The account logged in; <see langword="null"/> before the login, and for an unchecked one.</summary>
    public string? AccountName { get; private set; }

    /// <summary>Answers the connection until it ends.</summary>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync()
    {
        var failure = await CaptureFailureAsync(ServeUntilEndAsync);
        await heartbeatsCancellation.CancelAsync();
        await heartbeatsRunning;
        heartbeatsCancellation.Dispose();

        // Ending the bus connection requeues every unsettled delivery and stops the consumers,
        // so nothing more is queued for writing; then what was queued is written out.
        consumers.RemoveOwnedBy(this);
        busConnection?.Dispose();
        outbound?.Writer.TryComplete();
        var writeFailure = await CaptureFailureAsync(() => outboundPumping);
        (failure ?? writeFailure)?.Throw();
    }

    private async Task ServeUntilEndAsync()
    {
        if (await NegotiateWithinHeadTimeoutAsync())
        {
            busConnection = bus.Connect(AccountName);
            outbound = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
            outboundPumping = WriteOutboundAsync(outbound.Reader);
            await ServeOpenedUntilCancelledAsync();
        }
    }

    // ADR-0112 decisions 3 and 7: an open connection the engine cancels - for a limit or at
    // shutdown - is told connection.close 320 on a short deadline of its own, never on the
    // cancelled token (ADR-0006 section 5; ADR-0059); the cancellation then still escapes.
    private async Task ServeOpenedUntilCancelledAsync()
    {
        try
        {
            await ServeOpenedAsync();
        }
        catch (OperationCanceledException) when (!closeSent)
        {
            await WriteForcedCloseAsync();
            throw;
        }
    }

    private async Task WriteForcedCloseAsync()
    {
        using var deadline = new CancellationTokenSource(FarewellWriteDeadline, context.TimeProvider);
        var close = new Amqp0ConnectionClose((ushort)Amqp0ReplyCode.ConnectionForced, ForcedCloseText, 0, 0);
        try
        {
            await WriteNowAsync(Amqp0Frame.ForMethod(0, close.ToMethod()).Encode(), deadline.Token);
            context.Log.Note($"Closed the connection with 320 ({ForcedCloseText}).");
        }
        catch (OperationCanceledException)
        {
            context.Log.Note($"The connection.close 320 was not written within its {FarewellWriteDeadline.TotalSeconds}-second write deadline.");
        }
    }

    // After open-ok every frame but a heartbeat is written in the order it was queued, so a
    // delivery made on the bus's thread never overtakes the answer before it (consume-ok first).
    private async Task WriteOutboundAsync(ChannelReader<byte[]> queued)
    {
        await foreach (var bytes in queued.ReadAllAsync(context.CancellationToken))
        {
            await WriteNowAsync(bytes, context.CancellationToken);
        }
    }

    private void Send(byte[] bytes) => outbound!.Writer.TryWrite(bytes);

    // Runs work and hands back what it threw instead of throwing it, so the heartbeats are stopped
    // with an await outside any catch or finally block.
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

    // ADR-0112 decision 8: the head timeout runs from accept to connection.open-ok.
    private async ValueTask<bool> NegotiateWithinHeadTimeoutAsync()
    {
        using var headTimeout = new CancellationTokenSource(context.Limits.HeadTimeout, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, headTimeout.Token);
        try
        {
            return await NegotiateAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note("The connection was not open within the head timeout; closed with no reply.");
            return false;
        }
    }

    private async ValueTask<bool> NegotiateAsync(CancellationToken cancellationToken)
    {
        var header = await Amqp0ProtocolHeader.ReadAsync(connection, cancellationToken);
        if (header != Amqp0ProtocolHeaderOutcome.Accepted)
        {
            context.Log.Note($"The protocol header was not AMQP 0-9-1 ({header}); closed.");
            return false;
        }

        var offered = OfferedMechanisms();
        await WriteMethodAsync(new Amqp0ConnectionStart(ServerProperties, string.Join(' ', offered), "en_US").ToMethod(), cancellationToken);
        return await LogInAsync(offered, cancellationToken) && await TuneAsync(cancellationToken) && await OpenAsync(cancellationToken);
    }

    // ADR-0112 decision 3: tune-ok above either maximum, or a frame-max below 4096, ends it silently.
    private async ValueTask<bool> TuneAsync(CancellationToken cancellationToken)
    {
        var offeredFrameMax = Amqp0FrameLimits.OfferedFrameMax(context.Limits.MaxMessageBytes);
        var tune = new Amqp0ConnectionTuning(Amqp0Broker.ChannelMax, (uint)offeredFrameMax, options.OfferedHeartbeatSeconds);
        await WriteMethodAsync(tune.ToTuneMethod(), cancellationToken);
        if (await ReadConnectionMethodAsync(Amqp0ConnectionMethodId.TuneOk, Amqp0ConnectionTuning.Read, cancellationToken) is not { } tuneOk)
        {
            return false;
        }

        if (RefuseTuneOk(tuneOk, offeredFrameMax) is { } refusal)
        {
            context.Log.Note($"connection.tune-ok {refusal}; closed with no reply.");
            return false;
        }

        frameMax = tuneOk.FrameMax == 0 ? offeredFrameMax : (int)tuneOk.FrameMax;
        channelMax = tuneOk.ChannelMax == 0 ? Amqp0Broker.ChannelMax : tuneOk.ChannelMax;
        StartHeartbeats(tuneOk.Heartbeat);
        return true;
    }

    private static string? RefuseTuneOk(Amqp0ConnectionTuning tuneOk, int offeredFrameMax)
    {
        if (tuneOk.ChannelMax > Amqp0Broker.ChannelMax)
        {
            return $"asked for channel-max {tuneOk.ChannelMax}, above {Amqp0Broker.ChannelMax}";
        }

        if (tuneOk.FrameMax > offeredFrameMax)
        {
            return $"asked for frame-max {tuneOk.FrameMax}, above {offeredFrameMax}";
        }

        return tuneOk.FrameMax is > 0 and < Amqp0FrameLimits.FrameMinSize
            ? $"asked for frame-max {tuneOk.FrameMax}, below {Amqp0FrameLimits.FrameMinSize}"
            : null;
    }

    // ADR-0112 decision 4: one virtual host, "/".
    private async ValueTask<bool> OpenAsync(CancellationToken cancellationToken)
    {
        if (await ReadConnectionMethodAsync(Amqp0ConnectionMethodId.Open, Amqp0ConnectionOpen.Read, cancellationToken) is not { } open)
        {
            return false;
        }

        if (!string.Equals(open.VirtualHost, TheVirtualHost, StringComparison.Ordinal))
        {
            await CloseAsync(Amqp0ReplyCode.NotAllowed, "NOT_ALLOWED - unknown virtual host", Amqp0ConnectionMethodId.ClassId, Amqp0ConnectionMethodId.Open);
            return false;
        }

        await WriteMethodAsync(Amqp0ConnectionOpen.OpenOk, cancellationToken);
        return true;
    }

    // Reads the connection method due next during the negotiation; anything else ends it silently.
    private async ValueTask<T?> ReadConnectionMethodAsync<T>(ushort methodId, Func<ReadOnlyMemory<byte>, T> decode, CancellationToken cancellationToken)
        where T : class
    {
        if (await ReadNegotiationMethodAsync(cancellationToken) is not { } method)
        {
            return null;
        }

        if (method.ClassId != Amqp0ConnectionMethodId.ClassId || method.MethodId != methodId)
        {
            context.Log.Note($"Method {method.ClassId}.{method.MethodId} arrived where connection method {methodId} was due; closed with no reply.");
            return null;
        }

        try
        {
            return decode(method.Arguments);
        }
        catch (Amqp0SyntaxException exception)
        {
            context.Log.Note($"Connection method {methodId} did not decode ({exception.Message}); closed with no reply.");
            return null;
        }
    }

    private async ValueTask<Amqp0MethodPayload?> ReadNegotiationMethodAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            if (await ReadNegotiationFrameAsync(cancellationToken) is not { } frame)
            {
                return null;
            }

            if (frame.Type != Amqp0FrameType.Heartbeat)
            {
                return MethodOnChannelZeroOrNull(frame);
            }
        }
    }

    private async ValueTask<Amqp0Frame?> ReadNegotiationFrameAsync(CancellationToken cancellationToken)
    {
        var read = await ReadFrameAsync(cancellationToken);
        if (read?.Frame is not { } frame)
        {
            context.Log.Note($"No method frame arrived during the negotiation ({read?.Outcome.ToString() ?? "silent"}); closed with no reply.");
            return null;
        }

        return frame;
    }

    private Amqp0MethodPayload? MethodOnChannelZeroOrNull(Amqp0Frame frame)
    {
        if (frame.Type != Amqp0FrameType.Method || frame.Channel != 0)
        {
            context.Log.Note($"A {frame.Type} frame on channel {frame.Channel} arrived during the negotiation; closed with no reply.");
            return null;
        }

        return ReadMethodOrNull(frame);
    }

    private Amqp0MethodPayload? ReadMethodOrNull(Amqp0Frame frame)
    {
        try
        {
            return Amqp0MethodPayload.Read(frame.Payload);
        }
        catch (Amqp0SyntaxException)
        {
            context.Log.Note("A method frame was shorter than its class and method IDs.");
            return null;
        }
    }

    // Reads the next frame; null when the peer was silent for two heartbeat intervals (section 4.2.7).
    private async ValueTask<Amqp0FrameReadResult?> ReadFrameAsync(CancellationToken cancellationToken)
    {
        using var silence = new CancellationTokenSource(silenceLimit, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, silence.Token);
        try
        {
            return await reader.ReadFrameAsync(frameMax, cancellation.Token);
        }
        catch (OperationCanceledException) when (silence.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            context.Log.Note("Nothing arrived for two heartbeat intervals; closed without connection.close.");
            return null;
        }
    }

    private ValueTask WriteMethodAsync(Amqp0MethodPayload method, CancellationToken cancellationToken) =>
        WriteFrameAsync(Amqp0Frame.ForMethod(0, method), cancellationToken);

    private async ValueTask WriteFrameAsync(Amqp0Frame frame, CancellationToken cancellationToken)
    {
        if (outbound is not null)
        {
            Send(frame.Encode());
            return;
        }

        await WriteNowAsync(frame.Encode(), cancellationToken);
    }

    // The outbound queue (or, before open-ok, the reading of frames) and the heartbeats both
    // write; one write at a time.
    private async ValueTask WriteNowAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        await writeGate.WaitAsync(cancellationToken);
        try
        {
            await connection.WriteAsync(bytes, cancellationToken);
            Interlocked.Exchange(ref lastWrittenUtcTicks, context.TimeProvider.GetUtcNow().UtcTicks);
        }
        finally
        {
            writeGate.Release();
        }
    }
}
