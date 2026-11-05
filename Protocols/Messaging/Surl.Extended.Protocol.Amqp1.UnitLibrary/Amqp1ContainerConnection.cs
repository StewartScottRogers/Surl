using System.Runtime.ExceptionServices;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// One AMQP 1.0 connection once its headers and SASL are done (ADR-0107 decisions 3, 4 and 7): it
/// answers the client's <c>open</c>, <c>begin</c>, <c>attach</c>, <c>detach</c>, <c>end</c> and
/// <c>close</c> with their state machines (Part 2 sections 2.4 to 2.7), maps channels and handles,
/// resolves addresses to nodes through <see cref="IAmqp1NodeDirectory"/>, keeps both idle time-outs,
/// and ends each misuse with the error condition the specification names.
/// </summary>
/// <remarks>
/// <c>flow</c>, <c>transfer</c> and <c>disposition</c> on attached links are read and left to BL-513;
/// here only their handles are checked.
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
    private readonly IAmqp1NodeDirectory nodes;
    private readonly string? accountName;
    private readonly Amqp1FrameReader reader;
    private readonly uint offeredMaxFrameSize;
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private readonly CancellationTokenSource emptyFramesCancellation = new();
    private readonly Dictionary<ushort, Amqp1Session> sessionsByIncomingChannel = [];
    private Task emptyFramesRunning = Task.CompletedTask;
    private long lastWrittenUtcTicks;
    private ushort clientChannelMax;

    /// <summary>Prepares to serve one connection whose next bytes are AMQP frames.</summary>
    /// <param name="connection">The connection, past its <c>AMQP 0 1 0 0</c> header.</param>
    /// <param name="context">The exchange: its log, clock, limits and cancellation.</param>
    /// <param name="options">The container id and the idle timeout.</param>
    /// <param name="nodes">The nodes links attach to.</param>
    /// <param name="accountName">The account SASL logged in, or <see langword="null"/> when anonymous.</param>
    public Amqp1ContainerConnection(
        IConnection connection,
        ExchangeContext context,
        Amqp1ContainerOptions options,
        IAmqp1NodeDirectory nodes,
        string? accountName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(nodes);

        this.connection = connection;
        this.context = context;
        this.options = options;
        this.nodes = nodes;
        this.accountName = accountName;
        reader = new Amqp1FrameReader(connection);
        offeredMaxFrameSize = Amqp1FrameLimits.OfferedMaxFrameSize(context.Limits.MaxMessageBytes);
    }

    private CancellationToken CancellationToken => context.CancellationToken;

    /// <summary>Serves the connection until it is closed by either side.</summary>
    /// <returns>A task that completes when the connection is over.</returns>
    public async Task ServeAsync()
    {
        var failure = await CaptureFailureAsync(ServeUntilEndAsync);
        await emptyFramesCancellation.CancelAsync();
        await emptyFramesRunning;
        emptyFramesCancellation.Dispose();
        failure?.Throw();
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
            await ServeOpenedAsync();
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

            var (performative, refusal) = ReadPerformative(frame);
            var goesOn = refusal is null
                ? await AnswerAsync(frame.Channel, performative!)
                : await CloseWithErrorAsync(refusal);
            if (!goesOn)
            {
                return;
            }
        }
    }

    private ValueTask<bool> AnswerAsync(ushort channel, Amqp1Performative performative) => performative switch
    {
        Amqp1Close close => AnswerCloseAsync(close),
        Amqp1Begin begin => AnswerBeginAsync(channel, begin),
        Amqp1Open => CloseWithErrorAsync(Error(Amqp1ErrorCondition.FramingError, "a second open")),
        _ => AnswerOnChannelAsync(channel, performative),
    };

    private ValueTask<bool> AnswerOnChannelAsync(ushort channel, Amqp1Performative performative) =>
        sessionsByIncomingChannel.TryGetValue(channel, out var session)
            ? AnswerInSessionAsync(session, performative)
            : CloseWithErrorAsync(Error(Amqp1ErrorCondition.FramingError, "a frame on a channel with no session"));

    // The performative at the start of an AMQP frame, or the error that closes the connection.
    private static (Amqp1Performative? Performative, Amqp1Error? Refusal) ReadPerformative(Amqp1Frame frame)
    {
        if (frame.Type != Amqp1FrameType.Amqp)
        {
            return (null, Error(Amqp1ErrorCondition.FramingError, "a SASL frame after SASL"));
        }

        try
        {
            return (Amqp1FrameBody.Read(frame).Performative, null);
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
        NoteClientError("close", close.Error);
        await WriteAsync(0, new Amqp1Close());
        await connection.CompleteWritesAsync(CancellationToken);
        return false;
    }

    // Part 2 section 2.4.3: close with an error, then end the outgoing stream; never goes on.
    private async ValueTask<bool> CloseWithErrorAsync(Amqp1Error error)
    {
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
