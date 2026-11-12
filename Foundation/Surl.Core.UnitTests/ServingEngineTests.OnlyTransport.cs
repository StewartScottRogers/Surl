using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

// A listen URL that names the one listener it binds, so one dns listen URL can serve UDP alone
// while another serves TCP (ADR-0092, Amendment 1; BL-1036).
public sealed partial class ServingEngineTests
{
    [TestMethod]
    public async Task ServeAsync_OnlyTransportOnEachListenUrl_BindsOnlyTheListenersEachAsksFor()
    {
        var datagramsOnly = new ListenUrl("dns", "127.0.0.1", 5300) { OnlyTransport = ListenTransport.Datagrams };
        var connectionsOnly = new ListenUrl("dns", "127.0.0.1", 5301) { OnlyTransport = ListenTransport.Connections };
        var both = new ListenUrl("dns", "127.0.0.1", 5302);
        var factory = new FakeListenerFactory();
        var connectionServer = new FakeConnectionProtocolServer("dns");
        var datagramServer = new FakeDatagramProtocolServer("dns");
        var engine = CreateEngine(factory, new ManualTimeProvider(), new FakeExchangeLogFactory(), connectionServer, datagramServer);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([datagramsOnly, connectionsOnly, both], stop.Token);

        factory.DatagramListenerFor(datagramsOnly).Open(new FakeDatagramFlow([1], new IPEndPoint(IPAddress.Loopback, 5300), new IPEndPoint(IPAddress.Loopback, 51234)));
        var flow = await datagramServer.NextFlowAsync();
        factory.ListenerFor(connectionsOnly).Connect(new InMemoryConnection([]));
        var exchange = await connectionServer.NextExchangeAsync();
        await stop.CancelAsync();

        Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience.Timeout));
        CollectionAssert.AreEqual(new[] { connectionsOnly, both }, factory.StartRequests.ToArray());
        CollectionAssert.AreEqual(new[] { datagramsOnly, both }, factory.DatagramStartRequests.ToArray());
        Assert.AreEqual(datagramsOnly.WithBoundPort(5300), flow.Context.ListenUrl);
        Assert.AreEqual(connectionsOnly.WithBoundPort(5301), exchange.Context.ListenUrl);
    }

    [TestMethod]
    public async Task ServeAsync_ListenUrlAskingForConnectionsOfADatagramOnlyScheme_IsUnsupportedBeforeAnyListenerBinds()
    {
        var factory = new FakeListenerFactory();
        var engine = CreateEngine(
            factory, new ManualTimeProvider(), new FakeExchangeLogFactory(),
            new FakeConnectionProtocolServer("http"), new FakeDatagramProtocolServer("tftp"));
        ListenUrl[] listenUrls =
        [
            new("http", "127.0.0.1", 8080),
            new("tftp", "127.0.0.1", 6969) { OnlyTransport = ListenTransport.Connections },
        ];

        var exitCode = await engine.ServeAsync(listenUrls, CancellationToken.None).WaitAsync(Patience.Timeout);

        Assert.AreEqual(SurlExitCode.UnsupportedProtocol, exitCode);
        Assert.IsEmpty(factory.StartRequests);
        Assert.IsEmpty(factory.DatagramStartRequests);
    }

    [TestMethod]
    public async Task ServeAsync_ListenUrlAskingForDatagramsOfAConnectionOnlyScheme_IsUnsupportedBeforeAnyListenerBinds()
    {
        var factory = new FakeListenerFactory();
        var engine = CreateEngine(
            factory, new ManualTimeProvider(), new FakeExchangeLogFactory(),
            new FakeConnectionProtocolServer("http"), new FakeDatagramProtocolServer("tftp"));
        ListenUrl[] listenUrls =
        [
            new("tftp", "127.0.0.1", 6969),
            new("http", "127.0.0.1", 8080) { OnlyTransport = ListenTransport.Datagrams },
        ];

        var exitCode = await engine.ServeAsync(listenUrls, CancellationToken.None).WaitAsync(Patience.Timeout);

        Assert.AreEqual(SurlExitCode.UnsupportedProtocol, exitCode);
        Assert.IsEmpty(factory.StartRequests);
        Assert.IsEmpty(factory.DatagramStartRequests);
    }
}
