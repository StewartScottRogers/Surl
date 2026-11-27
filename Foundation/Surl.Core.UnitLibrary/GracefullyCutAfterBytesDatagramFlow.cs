using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// Wraps a datagram flow of an <see cref="IGracefulCutDatagramProtocolServer"/> under a
/// <see cref="FaultKind.Drop"/> rule with <see cref="FaultRule.AfterBytes"/> and
/// <see cref="FaultClose.Graceful"/> (ADR-0188, amendment 5): the server's datagrams are counted and
/// discarded as <see cref="SilencedAfterBytesDatagramFlow"/> does it, and the server's closing
/// datagrams are sent whatever the count.
/// </summary>
/// <param name="flow">The flow to wrap.</param>
/// <param name="afterBytes">How many bytes the server may send before the flow is cut.</param>
/// <param name="onCut">Called once, when the flow is cut.</param>
internal sealed class GracefullyCutAfterBytesDatagramFlow(IDatagramFlow flow, long afterBytes, Action onCut)
    : IGracefullyCutDatagramFlow
{
    private readonly SilencedAfterBytesDatagramFlow counted = new(flow, afterBytes, onCut);

    /// <inheritdoc/>
    public bool IsCut => counted.IsSilenced;

    /// <inheritdoc/>
    public EndPoint LocalEndPoint => counted.LocalEndPoint;

    /// <inheritdoc/>
    public EndPoint RemoteEndPoint => counted.RemoteEndPoint;

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> FirstDatagram => counted.FirstDatagram;

    /// <inheritdoc/>
    public ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken) =>
        counted.ReceiveAsync(cancellationToken);

    /// <inheritdoc/>
    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken) =>
        counted.SendAsync(datagram, cancellationToken);

    /// <inheritdoc/>
    public ValueTask SendClosingDatagramAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken) =>
        flow.SendAsync(datagram, cancellationToken);

    /// <inheritdoc/>
    public ValueTask MoveToNewLocalPortAsync(CancellationToken cancellationToken) =>
        counted.MoveToNewLocalPortAsync(cancellationToken);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => counted.DisposeAsync();
}
