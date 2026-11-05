using System.Net;
using System.Net.Sockets;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// <see cref="SocketOriginConnector"/> over real loopback sockets and the system resolver
/// (ADR-0088, decision 2): a listener the test starts on <c>127.0.0.1</c>, reached by address and
/// by <c>localhost</c>, and the failure reported for a closed port, a name that cannot resolve and
/// an origin the allowed-origin rule refuses. Every socket stays on loopback and the one name
/// looked up is in <c>.invalid</c> (RFC 6761 section 6.4), so these are fast tests (BL-551).
/// </summary>
[TestClass]
public sealed class SocketOriginConnectorLoopbackTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("127.0.0.1")]
    [DataRow("localhost")]
    public async Task ConnectAsync_LoopbackListener_CarriesBytesBothWays(string host)
    {
        using var origin = new TcpListener(IPAddress.Loopback, 0);
        origin.Start();
        var port = ((IPEndPoint)origin.LocalEndpoint).Port;
        var connector = new SocketOriginConnector(AllowedOriginRule.LoopbackOnly);

        var accept = origin.AcceptTcpClientAsync(TestContext.CancellationToken).AsTask();
        await using var connection = await connector.ConnectAsync(host, port, Timeout, TestContext.CancellationToken);
        using var originEnd = await accept;
        await connection.WriteAsync(Encoding.ASCII.GetBytes("ping"), TestContext.CancellationToken);
        var received = new byte[4];
        await originEnd.GetStream().ReadExactlyAsync(received, TestContext.CancellationToken);
        await originEnd.GetStream().WriteAsync(Encoding.ASCII.GetBytes("pong"), TestContext.CancellationToken);
        var answered = new byte[4];
        await ReadExactlyAsync(connection, answered);

        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, port), connection.RemoteEndPoint);
        Assert.AreEqual("ping", Encoding.ASCII.GetString(received));
        Assert.AreEqual("pong", Encoding.ASCII.GetString(answered));
        connection.Abort();
    }

    [TestMethod]
    public async Task ConnectAsync_ClosedPort_IsRefused()
    {
        var port = ClosedLoopbackPort();
        var connector = new SocketOriginConnector(AllowedOriginRule.LoopbackOnly);

        var exception = await Assert.ThrowsExactlyAsync<OriginConnectionException>(
            () => connector.ConnectAsync("127.0.0.1", port, Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(OriginConnectionFailure.Refused, exception.Failure);
    }

    [TestMethod]
    public async Task ConnectAsync_UnresolvableName_IsHostNotFound()
    {
        var connector = new SocketOriginConnector(AllowedOriginRule.LoopbackOnly);

        var exception = await Assert.ThrowsExactlyAsync<OriginConnectionException>(
            () => connector.ConnectAsync("surl-origin.invalid", 80, Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(OriginConnectionFailure.HostNotFound, exception.Failure);
    }

    [TestMethod]
    public async Task ConnectAsync_OriginTheRuleRefuses_IsNotAllowed()
    {
        using var origin = new TcpListener(IPAddress.Loopback, 0);
        origin.Start();
        var proxyEnd = (IPEndPoint)origin.LocalEndpoint;
        var connector = new SocketOriginConnector(AllowedOriginRule.LoopbackOnly.WithProxyEndPoint(proxyEnd));

        var exception = await Assert.ThrowsExactlyAsync<OriginConnectionException>(
            () => connector.ConnectAsync("127.0.0.1", proxyEnd.Port, Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(OriginConnectionFailure.NotAllowed, exception.Failure);
        Assert.IsFalse(origin.Pending());
    }

    [TestMethod]
    public async Task ConnectAsync_AddressOutsideLoopback_IsNotAllowed()
    {
        var connector = new SocketOriginConnector(AllowedOriginRule.LoopbackOnly);

        var exception = await Assert.ThrowsExactlyAsync<OriginConnectionException>(
            () => connector.ConnectAsync("192.0.2.1", 80, Timeout, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(OriginConnectionFailure.NotAllowed, exception.Failure);
    }

    private static int ClosedLoopbackPort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        return port;
    }

    private async Task ReadExactlyAsync(IConnection connection, byte[] buffer)
    {
        var filled = 0;

        while (filled < buffer.Length)
        {
            var read = await connection.ReadAsync(buffer.AsMemory(filled), TestContext.CancellationToken);
            Assert.AreNotEqual(0, read);
            filled += read;
        }
    }
}
