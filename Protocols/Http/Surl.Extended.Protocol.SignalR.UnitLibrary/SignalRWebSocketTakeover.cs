using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// Owns a hub connection's TCP connection from its <c>101</c> on: serves the
/// <see cref="SignalRHubSession"/>, then forgets the hub connection so its <c>id</c> is
/// <c>404</c> (ADR-0119, decision 2; ADR-0084, decision 1).
/// </summary>
/// <param name="connections">The hub's connections.</param>
/// <param name="hubConnection">The connection the upgrade took up.</param>
/// <param name="context">The exchange.</param>
internal sealed class SignalRWebSocketTakeover(
    SignalRHubConnections connections,
    SignalRNegotiatedConnection hubConnection,
    ExchangeContext context) : IHttpConnectionTakeover
{
    /// <inheritdoc/>
    public async Task TakeOverAsync(IConnection connection, HttpConnectionReader reader, CancellationToken cancellationToken)
    {
        try
        {
            using var session = new SignalRHubSession(connection, reader, context);
            await session.RunAsync();
        }
        finally
        {
            connections.End(hubConnection);
        }
    }
}
