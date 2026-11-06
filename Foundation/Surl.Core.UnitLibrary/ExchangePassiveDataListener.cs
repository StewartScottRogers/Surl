using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// Wraps a passive listener the engine's opener started, so the connection it accepts is handed
/// out through its exchange's <see cref="ExchangeDataConnections"/> (ADR-0052, decision 9).
/// </summary>
/// <param name="listener">The listener the opener started.</param>
/// <param name="owner">The exchange's data connections.</param>
internal sealed class ExchangePassiveDataListener(IPassiveDataListener listener, ExchangeDataConnections owner)
    : IPassiveDataListener
{
    /// <inheritdoc/>
    public IPEndPoint LocalEndPoint => listener.LocalEndPoint;

    /// <inheritdoc/>
    public async ValueTask<IConnection> AcceptAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var connection = await listener.AcceptAsync(timeout, cancellationToken);

        return owner.Opened(connection, "passive from");
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        owner.Forget(this);

        return listener.DisposeAsync();
    }
}
