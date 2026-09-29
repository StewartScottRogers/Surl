using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// Reads every listen socket of one datagram listener and sorts what arrives by remote
/// endpoint (ADR-0004, sections 3 and 6): a datagram from an endpoint with an open flow on that
/// socket goes to the flow; one from any other endpoint opens a flow, which
/// <see cref="AcceptFlowAsync"/> hands out. The sockets are the caller's, so this runs without
/// a socket in the fast tests.
/// </summary>
/// <remarks>
/// <see cref="StopAsync"/> stops opening flows, but the listen sockets stay open until every
/// flow still sending from them has moved or been disposed, so flows already handed out are
/// unaffected. <see cref="AcceptFlowAsync"/> is meant for one caller at a time.
/// </remarks>
internal sealed class DatagramDemultiplexer
{
    /// <summary>
    /// How many opened flows wait for <see cref="AcceptFlowAsync"/>; a datagram that would open
    /// another is dropped, as a full socket buffer drops it.
    /// </summary>
    public const int PendingFlowCapacity = 64;

    private readonly IReadOnlyList<IDatagramSocket> listenSockets;
    private readonly Func<IPEndPoint, IDatagramSocket> bindSocket;
    private readonly Dictionary<(IDatagramSocket ListenSocket, EndPoint RemoteEndPoint), DemultiplexedDatagramFlow> openFlows = [];
    private readonly Channel<DemultiplexedDatagramFlow> pendingFlows = Channel.CreateBounded<DemultiplexedDatagramFlow>(
        new BoundedChannelOptions(PendingFlowCapacity) { SingleReader = true });
    private readonly CancellationTokenSource lifetime = new();
    private readonly Lock gate = new();
    private readonly Task receiving;
    private int listenPortHolders;
    private bool stopped;
    private bool closed;

    /// <summary>
    /// Starts reading every one of <paramref name="listenSockets"/>.
    /// </summary>
    /// <param name="listenSockets">The bound listen sockets, at least one; disposed once every flow has left them after <see cref="StopAsync"/>.</param>
    /// <param name="bindSocket">Binds a UDP socket to an endpoint, for a flow that moves to a new port.</param>
    public DatagramDemultiplexer(IReadOnlyList<IDatagramSocket> listenSockets, Func<IPEndPoint, IDatagramSocket> bindSocket)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(listenSockets.Count, 1, nameof(listenSockets));
        ArgumentNullException.ThrowIfNull(bindSocket);

        this.listenSockets = listenSockets;
        this.bindSocket = bindSocket;
        receiving = Task.WhenAll(listenSockets.Select(ReceiveFromListenSocketAsync));
    }

    /// <summary>
    /// Waits for the first datagram from a remote endpoint that has no open flow.
    /// </summary>
    /// <param name="cancellationToken">Cuts the wait off with <see cref="OperationCanceledException"/>.</param>
    /// <returns>The flow that datagram opened.</returns>
    /// <exception cref="ObjectDisposedException"><see cref="StopAsync"/> was called.</exception>
    /// <exception cref="IOException">A listen socket failed; no flow opens on it again.</exception>
    public async ValueTask<IDatagramFlow> AcceptFlowAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await pendingFlows.Reader.ReadAsync(cancellationToken);
        }
        catch (ChannelClosedException exception)
        {
            throw exception.InnerException as IOException ?? (Exception)new ObjectDisposedException(GetType().FullName);
        }
    }

    /// <summary>
    /// Stops opening flows and disposes every flow opened but not yet handed out. The listen
    /// sockets are disposed now if no flow still sends from them, or else by the last such flow
    /// to move or be disposed. Calling it twice is harmless.
    /// </summary>
    /// <returns>A task that completes once flows have stopped opening, and, if the listen sockets were disposed here, once reading them has ended.</returns>
    public async ValueTask StopAsync()
    {
        lock (gate)
        {
            if (stopped)
            {
                return;
            }

            stopped = true;
            pendingFlows.Writer.TryComplete();
        }

        while (pendingFlows.Reader.TryRead(out var unclaimed))
        {
            await unclaimed.DisposeAsync();
        }

        CloseIfUnused();

        bool socketsClosed;
        lock (gate)
        {
            socketsClosed = closed;
        }

        if (socketsClosed)
        {
            await receiving;
        }
    }

    private async Task ReceiveFromListenSocketAsync(IDatagramSocket listenSocket)
    {
        await Task.Yield();

        try
        {
            while (true)
            {
                Route(listenSocket, await DatagramReceiving.ReceiveIgnoringConnectionResetsAsync(listenSocket, lifetime.Token));
            }
        }
        catch (Exception) when (lifetime.IsCancellationRequested)
        {
            // The sockets were closed under the receive: reading has ended.
        }
        catch (SocketException exception)
        {
            pendingFlows.Writer.TryComplete(new IOException(exception.Message, exception));
        }
    }

    private void Route(IDatagramSocket listenSocket, ReceivedDatagram datagram)
    {
        var key = (listenSocket, datagram.RemoteEndPoint);

        lock (gate)
        {
            if (openFlows.TryGetValue(key, out var flow))
            {
                flow.DeliverFromListenPort(datagram.Payload);
            }
            else if (!stopped)
            {
                OpenFlow(key, datagram.Payload);
            }
        }
    }

    // Called under the gate.
    private void OpenFlow((IDatagramSocket ListenSocket, EndPoint RemoteEndPoint) key, ReadOnlyMemory<byte> firstDatagram)
    {
        var flow = new DemultiplexedDatagramFlow(
            key.ListenSocket, key.RemoteEndPoint, firstDatagram, bindSocket, ReleaseListenPort, () => ForgetFlow(key));

        if (pendingFlows.Writer.TryWrite(flow))
        {
            openFlows.Add(key, flow);
            listenPortHolders++;
        }
    }

    private void ForgetFlow((IDatagramSocket ListenSocket, EndPoint RemoteEndPoint) key)
    {
        lock (gate)
        {
            openFlows.Remove(key);
        }
    }

    private void ReleaseListenPort()
    {
        lock (gate)
        {
            listenPortHolders--;
        }

        CloseIfUnused();
    }

    private void CloseIfUnused()
    {
        lock (gate)
        {
            if (!stopped || closed || listenPortHolders > 0)
            {
                return;
            }

            closed = true;
        }

        lifetime.Cancel();
        ListenerBinder.ReleaseAll(listenSockets, socket => socket.Dispose());
    }
}
