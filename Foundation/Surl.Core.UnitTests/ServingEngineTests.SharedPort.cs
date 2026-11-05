using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

// A scheme a connection server and a datagram server share: its listen URL binds both on one
// port, as https does with HTTP/3 (ADR-0082, decision 6; BL-384).
public sealed partial class ServingEngineTests
{
    private static readonly ListenUrl SharedHttps = new("https", "127.0.0.1", 0);
    private static readonly ListenUrl SharedHttpsUdp = new("https", "127.0.0.1", 40000);

    [TestMethod]
    public async Task ServeAsync_SchemeOfAConnectionAndADatagramServer_ServesBothOnTheConnectionListenersPort()
    {
        var factory = new FakeListenerFactory();
        var connectionServer = new FakeConnectionProtocolServer("https");
        var datagramServer = new FakeDatagramProtocolServer("https");
        var engine = CreateEngine(factory, new ManualTimeProvider(), new FakeExchangeLogFactory(), connectionServer, datagramServer);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([SharedHttps], stop.Token);

        factory.ListenerFor(SharedHttps).Connect(new InMemoryConnection([]));
        var exchange = await connectionServer.NextExchangeAsync();
        factory.DatagramListenerFor(SharedHttpsUdp).Open(new FakeDatagramFlow([1], new IPEndPoint(IPAddress.Loopback, 40000), new IPEndPoint(IPAddress.Loopback, 51234)));
        var flow = await datagramServer.NextFlowAsync();
        await stop.CancelAsync();

        Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience.Timeout));
        CollectionAssert.AreEqual(new[] { SharedHttps }, factory.StartRequests.ToArray());
        CollectionAssert.AreEqual(new[] { SharedHttpsUdp }, factory.DatagramStartRequests.ToArray());
        Assert.AreEqual("https", exchange.Context.Scheme);
        Assert.AreEqual(SharedHttpsUdp.WithBoundPort(40000), flow.Context.ListenUrl);
        Assert.IsTrue(factory.ListenerFor(SharedHttps).Disposed);
        Assert.IsTrue(factory.DatagramListenerFor(SharedHttpsUdp).Disposed);
    }

    [TestMethod]
    public async Task ServeAsync_DatagramServerRegisteredBeforeTheConnectionServer_StillSharesThePort()
    {
        var factory = new FakeListenerFactory();
        var connectionServer = new FakeConnectionProtocolServer("https");
        var engine = CreateEngine(factory, new ManualTimeProvider(), new FakeExchangeLogFactory(), new FakeDatagramProtocolServer("https"), connectionServer);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([SharedHttps], stop.Token);

        factory.ListenerFor(SharedHttps).Connect(new InMemoryConnection([]));
        _ = await connectionServer.NextExchangeAsync();
        await stop.CancelAsync();

        Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience.Timeout));
        CollectionAssert.AreEqual(new[] { SharedHttpsUdp }, factory.DatagramStartRequests.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_SharedPortDatagramListenerThatCannotBind_ReleasesTheConnectionListenerAndFailsTheBind()
    {
        var factory = new FakeListenerFactory()
            .FailToStart(SharedHttpsUdp, new ListenerBindException(SharedHttpsUdp, null, ListenerBindFailure.AddressInUse, null));
        var engine = CreateEngine(
            factory, new ManualTimeProvider(), new FakeExchangeLogFactory(),
            new FakeConnectionProtocolServer("https"), new FakeDatagramProtocolServer("https"));

        var exitCode = await engine.ServeAsync([SharedHttps], CancellationToken.None).WaitAsync(Patience.Timeout);

        Assert.AreEqual(SurlExitCode.BindFailed, exitCode);
        Assert.IsTrue(factory.ListenerFor(SharedHttps).Disposed);
    }

    [TestMethod]
    public void Constructor_TwoDatagramServersListingOneScheme_Throws()
    {
        IProtocolServer[] servers = [new FakeDatagramProtocolServer("tftp"), new FakeDatagramProtocolServer("tftp")];

        var exception = Assert.ThrowsExactly<ArgumentException>(() => new ServingEngine(
            new FakeListenerFactory(), servers, new FakeExchangeLogFactory(), new ManualTimeProvider(), GracePeriod));

        StringAssert.Contains(exception.Message, "'tftp'");
    }

    [TestMethod]
    public void Constructor_ASecondDatagramServerForASharedScheme_Throws()
    {
        IProtocolServer[] servers =
        [
            new FakeConnectionProtocolServer("https"), new FakeDatagramProtocolServer("https"), new FakeDatagramProtocolServer("https"),
        ];

        var exception = Assert.ThrowsExactly<ArgumentException>(() => new ServingEngine(
            new FakeListenerFactory(), servers, new FakeExchangeLogFactory(), new ManualTimeProvider(), GracePeriod));

        StringAssert.Contains(exception.Message, "'https'");
    }
}
