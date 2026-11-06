using System.Net;
using System.Threading.Channels;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// An <see cref="IDatagramFlow"/> that hands a server its first datagram and then the client's
/// datagrams, one per receive, and records every datagram sent, so a datagram protocol server
/// such as HTTP/3 over QUIC is tested with no network (ADR-0082, decision 5). It opens nothing
/// and constructs no transport type.
/// </summary>
/// <remarks>
/// The client's datagrams are those given to the constructor, then any a test adds with
/// <see cref="DeliverFromClient"/> while the server runs, so a test can answer what the server
/// sent. Once none is waiting, a receive waits until a datagram is delivered, the receive is
/// cancelled or the flow is disposed. Sends and deliveries are safe to make from different
/// threads, as a QUIC server's receive loop and its timers do.
/// </remarks>
public sealed class InMemoryDatagramFlow : IDatagramFlow
{
    private readonly Channel<ReadOnlyMemory<byte>> clientDatagrams = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
    private readonly List<byte[]> sentDatagrams = [];
    private readonly EndPoint newLocalEndPoint;

    /// <summary>
    /// Creates a flow opened by <paramref name="firstDatagram"/>.
    /// </summary>
    /// <param name="firstDatagram">The datagram that opened the flow, such as a QUIC Initial packet.</param>
    /// <param name="clientDatagrams">The client's later datagrams, in order; none when <see langword="null"/>.</param>
    /// <param name="localEndPoint">The listen endpoint; 127.0.0.1:443 when <see langword="null"/>.</param>
    /// <param name="remoteEndPoint">The client's endpoint; 127.0.0.1:50000 when <see langword="null"/>.</param>
    /// <param name="newLocalEndPoint">
    /// The endpoint <see cref="MoveToNewLocalPortAsync"/> moves to; 127.0.0.1:50001 when <see langword="null"/>.
    /// </param>
    public InMemoryDatagramFlow(
        ReadOnlyMemory<byte> firstDatagram,
        IEnumerable<ReadOnlyMemory<byte>>? clientDatagrams = null,
        EndPoint? localEndPoint = null,
        EndPoint? remoteEndPoint = null,
        EndPoint? newLocalEndPoint = null)
    {
        FirstDatagram = firstDatagram;
        LocalEndPoint = localEndPoint ?? new IPEndPoint(IPAddress.Loopback, 443);
        RemoteEndPoint = remoteEndPoint ?? new IPEndPoint(IPAddress.Loopback, 50000);
        this.newLocalEndPoint = newLocalEndPoint ?? new IPEndPoint(IPAddress.Loopback, 50001);

        foreach (var datagram in clientDatagrams ?? [])
        {
            this.clientDatagrams.Writer.TryWrite(datagram);
        }
    }

    /// <inheritdoc/>
    public EndPoint LocalEndPoint { get; private set; }

    /// <inheritdoc/>
    public EndPoint RemoteEndPoint { get; }

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> FirstDatagram { get; }

    /// <summary>
    /// A copy of every datagram sent, in order.
    /// </summary>
    public IReadOnlyList<byte[]> SentDatagrams
    {
        get
        {
            lock (sentDatagrams)
            {
                return [.. sentDatagrams];
            }
        }
    }

    /// <summary>
    /// Whether <see cref="DisposeAsync"/> was called.
    /// </summary>
    public bool Disposed { get; private set; }

    /// <summary>
    /// Adds one datagram from the client, after those already waiting; a pending receive gets it.
    /// </summary>
    /// <param name="datagram">The whole datagram.</param>
    public void DeliverFromClient(ReadOnlyMemory<byte> datagram)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);

        clientDatagrams.Writer.TryWrite(datagram);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Throws <see cref="ObjectDisposedException"/> once the flow is disposed, including for a
    /// receive that was waiting when it was.
    /// </remarks>
    public async ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Disposed, this);

        try
        {
            return await clientDatagrams.Reader.ReadAsync(cancellationToken);
        }
        catch (ChannelClosedException)
        {
            throw new ObjectDisposedException(nameof(InMemoryDatagramFlow));
        }
    }

    /// <inheritdoc/>
    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Disposed, this);

        lock (sentDatagrams)
        {
            sentDatagrams.Add(datagram.ToArray());
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Stands in for binding a fresh port: makes the endpoint given to the constructor the
    /// flow's <see cref="LocalEndPoint"/>.
    /// </summary>
    /// <param name="cancellationToken">Cuts the move off.</param>
    /// <returns>A completed task.</returns>
    public ValueTask MoveToNewLocalPortAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Disposed, this);

        LocalEndPoint = newLocalEndPoint;

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Closes the flow: a waiting receive throws <see cref="ObjectDisposedException"/>. Calling
    /// it twice is harmless.
    /// </summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        Disposed = true;
        clientDatagrams.Writer.TryComplete();

        return ValueTask.CompletedTask;
    }
}
