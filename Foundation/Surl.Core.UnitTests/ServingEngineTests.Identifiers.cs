using Surl.Protocol.Abstractions;

namespace Surl.Core;

// The engine stamps each exchange's identifier source (ADR-0124, decision 6; BL-997).
public sealed partial class ServingEngineTests
{
    [TestMethod]
    public async Task ServeAsync_NoIdentifierSourceGiven_GivesEveryExchangeTheCryptographicSource()
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

        Assert.AreSame(CryptographicIdentifierRandom.Instance, exchange.Context.Identifiers);
    }

    [TestMethod]
    public async Task ServeAsync_IdentifierSourceGiven_StampsEachExchangeWithTheSourceForItsExchangeId()
    {
        var factory = new FakeListenerFactory();
        var server = new FakeConnectionProtocolServer("http");
        var seeded = new SeededIdentifierRandom(42);
        var asked = new List<long>();
        var engine = new ServingEngine(
            factory, [server], new FakeExchangeLogFactory(), new ManualTimeProvider(), GracePeriod,
            ConnectionLimits.None, RefusingDataConnectionOpener.Instance, ExchangeLimits.Default,
            DiscardingSurlEventPublisher.Instance, EmptyFaultPlan.Instance)
        {
            IdentifiersForExchange = exchangeId =>
            {
                asked.Add(exchangeId);
                return seeded.ForExchange(exchangeId);
            },
        };
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(new InMemoryConnection([]));
        var exchange = await server.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        CollectionAssert.AreEqual(new long[] { 1 }, asked);
        var expected = new byte[16];
        var actual = new byte[16];
        seeded.ForExchange(1).NextBytes(expected);
        exchange.Context.Identifiers.NextBytes(actual);
        CollectionAssert.AreEqual(expected, actual);
    }
}
