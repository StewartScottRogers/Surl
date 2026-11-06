using System.Globalization;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;
using Surl.WebSocket;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// Answers each HTTP request an Engine.IO client sends (EIO "Request path", "Query parameters",
/// "Handshake", "WebSocket"; ADR-0120, decisions 1, 2 and 7).
/// </summary>
/// <remarks>
/// <para>
/// A path other than <c>/socket.io/</c> is <c>404</c> with no body. On it, the query is judged in
/// this order, each refusal <c>400</c> with <see cref="EngineIoRefusal"/>'s body: a
/// <c>transport</c> that is not <c>websocket</c> is code 0 (<c>polling</c> too, until BL-762); a
/// <c>sid</c> is code 3 when a session has it (every session already has its WebSocket) and code 1
/// otherwise; a method other than <c>GET</c> is code 2; an <c>EIO</c> other than <c>4</c> is code
/// 5; a request whose <c>Upgrade</c> does not list <c>websocket</c> is code 3.
/// </para>
/// <para>
/// The upgrade is then checked by <see cref="WebSocketUpgradeChecks"/> (ADR-0071's refusals); a
/// listener holding <see cref="SocketIoSessions.MaxSessions"/> answers <c>503</c> with no body; the
/// rest is <c>101</c>, no subprotocol chosen, and a <see cref="SocketIoSession"/> owns the connection
/// from then on. An <c>Authorization</c> field is never read: the login is the CONNECT payload's.
/// </para>
/// </remarks>
internal sealed class SocketIoRequestAnswerer(SocketIoListener listener, ExchangeContext context) : IHttpRequestHandler
{
    /// <summary>
    /// The one path Engine.IO is served on (EIO "Request path").
    /// </summary>
    public const string EngineIoPath = "/socket.io/";

    /// <inheritdoc/>
    public Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var target = SocketIoRequestTarget.Parse(request.Target);

        return Task.FromResult(target.Path == EngineIoPath ? AnswerEngineIo(request, target) : Answer(request, HttpStatus.NotFound));
    }

    private HttpAnswer AnswerEngineIo(HttpRequest request, SocketIoRequestTarget target)
    {
        // Http1ConnectionServer, the only server of this handler, always sets the version.
        var head = new HttpRequestHead(request.Method, request.Target, request.Http1Version!, request.Fields.Select(field => new HttpRequestField(field.Key, field.Value)));
        if (RefusalCode(head, target) is { } code)
        {
            context.Log.Note($"Engine.IO request refused with code {code}.");

            return Refused(request, code);
        }

        if (WebSocketUpgradeChecks.FindRefusal(head) is { } refusal)
        {
            return Answer(request, refusal.Status, refusal.Fields);
        }

        if (listener.Sessions.TryReserve() is not { } sessionId)
        {
            context.Log.Note($"Upgrade refused: the listener holds {SocketIoSessions.MaxSessions} sessions.");

            return Answer(request, HttpStatus.ServiceUnavailable);
        }

        return Upgrade(head, sessionId);
    }

    private int? RefusalCode(HttpRequestHead head, SocketIoRequestTarget target)
    {
        if (target.QueryParameter("transport") != "websocket")
        {
            return EngineIoRefusal.TransportUnknown;
        }

        if (target.QueryParameter("sid") is { } sessionId)
        {
            return listener.Sessions.Contains(sessionId) ? EngineIoRefusal.BadRequest : EngineIoRefusal.SessionIdUnknown;
        }

        return HandshakeRefusalCode(head, target);
    }

    // A request without sid opens a session: GET, EIO=4, and an upgrade to websocket.
    private static int? HandshakeRefusalCode(HttpRequestHead head, SocketIoRequestTarget target)
    {
        if (head.Method != "GET")
        {
            return EngineIoRefusal.BadHandshakeMethod;
        }

        if (target.QueryParameter("EIO") != "4")
        {
            return EngineIoRefusal.UnsupportedProtocolVersion;
        }

        return WebSocketUpgradeChecks.ListsToken(head, "Upgrade", "websocket") ? null : EngineIoRefusal.BadRequest;
    }

    private HttpAnswer Upgrade(HttpRequestHead head, string sessionId) =>
        new(
            WebSocketHttpStatuses.SwitchingProtocols,
            [
                DateField(),
                new("Upgrade", "websocket"),
                new("Connection", "Upgrade"),
                new("Sec-WebSocket-Accept", WebSocketAcceptKey.Compute(head.GetFieldValues("Sec-WebSocket-Key")[0])),
            ],
            HttpAnswerBody.None,
            IsRefusal: false,
            LeavesRequestBodyUnread: false,
            new SocketIoWebSocketTakeover(listener, sessionId, context));

    private HttpAnswer Refused(HttpRequest request, int code)
    {
        var body = EngineIoRefusal.Body(code);

        return new(
            HttpStatus.BadRequest,
            [DateField(), ContentLength(body.Length), new("Content-Type", "application/json")],
            new HttpAnswerBody.Bytes(body),
            IsRefusal: false,
            LeavesRequestBodyUnread: request.Body.IsAnnounced);
    }

    private HttpAnswer Answer(HttpRequest request, HttpStatus status, IReadOnlyList<KeyValuePair<string, string>>? fields = null) =>
        new(status, [DateField(), ContentLength(0), .. fields ?? []], HttpAnswerBody.None, IsRefusal: false, LeavesRequestBodyUnread: request.Body.IsAnnounced);

    private KeyValuePair<string, string> DateField() =>
        new("Date", context.TimeProvider.GetUtcNow().ToString("r", CultureInfo.InvariantCulture));

    private static KeyValuePair<string, string> ContentLength(int length) =>
        new("Content-Length", length.ToString(CultureInfo.InvariantCulture));
}
