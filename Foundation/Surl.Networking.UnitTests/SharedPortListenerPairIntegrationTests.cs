using System.Net;
using System.Net.Sockets;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

[TestClass]
[TestCategory("Integration")]
public sealed class SharedPortListenerPairIntegrationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task StartAsync_IPv4LoopbackPortZero_BindsTcpAndUdpOnTheSamePortReportedByBoundEndPoints()
    {
        var listenUrl = new ListenUrl("https", "127.0.0.1", 0);

        await using var pair = await SharedPortListenerPair.StartAsync(listenUrl, null, TestContext.CancellationToken);

        var tcp = (IPEndPoint)pair.ConnectionListener.BoundEndPoints.Single();
        var udp = (IPEndPoint)pair.DatagramListener.BoundEndPoints.Single();
        Assert.AreEqual(IPAddress.Loopback, udp.Address);
        Assert.AreNotEqual(0, udp.Port);
        Assert.AreEqual(tcp, udp);
        Assert.AreEqual(udp.Port, pair.DatagramListener.ListenUrl.BoundPort);
    }

    [TestMethod]
    public async Task AcceptFlowAsync_TwoUdpClients_OpensTwoFlowsOnTheSharedPort()
    {
        await using var pair = await SharedPortListenerPair.StartAsync(
            new ListenUrl("https", "127.0.0.1", 0), null, TestContext.CancellationToken);
        var listenEndPoint = (IPEndPoint)pair.DatagramListener.BoundEndPoints.Single();
        using var first = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var second = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        await first.SendAsync(Encoding.ASCII.GetBytes("first"), listenEndPoint, TestContext.CancellationToken);
        await using var firstFlow = await pair.DatagramListener.AcceptFlowAsync(TestContext.CancellationToken);
        await second.SendAsync(Encoding.ASCII.GetBytes("second"), listenEndPoint, TestContext.CancellationToken);
        await using var secondFlow = await pair.DatagramListener.AcceptFlowAsync(TestContext.CancellationToken);

        Assert.AreEqual(first.Client.LocalEndPoint, firstFlow.RemoteEndPoint);
        Assert.AreEqual(second.Client.LocalEndPoint, secondFlow.RemoteEndPoint);
        Assert.AreEqual("second", Encoding.ASCII.GetString(secondFlow.FirstDatagram.Span));
    }
}
