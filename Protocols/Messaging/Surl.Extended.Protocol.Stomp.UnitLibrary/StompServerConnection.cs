using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Stomp;

/// <summary>
/// One STOMP connection (ADR-0118): CONNECT or STOMP within the head timeout, CONNECTED or ERROR,
/// then frames routed through the shared bus until DISCONNECT, the client's close, a silent
/// heart-beating client, a slow consumer or an ERROR. Once connected, every byte the server sends
/// goes through one <see cref="StompOutbox"/>, in order.
/// </summary>
internal sealed partial class StompServerConnection
{
    /// <summary>How long the server keeps reading, and discarding, after an ERROR (decision 6).</summary>
    public static readonly TimeSpan Linger = TimeSpan.FromSeconds(1);

    private const int SessionHexDigits = 32;

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly IAuthenticationPolicy policy;
    private readonly Surl.MessageBus.MessageBus bus;
    private readonly StompServerOptions options;
    private readonly StompFrameReader reader;
    private readonly StompOutbox outbox;
    private readonly CancellationTokenSource stopReading = new();
    private readonly CancellationTokenSource stopBeating = new();
    private StompVersion version;
    private StompHeartBeat clientHeartBeat;
    private string? account;
    private BusConnection? busConnection;
    private bool dropped;

    public StompServerConnection(
        IConnection connection, ExchangeContext context, IAuthenticationPolicy policy, Surl.MessageBus.MessageBus bus, StompServerOptions options)
    {
        this.connection = connection;
        this.context = context;
        this.policy = policy;
        this.bus = bus;
        this.options = options;
        reader = new StompFrameReader(connection, context.Limits.MaxLineBytes, context.Limits.MaxMessageBytes) { Clock = context.TimeProvider };
        outbox = new StompOutbox(options.PendingBytesLimit);
    }

    private BusConnection OnBus => busConnection!;

    public async Task ServeAsync()
    {
        try
        {
            if (await ConnectWithinHeadTimeoutAsync())
            {
                await ServeConnectedAsync();
            }
        }
        catch (StompProtocolViolationException violation)
        {
            context.Log.Note($"{violation.Message}; answered ERROR '{violation.Error.Header("message")}' and closed.");
            await WriteAsync(violation.Error, reader.Version);
            await connection.CompleteWritesAsync(context.CancellationToken);
            await LingerAsync();
        }
        finally
        {
            outbox.Dispose();
            stopReading.Dispose();
            stopBeating.Dispose();
        }
    }

    // Decision 3: the head timeout runs from accept to a complete CONNECT; past it, close with no bytes.
    private async ValueTask<bool> ConnectWithinHeadTimeoutAsync()
    {
        using var headTimeout = new CancellationTokenSource(context.Limits.HeadTimeout, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, headTimeout.Token);
        StompFrame? frame;
        try
        {
            frame = await reader.ReadFrameAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note("No CONNECT arrived within the head timeout; closed.");
            return false;
        }

        if (frame is null)
        {
            return false;
        }

        await ConnectAsync(frame);
        return true;
    }

    private async Task ConnectAsync(StompFrame frame)
    {
        if (frame.Command is not ("CONNECT" or "STOMP"))
        {
            throw new StompProtocolViolationException(
                StompErrors.NotConnected, $"{frame.Command} arrived before CONNECT", frame.Header("receipt"));
        }

        RequireNoBody(frame);
        var negotiated = NegotiateVersion(frame);
        var heartBeat = ReadHeartBeat(frame, negotiated);
        await LogInAsync(frame);
        version = negotiated;
        clientHeartBeat = heartBeat;
        reader.Version = negotiated;
        context.Log.Note($"CONNECT accepted: version {StompVersionNegotiation.Text(negotiated)}, host {frame.Header("host") ?? "(none)"}.");
        await WriteAsync(Connected(negotiated), null);
    }

    private static StompVersion NegotiateVersion(StompFrame frame)
    {
        var negotiated = StompVersionNegotiation.ChooseHighest(frame.Header("accept-version"));
        if (negotiated is null || (frame.Command == "STOMP" && negotiated == StompVersion.V1_0))
        {
            throw new StompProtocolViolationException(
                StompErrors.UnsupportedVersionFrame(), $"{frame.Command} named no version the server speaks");
        }

        return negotiated.Value;
    }

    // Decision 7: 1.0 has no heart-beating, so its heart-beat header is not read.
    private static StompHeartBeat ReadHeartBeat(StompFrame frame, StompVersion negotiated)
    {
        StompHeartBeat heartBeat = default;
        if (negotiated != StompVersion.V1_0 && !StompHeartBeat.TryParse(frame.Header("heart-beat"), out heartBeat))
        {
            throw new StompProtocolViolationException(StompErrors.InvalidHeartBeat, "CONNECT's heart-beat was not two intervals");
        }

        return heartBeat;
    }

    private async Task LogInAsync(StompFrame frame)
    {
        var userName = frame.Header("login");
        var password = userName is null ? null : (ReadOnlyMemory<byte>?)Encoding.UTF8.GetBytes(frame.Header("passcode") ?? string.Empty);
        var verdict = await policy.CheckPasswordLoginAsync(
            new PasswordLogin(context.Scheme, userName, password, connection.TlsSession, LoginExchange.Of(context)), context.CancellationToken);
        if (verdict is not (PasswordLoginVerdict.Accepted or PasswordLoginVerdict.AcceptedUnchecked))
        {
            throw new StompProtocolViolationException(
                StompErrors.AuthenticationFailed, $"CONNECT's login was refused ({verdict})");
        }

        // The logged-in account, or none, is the caller topic access judges (decision 3).
        account = userName;
    }

    private StompFrame Connected(StompVersion negotiated)
    {
        var headers = new List<KeyValuePair<string, string>> { new("version", StompVersionNegotiation.Text(negotiated)) };
        if (negotiated != StompVersion.V1_0)
        {
            headers.Add(new("heart-beat", options.HeartBeatOffer.ToString()));
        }

        // Informational only (no later frame presents it), so it comes from the seedable source (ADR-0124 decision 6).
        Span<byte> sessionBytes = stackalloc byte[SessionHexDigits / 2];
        context.Identifiers.NextBytes(sessionBytes);
        headers.Add(new("session", Convert.ToHexStringLower(sessionBytes)));
        headers.Add(new("server", "surl"));
        return new StompFrame("CONNECTED", headers, ReadOnlyMemory<byte>.Empty);
    }

    // Reading, writing and heart-beating run side by side; however the reading ends, the
    // subscriptions end (their unacknowledged queue messages go back), open transactions are
    // aborted, the outbox drains, and an ERROR or a slow consumer is followed by lingering.
    private async Task ServeConnectedAsync()
    {
        busConnection = bus.Connect(account);
        var writing = WriteThenStopReadingAsync();
        var beating = HeartBeatAsync(stopBeating.Token);
        var (answeredError, failure) = await ReadAnsweringViolationAsync();
        await StopAsync(beating, writing);
        failure?.Throw();
        await FinishAsync(answeredError, writing);
    }

    // Reads frames until the reading ends; a violation is answered ERROR through the outbox, and
    // any other failure is handed back rather than thrown, so the connection stops before it leaves.
    private async Task<(bool AnsweredError, ExceptionDispatchInfo? Failure)> ReadAnsweringViolationAsync()
    {
        try
        {
            await ReadFramesAsync();
            return (false, null);
        }
        catch (StompProtocolViolationException violation)
        {
            context.Log.Note($"{violation.Message}; answered ERROR '{violation.Error.Header("message")}' and closed.");
            outbox.Enqueue(StompFrameWriter.Encode(violation.Error, version));
            return (true, null);
        }
        catch (Exception exception)
        {
            return (false, ExceptionDispatchInfo.Capture(exception));
        }
    }

    // However the reading ended - cancelled or failed too - the subscriptions end, the
    // heart-beats stop, and the writer drains and stops before anything leaves the connection.
    private async Task StopAsync(Task beating, Task writing)
    {
        EndSession();
        await stopBeating.CancelAsync();
        await beating;
        outbox.Complete();
        await Task.WhenAny(writing);
    }

    private async Task FinishAsync(bool answeredError, Task writing)
    {
        await writing;
        if (dropped)
        {
            connection.Abort();
            return;
        }

        if (outbox.HasOverflowed)
        {
            await CutOffSlowConsumerAsync();
        }

        await connection.CompleteWritesAsync(context.CancellationToken);
        if (answeredError || outbox.HasOverflowed)
        {
            await LingerAsync();
        }
    }

    // However the writing ends - the outbox drained, a slow consumer or a failed write - reading
    // stops with it.
    private async Task WriteThenStopReadingAsync()
    {
        try
        {
            await outbox.WriteAllAsync(connection, context.CancellationToken);
        }
        finally
        {
            stopReading.Cancel();
        }
    }

    private async Task ReadFramesAsync()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, stopReading.Token);
        try
        {
            while (!disconnected && await reader.ReadFrameAsync(cancellation.Token) is { } frame)
            {
                await AnswerOrApplyFaultAsync(frame);
            }
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            // Stopped by the writer's end or the heart-beat watch: the connection closes with no more bytes.
        }
    }

    // BL-1062: a fault rule may hold the frame, drop the connection once what is queued is written, or answer ERROR.
    private async Task AnswerOrApplyFaultAsync(StompFrame frame)
    {
        var fault = await StompFrameFaults.FindAsync(context, account, frame);
        if (fault is null)
        {
            Answer(frame);
        }
        else if (fault.Kind == FaultKind.Drop)
        {
            dropped = true;
            disconnected = true;
        }
        else
        {
            context.Log.Note($"{frame.Command} answered ERROR by fault rule {fault.Id}.");
            throw new StompProtocolViolationException(fault.Reply!, $"{frame.Command} met fault rule {fault.Id}", frame.Header("receipt"));
        }
    }

    // Decision 8: best effort and bounded; whatever happens, the connection then closes.
    private async Task CutOffSlowConsumerAsync()
    {
        context.Log.Note($"More than {options.PendingBytesLimit} bytes waited unwritten; answered ERROR '{StompErrors.ResourceLimitExceeded}' and closed.");
        using var timeout = new CancellationTokenSource(StompServerOptions.SlowConsumerErrorTimeout, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, timeout.Token);
        try
        {
            await connection.WriteAsync(StompFrameWriter.Encode(StompErrors.Frame(StompErrors.ResourceLimitExceeded), version), cancellation.Token);
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException)
        {
            context.Log.Note($"The slow consumer's ERROR was not written ({exception.GetType().Name}).");
        }
    }

    // The grammar gives only SEND a body.
    private static void RequireNoBody(StompFrame frame)
    {
        if (!frame.Body.IsEmpty)
        {
            throw new StompProtocolViolationException(
                StompErrors.MalformedFrame, $"{frame.Command} carried a body", frame.Header("receipt"));
        }
    }

    // Decision 6 ("Connection Lingering"): read and discard what the client still sends, up to a
    // second, so the ERROR is read before the close rather than lost to a reset.
    private async Task LingerAsync()
    {
        using var linger = new CancellationTokenSource(Linger, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, linger.Token);
        var discard = new byte[4096];
        try
        {
            while (await connection.ReadAsync(discard, cancellation.Token) > 0)
            {
            }
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
        }
    }

    private ValueTask WriteAsync(StompFrame frame, StompVersion? escaping) =>
        connection.WriteAsync(StompFrameWriter.Encode(frame, escaping), context.CancellationToken);

    private void Enqueue(StompFrame frame) => outbox.Enqueue(StompFrameWriter.Encode(frame, version));
}
