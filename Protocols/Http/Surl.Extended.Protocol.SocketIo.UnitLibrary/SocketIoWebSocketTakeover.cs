using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// Owns a session's TCP connection from its <c>101</c> on: serves the <see cref="SocketIoSession"/>,
/// then forgets its <c>sid</c> (ADR-0120, decision 2; ADR-0084, decision 1).
/// </summary>
/// <param name="listener">What the listener's sessions share.</param>
/// <param name="sessionId">The <c>sid</c> reserved for the session.</param>
/// <param name="context">The exchange.</param>
internal sealed class SocketIoWebSocketTakeover(
    SocketIoListener listener,
    string sessionId,
    ExchangeContext context) : IHttpConnectionTakeover
{
    /// <inheritdoc/>
    public async Task TakeOverAsync(IConnection connection, HttpConnectionReader reader, CancellationToken cancellationToken)
    {
        try
        {
            using var session = new SocketIoSession(sessionId, listener, connection, reader, context);
            listener.Sessions.Attach(sessionId, session);
            await session.RunAsync();
        }
        finally
        {
            listener.Sessions.Remove(sessionId);
        }
    }
}
