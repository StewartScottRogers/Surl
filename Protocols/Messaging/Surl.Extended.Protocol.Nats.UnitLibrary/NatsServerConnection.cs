using System.Runtime.ExceptionServices;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Nats;

/// <summary>
/// One NATS connection (ADR-0113 decisions 0, 1, 2, 3, 5 and 7): INFO, CONNECT within the head
/// timeout, then operations until the client closes, a protocol error or a stale connection.
/// </summary>
internal sealed class NatsServerConnection
{
    private static readonly byte[] OkLine = "+OK\r\n"u8.ToArray();
    private static readonly byte[] PingLine = "PING\r\n"u8.ToArray();
    private static readonly byte[] PongLine = "PONG\r\n"u8.ToArray();

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly IAuthenticationPolicy policy;
    private readonly NatsServerOptions options;
    private readonly NatsInfo info;
    private readonly NatsControlLineReader reader;
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private readonly CancellationTokenSource stopReading = new();
    private readonly CancellationTokenSource stopPinging = new();
    private int unansweredPings;

    public NatsServerConnection(
        IConnection connection, ExchangeContext context, IAuthenticationPolicy policy, NatsServerOptions options, NatsInfo info)
    {
        this.connection = connection;
        this.context = context;
        this.policy = policy;
        this.options = options;
        this.info = info;
        reader = new NatsControlLineReader(connection, context.Limits.MaxLineBytes);
    }

    public async Task ServeAsync()
    {
        try
        {
            await WriteAsync(info.Encode(), context.CancellationToken);
            await ServeAfterInfoAsync();
        }
        catch (NatsProtocolViolationException violation)
        {
            context.Log.Note($"{violation.Message}; answered -ERR '{violation.ErrorText}' and closed.");
            await WriteAsync(NatsErrors.Line(violation.ErrorText), context.CancellationToken);
        }
        finally
        {
            stopReading.Dispose();
            stopPinging.Dispose();
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

    // ADR-0113 decision 2: the head timeout runs from accept to a complete CONNECT line.
    private async ValueTask<NatsControlLine?> ReadConnectWithinHeadTimeoutAsync()
    {
        using var headTimeout = new CancellationTokenSource(context.Limits.HeadTimeout, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, headTimeout.Token);
        try
        {
            return await ReadConnectAsync(cancellation.Token);
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
        await LogInAsync(connect);
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

        context.Log.Note(
            $"CONNECT accepted: name {connect.Name ?? "(none)"}, lang {connect.Lang ?? "(none)"}, version {connect.Version ?? "(none)"}, verbose {connect.Verbose}, pedantic {connect.Pedantic}.");
        if (connect.Verbose)
        {
            await WriteAsync(OkLine, context.CancellationToken);
        }
    }

    private async Task LogInAsync(NatsConnectOptions connect)
    {
        if (connect.UsesKeys)
        {
            throw new NatsProtocolViolationException(
                NatsErrors.AuthorizationViolation, "CONNECT asked for an NKey or JWT login, which surl does not serve");
        }

        var verdict = await policy.CheckPasswordLoginAsync(
            connect.ToPasswordLogin(context.Scheme, connection.TlsSession), context.CancellationToken);
        if (verdict is not (PasswordLoginVerdict.Accepted or PasswordLoginVerdict.AcceptedUnchecked))
        {
            throw new NatsProtocolViolationException(
                NatsErrors.AuthorizationViolation, $"CONNECT's login was refused ({verdict})");
        }
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
                await AnswerAsync(NatsControlLine.Parse(bytes));
            }
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
        }
    }

    private async ValueTask AnswerAsync(NatsControlLine line)
    {
        switch (line.Operation)
        {
            case "":
                break;
            case "PING":
                RequireNoArguments(line);
                await WriteAsync(PongLine, context.CancellationToken);
                break;
            case "PONG":
                RequireNoArguments(line);
                Interlocked.Exchange(ref unansweredPings, 0);
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

    // ADR-0113 decision 5. However the pinging ends - stale, stopped, cancelled or a failed write -
    // reading stops too, so a connection is never left reading with nobody keeping it alive.
    private async Task PingAsync(CancellationToken cancellationToken)
    {
        try
        {
            await PingUntilStaleAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Stopped or cancelled: the only token the pinging waits and writes on.
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
            await WriteAsync(PingLine, cancellationToken);
            await Task.Delay(options.PingInterval, context.TimeProvider, cancellationToken);
        }

        context.Log.Note($"{NatsServerOptions.MaxUnansweredPings} PINGs went unanswered; answered -ERR '{NatsErrors.StaleConnection}' and closed.");
        await WriteAsync(NatsErrors.Line(NatsErrors.StaleConnection), cancellationToken);
    }

    private async ValueTask WriteAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        await writeGate.WaitAsync(cancellationToken);
        try
        {
            await connection.WriteAsync(bytes, cancellationToken);
        }
        finally
        {
            writeGate.Release();
        }
    }

    // Runs work and hands back what it threw instead of throwing it, so the pinging is stopped and
    // awaited before any failure leaves the connection.
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
