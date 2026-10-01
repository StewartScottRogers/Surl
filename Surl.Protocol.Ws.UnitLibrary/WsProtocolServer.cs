using Surl.Content;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ws;

/// <summary>
/// The WebSocket server: answers the HTTP/1.1 upgrade request upstream curl sends for a
/// <c>ws://</c> URL (RFC 6455 section 4) as ADR-0071 decides.
/// </summary>
/// <remarks>
/// <para>
/// The request head is read with <see cref="HttpConnectionReader"/> within
/// <see cref="ExchangeLimits.MaxRequestHeadBytes"/> and <see cref="ExchangeLimits.HeadTimeout"/>
/// (ADR-0070). A head that cannot be read is answered as the HTTP server answers it:
/// <c>431</c>, <c>408</c>, <c>505</c> or <c>400</c>, or nothing when the client closed or sent
/// no byte before the head timeout. A head that was read is checked in ADR-0071 decision 1's
/// order, the first failure answering: exactly one <c>Host</c> (<c>400</c>); the login through
/// the connection's <see cref="IHttpAuthenticationSession"/> (<c>401</c> with one
/// <c>WWW-Authenticate</c> field per value the session gives, or <c>403</c>); the method
/// <c>GET</c> (<c>405</c>, <c>Allow: GET</c>); <c>HTTP/1.1</c> (<c>400</c>); <c>Upgrade</c>
/// listing <c>websocket</c> (<c>426</c>, <c>Upgrade: websocket</c>,
/// <c>Sec-WebSocket-Version: 13</c>); <c>Connection</c> listing <c>Upgrade</c> (<c>400</c>); no
/// body announced (<c>400</c>); one <c>Sec-WebSocket-Key</c> of 16 base64 bytes (<c>400</c>);
/// <c>Sec-WebSocket-Version: 13</c> (<c>426</c>, <c>Sec-WebSocket-Version: 13</c>); and a path
/// that names a file, or a directory when listings are on, in the content store (<c>404</c>).
/// </para>
/// <para>
/// Every refusal carries <c>Date</c>, <c>Server: surl</c>, its own fields,
/// <c>Content-Length: 0</c>, any challenges and <c>Connection: close</c>, gets one second to be
/// written, and is followed by a half-close and a lingering read. A <c>401</c> keeps the
/// connection, without <c>Connection: close</c>, and its next request is read as a new upgrade
/// request - unless the request was HTTP/1.0, said <c>Connection: close</c> or announced a body.
/// </para>
/// <para>
/// An upgrade that passes every check is answered <c>101 Switching Protocols</c> with
/// <c>Date</c>, <c>Server: surl</c>, <c>Upgrade: websocket</c>, <c>Connection: Upgrade</c> and
/// <c>Sec-WebSocket-Accept</c>. No subprotocol or extension is ever selected. Every refusal and
/// every <c>101</c> is noted in the exchange log (decision 9).
/// </para>
/// <para>
/// After the <c>101</c> the server sends the file at the path as one binary message, or a
/// directory's listing as one text message, in frames of at most 65536 payload bytes, then an
/// empty <c>CLOSE</c>; or, when it echoes (<c>--ws-echo</c>), sends every client message back
/// whole. Throughout, it answers a <c>PING</c> with a <c>PONG</c>, a client <c>CLOSE</c> with a
/// <c>CLOSE</c> echoing its code, and an invalid client frame or message with a <c>CLOSE</c>
/// carrying <c>1002</c>, <c>1007</c> or <c>1009</c> (past <see cref="ExchangeLimits.MaxMessageBytes"/>).
/// An exchange cancelled for its idle timeout or maximum duration is answered <c>CLOSE</c>
/// <c>1001</c>. After its <c>CLOSE</c> the server half-closes and reads what the client still
/// sends for at most one second (ADR-0071 decisions 4 to 7).
/// </para>
/// </remarks>
public sealed class WsProtocolServer : IConnectionProtocolServer
{
    private readonly ContentStore contentStore;

    private readonly IAuthenticationPolicy authenticationPolicy;

    private readonly bool echoesMessages;

    /// <summary>
    /// Creates a WebSocket server that upgrades requests for paths in
    /// <paramref name="contentStore"/> that <paramref name="authenticationPolicy"/> lets in
    /// (ADR-0071, decisions 1 and 3).
    /// </summary>
    /// <param name="contentStore">The content store every request path is looked up in.</param>
    /// <param name="authenticationPolicy">
    /// Judges every upgrade request: upgraded, challenged with <c>401</c>, or refused with <c>403</c>.
    /// </param>
    public WsProtocolServer(ContentStore contentStore, IAuthenticationPolicy authenticationPolicy)
        : this(contentStore, authenticationPolicy, echoesMessages: false)
    {
    }

    /// <summary>
    /// Creates a WebSocket server that, when <paramref name="echoesMessages"/> is set
    /// (<c>--ws-echo</c>), upgrades any path <paramref name="authenticationPolicy"/> lets in and
    /// echoes every client message instead of sending the path (ADR-0071 decision 4).
    /// </summary>
    /// <param name="contentStore">The content store every request path is looked up in when the server does not echo.</param>
    /// <param name="authenticationPolicy">
    /// Judges every upgrade request: upgraded, challenged with <c>401</c>, or refused with <c>403</c>.
    /// </param>
    /// <param name="echoesMessages">Whether every upgrade echoes client messages instead of sending its path.</param>
    public WsProtocolServer(ContentStore contentStore, IAuthenticationPolicy authenticationPolicy, bool echoesMessages)
    {
        ArgumentNullException.ThrowIfNull(contentStore);
        ArgumentNullException.ThrowIfNull(authenticationPolicy);

        this.contentStore = contentStore;
        this.authenticationPolicy = authenticationPolicy;
        this.echoesMessages = echoesMessages;
    }

    /// <summary>
    /// The one scheme answered: <c>ws</c>. <c>wss</c> is this same server on connections the
    /// serving engine has already secured with TLS (ADR-0071 decision 8).
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["ws"]);

    /// <summary>
    /// Answers every upgrade request on <paramref name="connection"/> until the client closes
    /// it or an answer closes it.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        var reader = new HttpConnectionReader(connection, context.Limits.MaxRequestHeadBytes);
        var authenticationSession = authenticationPolicy.StartHttpConnection(connection.TlsSession);
        var responder = new WebSocketUpgradeResponder(connection, reader, context, contentStore, authenticationSession, echoesMessages);
        var isFirstHead = true;
        var keepsConnectionOpen = true;

        while (keepsConnectionOpen)
        {
            var result = await reader.ReadNextRequestHeadAsync(context, isFirstHead);
            isFirstHead = false;
            keepsConnectionOpen = result.Head is { } head
                ? await responder.AnswerUpgradeRequestAsync(head)
                : await responder.AnswerHeadNotReadAsync(result.Outcome);
        }
    }
}
