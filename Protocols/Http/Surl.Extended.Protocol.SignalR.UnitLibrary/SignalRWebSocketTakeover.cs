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
/// <param name="hubPath">The hub's endpoint base (<c>/chat</c>), as the fault plan names it.</param>
/// <param name="context">The exchange.</param>
internal sealed class SignalRWebSocketTakeover(
    SignalRHubConnections connections,
    SignalRNegotiatedConnection hubConnection,
    string hubPath,
    ExchangeContext context) : IHttpConnectionTakeover
{
    /// <inheritdoc/>
    public async Task TakeOverAsync(IConnection connection, HttpConnectionReader reader, CancellationToken cancellationToken)
    {
        var isSuspended = false;
        try
        {
            using var session = new SignalRHubSession(
                new SignalRWebSocketTransport(connection, reader, context),
                context,
                new SignalRInvocationFaults(context, hubPath, hubConnection.AccountName),
                connections,
                hubConnection.Reconnect);
            var end = await session.RunAsync();

            // A stateful-reconnect connection whose WebSocket was lost, with no Close from either side,
            // waits for a new one; any other end forgets it.
            isSuspended = hubConnection.Reconnect is not null && end is { HubCloseMessage: null, CloseCode: null };
        }
        finally
        {
            if (isSuspended)
            {
                connections.Suspend(hubConnection);
            }
            else
            {
                connections.End(hubConnection);
            }
        }
    }
}
