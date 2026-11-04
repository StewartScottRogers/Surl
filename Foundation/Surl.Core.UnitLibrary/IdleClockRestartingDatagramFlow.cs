using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// Wraps the datagram flow a protocol server is handed and restarts the exchange's idle
/// clock each time a datagram arrives or a send completes, so the exchange is idle only while
/// no datagram moves (ADR-0006, section 1).
/// </summary>
/// <param name="flow">The flow to wrap.</param>
/// <param name="deadlines">The exchange's clocks.</param>
internal sealed class IdleClockRestartingDatagramFlow(IDatagramFlow flow, ExchangeDeadlines deadlines) : IDatagramFlow
{
    /// <inheritdoc/>
    public EndPoint LocalEndPoint => flow.LocalEndPoint;

    /// <inheritdoc/>
    public EndPoint RemoteEndPoint => flow.RemoteEndPoint;

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> FirstDatagram => flow.FirstDatagram;

    /// <summary>
    /// Receives the next datagram from the flow, then restarts the idle clock.
    /// </summary>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>One whole datagram.</returns>
    public async ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken)
    {
        var datagram = await flow.ReceiveAsync(cancellationToken);

        deadlines.RestartIdleClock();

        return datagram;
    }

    /// <summary>
    /// Sends one datagram on the flow, then restarts the idle clock.
    /// </summary>
    /// <param name="datagram">The whole datagram.</param>
    /// <param name="cancellationToken">Cuts the send off.</param>
    /// <returns>A task that completes once the datagram has been sent.</returns>
    public async ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
    {
        await flow.SendAsync(datagram, cancellationToken);
        deadlines.RestartIdleClock();
    }

    /// <inheritdoc/>
    public ValueTask MoveToNewLocalPortAsync(CancellationToken cancellationToken) =>
        flow.MoveToNewLocalPortAsync(cancellationToken);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => flow.DisposeAsync();
}
