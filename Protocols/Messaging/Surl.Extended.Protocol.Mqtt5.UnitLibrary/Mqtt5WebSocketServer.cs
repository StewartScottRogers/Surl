using System.Globalization;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;
using Surl.WebSocket;

namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// The MQTT 5.0 broker over WebSocket, <c>mqtt5+ws</c> (MQTT 5.0 chapter 6, ADR-0093 decision 7):
/// accepts the upgrade, then lets the broker answer the binary frames as the byte stream it
/// reads, its packet handling unchanged.
/// </summary>
/// <remarks>
/// <para>
/// <b>The upgrade.</b> The request head is read under <see cref="ExchangeLimits.MaxRequestHeadBytes"/>
/// and <see cref="ExchangeLimits.HeadTimeout"/>. A client that closes, or sends nothing within the
/// head timeout, is closed with no bytes; a head too large is <c>431</c>, a version other than
/// HTTP/1.x <c>505</c>, a head timed out part-way <c>408</c>, any other unreadable head <c>400</c>.
/// Then a head without exactly one <c>Host</c> is <c>400</c>; RFC 6455 section 4.2.1's checks
/// follow, as <see cref="WebSocketUpgradeChecks"/> runs them; a path other than
/// <see cref="Path"/> is <c>404</c>; and an upgrade not offering <see cref="Subprotocol"/>
/// (<c>[MQTT-6.0.0-3]</c>) is <c>400</c>. Each refusal carries <c>Content-Length: 0</c> and
/// <c>Connection: close</c>, gets one second to be written, and is followed by a lingering close.
/// An accepted upgrade is answered <c>101 Switching Protocols</c> with
/// <c>Sec-WebSocket-Protocol: mqtt</c> (<c>[MQTT-6.0.0-4]</c>). No HTTP login is asked for.
/// </para>
/// <para>
/// <b>The broker.</b> It serves <see cref="Mqtt5WebSocketConnection"/> under a head timeout that
/// is what the upgrade left of it, so the time from accept to <c>CONNACK</c> is one head timeout.
/// When it is done, a close <c>1000</c> follows unless a close was already sent, the connection
/// is half-closed and what the client still sends is read and thrown away for up to a second.
/// </para>
/// </remarks>
public sealed class Mqtt5WebSocketServer : IConnectionProtocolServer
{
    /// <summary>The only path served (ADR-0093 decision 7).</summary>
    public const string Path = "/mqtt";

    /// <summary>The only WebSocket subprotocol spoken (<c>[MQTT-6.0.0-4]</c>).</summary>
    public const string Subprotocol = "mqtt";

    /// <summary>How long a refusal has to be written before the server gives up on it.</summary>
    public static readonly TimeSpan RefusalWriteDeadline = TimeSpan.FromSeconds(1);

    private static readonly Dictionary<HttpRequestHeadReadOutcome, HttpStatus> StatusesForHeadsNotRead = new()
    {
        [HttpRequestHeadReadOutcome.UnsupportedVersion] = HttpStatus.HttpVersionNotSupported,
        [HttpRequestHeadReadOutcome.HeadTooLarge] = HttpStatus.RequestHeaderFieldsTooLarge,
        [HttpRequestHeadReadOutcome.HeadTimedOut] = HttpStatus.RequestTimeout,
    };

    private readonly Mqtt5Broker broker;

    /// <summary>
    /// Creates the server in front of <paramref name="broker"/>.
    /// </summary>
    /// <param name="broker">The broker that answers the MQTT packets.</param>
    public Mqtt5WebSocketServer(Mqtt5Broker broker)
    {
        ArgumentNullException.ThrowIfNull(broker);

        this.broker = broker;
    }

    /// <summary>The scheme answered: <c>mqtt5+ws</c>.</summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["mqtt5+ws"]);

    /// <summary>
    /// Answers the upgrade on <paramref name="connection"/>, then every MQTT packet until the
    /// connection ends.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        var acceptedAt = context.TimeProvider.GetUtcNow();
        var reader = new HttpConnectionReader(connection, context.Limits.MaxRequestHeadBytes);
        var lingeringClose = new WebSocketLingeringClose(reader, context);
        var result = await reader.ReadNextRequestHeadAsync(context, isFirstHead: true);
        if (result.Head is not { } head)
        {
            await AnswerHeadNotReadAsync(connection, context, lingeringClose, result.Outcome);
            return;
        }

        if (FindRefusal(head, context) is { } refusal)
        {
            await RefuseAsync(connection, context, lingeringClose, refusal);
            return;
        }

        var acceptance = WebSocketUpgradeAcceptance.Accept(head, [Subprotocol], context.TimeProvider.GetUtcNow());
        await connection.WriteAsync(acceptance.ResponseHead.ToBytes(), context.CancellationToken);
        context.Log.Note($"WebSocket upgrade accepted for {Path} with subprotocol {Subprotocol}.");
        var framed = new Mqtt5WebSocketConnection(connection, reader.ReadAsync, context);
        await broker.ServeAsync(framed, context with { Limits = context.Limits with { HeadTimeout = HeadTimeoutLeft(context, acceptedAt) } });
        await framed.CompleteWritesAsync(context.CancellationToken);
        await lingeringClose.LingerAsync(context.CancellationToken);
    }

    // The subprotocol is judged last, by the acceptance the checks before it make buildable.
    private static WebSocketUpgradeRefusal? FindRefusal(HttpRequestHead head, ExchangeContext context)
    {
        if (head.GetFieldValues("Host").Count != 1)
        {
            return WebSocketUpgradeRefusal.WithoutFields(HttpStatus.BadRequest, "the request does not carry exactly one Host field");
        }

        if (WebSocketUpgradeChecks.FindRefusal(head) is { } refusal)
        {
            return refusal;
        }

        var path = RequestPath(head.RequestTarget);
        if (path != Path)
        {
            return WebSocketUpgradeRefusal.WithoutFields(HttpStatus.NotFound, $"the path {path} is not {Path}");
        }

        return WebSocketUpgradeAcceptance.Accept(head, [Subprotocol], context.TimeProvider.GetUtcNow()).ChosenSubprotocol is null
            ? WebSocketUpgradeRefusal.WithoutFields(HttpStatus.BadRequest, $"the upgrade does not offer the subprotocol {Subprotocol} ([MQTT-6.0.0-3])")
            : null;
    }

    // A client that closed, or sent nothing within the head timeout, is closed with no bytes;
    // any other head not read is refused.
    private static Task AnswerHeadNotReadAsync(IConnection connection, ExchangeContext context, WebSocketLingeringClose lingeringClose, HttpRequestHeadReadOutcome outcome)
    {
        if (outcome is HttpRequestHeadReadOutcome.ConnectionClosed or HttpRequestHeadReadOutcome.ConnectionClosedBeforeHeadEnded or HttpRequestHeadReadOutcome.HeadTimedOutBeforeAnyByte)
        {
            context.Log.Note($"No upgrade request was read: {outcome}; closed with no bytes.");
            return Task.CompletedTask;
        }

        return RefuseAsync(connection, context, lingeringClose, WebSocketUpgradeRefusal.WithoutFields(
            StatusesForHeadsNotRead.GetValueOrDefault(outcome, HttpStatus.BadRequest), $"no request head was read ({outcome})"));
    }

    private static async Task RefuseAsync(IConnection connection, ExchangeContext context, WebSocketLingeringClose lingeringClose, WebSocketUpgradeRefusal refusal)
    {
        context.Log.Note($"WebSocket upgrade refused: {refusal.Status.Code} {refusal.Status.ReasonPhrase}: {refusal.FailedCheck}; closed.");
        using var deadline = new CancellationTokenSource(RefusalWriteDeadline, context.TimeProvider);
        using var deadlineOrExchange = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, deadline.Token);
        try
        {
            await connection.WriteAsync(RefusalHead(refusal, context).ToBytes(), deadlineOrExchange.Token);
            await connection.CompleteWritesAsync(deadlineOrExchange.Token);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note($"The {refusal.Status.Code} was not written within its {RefusalWriteDeadline.TotalSeconds}-second write deadline; the connection is closed without it.");
            return;
        }

        await lingeringClose.LingerAsync(context.CancellationToken);
    }

    private static HttpResponseHead RefusalHead(WebSocketUpgradeRefusal refusal, ExchangeContext context)
    {
        var head = new HttpResponseHead(refusal.Status)
            .AddField("Date", context.TimeProvider.GetUtcNow().ToUniversalTime().ToString("r", CultureInfo.InvariantCulture))
            .AddField("Server", HttpResponseHead.ServerName);
        foreach (var field in refusal.Fields)
        {
            head.AddField(field.Key, field.Value);
        }

        return head
            .AddField("Content-Length", "0")
            .AddField("Connection", "close");
    }

    private static TimeSpan HeadTimeoutLeft(ExchangeContext context, DateTimeOffset acceptedAt)
    {
        var headTimeout = context.Limits.HeadTimeout;
        if (headTimeout == Timeout.InfiniteTimeSpan)
        {
            return headTimeout;
        }

        var left = headTimeout - (context.TimeProvider.GetUtcNow() - acceptedAt);
        return TimeSpan.FromTicks(Math.Max(left.Ticks, 0));
    }

    private static string RequestPath(string requestTarget)
    {
        var query = requestTarget.IndexOf('?', StringComparison.Ordinal);

        return query < 0 ? requestTarget : requestTarget[..query];
    }
}
