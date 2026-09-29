using System.Net;
using System.Net.Sockets;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

[TestClass]
public sealed class UdpDatagramListenerFastTests
{
    private static readonly IPEndPoint PeerA = new(IPAddress.Loopback, 50001);
    private static readonly IPEndPoint PeerB = new(IPAddress.Loopback, 50002);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task StartAsync_IPLiteralPortZero_ReportsTheBoundPortAndEndPoint()
    {
        var binder = new FakeDatagramSocketBinder();
        var listenUrl = new ListenUrl("tftp", "127.0.0.1", 0);

        await using var listener = await StartAsync(binder, listenUrl);

        Assert.AreEqual(listenUrl.WithBoundPort(40000), listener.ListenUrl);
        CollectionAssert.AreEqual(new EndPoint[] { new IPEndPoint(IPAddress.Loopback, 40000) }, listener.BoundEndPoints.ToArray());
    }

    [TestMethod]
    public async Task StartAsync_HostName_BindsEveryResolvedAddressOnOnePort()
    {
        var binder = new FakeDatagramSocketBinder();
        var listenUrl = new ListenUrl("tftp", "localhost", 0);

        await using var listener = await UdpDatagramListener.StartAsync(
            listenUrl,
            (_, _) => Task.FromResult(new[] { IPAddress.Loopback, IPAddress.IPv6Loopback }),
            binder.Bind,
            TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            new EndPoint[] { new IPEndPoint(IPAddress.Loopback, 40000), new IPEndPoint(IPAddress.IPv6Loopback, 40000) },
            listener.BoundEndPoints.ToArray());
    }

    [TestMethod]
    public async Task StartAsync_SecondAddressUnavailable_ReleasesTheFirstAndThrowsListenerBindException()
    {
        var binder = new FakeDatagramSocketBinder { UnavailableAddress = IPAddress.IPv6Loopback };

        var exception = await Assert.ThrowsExactlyAsync<ListenerBindException>(
            () => UdpDatagramListener.StartAsync(
                new ListenUrl("tftp", "localhost", 0),
                (_, _) => Task.FromResult(new[] { IPAddress.Loopback, IPAddress.IPv6Loopback }),
                binder.Bind,
                TestContext.CancellationToken).AsTask());

        Assert.AreEqual(ListenerBindFailure.AddressNotAvailable, exception.Failure);
        Assert.AreEqual(1, binder.Bound.Single().DisposeCount);
    }

    [TestMethod]
    public async Task StartAsync_BindFails_ThrowsListenerBindException()
    {
        var binder = new FakeDatagramSocketBinder { FailNextBind = SocketError.AddressAlreadyInUse };

        var exception = await Assert.ThrowsExactlyAsync<ListenerBindException>(
            () => StartAsync(binder, new ListenUrl("tftp", "127.0.0.1", 69)).AsTask());

        Assert.AreEqual(ListenerBindFailure.AddressInUse, exception.Failure);
    }

    [TestMethod]
    public async Task AcceptFlowAsync_FirstDatagramFromAPeer_OpensAFlowCarryingIt()
    {
        var (listener, listenSocket, _) = await StartOnLoopbackAsync();
        await using var started = listener;

        listenSocket.Arrive("RRQ", PeerA);
        await using var flow = await listener.AcceptFlowAsync(TestContext.CancellationToken);

        Assert.AreEqual("RRQ", Encoding.ASCII.GetString(flow.FirstDatagram.Span));
        Assert.AreEqual(PeerA, flow.RemoteEndPoint);
        Assert.AreEqual(listenSocket.LocalEndPoint, flow.LocalEndPoint);
    }

    [TestMethod]
    public async Task AcceptFlowAsync_TwoPeers_OpensTwoFlowsAndRoutesLaterDatagramsToTheirOwn()
    {
        var (listener, listenSocket, _) = await StartOnLoopbackAsync();
        await using var started = listener;

        listenSocket.Arrive("A1", PeerA);
        listenSocket.Arrive("A2", PeerA);
        listenSocket.Arrive("B1", PeerB);
        listenSocket.Arrive("B2", PeerB);
        await using var flowA = await listener.AcceptFlowAsync(TestContext.CancellationToken);
        await using var flowB = await listener.AcceptFlowAsync(TestContext.CancellationToken);

        Assert.AreEqual(PeerA, flowA.RemoteEndPoint);
        Assert.AreEqual(PeerB, flowB.RemoteEndPoint);
        Assert.AreEqual("A2", await ReceiveTextAsync(flowA));
        Assert.AreEqual("B2", await ReceiveTextAsync(flowB));
    }

    [TestMethod]
    public async Task SendAsync_BeforeAMove_SendsFromTheListenPortToThePeer()
    {
        var (listener, listenSocket, _) = await StartOnLoopbackAsync();
        await using var started = listener;
        await using var flow = await OpenFlowAsync(listener, listenSocket, PeerA);

        await flow.SendAsync(Encoding.ASCII.GetBytes("DATA"), TestContext.CancellationToken);

        Assert.AreEqual(("DATA", (EndPoint)PeerA), listenSocket.Sent.Single());
    }

    [TestMethod]
    public async Task MoveToNewLocalPortAsync_BindsAnEphemeralPortOnTheListenAddressAndSendsFromIt()
    {
        var (listener, listenSocket, binder) = await StartOnLoopbackAsync();
        await using var started = listener;
        await using var flow = await OpenFlowAsync(listener, listenSocket, PeerA);

        await flow.MoveToNewLocalPortAsync(TestContext.CancellationToken);
        await flow.SendAsync(Encoding.ASCII.GetBytes("DATA"), TestContext.CancellationToken);

        var ownSocket = binder.Bound[1];
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 40001), flow.LocalEndPoint);
        Assert.AreEqual(("DATA", (EndPoint)PeerA), ownSocket.Sent.Single());
        Assert.IsEmpty(listenSocket.Sent);
    }

    [TestMethod]
    public async Task ReceiveAsync_AfterAMove_ReadsTheNewPortAndDropsOtherEndpoints()
    {
        var (listener, listenSocket, binder) = await StartOnLoopbackAsync();
        await using var started = listener;
        await using var flow = await OpenFlowAsync(listener, listenSocket, PeerA);
        await flow.MoveToNewLocalPortAsync(TestContext.CancellationToken);
        var ownSocket = binder.Bound[1];

        ownSocket.Arrive("stranger", PeerB);
        ownSocket.FailNextReceive(SocketError.ConnectionReset);
        ownSocket.Arrive("ACK", PeerA);

        Assert.AreEqual("ACK", await ReceiveTextAsync(flow));
    }

    [TestMethod]
    public async Task ReceiveAsync_AfterAMove_DropsWhatThePeerSendsToTheListenPortWithoutOpeningAFlow()
    {
        var (listener, listenSocket, _) = await StartOnLoopbackAsync();
        await using var started = listener;
        await using var flow = await OpenFlowAsync(listener, listenSocket, PeerA);
        await flow.MoveToNewLocalPortAsync(TestContext.CancellationToken);

        listenSocket.Arrive("retransmitted RRQ", PeerA);
        listenSocket.Arrive("RRQ", PeerB);
        await using var next = await listener.AcceptFlowAsync(TestContext.CancellationToken);

        Assert.AreEqual(PeerB, next.RemoteEndPoint);
    }

    [TestMethod]
    public async Task ReceiveAsync_OwnPortFails_ThrowsIOException()
    {
        var (listener, listenSocket, binder) = await StartOnLoopbackAsync();
        await using var started = listener;
        await using var flow = await OpenFlowAsync(listener, listenSocket, PeerA);
        await flow.MoveToNewLocalPortAsync(TestContext.CancellationToken);

        binder.Bound[1].FailNextReceive(SocketError.NetworkDown);

        var exception = await Assert.ThrowsExactlyAsync<IOException>(() => flow.ReceiveAsync(TestContext.CancellationToken).AsTask());
        Assert.IsInstanceOfType<SocketException>(exception.InnerException);
    }

    [TestMethod]
    public async Task MoveToNewLocalPortAsync_Twice_ReleasesThePortTheFirstMoveBound()
    {
        var (listener, listenSocket, binder) = await StartOnLoopbackAsync();
        await using var started = listener;
        await using var flow = await OpenFlowAsync(listener, listenSocket, PeerA);

        await flow.MoveToNewLocalPortAsync(TestContext.CancellationToken);
        await flow.MoveToNewLocalPortAsync(TestContext.CancellationToken);

        Assert.AreEqual(1, binder.Bound[1].DisposeCount);
        Assert.AreEqual(binder.Bound[2].LocalEndPoint, flow.LocalEndPoint);
    }

    [TestMethod]
    public async Task MoveToNewLocalPortAsync_BindFails_ThrowsIOExceptionAndStaysOnTheListenPort()
    {
        var (listener, listenSocket, binder) = await StartOnLoopbackAsync();
        await using var started = listener;
        await using var flow = await OpenFlowAsync(listener, listenSocket, PeerA);
        binder.FailNextBind = SocketError.AddressNotAvailable;

        await Assert.ThrowsExactlyAsync<IOException>(() => flow.MoveToNewLocalPortAsync(TestContext.CancellationToken).AsTask());

        Assert.AreEqual(listenSocket.LocalEndPoint, flow.LocalEndPoint);
        listenSocket.Arrive("still here", PeerA);
        Assert.AreEqual("still here", await ReceiveTextAsync(flow));
    }

    [TestMethod]
    public async Task SendAsync_SocketFails_ThrowsIOException()
    {
        var (listener, listenSocket, _) = await StartOnLoopbackAsync();
        await using var started = listener;
        await using var flow = await OpenFlowAsync(listener, listenSocket, PeerA);
        listenSocket.SendFailure = SocketError.HostUnreachable;

        await Assert.ThrowsExactlyAsync<IOException>(() => flow.SendAsync(new byte[1], TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task EveryCall_AfterDispose_ThrowsObjectDisposedException()
    {
        var (listener, listenSocket, _) = await StartOnLoopbackAsync();
        await using var started = listener;
        var flow = await OpenFlowAsync(listener, listenSocket, PeerA);

        await flow.DisposeAsync();
        await flow.DisposeAsync();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => flow.ReceiveAsync(TestContext.CancellationToken).AsTask());
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => flow.SendAsync(new byte[1], TestContext.CancellationToken).AsTask());
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => flow.MoveToNewLocalPortAsync(TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task ReceiveAsync_DisposedWhileWaitingOnTheListenPort_ThrowsObjectDisposedException()
    {
        var (listener, listenSocket, _) = await StartOnLoopbackAsync();
        await using var started = listener;
        var flow = await OpenFlowAsync(listener, listenSocket, PeerA);

        var receiving = flow.ReceiveAsync(TestContext.CancellationToken).AsTask();
        await flow.DisposeAsync();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => receiving);
    }

    [TestMethod]
    public async Task ReceiveAsync_AlreadyCancelled_ThrowsOperationCanceledException()
    {
        var (listener, listenSocket, _) = await StartOnLoopbackAsync();
        await using var started = listener;
        await using var flow = await OpenFlowAsync(listener, listenSocket, PeerA);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => flow.ReceiveAsync(new CancellationToken(canceled: true)).AsTask());
    }

    [TestMethod]
    public async Task DisposeAsync_OnAMovedFlow_ReleasesItsPortAndADatagramFromThePeerOpensANewFlow()
    {
        var (listener, listenSocket, binder) = await StartOnLoopbackAsync();
        await using var started = listener;
        var flow = await OpenFlowAsync(listener, listenSocket, PeerA);
        await flow.MoveToNewLocalPortAsync(TestContext.CancellationToken);

        await flow.DisposeAsync();
        listenSocket.Arrive("RRQ again", PeerA);
        await using var next = await listener.AcceptFlowAsync(TestContext.CancellationToken);

        Assert.AreEqual(1, binder.Bound[1].DisposeCount);
        Assert.AreEqual("RRQ again", Encoding.ASCII.GetString(next.FirstDatagram.Span));
    }

    [TestMethod]
    public async Task AcceptFlowAsync_CancelledWhileWaiting_ThrowsOperationCanceledException()
    {
        var (listener, _, _) = await StartOnLoopbackAsync();
        await using var started = listener;
        using var source = new CancellationTokenSource();

        var accepting = listener.AcceptFlowAsync(source.Token).AsTask();
        await source.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => accepting);
    }

    [TestMethod]
    public async Task AcceptFlowAsync_ListenSocketResetsThenFails_SkipsTheResetAndThrowsIOException()
    {
        var (listener, listenSocket, _) = await StartOnLoopbackAsync();
        await using var started = listener;

        listenSocket.FailNextReceive(SocketError.ConnectionReset);
        listenSocket.Arrive("RRQ", PeerA);
        listenSocket.FailNextReceive(SocketError.NetworkDown);
        await using var flow = await listener.AcceptFlowAsync(TestContext.CancellationToken);

        Assert.AreEqual(PeerA, flow.RemoteEndPoint);
        await Assert.ThrowsExactlyAsync<IOException>(() => listener.AcceptFlowAsync(TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task DisposeAsync_NoFlowOpen_ClosesTheListenSocketsAndRefusesLaterAccepts()
    {
        var (listener, listenSocket, _) = await StartOnLoopbackAsync();

        await listener.DisposeAsync();
        await listener.DisposeAsync();

        Assert.AreEqual(1, listenSocket.DisposeCount);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => listener.AcceptFlowAsync(TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task DisposeAsync_FlowOpenedButNotHandedOut_DisposesItAndClosesTheListenSocket()
    {
        var (listener, listenSocket, _) = await StartOnLoopbackAsync();
        listenSocket.Arrive("RRQ", PeerA);
        await listenSocket.WaitUntilReaderIsIdleAsync();

        await listener.DisposeAsync();

        Assert.AreEqual(1, listenSocket.DisposeCount);
    }

    [TestMethod]
    public async Task DisposeAsync_FlowHandedOutOnTheListenPort_LeavesItWorkingAndOpensNoNewFlow()
    {
        var (listener, listenSocket, _) = await StartOnLoopbackAsync();
        var flow = await OpenFlowAsync(listener, listenSocket, PeerA);

        await listener.DisposeAsync();
        listenSocket.Arrive("RRQ", PeerB);
        listenSocket.Arrive("ACK", PeerA);
        await flow.SendAsync(Encoding.ASCII.GetBytes("DATA"), TestContext.CancellationToken);

        Assert.AreEqual("ACK", await ReceiveTextAsync(flow));
        Assert.AreEqual("DATA", listenSocket.Sent.Single().Text);
        Assert.AreEqual(0, listenSocket.DisposeCount);

        await flow.DisposeAsync();

        Assert.AreEqual(1, listenSocket.DisposeCount);
    }

    [TestMethod]
    public async Task DisposeAsync_FlowHandedOut_ClosesTheListenSocketOnceTheFlowMoves()
    {
        var (listener, listenSocket, _) = await StartOnLoopbackAsync();
        await using var flow = await OpenFlowAsync(listener, listenSocket, PeerA);

        await listener.DisposeAsync();
        await flow.MoveToNewLocalPortAsync(TestContext.CancellationToken);

        Assert.AreEqual(1, listenSocket.DisposeCount);
    }

    [TestMethod]
    public async Task AcceptFlowAsync_MorePeersThanPendingFlowCapacity_DropsTheDatagramsThatWouldOpenMore()
    {
        var (listener, listenSocket, _) = await StartOnLoopbackAsync();
        await using var started = listener;
        var latePeer = new IPEndPoint(IPAddress.Loopback, 60000 + DatagramDemultiplexer.PendingFlowCapacity);

        for (var peer = 0; peer <= DatagramDemultiplexer.PendingFlowCapacity; peer++)
        {
            listenSocket.Arrive("RRQ", new IPEndPoint(IPAddress.Loopback, 60000 + peer));
        }

        await listenSocket.WaitUntilReaderIsIdleAsync();
        var flows = new List<IDatagramFlow>();
        for (var accepted = 0; accepted < DatagramDemultiplexer.PendingFlowCapacity; accepted++)
        {
            flows.Add(await listener.AcceptFlowAsync(TestContext.CancellationToken));
        }

        listenSocket.Arrive("RRQ retried", latePeer);
        var late = await listener.AcceptFlowAsync(TestContext.CancellationToken);

        Assert.AreEqual("RRQ retried", Encoding.ASCII.GetString(late.FirstDatagram.Span));
        Assert.IsFalse(flows.Any(flow => flow.RemoteEndPoint.Equals(latePeer)));
    }

    [TestMethod]
    public async Task ReceiveAsync_MoreDatagramsThanTheInboxHolds_DropsTheLaterOnes()
    {
        var (listener, listenSocket, _) = await StartOnLoopbackAsync();
        await using var started = listener;
        await using var flow = await OpenFlowAsync(listener, listenSocket, PeerA);

        for (var datagram = 0; datagram <= DemultiplexedDatagramFlow.ListenPortInboxCapacity; datagram++)
        {
            listenSocket.Arrive($"datagram {datagram}", PeerA);
        }

        await listenSocket.WaitUntilReaderIsIdleAsync();
        for (var datagram = 0; datagram < DemultiplexedDatagramFlow.ListenPortInboxCapacity; datagram++)
        {
            Assert.AreEqual($"datagram {datagram}", await ReceiveTextAsync(flow));
        }

        listenSocket.Arrive("last", PeerA);
        Assert.AreEqual("last", await ReceiveTextAsync(flow));
    }

    [TestMethod]
    public void Constructor_NoListenSocket_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new DatagramDemultiplexer([], new FakeDatagramSocketBinder().Bind));

    [TestMethod]
    public void Constructor_NoBinder_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new DatagramDemultiplexer([new FakeDatagramSocket(PeerA)], null!));

    private ValueTask<UdpDatagramListener> StartAsync(FakeDatagramSocketBinder binder, ListenUrl listenUrl) =>
        UdpDatagramListener.StartAsync(
            listenUrl, (_, _) => Task.FromResult(Array.Empty<IPAddress>()), binder.Bind, TestContext.CancellationToken);

    private async Task<(UdpDatagramListener Listener, FakeDatagramSocket ListenSocket, FakeDatagramSocketBinder Binder)> StartOnLoopbackAsync()
    {
        var binder = new FakeDatagramSocketBinder();
        var listener = await StartAsync(binder, new ListenUrl("tftp", "127.0.0.1", 0));

        return (listener, binder.Bound[0], binder);
    }

    private async Task<IDatagramFlow> OpenFlowAsync(UdpDatagramListener listener, FakeDatagramSocket listenSocket, EndPoint peer)
    {
        listenSocket.Arrive("RRQ", peer);

        return await listener.AcceptFlowAsync(TestContext.CancellationToken);
    }

    private async Task<string> ReceiveTextAsync(IDatagramFlow flow) =>
        Encoding.ASCII.GetString((await flow.ReceiveAsync(TestContext.CancellationToken)).Span);
}
