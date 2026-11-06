using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// The server's schemes and its arguments (ADR-0120, decision 0).
/// </summary>
[TestClass]
public sealed class SocketIoProtocolServerTests
{
    [TestMethod]
    public void Schemes_AreSocketIoAndSocketIos()
    {
        var harness = new SocketIoServerHarness();

        CollectionAssert.AreEqual(new[] { "socketio", "socketios" }, harness.Server.Schemes.ToArray());
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        var namespaces = SocketIoServedNamespaces.FromListenPath("/");
        var sessions = new SocketIoSessions();
        var policy = new UnitTestPasswordPolicy(false);

        Assert.ThrowsExactly<ArgumentNullException>(() => new SocketIoProtocolServer(null!, sessions, policy));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SocketIoProtocolServer(namespaces, null!, policy));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SocketIoProtocolServer(namespaces, sessions, null!));
    }

    [TestMethod]
    public async Task ServeAsync_NullArgument_Throws()
    {
        var harness = new SocketIoServerHarness();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => harness.Server.ServeAsync(null!, harness.Context()));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => harness.Server.ServeAsync(new InMemoryConnection([]), null!));
    }
}
