using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// Wraps a datagram flow under a <see cref="FaultKind.Drop"/> rule with
/// <see cref="FaultRule.AfterBytes"/> (ADR-0124, decision 4): the server's datagrams are sent
/// whole while the bytes sent stay within the count; the first that would pass it, and every
/// datagram after it, is discarded, so the flow stops answering. Datagrams still arrive.
/// </summary>
/// <param name="flow">The flow to wrap.</param>
/// <param name="afterBytes">How many bytes the server may send before the flow stops answering.</param>
/// <param name="onSilenced">Called once, when the flow stops answering.</param>
internal sealed class SilencedAfterBytesDatagramFlow(IDatagramFlow flow, long afterBytes, Action onSilenced) : IDatagramFlow
{
    private long sentByteCount;
    private bool silenced;

    /// <inheritdoc/>
    public EndPoint LocalEndPoint => flow.LocalEndPoint;

    /// <inheritdoc/>
    public EndPoint RemoteEndPoint => flow.RemoteEndPoint;

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> FirstDatagram => flow.FirstDatagram;

    /// <inheritdoc/>
    public ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken) =>
        flow.ReceiveAsync(cancellationToken);

    /// <summary>
    /// Sends the datagram while the bytes sent stay within the count; otherwise discards it.
    /// </summary>
    /// <param name="datagram">The datagram.</param>
    /// <param name="cancellationToken">Cuts the send off.</param>
    /// <returns>A task that completes once the datagram was sent or discarded.</returns>
    public async ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
    {
        if (!silenced && sentByteCount + datagram.Length > afterBytes)
        {
            silenced = true;
            onSilenced();
        }

        if (silenced)
        {
            return;
        }

        await flow.SendAsync(datagram, cancellationToken);
        sentByteCount += datagram.Length;
    }

    /// <inheritdoc/>
    public ValueTask MoveToNewLocalPortAsync(CancellationToken cancellationToken) =>
        flow.MoveToNewLocalPortAsync(cancellationToken);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => flow.DisposeAsync();
}
