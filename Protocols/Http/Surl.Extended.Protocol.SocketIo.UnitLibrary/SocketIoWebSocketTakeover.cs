using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// Owns a WebSocket's TCP connection from its <c>101</c> on (ADR-0120, decision 2; ADR-0084, decision
/// 1): for a new session, serves it over the WebSocket and then forgets its <c>sid</c>; for a polling
/// session's upgrade, probes the WebSocket and carries the session once it moves.
/// </summary>
/// <param name="listener">What the listener's sessions share.</param>
/// <param name="sessionId">The <c>sid</c> reserved for a new session, or the polling session's.</param>
/// <param name="pollingSession">The polling session being upgraded, or <see langword="null"/> for a new session.</param>
/// <param name="context">The exchange.</param>
internal sealed class SocketIoWebSocketTakeover(
    SocketIoListener listener,
    string sessionId,
    SocketIoSession? pollingSession,
    ExchangeContext context) : IHttpConnectionTakeover
{
    /// <inheritdoc/>
    public async Task TakeOverAsync(IConnection connection, HttpConnectionReader reader, CancellationToken cancellationToken)
    {
        if (pollingSession is not null)
        {
            await new SocketIoWebSocketTransport(pollingSession, connection, reader, context).ProbeThenServeAsync();

            return;
        }

        try
        {
            var session = new SocketIoSession(sessionId, listener, context, connection.TlsSession, SocketIoTransportState.WebSocket);
            listener.Sessions.Attach(sessionId, session);
            await new SocketIoWebSocketTransport(session, connection, reader, context).ServeAsync();
        }
        finally
        {
            listener.Sessions.Remove(sessionId);
        }
    }
}
