using System.Globalization;
using System.Security.Cryptography;
using Surl.Content;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ws;

/// <summary>
/// Answers each upgrade request read from one connection, as <see cref="WsProtocolServer"/>
/// describes, and says whether the connection stays open for another.
/// </summary>
internal sealed class WebSocketUpgradeResponder
{
    /// <summary>
    /// How long a refusal - any answer that closes the connection because of the request - may
    /// take to write before the server gives up on it (ADR-0006 section 5, ADR-0019).
    /// </summary>
    public static readonly TimeSpan RefusalWriteDeadline = TimeSpan.FromSeconds(1);

    // The empty CLOSE that ends the exchange after the 101 until the message exchange is built
    // (ADR-0071, decisions 4 and 5).
    private static readonly byte[] EmptyCloseFrame = WebSocketFrameEncoder.EncodeFrame(true, WebSocketOpcode.Close, []);

    // Every other failure to read a head is a malformed head: 400 (ADR-0071 decision 1, check 1).
    private static readonly Dictionary<HttpRequestHeadReadOutcome, HttpStatus> StatusesForHeadsNotRead = new()
    {
        [HttpRequestHeadReadOutcome.UnsupportedVersion] = HttpStatus.HttpVersionNotSupported,
        [HttpRequestHeadReadOutcome.HeadTooLarge] = HttpStatus.RequestHeaderFieldsTooLarge,
        [HttpRequestHeadReadOutcome.HeadTimedOut] = HttpStatus.RequestTimeout,
    };

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly ContentStore contentStore;
    private readonly IHttpAuthenticationSession authenticationSession;
    private readonly WebSocketLingeringClose lingeringClose;

    // The WWW-Authenticate values the authentication session gave for the request being
    // answered, written on whatever answer it gets; none before a request is judged.
    private IReadOnlyList<string> wwwAuthenticateValues = [];

    /// <summary>
    /// Creates a responder for one connection.
    /// </summary>
    /// <param name="connection">Where answers are written.</param>
    /// <param name="reader">The connection's reader, through which the lingering close reads.</param>
    /// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
    /// <param name="contentStore">Where request paths are looked up.</param>
    /// <param name="authenticationSession">The connection's authentication session, which judges every upgrade request.</param>
    public WebSocketUpgradeResponder(IConnection connection, HttpConnectionReader reader, ExchangeContext context, ContentStore contentStore, IHttpAuthenticationSession authenticationSession)
    {
        this.connection = connection;
        this.context = context;
        this.contentStore = contentStore;
        this.authenticationSession = authenticationSession;
        lingeringClose = new WebSocketLingeringClose(reader, context);
    }

    /// <summary>
    /// Answers an upgrade request whose head was read, in ADR-0071 decision 1's order: the
    /// <c>Host</c>, the login, the method and WebSocket fields, then the path.
    /// </summary>
    /// <param name="head">The request head.</param>
    /// <returns><see langword="true"/> when the connection stays open for another upgrade request.</returns>
    public async Task<bool> AnswerUpgradeRequestAsync(HttpRequestHead head)
    {
        wwwAuthenticateValues = [];
        if (head.GetFieldValues("Host").Count != 1)
        {
            return await RefuseAsync(WebSocketUpgradeRefusal.WithoutFields(HttpStatus.BadRequest, "the request does not carry exactly one Host field"));
        }

        var verdict = await JudgeLoginAsync(head);
        wwwAuthenticateValues = verdict.WwwAuthenticateValues;

        return verdict.Outcome switch
        {
            HttpAuthenticationOutcome.Proceed => await AnswerLetInRequestAsync(head),
            HttpAuthenticationOutcome.Challenge => await ChallengeAsync(head),
            _ => await RefuseAsync(WebSocketUpgradeRefusal.WithoutFields(HttpStatus.Forbidden, "the authentication policy forbade the upgrade")),
        };
    }

    /// <summary>
    /// Answers a connection on which no request head could be read: nothing when the client
    /// closed it or never sent a byte before the head timeout, otherwise the refusal the HTTP
    /// server answers the same outcome with (ADR-0071 decision 1, check 1).
    /// </summary>
    /// <param name="outcome">Why no head was read.</param>
    /// <returns><see langword="false"/>: the connection never stays open after this.</returns>
    public Task<bool> AnswerHeadNotReadAsync(HttpRequestHeadReadOutcome outcome)
    {
        wwwAuthenticateValues = [];
        if (outcome is HttpRequestHeadReadOutcome.ConnectionClosed or HttpRequestHeadReadOutcome.ConnectionClosedBeforeHeadEnded)
        {
            context.Log.Note($"The client closed the connection: {outcome}.");

            return Task.FromResult(false);
        }

        if (outcome is HttpRequestHeadReadOutcome.HeadTimedOutBeforeAnyByte)
        {
            context.Log.Note($"No request head was read: {outcome}; closed with no bytes.");

            return Task.FromResult(false);
        }

        var status = StatusesForHeadsNotRead.GetValueOrDefault(outcome, HttpStatus.BadRequest);

        return RefuseAsync(WebSocketUpgradeRefusal.WithoutFields(status, $"no request head was read ({outcome})"));
    }

    // An upgrade is a read (ADR-0071 decision 3): the session never sees it as a write. A login
    // bound to the body (AWS Signature Version 4, ADR-0045) is checked against the body an
    // upgrade GET carries, none: a request that announces one is refused at check 7 anyway.
    private async Task<HttpAuthenticationVerdict> JudgeLoginAsync(HttpRequestHead head)
    {
        var request = new HttpAuthenticationRequest(
            head.Method,
            head.RequestTarget,
            false,
            head.Fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value)).ToArray());
        var verdict = await authenticationSession.JudgeAsync(request, context.CancellationToken);
        NoteCheckedLogin(verdict);
        if (verdict.Outcome != HttpAuthenticationOutcome.Proceed || verdict.BodyCheck is null)
        {
            return verdict;
        }

        var bodyVerdict = await verdict.BodyCheck.JudgeBodyAsync(SHA256.HashData(ReadOnlySpan<byte>.Empty), context.CancellationToken);
        NoteCheckedLogin(bodyVerdict);

        return bodyVerdict;
    }

    private void NoteCheckedLogin(HttpAuthenticationVerdict verdict)
    {
        if (verdict.CheckedLogin is not null)
        {
            context.Log.Note(verdict.CheckedLogin.Note);
        }
    }

    // A 401 keeps the connection, its next request read as a new upgrade request, as ADR-0032
    // section 4 has it for HTTP - unless the request was HTTP/1.0, said Connection: close, or
    // announced a body that is never read; then it is a refusal like every other.
    private async Task<bool> ChallengeAsync(HttpRequestHead head)
    {
        var refusal = WebSocketUpgradeRefusal.WithoutFields(HttpStatus.Unauthorized, "a login is needed");
        var keepsConnectionOpen = head.Version.Minor >= 1
            && !WebSocketUpgradeChecks.ListsToken(head, "Connection", "close")
            && HttpRequestBodyFraming.Of(head).Kind == HttpRequestBodyFramingKind.None;
        if (!keepsConnectionOpen)
        {
            return await RefuseAsync(refusal);
        }

        NoteRefusal(refusal, "the connection stays open");
        await connection.WriteAsync(RefusalHead(refusal, false).ToBytes(), context.CancellationToken);

        return true;
    }

    private Task<bool> AnswerLetInRequestAsync(HttpRequestHead head)
    {
        var path = RequestPath(head.RequestTarget);
        var refusal = WebSocketUpgradeChecks.FindRefusal(head) ?? FindPathRefusal(path);

        return refusal is null ? AcceptAsync(head, path) : RefuseAsync(refusal);
    }

    // Check 10: a file, or a directory when listings are on, after the content store's
    // exposure checks; anything else is answered as absent (ADR-0006 section 2).
    private WebSocketUpgradeRefusal? FindPathRefusal(string path)
    {
        var mapping = contentStore.MapRequestPath(path);
        if (!mapping.IsMapped)
        {
            return WebSocketUpgradeRefusal.WithoutFields(HttpStatus.NotFound, $"the path {path} was refused ({mapping.Refusal})");
        }

        var whyNotFound = WhyMappedPathIsNotServed(mapping.EntryKind, path);

        return whyNotFound is null ? null : WebSocketUpgradeRefusal.WithoutFields(HttpStatus.NotFound, whyNotFound);
    }

    // Null when a mapped path is served: a file, or a directory when listings are on.
    private string? WhyMappedPathIsNotServed(ContentEntryKind entryKind, string path) => entryKind switch
    {
        ContentEntryKind.File => null,
        ContentEntryKind.Directory => contentStore.ExposureOptions.ListDirectories ? null : $"{path} is a directory and directory listings are off",
        _ => $"nothing exists at {path}",
    };

    // The 101 (ADR-0071 decision 2), then, until the message exchange is built, the empty
    // CLOSE and decision 5's close: the sending side shut down and a lingering read.
    private async Task<bool> AcceptAsync(HttpRequestHead head, string path)
    {
        var key = head.GetFieldValues("Sec-WebSocket-Key")[0];
        var accepted = new HttpResponseHead(WebSocketHttpStatuses.SwitchingProtocols)
            .AddField("Date", FormatHttpDate(context.TimeProvider.GetUtcNow()))
            .AddField("Server", HttpResponseHead.ServerName)
            .AddField("Upgrade", "websocket")
            .AddField("Connection", "Upgrade")
            .AddField("Sec-WebSocket-Accept", WebSocketAcceptKey.Compute(key))
            .AddChallengeFields(wwwAuthenticateValues);

        context.Log.Note($"WebSocket upgrade accepted for {path}");
        await connection.WriteAsync(accepted.ToBytes(), context.CancellationToken);
        await connection.WriteAsync(EmptyCloseFrame, context.CancellationToken);
        await connection.CompleteWritesAsync(context.CancellationToken);
        await lingeringClose.LingerAsync();

        return false;
    }

    // A refusal gets one second to be written and half-closed; past that the server gives up
    // on it and leaves the close to the engine's dispose, never an abort (ADR-0006, section 5).
    // Once the refusal is written, what the client still sends is read and thrown away.
    private async Task<bool> RefuseAsync(WebSocketUpgradeRefusal refusal)
    {
        NoteRefusal(refusal, "closed");
        if (await TryWriteRefusalAsync(refusal))
        {
            await lingeringClose.LingerAsync();
        }

        return false;
    }

    private async Task<bool> TryWriteRefusalAsync(WebSocketUpgradeRefusal refusal)
    {
        using var deadline = new CancellationTokenSource(RefusalWriteDeadline, context.TimeProvider);
        using var deadlineOrExchange = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, deadline.Token);
        try
        {
            await connection.WriteAsync(RefusalHead(refusal, true).ToBytes(), deadlineOrExchange.Token);
            await connection.CompleteWritesAsync(deadlineOrExchange.Token);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note($"The {refusal.Status.Code} was not written within its {RefusalWriteDeadline.TotalSeconds}-second write deadline; the connection is closed without it.");

            return false;
        }

        return true;
    }

    private void NoteRefusal(WebSocketUpgradeRefusal refusal, string then) =>
        context.Log.Note($"WebSocket upgrade refused: {refusal.Status.Code} {refusal.Status.ReasonPhrase}: {refusal.FailedCheck}; {then}.");

    // ADR-0071 decision 1: Date, Server, the refusal's own fields, Content-Length: 0, the
    // challenges, and Connection: close on every refusal but a 401 that keeps the connection.
    private HttpResponseHead RefusalHead(WebSocketUpgradeRefusal refusal, bool closesConnection)
    {
        var head = new HttpResponseHead(refusal.Status)
            .AddField("Date", FormatHttpDate(context.TimeProvider.GetUtcNow()))
            .AddField("Server", HttpResponseHead.ServerName);
        foreach (var field in refusal.Fields)
        {
            head.AddField(field.Key, field.Value);
        }

        return head
            .AddField("Content-Length", "0")
            .AddChallengeFields(wwwAuthenticateValues)
            .AddField("Connection", closesConnection ? "close" : null);
    }

    // The path of the request target, without its query.
    private static string RequestPath(string requestTarget)
    {
        var query = requestTarget.IndexOf('?', StringComparison.Ordinal);

        return query < 0 ? requestTarget : requestTarget[..query];
    }

    // IMF-fixdate (RFC 9110, section 5.6.7): "r" is exactly that format, in UTC.
    private static string FormatHttpDate(DateTimeOffset time) => time.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture);
}
