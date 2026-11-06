using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

// The engine applies the fault plan's Accept-stage delay and drop rules (ADR-0124, decision 4;
// BL-792).
public sealed partial class ServingEngineTests
{
    private static readonly ListenUrl Tftp69 = new("tftp", "127.0.0.1", 40000);

    [TestMethod]
    public void Constructor_NoFaultPlan_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ServingEngine(
            new FakeListenerFactory(), [], new FakeExchangeLogFactory(), new ManualTimeProvider(), GracePeriod,
            ConnectionLimits.None, RefusingDataConnectionOpener.Instance, ExchangeLimits.Default,
            DiscardingSurlEventPublisher.Instance, null!));
    }

    [TestMethod]
    public async Task ServeAsync_NoFaultPlanGiven_GivesEveryExchangeTheEmptyPlan()
    {
        var factory = new FakeListenerFactory();
        var server = new FakeConnectionProtocolServer("http");
        var engine = CreateEngine(factory, new ManualTimeProvider(), new FakeExchangeLogFactory(), server);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(new InMemoryConnection([]));
        var exchange = await server.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.AreSame(EmptyFaultPlan.Instance, exchange.Context.Faults);
    }

    [TestMethod]
    public async Task ServeAsync_DropRuleOnOneListener_ClosesItsConnectionBeforeTheServerReadsAByte_WhileAnotherListenerServes()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var subscription = stream.Subscribe(new SurlEventSubscriptionOptions { Filter = e => e is FaultAppliedEvent });
        var plan = new FaultPlan();
        var rule = plan.Add(new FaultRule("x", FaultKind.Drop) { Listener = "http://127.0.0.1:8080" });
        var httpServer = new FakeConnectionProtocolServer("http");
        var gopherServer = new FakeConnectionProtocolServer("gopher");
        var engine = CreateFaultingEngine(factory, new ManualTimeProvider(), logs, stream, plan, httpServer, gopherServer);
        var dropped = new InMemoryConnection(["GET / HTTP/1.1\r\n\r\n"u8.ToArray()]);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http, Gopher], stop.Token);

        factory.ListenerFor(Http).Connect(dropped);
        await InMemoryConnectionWaits.WaitUntilDisposedAsync(dropped);
        factory.ListenerFor(Gopher).Connect(new InMemoryConnection([]));
        var gopherExchange = await gopherServer.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsTrue(dropped.Aborted);
        Assert.IsFalse(httpServer.TryTakeExchange(out _));
        Assert.AreSame(plan, gopherExchange.Context.Faults);
        CollectionAssert.Contains(logs.LogOf(1).Notes.ToList(), "Exchange 1 dropped before any byte by fault rule f1.");
        Assert.IsFalse(logs.LogOf(1).Entries.Any(entry => entry.Bytes.Length > 0));
        var applied = (FaultAppliedEvent)Drain(subscription).Single();
        Assert.AreEqual((rule.Id, FaultKind.Drop, FaultStage.Accept, 1L, "http://127.0.0.1:8080/"),
            (applied.RuleId, applied.Kind, applied.Stage, applied.ExchangeId, applied.Target));
    }

    [TestMethod]
    public async Task ServeAsync_DropRuleOnAnImplicitTlsListener_ClosesTheConnectionBeforeTheHandshake()
    {
        var factory = new FakeListenerFactory();
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Drop) { Scheme = "https" });
        var server = new FakeConnectionProtocolServer("https");
        var engine = CreateFaultingEngine(
            factory, new ManualTimeProvider(), new FakeExchangeLogFactory(), DiscardingSurlEventPublisher.Instance, plan, server);
        var connection = new InMemoryConnection([]);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([HttpsListenUrl], stop.Token);

        factory.ListenerFor(HttpsListenUrl).Connect(connection);
        await InMemoryConnectionWaits.WaitUntilDisposedAsync(connection);
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsTrue(connection.Aborted);
        Assert.IsFalse(connection.UpgradeRequested);
        Assert.IsFalse(server.TryTakeExchange(out _));
    }

    [TestMethod]
    public async Task ServeAsync_DelayRule_HoldsTheExchangeUntilTheClockAdvancesByTheDelay_AndPublishesIt()
    {
        var factory = new FakeListenerFactory();
        var time = new ManualTimeProvider();
        var logs = new FakeExchangeLogFactory();
        var stream = new SurlEventStream(time);
        await using var subscription = stream.Subscribe(new SurlEventSubscriptionOptions { Filter = e => e is FaultAppliedEvent });
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Delay) { DelayMilliseconds = 500, Scheme = "http" });
        var server = new FakeConnectionProtocolServer("http");
        var engine = CreateFaultingEngine(factory, time, logs, stream, plan, server);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(new InMemoryConnection([]));
        await time.TimersCreated(1).WaitAsync(Patience.Timeout);
        time.Advance(TimeSpan.FromMilliseconds(499));
        var servedEarly = server.TryTakeExchange(out _);
        time.Advance(TimeSpan.FromMilliseconds(1));
        var exchange = await server.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsFalse(servedEarly);
        Assert.IsFalse(exchange.Context.CancellationToken.IsCancellationRequested);
        CollectionAssert.Contains(logs.LogOf(1).Notes.ToList(), "Exchange 1 held for 0.5 s by fault rule f1.");
        var applied = (FaultAppliedEvent)Drain(subscription).Single();
        Assert.AreEqual((FaultKind.Delay, FaultStage.Accept), (applied.Kind, applied.Stage));
    }

    [TestMethod]
    public async Task ServeAsync_DelayLongerThanTheIdleTimeout_DoesNotIdleTheExchangeOut()
    {
        var factory = new FakeListenerFactory();
        var time = new ManualTimeProvider();
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Delay) { DelayMilliseconds = 5000 });
        var server = new FakeConnectionProtocolServer("http");
        var limits = ConnectionLimits.None with { IdleTimeout = TimeSpan.FromSeconds(1) };
        var engine = new ServingEngine(
            factory, [server], new FakeExchangeLogFactory(), time, GracePeriod, limits,
            RefusingDataConnectionOpener.Instance, ExchangeLimits.Default, DiscardingSurlEventPublisher.Instance, plan);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(new InMemoryConnection([]));
        await time.TimersCreated(2).WaitAsync(Patience.Timeout);
        time.Advance(TimeSpan.FromSeconds(5));
        var exchange = await server.NextExchangeAsync();
        var cancelledOnArrival = exchange.Context.CancellationToken.IsCancellationRequested;
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsFalse(cancelledOnArrival);
    }

    [TestMethod]
    public async Task ServeAsync_DelayRuleWithoutMilliseconds_FromAnotherPlan_ServesAtOnce()
    {
        var factory = new FakeListenerFactory();
        var plan = new ScriptedFaultPlan(new FaultRule("s1", FaultKind.Delay));
        var server = new FakeConnectionProtocolServer("http");
        var engine = CreateFaultingEngine(
            factory, new ManualTimeProvider(), new FakeExchangeLogFactory(), DiscardingSurlEventPublisher.Instance, plan, server);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(new InMemoryConnection([]));
        var exchange = await server.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        var point = plan.Points.Single();
        Assert.AreEqual((FaultStage.Accept, 1L, "http://127.0.0.1:8080/", "http"), (point.Stage, point.ExchangeId, point.ListenUrl, point.Scheme));
        Assert.AreSame(plan, exchange.Context.Faults);
    }

    [TestMethod]
    public async Task ServeAsync_RuleOfAKindTheEngineDoesNotApply_IsIgnoredAtAccept()
    {
        var factory = new FakeListenerFactory();
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var subscription = stream.Subscribe(new SurlEventSubscriptionOptions { Filter = e => e is FaultAppliedEvent });
        var plan = new ScriptedFaultPlan(new FaultRule("s1", FaultKind.RefuseLogin));
        var server = new FakeConnectionProtocolServer("http");
        var engine = CreateFaultingEngine(factory, new ManualTimeProvider(), new FakeExchangeLogFactory(), stream, plan, server);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(new InMemoryConnection([]));
        await server.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsEmpty(Drain(subscription));
    }

    [TestMethod]
    public async Task ServeAsync_DropRuleAfterBytes_LetsTheServerSendThatManyBytesThenDropsTheConnection()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var subscription = stream.Subscribe(new SurlEventSubscriptionOptions { Filter = e => e is FaultAppliedEvent });
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Drop) { AfterBytes = 6 });
        var server = new FakeConnectionProtocolServer(
            async (connection, context) =>
            {
                await connection.WriteAsync("abcd"u8.ToArray(), context.CancellationToken);
                await connection.WriteAsync("efgh"u8.ToArray(), context.CancellationToken);
            },
            "http");
        var engine = CreateFaultingEngine(factory, new ManualTimeProvider(), logs, stream, plan, server);
        var connection = new InMemoryConnection([]);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(connection);
        await InMemoryConnectionWaits.WaitUntilDisposedAsync(connection);
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.AreEqual("abcdef", Encoding.ASCII.GetString(connection.WrittenBytes));
        Assert.IsTrue(connection.Aborted);
        CollectionAssert.Contains(logs.LogOf(1).Notes.ToList(), "Exchange 1 dropped after 6 bytes sent by fault rule f1.");
        Assert.AreEqual(FaultKind.Drop, ((FaultAppliedEvent)Drain(subscription).Single()).Kind);
    }

    [TestMethod]
    public async Task ServeAsync_DropRuleAfterMoreBytesThanTheServerSends_EndsTheExchangeAsUsual()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Drop) { AfterBytes = 100 });
        var server = PingPongServer();
        var engine = CreateFaultingEngine(
            factory, new ManualTimeProvider(), logs, DiscardingSurlEventPublisher.Instance, plan, server);
        var connection = new InMemoryConnection(["ping"u8.ToArray()]);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(connection);
        await InMemoryConnectionWaits.WaitUntilDisposedAsync(connection);
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.AreEqual("pong", Encoding.ASCII.GetString(connection.WrittenBytes));
        Assert.IsFalse(connection.Aborted);
        Assert.IsFalse(logs.LogOf(1).Notes.Any(note => note.Contains("dropped", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_DatagramFlowUnderADropRule_GetsNoAnswer()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var subscription = stream.Subscribe(new SurlEventSubscriptionOptions { Filter = e => e is FaultAppliedEvent });
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Drop) { Scheme = "tftp" });
        var server = new FakeDatagramProtocolServer(
            (flow, context) => flow.SendAsync("data"u8.ToArray(), context.CancellationToken).AsTask(), "tftp");
        var engine = CreateFaultingEngine(factory, new ManualTimeProvider(), logs, stream, plan, server);
        var flow = new FakeDatagramFlow("rrq"u8.ToArray());
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Tftp69], stop.Token);

        factory.DatagramListenerFor(Tftp69).Open(flow);
        await flow.WhenDisposed.WaitAsync(Patience.Timeout);
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsEmpty(flow.Sent);
        Assert.IsFalse(server.TryTakeFlow(out _));
        CollectionAssert.Contains(logs.LogOf(1).Notes.ToList(), "Exchange 1 dropped before any byte by fault rule f1.");
        Assert.AreEqual("tftp://127.0.0.1:40000/", ((FaultAppliedEvent)Drain(subscription).Single()).Target);
    }

    [TestMethod]
    public async Task ServeAsync_DatagramFlowUnderADropRuleAfterBytes_StopsAnsweringPastTheCount()
    {
        var factory = new FakeListenerFactory();
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Drop) { Scheme = "tftp", AfterBytes = 5 });
        var server = new FakeDatagramProtocolServer(
            async (flow, context) =>
            {
                await flow.SendAsync("data"u8.ToArray(), context.CancellationToken);
                await flow.SendAsync("more"u8.ToArray(), context.CancellationToken);
                await flow.SendAsync("x"u8.ToArray(), context.CancellationToken);
            },
            "tftp");
        var engine = CreateFaultingEngine(
            factory, new ManualTimeProvider(), new FakeExchangeLogFactory(), DiscardingSurlEventPublisher.Instance, plan, server);
        var flow = new FakeDatagramFlow("rrq"u8.ToArray());
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Tftp69], stop.Token);

        factory.DatagramListenerFor(Tftp69).Open(flow);
        await flow.WhenDisposed.WaitAsync(Patience.Timeout);
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        CollectionAssert.AreEqual(new[] { "data" }, flow.Sent.Select(sent => Encoding.ASCII.GetString(sent)).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_DatagramFlowUnderADelayRule_IsServedOnceTheClockAdvances()
    {
        var factory = new FakeListenerFactory();
        var time = new ManualTimeProvider();
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Delay) { DelayMilliseconds = 100 });
        var server = new FakeDatagramProtocolServer("tftp");
        var engine = CreateFaultingEngine(
            factory, time, new FakeExchangeLogFactory(), DiscardingSurlEventPublisher.Instance, plan, server);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Tftp69], stop.Token);

        factory.DatagramListenerFor(Tftp69).Open(new FakeDatagramFlow("rrq"u8.ToArray()));
        await time.TimersCreated(1).WaitAsync(Patience.Timeout);
        var servedEarly = server.TryTakeFlow(out _);
        time.Advance(TimeSpan.FromMilliseconds(100));
        var served = await server.NextFlowAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsFalse(servedEarly);
        Assert.AreSame(plan, served.Context.Faults);
    }

    private static ServingEngine CreateFaultingEngine(
        FakeListenerFactory factory,
        TimeProvider time,
        FakeExchangeLogFactory logs,
        ISurlEventPublisher events,
        IFaultPlan faults,
        params IProtocolServer[] servers) =>
        new(factory, servers, logs, time, GracePeriod, ConnectionLimits.None,
            RefusingDataConnectionOpener.Instance, ExchangeLimits.Default, events, faults);
}
