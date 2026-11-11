using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// The Socket.IO server: Engine.IO v4 sessions carrying Socket.IO v5 on <c>/socket.io/</c>,
/// answering the <c>socketio</c> and <c>socketios</c> schemes the Curl port adds over HTTP/1.1
/// (ADR-0120; ADR-0083). It is judged by the Engine.IO and Socket.IO protocols, not by upstream
/// curl, which has no Socket.IO.
/// </summary>
/// <remarks>
/// Sessions open over HTTP long-polling, and may move to a WebSocket, or open over the WebSocket directly.
/// Each session connects namespaces after judging the CONNECT payload's credentials through the
/// <see cref="IAuthenticationPolicy"/>, echoes events, broadcasts <c>broadcast</c>, acknowledges
/// events that ask, and carries binary attachments. <c>socketios</c> is <c>socketio</c> inside the
/// TLS the listener seam provides. HTTP/1.1 framing, the head limit and timeouts are
/// <see cref="Http1ConnectionServer"/>'s.
/// </remarks>
public sealed class SocketIoProtocolServer : IConnectionProtocolServer
{
    /// <summary>
    /// The plain scheme.
    /// </summary>
    public const string PlainScheme = "socketio";

    /// <summary>
    /// The scheme served inside TLS.
    /// </summary>
    public const string TlsScheme = "socketios";

    private readonly SocketIoListener listener;

    /// <summary>
    /// Creates the server.
    /// </summary>
    /// <param name="namespaces">The namespaces served, from the listen URL's path.</param>
    /// <param name="sessions">The listener's sessions, shared by every connection the server serves.</param>
    /// <param name="authenticationPolicy">The policy that judges each CONNECT's login.</param>
    public SocketIoProtocolServer(SocketIoServedNamespaces namespaces, SocketIoSessions sessions, IAuthenticationPolicy authenticationPolicy)
    {
        ArgumentNullException.ThrowIfNull(namespaces);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(authenticationPolicy);

        listener = new SocketIoListener(namespaces, sessions, authenticationPolicy);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes { get; } = [PlainScheme, TlsScheme];

    /// <inheritdoc/>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        return new Http1ConnectionServer(new SocketIoRequestAnswerer(listener, context, connection.TlsSession), context).ServeAsync(connection);
    }
}
