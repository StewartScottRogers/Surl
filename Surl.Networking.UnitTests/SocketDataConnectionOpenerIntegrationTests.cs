using System.Net;
using System.Net.Sockets;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// <see cref="SocketDataConnectionOpener"/> over real loopback sockets: a passive data connection
/// the test connects to, and an active one to a listener the test opens, each moving bytes both
/// ways (ADR-0052, decision 9).
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class SocketDataConnectionOpenerIntegrationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("127.0.0.1")]
    [DataRow("::1")]
    public async Task PassiveDataConnection_OnLoopback_CarriesBytesBothWays(string address)
    {
        var loopback = IPAddress.Parse(address);
        var opener = new SocketDataConnectionOpener(null);
        await using var listener = await opener.StartPassiveListenerAsync(
            new IPEndPoint(loopback, 21), new IPEndPoint(loopback, 50000), TestContext.CancellationToken);
        using var client = new TcpClient(loopback.AddressFamily);

        Assert.AreEqual(loopback, listener.LocalEndPoint.Address);
        Assert.AreNotEqual(0, listener.LocalEndPoint.Port);
        var accept = listener.AcceptAsync(Timeout, TestContext.CancellationToken).AsTask();
        await client.ConnectAsync(listener.LocalEndPoint, TestContext.CancellationToken);
        await using var connection = await accept;

        Assert.AreEqual(client.Client.LocalEndPoint, connection.RemoteEndPoint);
        await AssertCarriesBytesBothWaysAsync(connection, client);
    }

    [TestMethod]
    public async Task ActiveDataConnection_ToThePeersListener_CarriesBytesBothWays()
    {
        using var peerListener = new TcpListener(IPAddress.Loopback, 0);
        peerListener.Start();
        var target = (IPEndPoint)peerListener.LocalEndpoint;
        var opener = new SocketDataConnectionOpener(null);

        var peerAccept = peerListener.AcceptTcpClientAsync(TestContext.CancellationToken).AsTask();
        await using var connection = await opener.ConnectActiveAsync(
            new IPEndPoint(IPAddress.Loopback, 50000), target, Timeout, TestContext.CancellationToken);
        using var client = await peerAccept;

        Assert.AreEqual(target, connection.RemoteEndPoint);
        await AssertCarriesBytesBothWaysAsync(connection, client);
    }

    [TestMethod]
    public async Task ActiveDataConnection_NobodyListening_IsUnreachable()
    {
        int closedPort;
        using (var probe = new TcpListener(IPAddress.Loopback, 0))
        {
            probe.Start();
            closedPort = ((IPEndPoint)probe.LocalEndpoint).Port;
        }

        var opener = new SocketDataConnectionOpener(null);

        var exception = await Assert.ThrowsExactlyAsync<DataConnectionException>(
            () => opener.ConnectActiveAsync(
                new IPEndPoint(IPAddress.Loopback, 50000), new IPEndPoint(IPAddress.Loopback, closedPort), Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(DataConnectionFailure.Unreachable, exception.Failure);
    }

    private async Task AssertCarriesBytesBothWaysAsync(IConnection connection, TcpClient client)
    {
        var stream = client.GetStream();

        await connection.WriteAsync(Encoding.ASCII.GetBytes("listing\r\n"), TestContext.CancellationToken);
        await connection.CompleteWritesAsync(TestContext.CancellationToken);
        var toClient = await ReadToEndAsync(stream);
        Assert.AreEqual("listing\r\n", toClient);

        await stream.WriteAsync(Encoding.ASCII.GetBytes("upload"), TestContext.CancellationToken);
        client.Client.Shutdown(SocketShutdown.Send);
        var toServer = new MemoryStream();
        var buffer = new byte[64];
        int count;
        while ((count = await connection.ReadAsync(buffer, TestContext.CancellationToken)) > 0)
        {
            toServer.Write(buffer, 0, count);
        }

        Assert.AreEqual("upload", Encoding.ASCII.GetString(toServer.ToArray()));
    }

    private async Task<string> ReadToEndAsync(NetworkStream stream)
    {
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);

        return await reader.ReadToEndAsync(TestContext.CancellationToken);
    }
}
