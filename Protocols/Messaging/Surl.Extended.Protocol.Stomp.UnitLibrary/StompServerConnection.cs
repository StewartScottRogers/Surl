using System.Security.Cryptography;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Stomp;

/// <summary>
/// One STOMP connection (ADR-0118 decisions 0 to 3, 6 and 8): CONNECT or STOMP within the head
/// timeout, CONNECTED or ERROR, then frames until DISCONNECT, the client's close or an ERROR.
/// </summary>
internal sealed class StompServerConnection
{
    /// <summary>How long the server keeps reading, and discarding, after an ERROR (decision 6).</summary>
    public static readonly TimeSpan Linger = TimeSpan.FromSeconds(1);

    private const int SessionHexDigits = 32;

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly IAuthenticationPolicy policy;
    private readonly StompServerOptions options;
    private readonly StompFrameReader reader;

    public StompServerConnection(IConnection connection, ExchangeContext context, IAuthenticationPolicy policy, StompServerOptions options)
    {
        this.connection = connection;
        this.context = context;
        this.policy = policy;
        this.options = options;
        reader = new StompFrameReader(connection, context.Limits.MaxLineBytes, context.Limits.MaxMessageBytes);
    }

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
        var version = NegotiateVersion(frame);
        if (version != StompVersion.V1_0 && !StompHeartBeat.TryParse(frame.Header("heart-beat"), out _))
        {
            throw new StompProtocolViolationException(StompErrors.InvalidHeartBeat, "CONNECT's heart-beat was not two intervals");
        }

        await LogInAsync(frame);
        reader.Version = version;
        context.Log.Note($"CONNECT accepted: version {StompVersionNegotiation.Text(version)}, host {frame.Header("host") ?? "(none)"}.");
        await WriteAsync(Connected(version), null);
    }

    private static StompVersion NegotiateVersion(StompFrame frame)
    {
        var version = StompVersionNegotiation.ChooseHighest(frame.Header("accept-version"));
        if (version is null || (frame.Command == "STOMP" && version == StompVersion.V1_0))
        {
            throw new StompProtocolViolationException(
                StompErrors.UnsupportedVersionFrame(), $"{frame.Command} named no version the server speaks");
        }

        return version.Value;
    }

    private async Task LogInAsync(StompFrame frame)
    {
        var userName = frame.Header("login");
        var password = userName is null ? null : (ReadOnlyMemory<byte>?)Encoding.UTF8.GetBytes(frame.Header("passcode") ?? string.Empty);
        var verdict = await policy.CheckPasswordLoginAsync(
            new PasswordLogin(context.Scheme, userName, password, connection.TlsSession), context.CancellationToken);
        if (verdict is not (PasswordLoginVerdict.Accepted or PasswordLoginVerdict.AcceptedUnchecked))
        {
            throw new StompProtocolViolationException(
                StompErrors.AuthenticationFailed, $"CONNECT's login was refused ({verdict})");
        }
    }

    private StompFrame Connected(StompVersion version)
    {
        var headers = new List<KeyValuePair<string, string>> { new("version", StompVersionNegotiation.Text(version)) };
        if (version != StompVersion.V1_0)
        {
            headers.Add(new("heart-beat", options.HeartBeatOffer.ToString()));
        }

        headers.Add(new("session", RandomNumberGenerator.GetHexString(SessionHexDigits, lowercase: true)));
        headers.Add(new("server", "surl"));
        return new StompFrame("CONNECTED", headers, ReadOnlyMemory<byte>.Empty);
    }

    private async Task ServeConnectedAsync()
    {
        // Every frame this library serves after CONNECTED ends the connection - DISCONNECT, or an
        // ERROR - so one is read. BL-532's frames (SEND, SUBSCRIBE and the rest) make this a loop.
        if (await reader.ReadFrameAsync(context.CancellationToken) is { } frame)
        {
            await AnswerAsync(frame);
        }

        await connection.CompleteWritesAsync(context.CancellationToken);
    }

    private async ValueTask AnswerAsync(StompFrame frame)
    {
        var receipt = frame.Header("receipt");
        switch (frame.Command)
        {
            case "CONNECT" or "STOMP":
                throw new StompProtocolViolationException(StompErrors.AlreadyConnected, $"A second {frame.Command} arrived");
            case "DISCONNECT":
                RequireNoBody(frame);
                await DisconnectAsync(receipt);
                break;
            default:
                throw new StompProtocolViolationException(
                    StompErrors.UnknownCommand, $"The command {frame.Command} is not served", receipt);
        }
    }

    // Decision 6: with receipt, RECEIPT then a graceful close; without, a close with no bytes.
    private async ValueTask DisconnectAsync(string? receipt)
    {
        context.Log.Note("DISCONNECT; closed.");
        if (receipt is not null)
        {
            await WriteAsync(new StompFrame("RECEIPT", new KeyValuePair<string, string>("receipt-id", receipt)), reader.Version);
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
}
