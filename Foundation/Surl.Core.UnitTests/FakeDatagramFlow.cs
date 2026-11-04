using System.Net;
using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// An <see cref="IDatagramFlow"/> opened by the first datagram a test gives it, that receives
/// the datagrams a test queues, records every datagram sent, and moves to local port 40001
/// when asked for a new one.
/// </summary>
internal sealed class FakeDatagramFlow(
    byte[] firstDatagram, EndPoint? localEndPoint = null, EndPoint? remoteEndPoint = null) : IDatagramFlow
{
    private readonly Channel<byte[]> arrivals = Channel.CreateUnbounded<byte[]>();
    private readonly List<byte[]> sent = [];
    private readonly TaskCompletionSource disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public EndPoint LocalEndPoint { get; private set; } = localEndPoint ?? new IPEndPoint(IPAddress.Loopback, 40000);

    public EndPoint RemoteEndPoint { get; } = remoteEndPoint ?? new IPEndPoint(IPAddress.Loopback, 51234);

    public ReadOnlyMemory<byte> FirstDatagram { get; } = firstDatagram;

    public IReadOnlyList<byte[]> Sent => [.. sent];

    public bool Disposed => disposed.Task.IsCompleted;

    public Task WhenDisposed => disposed.Task;

    public void Arrive(byte[] datagram) => arrivals.Writer.TryWrite(datagram);

    public async ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken) =>
        await arrivals.Reader.ReadAsync(cancellationToken);

    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
    {
        sent.Add(datagram.ToArray());

        return ValueTask.CompletedTask;
    }

    public ValueTask MoveToNewLocalPortAsync(CancellationToken cancellationToken)
    {
        LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 40001);

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        disposed.TrySetResult();

        return ValueTask.CompletedTask;
    }
}
