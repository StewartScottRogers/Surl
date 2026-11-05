using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Tftp;

/// <summary>
/// A flow that waits <paramref name="delay"/> on <paramref name="timeProvider"/> before sending
/// each DATA packet, an upstream test case's <c>writedelay:</c>, and is otherwise the flow it wraps.
/// </summary>
/// <param name="flow">The flow wrapped.</param>
/// <param name="delay">The wait before each DATA packet.</param>
/// <param name="timeProvider">The clock the wait runs on.</param>
internal sealed class WriteDelayingDatagramFlow(IDatagramFlow flow, TimeSpan delay, TimeProvider timeProvider) : IDatagramFlow
{
    public EndPoint LocalEndPoint => flow.LocalEndPoint;

    public EndPoint RemoteEndPoint => flow.RemoteEndPoint;

    public ReadOnlyMemory<byte> FirstDatagram => flow.FirstDatagram;

    public ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken) => flow.ReceiveAsync(cancellationToken);

    public async ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
    {
        if (TftpPacket.ReadOpcode(datagram.Span) == TftpPacket.Data)
        {
            await Task.Delay(delay, timeProvider, cancellationToken);
        }

        await flow.SendAsync(datagram, cancellationToken);
    }

    public ValueTask MoveToNewLocalPortAsync(CancellationToken cancellationToken) => flow.MoveToNewLocalPortAsync(cancellationToken);

    public ValueTask DisposeAsync() => flow.DisposeAsync();
}
