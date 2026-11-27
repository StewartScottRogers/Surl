using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Quic;

/// <summary>
/// An <see cref="IGracefullyCutDatagramFlow"/> over an <see cref="InMemoryDatagramFlow"/>: it sends the
/// server's datagrams while they stay within <see cref="BytesLeft"/>, is cut by the first that would pass
/// it, and records which datagrams were sent as closing ones.
/// </summary>
internal sealed class UnitTestGracefullyCutDatagramFlow(InMemoryDatagramFlow inner, long bytesLeft) : IGracefullyCutDatagramFlow
{
    private readonly List<byte[]> closing = [];

    /// <summary>How many more bytes the server may send before the flow is cut.</summary>
    public long BytesLeft { get; set; } = bytesLeft;

    public bool IsCut { get; private set; }

    /// <summary>A copy of every datagram sent as a closing one, in order.</summary>
    public IReadOnlyList<byte[]> ClosingDatagrams
    {
        get
        {
            lock (closing)
            {
                return [.. closing];
            }
        }
    }

    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public ReadOnlyMemory<byte> FirstDatagram => inner.FirstDatagram;

    public ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken) => inner.ReceiveAsync(cancellationToken);

    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
    {
        IsCut = IsCut || datagram.Length > BytesLeft;
        if (IsCut)
        {
            return ValueTask.CompletedTask;
        }

        BytesLeft -= datagram.Length;
        return inner.SendAsync(datagram, cancellationToken);
    }

    public ValueTask SendClosingDatagramAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
    {
        lock (closing)
        {
            closing.Add(datagram.ToArray());
        }

        return inner.SendAsync(datagram, cancellationToken);
    }

    public ValueTask MoveToNewLocalPortAsync(CancellationToken cancellationToken) => inner.MoveToNewLocalPortAsync(cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
