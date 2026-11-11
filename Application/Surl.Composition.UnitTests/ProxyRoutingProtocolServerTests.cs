using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class ProxyRoutingProtocolServerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_ANullServer_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ProxyRoutingProtocolServer(null!, new NamedServer("proxy")));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ProxyRoutingProtocolServer(new NamedServer("origin"), null!));
    }

    [TestMethod]
    public void Schemes_AreTheOriginServers()
    {
        var router = new ProxyRoutingProtocolServer(new NamedServer("origin", "http"), new NamedServer("proxy", "http", "https"));

        CollectionAssert.AreEqual(new[] { "http" }, router.Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_AListenUrlNamedByProxy_IsServedByTheProxyServer()
    {
        var origin = new NamedServer("origin", "http");
        var proxy = new NamedServer("proxy", "http");
        var router = new ProxyRoutingProtocolServer(origin, proxy);

        await router.ServeAsync(new InMemoryConnection([]), ContextFor(new ListenUrl("http", "127.0.0.1", 3128) { IsProxy = true }));

        Assert.AreEqual(0, origin.Served);
        Assert.AreEqual(1, proxy.Served);
    }

    [TestMethod]
    public async Task ServeAsync_AnyOtherListenUrl_IsServedByTheOriginServer()
    {
        var origin = new NamedServer("origin", "http");
        var proxy = new NamedServer("proxy", "http");
        var router = new ProxyRoutingProtocolServer(origin, proxy);

        await router.ServeAsync(new InMemoryConnection([]), ContextFor(new ListenUrl("http", "127.0.0.1", 8080)));

        Assert.AreEqual(1, origin.Served);
        Assert.AreEqual(0, proxy.Served);
    }

    [TestMethod]
    public async Task ServeAsync_ANullContext_Throws()
    {
        var router = new ProxyRoutingProtocolServer(new NamedServer("origin"), new NamedServer("proxy"));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => router.ServeAsync(new InMemoryConnection([]), null!));
    }

    [TestMethod]
    public async Task WriteRefusalAsync_AnOriginServerThatWritesRefusals_WritesItsRefusal()
    {
        var origin = new NamedServer("origin", "http");
        var router = new ProxyRoutingProtocolServer(origin, new NamedServer("proxy"));

        await router.WriteRefusalAsync(new InMemoryConnection([]), ConnectionRefusal.TooManyConnections, TestContext.CancellationToken);

        Assert.AreEqual(1, origin.Refused);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_AnOriginServerWithNoRefusal_WritesNothing()
    {
        var connection = new InMemoryConnection([]);
        var router = new ProxyRoutingProtocolServer(new PlainServer(), new NamedServer("proxy"));

        await router.WriteRefusalAsync(connection, ConnectionRefusal.TooManyConnections, TestContext.CancellationToken);

        Assert.AreEqual(0, connection.WrittenBytes.Length);
    }

    private static ExchangeContext ContextFor(ListenUrl listenUrl) =>
        new(1, listenUrl, new IPEndPoint(IPAddress.Loopback, 3128), new IPEndPoint(IPAddress.Loopback, 50000), new RecordingExchangeLog(), TimeProvider.System, CancellationToken.None);

    private sealed class NamedServer(string name, params string[] schemes) : IConnectionProtocolServer, IConnectionRefusalWriter
    {
        public string Name { get; } = name;

        public IReadOnlyList<string> Schemes { get; } = schemes;

        public int Served { get; private set; }

        public int Refused { get; private set; }

        public Task ServeAsync(IConnection connection, ExchangeContext context)
        {
            Served++;
            return Task.CompletedTask;
        }

        public ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, CancellationToken cancellationToken)
        {
            Refused++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class PlainServer : IConnectionProtocolServer
    {
        public IReadOnlyList<string> Schemes { get; } = ["http"];

        public Task ServeAsync(IConnection connection, ExchangeContext context) => Task.CompletedTask;
    }
}
