using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The server's schemes, its arguments, and the HTTP session it starts for each connection
/// (ADR-0119, decisions 0 and 5).
/// </summary>
[TestClass]
public sealed class SignalRProtocolServerTests
{
    [TestMethod]
    public void Schemes_AreSignalRAndSignalRs()
    {
        var harness = new SignalRServerHarness();

        CollectionAssert.AreEqual(new[] { "signalr", "signalrs" }, harness.Server.Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_TlsConnection_StartsTheHttpSessionWithItsTlsSession()
    {
        var harness = new SignalRServerHarness();
        var tlsSession = InMemoryConnection.DefaultUpgradeTlsSession;
        var connection = new InMemoryConnection(
            [Encoding.ASCII.GetBytes("POST /chat/negotiate HTTP/1.1\r\nHost: h\r\n" + SignalRServerHarness.BearerField + "\r\nConnection: close\r\n\r\n")],
            initialTlsSession: tlsSession);

        await harness.Server.ServeAsync(connection, harness.Context());

        Assert.AreSame(tlsSession, harness.Policy.StartedConnections.Single());
        Assert.StartsWith("HTTP/1.1 200 OK\r\n", Encoding.ASCII.GetString(connection.WrittenBytes));
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        var hubPath = SignalRHubPath.FromListenPath("/chat");
        var connections = new SignalRHubConnections(TimeProvider.System);
        var policy = new UnitTestBearerAuthenticationPolicy(false);

        Assert.ThrowsExactly<ArgumentNullException>(() => new SignalRProtocolServer(null!, connections, policy));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SignalRProtocolServer(hubPath, null!, policy));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SignalRProtocolServer(hubPath, connections, null!));
    }

    [TestMethod]
    public async Task ServeAsync_NullArgument_Throws()
    {
        var harness = new SignalRServerHarness();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => harness.Server.ServeAsync(null!, harness.Context()));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => harness.Server.ServeAsync(new InMemoryConnection([]), null!));
    }
}
