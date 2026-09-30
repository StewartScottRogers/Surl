using Surl.Protocol.Abstractions;

namespace Surl.Core;

// The exchange limits the engine hands every exchange's ExchangeContext (ADR-0006, sections 1 and 6).
public sealed partial class ServingEngineTests
{
    private static readonly ExchangeLimits OperatorLimits = new(
        TimeSpan.FromSeconds(7),
        maxRequestHeadBytes: 2048,
        maxLineBytes: 100,
        maxMessageBytes: 4096,
        maxUploadBytes: 10);

    [TestMethod]
    public void Constructor_NullExchangeLimits_Throws()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new ServingEngine(
            new FakeListenerFactory(), [], new FakeExchangeLogFactory(), new ManualTimeProvider(), GracePeriod,
            ConnectionLimits.Default, RefusingDataConnectionOpener.Instance, null!));

        Assert.AreEqual("exchangeLimits", exception.ParamName);
    }

    [TestMethod]
    public async Task ServeAsync_EngineGivenNoExchangeLimits_HandsEachExchangeTheDefaultLimits()
    {
        var factory = new FakeListenerFactory();
        var server = new FakeConnectionProtocolServer("http");
        var engine = new ServingEngine(
            factory, [server], new FakeExchangeLogFactory(), new ManualTimeProvider(), GracePeriod,
            ConnectionLimits.None, RefusingDataConnectionOpener.Instance);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(new InMemoryConnection([]));
        var exchange = await server.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.AreSame(ExchangeLimits.Default, exchange.Context.Limits);
    }

    [TestMethod]
    public async Task ServeAsync_EngineGivenExchangeLimits_HandsEachExchangeThoseLimits()
    {
        var factory = new FakeListenerFactory();
        var server = new FakeConnectionProtocolServer("http");
        var engine = new ServingEngine(
            factory, [server], new FakeExchangeLogFactory(), new ManualTimeProvider(), GracePeriod,
            ConnectionLimits.None, RefusingDataConnectionOpener.Instance, OperatorLimits);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(new InMemoryConnection([]));
        var first = await server.NextExchangeAsync();
        factory.ListenerFor(Http).Connect(new InMemoryConnection([]));
        var second = await server.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.AreEqual(OperatorLimits, first.Context.Limits);
        Assert.AreEqual(OperatorLimits, second.Context.Limits);
    }

    [TestMethod]
    public async Task ServeAsync_NoHandshakeWithinTheGivenHeadTimeout_ClosesTheConnectionUnserved()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var time = new ManualTimeProvider();
        var server = new FakeConnectionProtocolServer("https");
        var engine = new ServingEngine(
            factory, [server], logs, time, TimeSpan.Zero, ConnectionLimits.None, RefusingDataConnectionOpener.Instance, OperatorLimits);
        var connection = new PendingHandshakeConnection();
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([HttpsListenUrl], stop.Token);

        factory.ListenerFor(HttpsListenUrl).Connect(connection);
        await connection.HandshakeStarted.WaitAsync(Patience.Timeout);
        await time.FirstTimerCreated.WaitAsync(Patience.Timeout);
        time.Advance(OperatorLimits.HeadTimeout - TimeSpan.FromTicks(1));

        Assert.IsFalse(connection.HandshakeCancelled.IsCompleted);

        time.Advance(TimeSpan.FromTicks(1));
        await connection.Disposed.WaitAsync(Patience.Timeout);
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsTrue(connection.HandshakeCancelled.IsCompleted);
        Assert.IsFalse(server.TryTakeExchange(out _));
        CollectionAssert.Contains(
            logs.LogOf(1).Notes.ToList(), "TLS handshake failed: no handshake within the head timeout of 7 s");
    }
}
