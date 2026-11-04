using System.Net;
using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Kerberos.TestKdc;

/// <summary>
/// A hand-written <see cref="IDatagramFlow" /> whose first datagram is the client's request: it
/// records every datagram sent, can fail its send, and says when it is disposed.
/// </summary>
internal sealed class FakeDatagramFlow(byte[] firstDatagram, Exception? sendFailure = null) : IDatagramFlow
{
    private readonly TaskCompletionSource disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 88);

    public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

    public ReadOnlyMemory<byte> FirstDatagram { get; } = firstDatagram;

    public List<byte[]> Sent { get; } = [];

    public Task WhenDisposed => disposed.Task;

    public ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken) => throw new NotSupportedException("The KDC answers one datagram per flow.");

    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (sendFailure is not null)
        {
            return ValueTask.FromException(sendFailure);
        }

        Sent.Add(datagram.ToArray());
        return ValueTask.CompletedTask;
    }

    public ValueTask MoveToNewLocalPortAsync(CancellationToken cancellationToken) => throw new NotSupportedException("The KDC answers from the listen port.");

    public ValueTask DisposeAsync()
    {
        disposed.TrySetResult();
        return ValueTask.CompletedTask;
    }
}

/// <summary>An <see cref="IConnection" /> that passes everything to an <see cref="InMemoryConnection" /> and says when it is first read and when it is disposed.</summary>
internal sealed class DisposalWatchingConnection(InMemoryConnection inner) : IConnection
{
    private readonly TaskCompletionSource disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public InMemoryConnection Inner => inner;

    public Task WhenDisposed => disposed.Task;

    public Task WhenReadStarted => readStarted.Task;

    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

    public TlsSession? TlsSession => inner.TlsSession;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        readStarted.TrySetResult();
        return inner.ReadAsync(buffer, cancellationToken);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) => inner.WriteAsync(bytes, cancellationToken);

    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => inner.CompleteWritesAsync(cancellationToken);

    public void Abort() => inner.Abort();

    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => inner.UpgradeToTlsAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync();
        disposed.TrySetResult();
    }
}

/// <summary>An <see cref="IDatagramListener" /> that opens the flows a test hands it, in order.</summary>
internal sealed class FakeDatagramListener : IDatagramListener
{
    private readonly Channel<IDatagramFlow> arrivals = Channel.CreateUnbounded<IDatagramFlow>();

    public ListenUrl ListenUrl { get; } = new ListenUrl("kerberos", "127.0.0.1", 88).WithBoundPort(88);

    public IReadOnlyList<EndPoint> BoundEndPoints { get; } = [new IPEndPoint(IPAddress.Loopback, 88)];

    public void Open(IDatagramFlow flow) => arrivals.Writer.TryWrite(flow);

    public async ValueTask<IDatagramFlow> AcceptFlowAsync(CancellationToken cancellationToken) => await arrivals.Reader.ReadAsync(cancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>An <see cref="IConnectionListener" /> that accepts the connections a test hands it, in order.</summary>
internal sealed class FakeConnectionListener : IConnectionListener
{
    private readonly Channel<IConnection> arrivals = Channel.CreateUnbounded<IConnection>();

    public ListenUrl ListenUrl { get; } = new ListenUrl("kerberos", "127.0.0.1", 88).WithBoundPort(88);

    public IReadOnlyList<EndPoint> BoundEndPoints { get; } = [new IPEndPoint(IPAddress.Loopback, 88)];

    public void Connect(IConnection connection) => arrivals.Writer.TryWrite(connection);

    public async ValueTask<IConnection> AcceptAsync(CancellationToken cancellationToken) => await arrivals.Reader.ReadAsync(cancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
