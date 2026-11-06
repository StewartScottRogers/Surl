using System.Net;
using System.Net.Sockets;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

[TestClass]
[TestCategory("Integration")]
public sealed class SocketListenerFactoryTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task StartConnectionListenerAsync_IPv4LoopbackPortZero_AcceptsATcpClient()
    {
        IListenerFactory factory = new SocketListenerFactory();
        await using var listener = await factory.StartConnectionListenerAsync(
            new ListenUrl("http", "127.0.0.1", 0), TestContext.CancellationToken);
        var listenEndPoint = (IPEndPoint)listener.BoundEndPoints.Single();
        using var client = new TcpClient(AddressFamily.InterNetwork);

        await client.ConnectAsync(listenEndPoint, TestContext.CancellationToken);
        await using var connection = await listener.AcceptAsync(TestContext.CancellationToken);

        Assert.AreEqual(client.Client.LocalEndPoint, connection.RemoteEndPoint);
        Assert.AreEqual(listenEndPoint.Port, listener.ListenUrl.BoundPort);
    }

    [TestMethod]
    public async Task StartDatagramListenerAsync_IPv4LoopbackPortZero_ReceivesAUdpClientDatagram()
    {
        IListenerFactory factory = new SocketListenerFactory();
        await using var listener = await factory.StartDatagramListenerAsync(
            new ListenUrl("tftp", "127.0.0.1", 0), TestContext.CancellationToken);
        var listenEndPoint = (IPEndPoint)listener.BoundEndPoints.Single();
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        await client.SendAsync(Encoding.ASCII.GetBytes("RRQ"), listenEndPoint, TestContext.CancellationToken);
        await using var flow = await listener.AcceptFlowAsync(TestContext.CancellationToken);

        Assert.AreEqual("RRQ", Encoding.ASCII.GetString(flow.FirstDatagram.Span));
        Assert.AreEqual(client.Client.LocalEndPoint, flow.RemoteEndPoint);
        Assert.AreEqual(listenEndPoint.Port, listener.ListenUrl.BoundPort);
    }
}
