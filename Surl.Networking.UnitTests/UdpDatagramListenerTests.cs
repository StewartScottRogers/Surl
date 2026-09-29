using System.Net;
using System.Net.Sockets;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

[TestClass]
[TestCategory("Integration")]
public sealed class UdpDatagramListenerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task StartAsync_IPv4LoopbackPortZero_ReportsTheBoundPort()
    {
        var listenUrl = new ListenUrl("tftp", "127.0.0.1", 0);

        await using var listener = await UdpDatagramListener.StartAsync(listenUrl, TestContext.CancellationToken);

        var bound = (IPEndPoint)listener.BoundEndPoints.Single();
        Assert.AreEqual(IPAddress.Loopback, bound.Address);
        Assert.AreNotEqual(0, bound.Port);
        Assert.AreEqual(listenUrl.WithBoundPort(bound.Port), listener.ListenUrl);
    }

    [TestMethod]
    public async Task AcceptFlowAsync_IPv4Loopback_ExchangesDatagramsOnTheListenPort()
    {
        await using var listener = await UdpDatagramListener.StartAsync(
            new ListenUrl("tftp", "127.0.0.1", 0), TestContext.CancellationToken);

        await ExchangeOnTheListenPortAsync(listener, IPAddress.Loopback, AddressFamily.InterNetwork);
    }

    [TestMethod]
    public async Task AcceptFlowAsync_IPv6Loopback_ExchangesDatagramsOnTheListenPort()
    {
        UdpDatagramListener listener;
        try
        {
            listener = await UdpDatagramListener.StartAsync(new ListenUrl("tftp", "::1", 0), TestContext.CancellationToken);
        }
        catch (ListenerBindException exception) when (exception.Failure == ListenerBindFailure.AddressNotAvailable)
        {
            Assert.Inconclusive("This machine has no IPv6 loopback.");
            throw;
        }

        await using (listener)
        {
            await ExchangeOnTheListenPortAsync(listener, IPAddress.IPv6Loopback, AddressFamily.InterNetworkV6);
        }
    }

    [TestMethod]
    public async Task MoveToNewLocalPortAsync_IPv4Loopback_RepliesFromANewPortAndReceivesOnIt()
    {
        await using var listener = await UdpDatagramListener.StartAsync(
            new ListenUrl("tftp", "127.0.0.1", 0), TestContext.CancellationToken);
        var listenEndPoint = (IPEndPoint)listener.BoundEndPoints.Single();
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        await client.SendAsync(Encoding.ASCII.GetBytes("RRQ"), listenEndPoint, TestContext.CancellationToken);
        await using var flow = await listener.AcceptFlowAsync(TestContext.CancellationToken);
        await flow.MoveToNewLocalPortAsync(TestContext.CancellationToken);
        await flow.SendAsync(Encoding.ASCII.GetBytes("DATA"), TestContext.CancellationToken);
        var reply = await client.ReceiveAsync(TestContext.CancellationToken);

        Assert.AreEqual("DATA", Encoding.ASCII.GetString(reply.Buffer));
        Assert.AreEqual(flow.LocalEndPoint, reply.RemoteEndPoint);
        Assert.AreNotEqual(listenEndPoint.Port, reply.RemoteEndPoint.Port);

        await client.SendAsync(Encoding.ASCII.GetBytes("ACK"), reply.RemoteEndPoint, TestContext.CancellationToken);

        Assert.AreEqual("ACK", Encoding.ASCII.GetString((await flow.ReceiveAsync(TestContext.CancellationToken)).Span));
    }

    [TestMethod]
    public async Task AcceptFlowAsync_TwoPeers_OpensTwoFlows()
    {
        await using var listener = await UdpDatagramListener.StartAsync(
            new ListenUrl("tftp", "127.0.0.1", 0), TestContext.CancellationToken);
        var listenEndPoint = (IPEndPoint)listener.BoundEndPoints.Single();
        using var first = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var second = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        await first.SendAsync(Encoding.ASCII.GetBytes("first"), listenEndPoint, TestContext.CancellationToken);
        await using var firstFlow = await listener.AcceptFlowAsync(TestContext.CancellationToken);
        await second.SendAsync(Encoding.ASCII.GetBytes("second"), listenEndPoint, TestContext.CancellationToken);
        await using var secondFlow = await listener.AcceptFlowAsync(TestContext.CancellationToken);

        Assert.AreEqual(first.Client.LocalEndPoint, firstFlow.RemoteEndPoint);
        Assert.AreEqual(second.Client.LocalEndPoint, secondFlow.RemoteEndPoint);
        Assert.AreEqual("first", Encoding.ASCII.GetString(firstFlow.FirstDatagram.Span));
        Assert.AreEqual("second", Encoding.ASCII.GetString(secondFlow.FirstDatagram.Span));
    }

    private async Task ExchangeOnTheListenPortAsync(UdpDatagramListener listener, IPAddress loopback, AddressFamily family)
    {
        var listenEndPoint = (IPEndPoint)listener.BoundEndPoints.Single();
        using var client = new UdpClient(new IPEndPoint(loopback, 0));
        Assert.AreEqual(family, listenEndPoint.AddressFamily);

        await client.SendAsync(Encoding.ASCII.GetBytes("RRQ"), listenEndPoint, TestContext.CancellationToken);
        await using var flow = await listener.AcceptFlowAsync(TestContext.CancellationToken);
        await client.SendAsync(Encoding.ASCII.GetBytes("second"), listenEndPoint, TestContext.CancellationToken);
        await flow.SendAsync(Encoding.ASCII.GetBytes("reply"), TestContext.CancellationToken);
        var reply = await client.ReceiveAsync(TestContext.CancellationToken);

        Assert.AreEqual("RRQ", Encoding.ASCII.GetString(flow.FirstDatagram.Span));
        Assert.AreEqual(client.Client.LocalEndPoint, flow.RemoteEndPoint);
        Assert.AreEqual("second", Encoding.ASCII.GetString((await flow.ReceiveAsync(TestContext.CancellationToken)).Span));
        Assert.AreEqual("reply", Encoding.ASCII.GetString(reply.Buffer));
        Assert.AreEqual(listenEndPoint, reply.RemoteEndPoint);
    }
}
