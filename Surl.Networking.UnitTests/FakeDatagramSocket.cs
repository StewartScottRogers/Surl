using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

namespace Surl.Networking;

/// <summary>
/// An <see cref="IDatagramSocket"/> the test feeds by hand: <see cref="Arrive"/> queues a
/// datagram for the next receive, <see cref="FailNextReceive"/> a socket error, and every send
/// is recorded.
/// </summary>
internal sealed class FakeDatagramSocket(EndPoint localEndPoint) : IDatagramSocket
{
    private readonly Channel<Func<ReceivedDatagram>> inbound = Channel.CreateUnbounded<Func<ReceivedDatagram>>();
    private readonly Lock gate = new();
    private readonly List<(string Text, EndPoint RemoteEndPoint)> sent = [];
    private TaskCompletionSource idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int disposeCount;

    public EndPoint LocalEndPoint { get; } = localEndPoint;

    public SocketError? SendFailure { get; set; }

    public int DisposeCount => Volatile.Read(ref disposeCount);

    public IReadOnlyList<(string Text, EndPoint RemoteEndPoint)> Sent
    {
        get
        {
            lock (gate)
            {
                return [.. sent];
            }
        }
    }

    public void Arrive(string text, EndPoint from) => Enqueue(() => new ReceivedDatagram(Encoding.ASCII.GetBytes(text), from));

    public void FailNextReceive(SocketError error) => Enqueue(() => throw new SocketException((int)error));

    /// <summary>
    /// Completes once the reader has taken everything queued and is waiting for more, so every
    /// datagram queued before the call has been handled.
    /// </summary>
    public Task WaitUntilReaderIsIdleAsync()
    {
        lock (gate)
        {
            return idle.Task;
        }
    }

    public async ValueTask<ReceivedDatagram> ReceiveAsync(CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (inbound.Reader.Count == 0)
            {
                idle.TrySetResult();
            }
        }

        try
        {
            return (await inbound.Reader.ReadAsync(cancellationToken))();
        }
        catch (ChannelClosedException)
        {
            throw new ObjectDisposedException(nameof(FakeDatagramSocket));
        }
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint remoteEndPoint, CancellationToken cancellationToken)
    {
        if (SendFailure is { } failure)
        {
            throw new SocketException((int)failure);
        }

        lock (gate)
        {
            sent.Add((Encoding.ASCII.GetString(datagram.Span), remoteEndPoint));
        }

        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        Interlocked.Increment(ref disposeCount);
        inbound.Writer.TryComplete();
    }

    private void Enqueue(Func<ReceivedDatagram> next)
    {
        lock (gate)
        {
            if (idle.Task.IsCompleted)
            {
                idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            inbound.Writer.TryWrite(next);
        }
    }
}

/// <summary>
/// Binds <see cref="FakeDatagramSocket"/>s: port 0 gets the next port from 40000 up, any other
/// port is kept; <see cref="FailNextBind"/> makes the next bind throw.
/// </summary>
internal sealed class FakeDatagramSocketBinder
{
    private readonly Lock gate = new();
    private readonly List<FakeDatagramSocket> bound = [];
    private int nextEphemeralPort = 40000;

    public SocketError? FailNextBind { get; set; }

    public IPAddress? UnavailableAddress { get; set; }

    public IReadOnlyList<FakeDatagramSocket> Bound
    {
        get
        {
            lock (gate)
            {
                return [.. bound];
            }
        }
    }

    public IDatagramSocket Bind(IPEndPoint endPoint)
    {
        lock (gate)
        {
            if (FailNextBind is { } failure)
            {
                FailNextBind = null;
                throw new SocketException((int)failure);
            }

            if (endPoint.Address.Equals(UnavailableAddress))
            {
                throw new SocketException((int)SocketError.AddressNotAvailable);
            }

            var port = endPoint.Port == 0 ? nextEphemeralPort++ : endPoint.Port;
            var socket = new FakeDatagramSocket(new IPEndPoint(endPoint.Address, port));
            bound.Add(socket);

            return socket;
        }
    }
}
