using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// One AMQP 1.0 connection once its headers and SASL are done (ADR-0107 decisions 3 to 7): it
/// answers the client's <c>open</c>, <c>begin</c>, <c>attach</c>, <c>flow</c>, <c>transfer</c>,
/// <c>disposition</c>, <c>detach</c>, <c>end</c> and <c>close</c> with their state machines (Part 2
/// sections 2.4 to 2.7), maps channels and handles, resolves addresses to nodes through
/// <see cref="IAmqp1NodeDirectory"/>, stores what client senders send on the shared message bus and
/// sends client receivers what the bus hands their links as their credit allows, keeps both idle
/// time-outs, and ends each misuse with the error condition the specification names.
/// </summary>
/// <remarks>
/// Frames are answered one at a time under one gate. A message the bus hands a link on another
/// connection's thread waits on the link and wakes a pump that sends it under the same gate.
/// </remarks>
public sealed partial class Amqp1ContainerConnection
{
    /// <summary>The container's <c>channel-max</c>: 64 sessions a connection (ADR-0107 decision 7).</summary>
    public const ushort ContainerChannelMax = 63;

    /// <summary>The container's <c>handle-max</c>: 256 links a session (ADR-0107 decision 7).</summary>
    public const uint ContainerHandleMax = 255;

    /// <summary>The container's <c>incoming-window</c> and <c>outgoing-window</c> (ADR-0107 decision 4).</summary>
    public const uint ContainerWindow = 2147483647;

    /// <summary>The value of the <c>product</c> property in the container's <c>open</c>; no version is sent.</summary>
    public const string ProductName = "surl";

    private static readonly Amqp1Symbol ProductKey = new("product");

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly Amqp1ContainerOptions options;
    private readonly MessageBus.MessageBus bus;
    private readonly BusConnection busConnection;
    private readonly IAmqp1NodeDirectory nodes;
    private readonly string? accountName;
    private readonly Amqp1FrameReader reader;
    private readonly uint offeredMaxFrameSize;
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private readonly SemaphoreSlim stateGate = new(1, 1);
    private readonly Channel<bool> wakeups = Channel.CreateUnbounded<bool>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource emptyFramesCancellation = new();
    private readonly Dictionary<ushort, Amqp1Session> sessionsByIncomingChannel = [];
    private Task emptyFramesRunning = Task.CompletedTask;
    private Task pumping = Task.CompletedTask;
    private long lastWrittenUtcTicks;
    private ushort clientChannelMax;
    private uint clientMaxFrameSize;
    private CancellationToken openDeadline;
    private bool closed;

    /// <summary>Prepares to serve one connection whose next bytes are AMQP frames.</summary>
    /// <param name="connection">The connection, past its <c>AMQP 0 1 0 0</c> header.</param>
    /// <param name="context">The exchange: its log, clock, limits and cancellation.</param>
    /// <param name="options">The container id and the idle timeout.</param>
    /// <param name="bus">The process's message bus, whose topics and queues are the container's nodes.</param>
    /// <param name="accountName">The account SASL logged in, or <see langword="null"/> when anonymous.</param>
    public Amqp1ContainerConnection(
        IConnection connection,
        ExchangeContext context,
        Amqp1ContainerOptions options,
        MessageBus.MessageBus bus,
        string? accountName)
        : this(connection, context, options, bus, accountName, null)
    {
    }

    /// <summary>
    /// Prepares to serve one connection, its nodes resolved by the directory <paramref name="nodesFor"/> makes;
    /// when it is <see langword="null"/>, a bus directory drawing from <see cref="ExchangeContext.Identifiers"/>.
    /// </summary>
    internal Amqp1ContainerConnection(
        IConnection connection,
        ExchangeContext context,
        Amqp1ContainerOptions options,
        MessageBus.MessageBus bus,
        string? accountName,
        Func<BusConnection, IAmqp1NodeDirectory>? nodesFor)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(bus);
        if (options.ContainerId is null)
        {
            throw new ArgumentException("The options carry no container id; the container draws it.", nameof(options));
        }

        this.connection = connection;
        this.context = context;
        this.options = options;
        this.bus = bus;
        this.accountName = accountName;
        busConnection = bus.Connect(accountName);
        nodes = nodesFor is null ? new Amqp1BusNodeDirectory(bus, busConnection, context.Identifiers) : nodesFor(busConnection);
        reader = new Amqp1FrameReader(connection);
        offeredMaxFrameSize = Amqp1FrameLimits.OfferedMaxFrameSize(context.Limits.MaxMessageBytes);
    }

    private CancellationToken CancellationToken => context.CancellationToken;

    /// <summary>
    /// How long the <c>close</c> of an exchange cancelled for a limit may take to write (ADR-0006
    /// section 5; ADR-0059).
    /// </summary>
    public static readonly TimeSpan LimitCloseWriteDeadline = TimeSpan.FromSeconds(1);

    /// <summary>The <c>description</c> of the <c>close</c> written when the exchange is cancelled for a limit.</summary>
    public const string LimitCloseDescription = "an exchange limit was reached";

    /// <summary>Serves the connection until it is closed by either side.</summary>
    /// <returns>A task that completes when the connection is over.</returns>
    /// <remarks>
    /// When the engine cancels the exchange for a limit (<c>--max-time</c>, or its idle timeout) once
    /// <c>open</c> has been exchanged, the container writes <c>close</c> with
    /// <c>amqp:resource-limit-exceeded</c> within <see cref="LimitCloseWriteDeadline"/>, unless it had
    /// already closed; before <c>open</c> it writes nothing. Either way the cancellation does not
    /// escape. At shutdown nothing more is written and the cancellation escapes.
    /// </remarks>
    public Task ServeAsync() => ServeAsync(CancellationToken.None);

    /// <summary>Serves the connection, ending it with no bytes when <paramref name="headDeadline"/> fires before the client's <c>open</c> is read.</summary>
    /// <param name="headDeadline">Cancelled when the head timeout runs out (ADR-0107 decision 7).</param>
    /// <returns>A task that completes when the connection is over.</returns>
    internal async Task ServeAsync(CancellationToken headDeadline)
    {
        openDeadline = headDeadline;
        var failure = await CaptureFailureAsync(ServeUntilEndAsync);
        await StopWritingAsync();
        await emptyFramesCancellation.CancelAsync();
        await emptyFramesRunning;
        emptyFramesCancellation.Dispose();
        wakeups.Writer.TryComplete();
        var pumpFailure = await CaptureFailureAsync(() => pumping);

        // Every link ends, and the bus connection with them: unsettled queue deliveries go back.
        foreach (var session in sessionsByIncomingChannel.Values)
        {
            EndLinks(session);
        }

        busConnection.Dispose();
        (UnlessCancelledForALimit(failure) ?? UnlessCancelledForALimit(pumpFailure))?.Throw();
    }

    // A limit's cancellation has been answered (or needs no answer before open); only shutdown escapes.
    private ExceptionDispatchInfo? UnlessCancelledForALimit(ExceptionDispatchInfo? failure) =>
        failure?.SourceException is OperationCanceledException && context.IsCancelledForALimit ? null : failure;

    // Once serving is over nothing more is written, whatever the pump still holds: the client may be gone.
    private async Task StopWritingAsync()
    {
        await stateGate.WaitAsync(CancellationToken.None);
        closed = true;
        stateGate.Release();
    }

    // Runs work and hands back what it threw instead of throwing it, so the empty frames are stopped
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

    private async Task ServeUntilEndAsync()
    {
        if (await OpenAsync())
        {
            pumping = PumpAsync();
            try
            {
                await ServeOpenedAsync();
            }
            catch (OperationCanceledException) when (context.IsCancelledForALimit && !closed)
            {
                await CloseForALimitAsync();
            }
        }
    }

    // ADR-0107 decision 7: --max-time once open has been exchanged is close with
    // amqp:resource-limit-exceeded, on a deadline of its own since the exchange's token is cancelled.
    private async Task CloseForALimitAsync()
    {
        closed = true;
        var error = Error(Amqp1ErrorCondition.ResourceLimitExceeded, LimitCloseDescription);
        using var deadline = new CancellationTokenSource(LimitCloseWriteDeadline, context.TimeProvider);
        try
        {
            await WriteFrameAsync(Amqp1Frame.Carrying(0, new Amqp1Close(error)), deadline.Token);
            await connection.CompleteWritesAsync(deadline.Token);
            context.Log.Note($"The exchange was cancelled for a limit; closed with {error.Condition.Value}: {error.Description}.");
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            context.Log.Note("The close was not written within its one-second write deadline; the connection is closed without it.");
        }
    }

    private async Task ServeOpenedAsync()
    {
        while (await ReadOpenedFrameAsync() is { } frame)
        {
            if (frame.IsEmpty)
            {
                continue;
            }

            if (!await AnswerUnderGateAsync(frame))
            {
                return;
            }
        }
    }

    // One frame at a time, then whatever the frame let the links send.
    private async ValueTask<bool> AnswerUnderGateAsync(Amqp1Frame frame)
    {
        await stateGate.WaitAsync(CancellationToken);
        try
        {
            var (body, refusal) = ReadPerformative(frame);
            var goesOn = refusal is null
                ? await AnswerAsync(frame.Channel, body!.Performative!, body.Payload)
                : await CloseWithErrorAsync(refusal);
            await SendWaitingAsync();
            return goesOn;
        }
        finally
        {
            stateGate.Release();
        }
    }

    // Sends what the bus handed links on other threads, once per wake-up.
    private async Task PumpAsync()
    {
        await foreach (var _ in wakeups.Reader.ReadAllAsync(CancellationToken))
        {
            await stateGate.WaitAsync(CancellationToken);
            try
            {
                await SendWaitingAsync();
            }
            finally
            {
                stateGate.Release();
            }
        }
    }

    private void WakePump() => wakeups.Writer.TryWrite(true);

    private ValueTask<bool> AnswerAsync(ushort channel, Amqp1Performative performative, ReadOnlyMemory<byte> payload) => performative switch
    {
        Amqp1Close close => AnswerCloseAsync(close),
        Amqp1Begin begin => AnswerBeginAsync(channel, begin),
        Amqp1Open => CloseWithErrorAsync(Error(Amqp1ErrorCondition.FramingError, "a second open")),
        _ => AnswerOnChannelAsync(channel, performative, payload),
    };

    private ValueTask<bool> AnswerOnChannelAsync(ushort channel, Amqp1Performative performative, ReadOnlyMemory<byte> payload) =>
        sessionsByIncomingChannel.TryGetValue(channel, out var session)
            ? AnswerInSessionAsync(session, performative, payload)
            : CloseWithErrorAsync(Error(Amqp1ErrorCondition.FramingError, "a frame on a channel with no session"));

    // The frame body of an AMQP frame, or the error that closes the connection.
    private static (Amqp1FrameBody? Body, Amqp1Error? Refusal) ReadPerformative(Amqp1Frame frame)
    {
        if (frame.Type != Amqp1FrameType.Amqp)
        {
            return (null, Error(Amqp1ErrorCondition.FramingError, "a SASL frame after SASL"));
        }

        try
        {
            return (Amqp1FrameBody.Read(frame), null);
        }
        catch (Amqp1DecodeException exception)
        {
            var description = exception.Condition == Amqp1ErrorCondition.FramingError
                ? "a frame body that is not a performative"
                : "a performative that does not decode";
            return (null, Error(exception.Condition, description));
        }
    }

    private async ValueTask<bool> AnswerCloseAsync(Amqp1Close close)
    {
        closed = true;
        NoteClientError("close", close.Error);
        await WriteAsync(0, new Amqp1Close());
        await connection.CompleteWritesAsync(CancellationToken);
        return false;
    }

    // The pump writes under the gate too, so nothing it sends can follow the close.
    private async ValueTask CloseUnderGateAsync(Amqp1Error error)
    {
        await stateGate.WaitAsync(CancellationToken);
        try
        {
            await CloseWithErrorAsync(error);
        }
        finally
        {
            stateGate.Release();
        }
    }

    // Part 2 section 2.4.3: close with an error, then end the outgoing stream; never goes on.
    private async ValueTask<bool> CloseWithErrorAsync(Amqp1Error error)
    {
        closed = true;
        context.Log.Note($"Closed with {error.Condition.Value}: {error.Description}.");
        await WriteAsync(0, new Amqp1Close(error));
        await connection.CompleteWritesAsync(CancellationToken);
        return false;
    }

    private void NoteClientError(string performative, Amqp1Error? error)
    {
        if (error is not null)
        {
            context.Log.Note($"The client's {performative} carried {error.Condition.Value}.");
        }
    }

    private static Amqp1Error Error(Amqp1Symbol condition, string description) => new(condition) { Description = description };

    private ValueTask WriteAsync(ushort channel, Amqp1Performative performative) =>
        WriteFrameAsync(Amqp1Frame.Carrying(channel, performative), CancellationToken);

    // The reading of frames and the empty frames both write; one write at a time.
    private async ValueTask WriteFrameAsync(Amqp1Frame frame, CancellationToken cancellationToken)
    {
        await writeGate.WaitAsync(cancellationToken);
        try
        {
            await connection.WriteAsync(frame.Encode(), cancellationToken);
            Interlocked.Exchange(ref lastWrittenUtcTicks, context.TimeProvider.GetUtcNow().UtcTicks);
        }
        finally
        {
            writeGate.Release();
        }
    }
}
