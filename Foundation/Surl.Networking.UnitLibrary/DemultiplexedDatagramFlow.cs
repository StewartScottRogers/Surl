using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// An <see cref="IDatagramFlow"/> that a <see cref="DatagramDemultiplexer"/> opened for one
/// remote endpoint (ADR-0004, section 3). Until it moves, it shares the listen socket: the
/// demultiplexer delivers its peer's datagrams to it, and it sends from the listen port. After
/// <see cref="MoveToNewLocalPortAsync"/> it owns a socket of its own and reads it directly,
/// dropping datagrams from any other endpoint. The sockets are the caller's, so this runs
/// without a socket in the fast tests.
/// </summary>
/// <remarks>
/// Not safe for concurrent calls. Transport failures are <see cref="IOException"/>, as on a
/// connection (ADR-0004, section 2).
/// </remarks>
internal sealed class DemultiplexedDatagramFlow : IDatagramFlow
{
    /// <summary>
    /// How many datagrams from the peer wait on the listen port for <see cref="ReceiveAsync"/>;
    /// later ones are dropped, as a full socket buffer drops them.
    /// </summary>
    public const int ListenPortInboxCapacity = 64;

    private readonly IDatagramSocket listenSocket;
    private readonly Func<IPEndPoint, IDatagramSocket> bindSocket;
    private readonly Action releaseListenPort;
    private readonly Action close;
    private readonly Channel<ReadOnlyMemory<byte>> listenPortInbox = Channel.CreateBounded<ReadOnlyMemory<byte>>(
        new BoundedChannelOptions(ListenPortInboxCapacity) { SingleReader = true });
    private IDatagramSocket? ownSocket;
    private bool disposed;

    /// <summary>
    /// Creates a flow opened by <paramref name="firstDatagram"/> on <paramref name="listenSocket"/>.
    /// </summary>
    /// <param name="listenSocket">The listen socket the first datagram arrived on; the flow never disposes it.</param>
    /// <param name="remoteEndPoint">The endpoint that sent the first datagram.</param>
    /// <param name="firstDatagram">The datagram that opened the flow.</param>
    /// <param name="bindSocket">Binds a UDP socket to an endpoint, for <see cref="MoveToNewLocalPortAsync"/>.</param>
    /// <param name="releaseListenPort">Called once, when the flow stops using the listen socket: on its first move, or on dispose.</param>
    /// <param name="close">Called once, on dispose.</param>
    public DemultiplexedDatagramFlow(
        IDatagramSocket listenSocket,
        EndPoint remoteEndPoint,
        ReadOnlyMemory<byte> firstDatagram,
        Func<IPEndPoint, IDatagramSocket> bindSocket,
        Action releaseListenPort,
        Action close)
    {
        this.listenSocket = listenSocket;
        RemoteEndPoint = remoteEndPoint;
        FirstDatagram = firstDatagram;
        this.bindSocket = bindSocket;
        this.releaseListenPort = releaseListenPort;
        this.close = close;
    }

    /// <inheritdoc/>
    public EndPoint LocalEndPoint => (ownSocket ?? listenSocket).LocalEndPoint;

    /// <inheritdoc/>
    public EndPoint RemoteEndPoint { get; }

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> FirstDatagram { get; }

    /// <summary>
    /// Hands the flow a datagram its peer sent to the listen port. Once the flow has moved or
    /// been disposed, or while its inbox is full, the datagram is dropped.
    /// </summary>
    /// <param name="datagram">The whole datagram.</param>
    public void DeliverFromListenPort(ReadOnlyMemory<byte> datagram) => listenPortInbox.Writer.TryWrite(datagram);

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The flow was disposed.</exception>
    /// <exception cref="IOException">The receive failed.</exception>
    public async ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken)
    {
        ThrowIfUnusable(cancellationToken);

        return ownSocket is null
            ? await ReceiveFromListenPortAsync(cancellationToken)
            : await ReceiveFromOwnPortAsync(ownSocket, cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The flow was disposed.</exception>
    /// <exception cref="IOException">The send failed.</exception>
    public async ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
    {
        ThrowIfUnusable(cancellationToken);

        try
        {
            await (ownSocket ?? listenSocket).SendAsync(datagram, RemoteEndPoint, cancellationToken);
        }
        catch (SocketException exception)
        {
            throw new IOException(exception.Message, exception);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Datagrams the peer sends to the listen port from then on are dropped; they open no new
    /// flow while this one is open. Moving again releases the port the previous move bound.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The flow was disposed.</exception>
    /// <exception cref="IOException">The bind failed; the flow stays where it was.</exception>
    public ValueTask MoveToNewLocalPortAsync(CancellationToken cancellationToken)
    {
        ThrowIfUnusable(cancellationToken);

        var newSocket = BindNewPort(((IPEndPoint)LocalEndPoint).Address);
        var previousSocket = ownSocket;
        ownSocket = newSocket;

        if (previousSocket is null)
        {
            listenPortInbox.Writer.TryComplete();
            releaseListenPort();
        }
        else
        {
            previousSocket.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Releases the port the flow bound, if it moved, and stops the demultiplexer delivering to
    /// it. Calling it twice is harmless.
    /// </summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        if (!disposed)
        {
            disposed = true;
            listenPortInbox.Writer.TryComplete();
            ReleaseWhatTheFlowHolds();
            close();
        }

        return ValueTask.CompletedTask;
    }

    private void ReleaseWhatTheFlowHolds()
    {
        if (ownSocket is null)
        {
            releaseListenPort();
        }
        else
        {
            ownSocket.Dispose();
        }
    }

    private void ThrowIfUnusable(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private IDatagramSocket BindNewPort(IPAddress address)
    {
        try
        {
            return bindSocket(new IPEndPoint(address, 0));
        }
        catch (SocketException exception)
        {
            throw new IOException(exception.Message, exception);
        }
    }

    private async ValueTask<ReadOnlyMemory<byte>> ReceiveFromListenPortAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await listenPortInbox.Reader.ReadAsync(cancellationToken);
        }
        catch (ChannelClosedException)
        {
            // Only a dispose during the wait closes the inbox under a waiting receive.
            throw new ObjectDisposedException(GetType().FullName);
        }
    }

    private async ValueTask<ReadOnlyMemory<byte>> ReceiveFromOwnPortAsync(IDatagramSocket socket, CancellationToken cancellationToken)
    {
        while (true)
        {
            var received = await ReceiveOrThrowIOExceptionAsync(socket, cancellationToken);

            if (received.RemoteEndPoint.Equals(RemoteEndPoint))
            {
                return received.Payload;
            }

            // Another endpoint's datagram never reaches the flow (ADR-0004, section 3); it is dropped.
        }
    }

    private static async ValueTask<ReceivedDatagram> ReceiveOrThrowIOExceptionAsync(
        IDatagramSocket socket, CancellationToken cancellationToken)
    {
        try
        {
            return await DatagramReceiving.ReceiveIgnoringConnectionResetsAsync(socket, cancellationToken);
        }
        catch (SocketException exception)
        {
            throw new IOException(exception.Message, exception);
        }
    }
}
