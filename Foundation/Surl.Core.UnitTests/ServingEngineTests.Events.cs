using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

// The engine publishes each exchange's start, bytes and end on the instance's event stream
// (ADR-0124, decision 1; BL-787).
public sealed partial class ServingEngineTests
{
    [TestMethod]
    public void Constructor_NoEventPublisher_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ServingEngine(
            new FakeListenerFactory(), [], new FakeExchangeLogFactory(), new ManualTimeProvider(), GracePeriod,
            ConnectionLimits.None, RefusingDataConnectionOpener.Instance, ExchangeLimits.Default, null!));
    }

    [TestMethod]
    public async Task ServeAsync_TcpExchange_PublishesItsStartWithItsListenUrl_ItsBytesInOrder_AndItsEnd()
    {
        var factory = new FakeListenerFactory();
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var subscription = stream.Subscribe(new SurlEventSubscriptionOptions { IncludeBytes = true });
        var server = PingPongServer();
        var engine = CreateEngine(factory, stream, new FakeExchangeLogFactory(), server);
        var remote = new IPEndPoint(IPAddress.Loopback, 51234);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(new InMemoryConnection(["pi"u8.ToArray(), "ng"u8.ToArray()], remoteEndPoint: remote));
        var exchange = await server.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.AreSame(stream, exchange.Context.Events);
        CollectionAssert.AreEqual(
            new[]
            {
                "Started 1 http://127.0.0.1:8080/ http 127.0.0.1:51234",
                "Received 1 pi",
                "Received 1 ng",
                "Sent 1 pong",
                "Ended 1 4 4",
            },
            Describe(Drain(subscription)));
    }

    [TestMethod]
    public async Task ServeAsync_DatagramExchange_PublishesItsStart_ItsDatagramsInOrderFirstDatagramFirst_AndItsEnd()
    {
        var factory = new FakeListenerFactory();
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var subscription = stream.Subscribe(new SurlEventSubscriptionOptions { IncludeBytes = true });
        var server = new FakeDatagramProtocolServer(
            async (flow, context) =>
            {
                await flow.SendAsync("data"u8.ToArray(), context.CancellationToken);
                await flow.ReceiveAsync(context.CancellationToken);
            },
            "tftp");
        var engine = CreateEngine(factory, stream, new FakeExchangeLogFactory(), server);
        var flow = new FakeDatagramFlow("rrq"u8.ToArray());
        flow.Arrive("ack"u8.ToArray());
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Tftp], stop.Token);

        factory.DatagramListenerFor(Tftp).Open(flow);
        await server.NextFlowAsync();
        await flow.WhenDisposed.WaitAsync(Patience.Timeout);
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        CollectionAssert.AreEqual(
            new[]
            {
                "Started 1 tftp://127.0.0.1:40000/ tftp 127.0.0.1:51234",
                "Received 1 rrq",
                "Sent 1 data",
                "Received 1 ack",
                "Ended 1 6 4",
            },
            Describe(Drain(subscription)));
    }

    [TestMethod]
    public async Task ServeAsync_TwoListeners_PublishEachExchangeWithItsOwnListenUrl()
    {
        var factory = new FakeListenerFactory();
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var subscription = stream.Subscribe(
            new SurlEventSubscriptionOptions { Filter = surlEvent => surlEvent is ExchangeStartedEvent });
        var httpServer = new FakeConnectionProtocolServer("http");
        var gopherServer = new FakeConnectionProtocolServer("gopher");
        var engine = CreateEngine(factory, stream, new FakeExchangeLogFactory(), httpServer, gopherServer);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http, Gopher], stop.Token);

        factory.ListenerFor(Http).Connect(new InMemoryConnection([]));
        var httpExchange = await httpServer.NextExchangeAsync();
        factory.ListenerFor(Gopher).Connect(new InMemoryConnection([]));
        var gopherExchange = await gopherServer.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        var listenUrlsByExchange = Drain(subscription).Cast<ExchangeStartedEvent>()
            .ToDictionary(started => started.ExchangeId, started => (started.ListenUrl, started.Scheme));
        Assert.AreEqual(("http://127.0.0.1:8080/", "http"), listenUrlsByExchange[httpExchange.Context.ExchangeId]);
        Assert.AreEqual(("gopher://127.0.0.1:40000/", "gopher"), listenUrlsByExchange[gopherExchange.Context.ExchangeId]);
    }

    [TestMethod]
    public async Task ServeAsync_NoSubscriptionAskedForBytes_PublishesStartAndEndWithTheCountsButNoBytes()
    {
        var factory = new FakeListenerFactory();
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var subscription = stream.Subscribe(new SurlEventSubscriptionOptions());
        var server = PingPongServer();
        var engine = CreateEngine(factory, stream, new FakeExchangeLogFactory(), server);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(new InMemoryConnection(["ping"u8.ToArray()]));
        await server.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        CollectionAssert.AreEqual(
            new[] { "Started 1 http://127.0.0.1:8080/ http 127.0.0.1:50000", "Ended 1 4 4" },
            Describe(Drain(subscription)));
        Assert.AreEqual(2, stream.LastSequence);
    }

    [TestMethod]
    public async Task ServeAsync_NoPublisherGiven_GivesTheDiscardingPublisher_AndLogsExactlyAsWithAStream()
    {
        var withoutStream = await ServePingPongAsync(events: null);
        var withStream = await ServePingPongAsync(new SurlEventStream(new ManualTimeProvider()));

        Assert.AreSame(DiscardingSurlEventPublisher.Instance, withoutStream.Events);
        CollectionAssert.AreEqual(withoutStream.LogEntries, withStream.LogEntries);
    }

    [TestMethod]
    [DataRow("http", "::1", 8080, "http://[::1]:8080/")]
    [DataRow("http", "fe80::1%eth0", 8080, "http://[fe80::1%25eth0]:8080/")]
    [DataRow("dict", "localhost", 2628, "dict://localhost:2628/")]
    public void FormatListenUrl_UnboundListenUrl_UsesThePortAskedFor_AndBracketsAnIpv6Host(
        string scheme, string host, int port, string expected)
    {
        Assert.AreEqual(expected, ServingEngine.FormatListenUrl(new ListenUrl(scheme, host, port)));
    }

    [TestMethod]
    public void FormatListenUrl_BoundListenUrl_UsesTheBoundPort()
    {
        Assert.AreEqual("http://127.0.0.1:49731/", ServingEngine.FormatListenUrl(new ListenUrl("http", "127.0.0.1", 0).WithBoundPort(49731)));
    }

    private static async Task<(ISurlEventPublisher Events, string[] LogEntries)> ServePingPongAsync(ISurlEventPublisher? events)
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var server = PingPongServer();
        var engine = events is null
            ? CreateEngine(factory, new ManualTimeProvider(), logs, server)
            : CreateEngine(factory, events, logs, server);
        using var stop = new CancellationTokenSource();
        var served = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(new InMemoryConnection(["ping"u8.ToArray()]));
        var exchange = await server.NextExchangeAsync();
        await stop.CancelAsync();
        await served.WaitAsync(Patience.Timeout);

        var entries = logs.LogOf(1).Entries
            .Select(entry => $"{entry.Kind}:{Encoding.ASCII.GetString(entry.Bytes)}:{entry.Text}")
            .ToArray();

        return (exchange.Context.Events, entries);
    }

    private static FakeConnectionProtocolServer PingPongServer() =>
        new(
            async (connection, context) =>
            {
                var buffer = new byte[16];
                var count = 0;
                while (count < 4)
                {
                    count += await connection.ReadAsync(buffer.AsMemory(count), context.CancellationToken);
                }

                await connection.WriteAsync("pong"u8.ToArray(), context.CancellationToken);
            },
            "http");

    private static string[] Describe(IEnumerable<SurlEvent> events) =>
        [.. events.Select(surlEvent => surlEvent switch
        {
            ExchangeStartedEvent started => $"Started {started.ExchangeId} {started.ListenUrl} {started.Scheme} {started.Client}",
            ExchangeBytesEvent bytes => $"{bytes.Direction} {bytes.ExchangeId} {Encoding.ASCII.GetString(bytes.Bytes.Span)}",
            ExchangeEndedEvent ended => $"Ended {ended.ExchangeId} {ended.ReceivedByteCount} {ended.SentByteCount}",
            _ => surlEvent.ToString(),
        })];

    private static List<SurlEvent> Drain(SurlEventSubscription subscription)
    {
        var events = new List<SurlEvent>();

        while (subscription.TryRead(out var surlEvent))
        {
            events.Add(surlEvent!);
        }

        return events;
    }

    private static ServingEngine CreateEngine(
        FakeListenerFactory factory, ISurlEventPublisher events, FakeExchangeLogFactory logs, params IProtocolServer[] servers) =>
        new(factory, servers, logs, new ManualTimeProvider(), GracePeriod, ConnectionLimits.None,
            RefusingDataConnectionOpener.Instance, ExchangeLimits.Default, events);
}
