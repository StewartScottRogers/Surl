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
        Assert.AreEqual(FaultClose.Reset, applied.Close);
    }

    [TestMethod]
    public async Task ServeAsync_GracefulCloseDropRuleWithoutAfterBytes_AbortsAndPublishesCloseReset()
    {
        var factory = new FakeListenerFactory();
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var subscription = stream.Subscribe(new SurlEventSubscriptionOptions { Filter = e => e is FaultAppliedEvent });
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Drop) { Close = FaultClose.Graceful });
        var server = new FakeConnectionProtocolServer("http");
        var engine = CreateFaultingEngine(factory, new ManualTimeProvider(), new FakeExchangeLogFactory(), stream, plan, server);
        var connection = new InMemoryConnection([]);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(connection);
        await InMemoryConnectionWaits.WaitUntilDisposedAsync(connection);
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsTrue(connection.Aborted);
        Assert.IsFalse(server.TryTakeExchange(out _));
        var applied = (FaultAppliedEvent)Drain(subscription).Single();
        Assert.AreEqual(FaultClose.Reset, applied.Close);
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

        factory.ListenerFor(Http).Connect(new InMemoryConnection([], peerHalfClosesWhenExhausted: false));
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
        Assert.IsNull(applied.Close);
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

        factory.ListenerFor(Http).Connect(new InMemoryConnection([], peerHalfClosesWhenExhausted: false));
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
        var applied = (FaultAppliedEvent)Drain(subscription).Single();
        Assert.AreEqual(FaultKind.Drop, applied.Kind);
        Assert.AreEqual(FaultClose.Reset, applied.Close);
    }

    [TestMethod]
    public async Task ServeAsync_GracefulDropRuleAfterBytes_CutsAtTheCountThenDisposesWithoutAbort()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var subscription = stream.Subscribe(new SurlEventSubscriptionOptions { Filter = e => e is FaultAppliedEvent });
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Drop) { AfterBytes = 6, Close = FaultClose.Graceful });
        var server = new FakeConnectionProtocolServer(
            async (connection, context) =>
            {
                await connection.WriteAsync("abcd"u8.ToArray(), context.CancellationToken);
                await Assert.ThrowsExactlyAsync<IOException>(
                    async () => await connection.WriteAsync("efgh"u8.ToArray(), context.CancellationToken));
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
        Assert.IsFalse(connection.Aborted);
        CollectionAssert.Contains(
            logs.LogOf(1).Notes.ToList(), "Exchange 1 cut after 6 bytes sent by fault rule f1, closing gracefully.");
        Assert.AreEqual(1, logs.LogOf(1).Notes.Count(note => note.Contains("cut after", StringComparison.Ordinal)));
        var applied = (FaultAppliedEvent)Drain(subscription).Single();
        Assert.AreEqual(FaultKind.Drop, applied.Kind);
        Assert.AreEqual(FaultClose.Graceful, applied.Close);
    }

    [TestMethod]
    public async Task ServeAsync_GracefulDropRuleInsideADataPayload_ClosesDataAndControlGracefully()
    {
        var (control, data, logs, events) = await RunDropAfterBytesOverControlAndDataAsync(
            12, async (connection, context) =>
            {
                await connection.WriteAsync("220 hi\r\n"u8.ToArray(), context.CancellationToken);
                await using var dataConnection = await context.DataConnections.ConnectActiveAsync(
                    connection.RemoteEndPoint, CurlDataPort, TimeSpan.FromSeconds(30), context.CancellationToken);
                await Assert.ThrowsExactlyAsync<IOException>(
                    async () => await dataConnection.WriteAsync("0123456789"u8.ToArray(), context.CancellationToken));
            },
            FaultClose.Graceful);

        Assert.AreEqual("0123", Encoding.ASCII.GetString(data.WrittenBytes));
        Assert.IsTrue(control.Disposed);
        Assert.IsTrue(data.Disposed);
        Assert.IsFalse(control.Aborted);
        Assert.IsFalse(data.Aborted);
        Assert.AreEqual(
            1, logs.LogOf(1).Notes.Count(note => note == "Exchange 1 cut after 12 bytes sent by fault rule f1, closing gracefully."));
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(FaultClose.Graceful, ((FaultAppliedEvent)events.Single()).Close);
    }

    [TestMethod]
    public async Task ServeAsync_GracefulDropRuleOnADatagramFlow_BehavesAsADrop()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var subscription = stream.Subscribe(new SurlEventSubscriptionOptions { Filter = e => e is FaultAppliedEvent });
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Drop) { Scheme = "tftp", AfterBytes = 5, Close = FaultClose.Graceful });
        var server = new FakeDatagramProtocolServer(
            async (flow, context) =>
            {
                await flow.SendAsync("data"u8.ToArray(), context.CancellationToken);
                await flow.SendAsync("more"u8.ToArray(), context.CancellationToken);
            },
            "tftp");
        var engine = CreateFaultingEngine(factory, new ManualTimeProvider(), logs, stream, plan, server);
        var flow = new FakeDatagramFlow("rrq"u8.ToArray());
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Tftp69], stop.Token);

        factory.DatagramListenerFor(Tftp69).Open(flow);
        await flow.WhenDisposed.WaitAsync(Patience.Timeout);
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        CollectionAssert.AreEqual(new[] { "data" }, flow.Sent.Select(sent => Encoding.ASCII.GetString(sent)).ToArray());
        CollectionAssert.Contains(logs.LogOf(1).Notes.ToList(), "Exchange 1 dropped after 5 bytes sent by fault rule f1.");
        Assert.AreEqual(FaultClose.Reset, ((FaultAppliedEvent)Drain(subscription).Single()).Close);
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
    public async Task ServeAsync_DropRuleAfterBytesInsideADataPayload_CutsTheDataConnectionThere_AndAbortsTheControlConnectionToo()
    {
        var (control, data, logs, events) = await RunDropAfterBytesOverControlAndDataAsync(
            12, async (connection, context) =>
            {
                await connection.WriteAsync("220 hi\r\n"u8.ToArray(), context.CancellationToken);
                await using var dataConnection = await context.DataConnections.ConnectActiveAsync(
                    connection.RemoteEndPoint, CurlDataPort, TimeSpan.FromSeconds(30), context.CancellationToken);
                await Assert.ThrowsExactlyAsync<IOException>(
                    async () => await dataConnection.WriteAsync("0123456789"u8.ToArray(), context.CancellationToken));
                await Assert.ThrowsExactlyAsync<IOException>(
                    async () => await connection.WriteAsync("226 done\r\n"u8.ToArray(), context.CancellationToken));
            });

        Assert.AreEqual("220 hi\r\n", Encoding.ASCII.GetString(control.WrittenBytes));
        Assert.AreEqual("0123", Encoding.ASCII.GetString(data.WrittenBytes));
        Assert.IsTrue(control.Aborted);
        Assert.IsTrue(data.Aborted);
        Assert.AreEqual(1, logs.LogOf(1).Notes.Count(note => note == "Exchange 1 dropped after 12 bytes sent by fault rule f1."));
        Assert.AreEqual(FaultKind.Drop, ((FaultAppliedEvent)events.Single()).Kind);
    }

    [TestMethod]
    public async Task ServeAsync_DropRuleReachedExactlyAtTheEndOfADataPayload_PublishesOneEventAndNote_AndAbortsBoth()
    {
        var (control, data, logs, events) = await RunDropAfterBytesOverControlAndDataAsync(
            18, async (connection, context) =>
            {
                await connection.WriteAsync("220 hi\r\n"u8.ToArray(), context.CancellationToken);
                await using var dataConnection = await context.DataConnections.ConnectActiveAsync(
                    connection.RemoteEndPoint, CurlDataPort, TimeSpan.FromSeconds(30), context.CancellationToken);
                await dataConnection.WriteAsync("0123456789"u8.ToArray(), context.CancellationToken);
                await Assert.ThrowsExactlyAsync<IOException>(
                    async () => await dataConnection.ReadAsync(new byte[1], context.CancellationToken));
                await Assert.ThrowsExactlyAsync<IOException>(
                    async () => await connection.PeekByteAsync(TimeSpan.Zero, context.CancellationToken));
            });

        Assert.AreEqual("0123456789", Encoding.ASCII.GetString(data.WrittenBytes));
        Assert.IsTrue(control.Aborted);
        Assert.IsTrue(data.Aborted);
        Assert.AreEqual(1, logs.LogOf(1).Notes.Count(note => note.Contains("dropped", StringComparison.Ordinal)));
        Assert.AreEqual(1, events.Count);
        var applied = (FaultAppliedEvent)events.Single();
        Assert.AreEqual(FaultKind.Drop, applied.Kind);
        Assert.AreEqual(FaultStage.Accept, applied.Stage);
    }

    [TestMethod]
    public async Task ServeAsync_DropRuleAfterMoreBytesThanControlAndDataSend_EndsTheExchangeAsUsual()
    {
        var (control, data, logs, events) = await RunDropAfterBytesOverControlAndDataAsync(
            100, async (connection, context) =>
            {
                await connection.WriteAsync("220 hi\r\n"u8.ToArray(), context.CancellationToken);
                await using var dataConnection = await context.DataConnections.ConnectActiveAsync(
                    connection.RemoteEndPoint, CurlDataPort, TimeSpan.FromSeconds(30), context.CancellationToken);
                await dataConnection.WriteAsync("0123456789"u8.ToArray(), context.CancellationToken);
            });

        Assert.AreEqual("0123456789", Encoding.ASCII.GetString(data.WrittenBytes));
        Assert.IsFalse(control.Aborted);
        Assert.IsFalse(data.Aborted);
        Assert.IsFalse(logs.LogOf(1).Notes.Any(note => note.Contains("dropped", StringComparison.Ordinal)));
        Assert.AreEqual(0, events.Count);
    }

    private static async Task<(InMemoryConnection Control, InMemoryConnection Data, FakeExchangeLogFactory Logs, List<SurlEvent> Events)>
        RunDropAfterBytesOverControlAndDataAsync(
            long afterBytes, Func<IConnection, ExchangeContext, Task> serve, FaultClose close = FaultClose.Reset)
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var subscription = stream.Subscribe(new SurlEventSubscriptionOptions { Filter = e => e is FaultAppliedEvent });
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Drop) { AfterBytes = afterBytes, Close = close });
        var data = new InMemoryConnection([], remoteEndPoint: CurlDataPort, peerHalfClosesWhenExhausted: false);
        var opener = new InMemoryDataConnections().ScriptActiveConnection(data);
        var engine = new ServingEngine(
            factory, [new FakeConnectionProtocolServer(serve, "ftp")], logs, new ManualTimeProvider(), GracePeriod,
            ConnectionLimits.None, opener, ExchangeLimits.Default, stream, plan);
        var control = new InMemoryConnection([]);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Ftp], stop.Token);

        factory.ListenerFor(Ftp).Connect(control);
        await InMemoryConnectionWaits.WaitUntilDisposedAsync(control);
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        return (control, data, logs, [.. Drain(subscription)]);
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
        var applied = (FaultAppliedEvent)Drain(subscription).Single();
        Assert.AreEqual("tftp://127.0.0.1:40000/", applied.Target);
        Assert.AreEqual(FaultClose.Reset, applied.Close);
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

    // A server holding a command at a command-stage delay pauses the exchange's own idle clock
    // through its context (ADR-0141): idle past the timeout while paused, not cancelled; restarted,
    // the idle timeout counts afresh.
    [TestMethod]
    public async Task ServeAsync_ServerPausesItsContextsIdleClock_TheIdleTimeoutWaitsUntilItRestarts()
    {
        var factory = new FakeListenerFactory();
        var time = new ManualTimeProvider();
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = new FakeConnectionProtocolServer(
            (_, context) =>
            {
                context.IdleClock.Pause();
                paused.SetResult();
                return release.Task;
            },
            "http");
        var limits = ConnectionLimits.None with { IdleTimeout = TimeSpan.FromSeconds(1) };
        var engine = new ServingEngine(
            factory, [server], new FakeExchangeLogFactory(), time, GracePeriod, limits,
            RefusingDataConnectionOpener.Instance, ExchangeLimits.Default, DiscardingSurlEventPublisher.Instance, new FaultPlan());
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(new InMemoryConnection([]));
        var exchange = await server.NextExchangeAsync();
        await paused.Task.WaitAsync(Patience.Timeout);
        time.Advance(TimeSpan.FromSeconds(5));
        var cancelledWhilePaused = exchange.Context.CancellationToken.IsCancellationRequested;
        exchange.Context.IdleClock.Restart();
        time.Advance(TimeSpan.FromSeconds(1));
        var cancelledAfterTheRestart = exchange.Context.CancellationToken.IsCancellationRequested;
        release.SetResult();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsFalse(cancelledWhilePaused);
        Assert.IsTrue(cancelledAfterTheRestart);
    }

    // The HTTP, RTSP and WebSocket servers hold a request through CommandStageFaults (BL-902,
    // ADR-0141 amendment 1): a command-stage delay longer than the idle timeout leaves the exchange
    // running, and the held command is then served.
    [TestMethod]
    public async Task ServeAsync_CommandStageDelayLongerThanTheIdleTimeout_IdleTimeoutDoesNotFireDuringIt()
    {
        var factory = new FakeListenerFactory();
        var time = new ManualTimeProvider();
        var plan = new ScriptedFaultPlan(new FaultRule("c1", FaultKind.Delay) { Stage = FaultStage.Command, DelayMilliseconds = 5000 });
        var held = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = new FakeConnectionProtocolServer(
            async (_, context) =>
            {
                await CommandStageFaults.FindAndApplyAsync(context, new FaultPoint(FaultStage.Command) { Command = "GET" }, "/a");
                held.SetResult(context.CancellationToken.IsCancellationRequested);
            },
            "http");
        var limits = ConnectionLimits.None with { IdleTimeout = TimeSpan.FromSeconds(1) };
        var engine = new ServingEngine(
            factory, [server], new FakeExchangeLogFactory(), time, GracePeriod, limits,
            RefusingDataConnectionOpener.Instance, ExchangeLimits.Default, DiscardingSurlEventPublisher.Instance, plan);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(new InMemoryConnection([]));
        await time.TimersCreated(2).WaitAsync(Patience.Timeout);
        time.Advance(TimeSpan.FromSeconds(5));
        var cancelledAfterTheHold = await held.Task.WaitAsync(Patience.Timeout);
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsFalse(cancelledAfterTheHold);
    }

    // ADR-0181: an Accept-stage rule's times count is spent by any connection, so a readiness probe that
    // opens and closes without a byte spends it, and the first real request is served untouched.
    [TestMethod]
    public async Task ServeAsync_ConnectAndCloseThenARealRequest_AgainstAnAcceptDropRuleWithTimesOne_TheProbeSpendsItAndTheRequestIsServed()
    {
        var factory = new FakeListenerFactory();
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Drop) { Times = 1 });
        var server = new FakeConnectionProtocolServer("http");
        var engine = CreateFaultingEngine(
            factory, new ManualTimeProvider(), new FakeExchangeLogFactory(), DiscardingSurlEventPublisher.Instance, plan, server);
        var probe = new InMemoryConnection([]);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(probe);
        await InMemoryConnectionWaits.WaitUntilDisposedAsync(probe);
        factory.ListenerFor(Http).Connect(new InMemoryConnection(["GET / HTTP/1.1\r\n\r\n"u8.ToArray()]));
        await server.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsTrue(probe.Aborted);
        Assert.AreEqual(0, plan.Rules.Single().Times);
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
