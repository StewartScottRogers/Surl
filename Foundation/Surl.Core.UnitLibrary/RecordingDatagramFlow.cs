using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// Wraps the datagram flow a protocol server is handed and reports every datagram it
/// receives or sends to the exchange log, so a server cannot forget to log one (ADR-0004,
/// section 5). The flow's first datagram is logged by the engine when the exchange opens.
/// </summary>
/// <param name="flow">The accepted flow.</param>
/// <param name="log">The exchange's log.</param>
internal sealed class RecordingDatagramFlow(IDatagramFlow flow, IExchangeLog log) : IDatagramFlow
{
    /// <inheritdoc/>
    public EndPoint LocalEndPoint => flow.LocalEndPoint;

    /// <inheritdoc/>
    public EndPoint RemoteEndPoint => flow.RemoteEndPoint;

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> FirstDatagram => flow.FirstDatagram;

    /// <summary>
    /// Receives the next datagram from the flow and reports it as received.
    /// </summary>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>One whole datagram.</returns>
    public async ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken)
    {
        var datagram = await flow.ReceiveAsync(cancellationToken);

        log.BytesReceived(datagram.Span);

        return datagram;
    }

    /// <summary>
    /// Sends one datagram on the flow, then reports it as sent.
    /// </summary>
    /// <param name="datagram">The whole datagram.</param>
    /// <param name="cancellationToken">Cuts the send off.</param>
    /// <returns>A task that completes once the datagram has been sent.</returns>
    public async ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
    {
        await flow.SendAsync(datagram, cancellationToken);
        log.BytesSent(datagram.Span);
    }

    /// <inheritdoc/>
    public ValueTask MoveToNewLocalPortAsync(CancellationToken cancellationToken) =>
        flow.MoveToNewLocalPortAsync(cancellationToken);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => flow.DisposeAsync();
}
