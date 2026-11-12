using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;
using Surl.WebSocket;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// Answers each HTTP request an Engine.IO client sends (EIO "Request path", "Query parameters",
/// "Handshake", "HTTP long-polling", "WebSocket", "Upgrade"; ADR-0120, decisions 1, 2 and 7).
/// </summary>
/// <remarks>
/// <para>
/// A path other than <c>/socket.io/</c> is <c>404</c> with no body. On it, the query is judged in
/// this order, each refusal <c>400</c> with <see cref="EngineIoRefusal"/>'s body: a
/// <c>transport</c> that is neither <c>websocket</c> nor <c>polling</c> is code 0; a <c>sid</c> no
/// session has is code 1; without a <c>sid</c>, a method other than <c>GET</c> is code 2 and an
/// <c>EIO</c> other than <c>4</c> code 5.
/// </para>
/// <para>
/// A polling request without <c>sid</c> opens a session and one with it polls or posts
/// (<see cref="SocketIoPollingAnswerer"/>). A WebSocket request whose <c>Upgrade</c> does not list
/// <c>websocket</c> is code 3; the upgrade is then checked by <see cref="WebSocketUpgradeChecks"/>
/// (ADR-0071's refusals). Without <c>sid</c> it opens a session, <c>503</c> with no body when the
/// listener holds <see cref="SocketIoSessions.MaxSessions"/>; with one, it upgrades that polling
/// session, and is code 3 for a session that already has a WebSocket or is probing one. The rest is
/// <c>101</c>, no subprotocol chosen, and a <see cref="SocketIoWebSocketTakeover"/> owns the
/// connection from then on. An <c>Authorization</c> field is never read: the login is the CONNECT
/// payload's.
/// </para>
/// </remarks>
internal sealed class SocketIoRequestAnswerer : IHttpRequestHandler
{
    /// <summary>
    /// The one path Engine.IO is served on (EIO "Request path").
    /// </summary>
    public const string EngineIoPath = "/socket.io/";

    private const string PollingTransport = "polling";

    private const string WebSocketTransport = "websocket";

    private readonly SocketIoListener listener;
    private readonly ExchangeContext context;
    private readonly SocketIoAnswers answers;
    private readonly SocketIoPollingAnswerer polling;

    /// <summary>
    /// Creates the answerer for one connection.
    /// </summary>
    /// <param name="listener">What the listener's sessions share.</param>
    /// <param name="context">The connection's exchange.</param>
    /// <param name="tlsSession">The connection's TLS session, for a session it opens.</param>
    public SocketIoRequestAnswerer(SocketIoListener listener, ExchangeContext context, TlsSession? tlsSession)
    {
        this.listener = listener;
        this.context = context;
        answers = new SocketIoAnswers(context);
        polling = new SocketIoPollingAnswerer(listener, context, tlsSession, answers);
    }

    /// <inheritdoc/>
    public Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var target = SocketIoRequestTarget.Parse(request.Target);

        return target.Path == EngineIoPath
            ? AnswerEngineIoAsync(request, target, cancellationToken)
            : Task.FromResult(answers.Empty(request, HttpStatus.NotFound));
    }

    private static int? HandshakeRefusalCode(HttpRequest request, SocketIoRequestTarget target)
    {
        if (request.Method != "GET")
        {
            return EngineIoRefusal.BadHandshakeMethod;
        }

        return target.QueryParameter("EIO") == "4" ? null : EngineIoRefusal.UnsupportedProtocolVersion;
    }

    private Task<HttpAnswer> AnswerEngineIoAsync(HttpRequest request, SocketIoRequestTarget target, CancellationToken cancellationToken)
    {
        var transport = target.QueryParameter("transport");
        if (transport is not (PollingTransport or WebSocketTransport))
        {
            return Refuse(request, EngineIoRefusal.TransportUnknown);
        }

        var sessionId = target.QueryParameter("sid");
        if (sessionId is null)
        {
            return HandshakeRefusalCode(request, target) is { } code ? Refuse(request, code) : Task.FromResult(Open(request, transport));
        }

        if (!listener.Sessions.TryFind(sessionId, out var session))
        {
            return Refuse(request, EngineIoRefusal.SessionIdUnknown);
        }

        if (session is null)
        {
            // Reserved for a WebSocket whose takeover has not begun: it has its WebSocket already.
            return Refuse(request, EngineIoRefusal.BadRequest);
        }

        return transport == PollingTransport
            ? polling.AnswerAsync(request, session, cancellationToken)
            : Task.FromResult(UpgradeAnswer(request, sessionId, session));
    }

    private HttpAnswer Open(HttpRequest request, string transport) =>
        transport == PollingTransport ? polling.Open(request) : UpgradeAnswer(request, null, null);

    // A new session's WebSocket, or a polling session's upgrade.
    private HttpAnswer UpgradeAnswer(HttpRequest request, string? sessionId, SocketIoSession? pollingSession)
    {
        // Http1ConnectionServer, the only server of this handler, always sets the version.
        var head = new HttpRequestHead(request.Method, request.Target, request.Http1Version!, request.Fields.Select(field => new HttpRequestField(field.Key, field.Value)));
        if (!WebSocketUpgradeChecks.ListsToken(head, "Upgrade", WebSocketTransport))
        {
            return Refused(request, EngineIoRefusal.BadRequest);
        }

        if (WebSocketUpgradeChecks.FindRefusal(head) is { } refusal)
        {
            return answers.Empty(request, refusal.Status, refusal.Fields);
        }

        if (pollingSession is not null)
        {
            return pollingSession.TryBeginProbe()
                ? Upgrade(head, new SocketIoWebSocketTakeover(listener, sessionId!, pollingSession, context))
                : Refused(request, EngineIoRefusal.BadRequest);
        }

        if (listener.Sessions.TryReserve() is not { } reserved)
        {
            context.Log.Note($"Upgrade refused: the listener holds {SocketIoSessions.MaxSessions} sessions.");

            return answers.Empty(request, HttpStatus.ServiceUnavailable);
        }

        return Upgrade(head, new SocketIoWebSocketTakeover(listener, reserved, null, context));
    }

    private HttpAnswer Upgrade(HttpRequestHead head, SocketIoWebSocketTakeover takeover) =>
        new(
            WebSocketHttpStatuses.SwitchingProtocols,
            [
                answers.DateField(),
                new("Upgrade", "websocket"),
                new("Connection", "Upgrade"),
                new("Sec-WebSocket-Accept", WebSocketAcceptKey.Compute(head.GetFieldValues("Sec-WebSocket-Key")[0])),
            ],
            HttpAnswerBody.None,
            IsRefusal: false,
            LeavesRequestBodyUnread: false,
            takeover);

    private Task<HttpAnswer> Refuse(HttpRequest request, int code) => Task.FromResult(Refused(request, code));

    private HttpAnswer Refused(HttpRequest request, int code)
    {
        context.Log.Note($"Engine.IO request refused with code {code}.");

        return answers.Refused(request, code);
    }
}
