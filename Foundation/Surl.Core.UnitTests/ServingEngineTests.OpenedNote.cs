using Surl.Protocol.Abstractions;
using static Surl.Core.InMemoryConnectionWaits;

namespace Surl.Core;

// The `Exchange <id> opened` note names the listen URL that accepted the exchange (ADR-0033 amendment 1, BL-1592).
public sealed partial class ServingEngineTests
{
    [TestMethod]
    public async Task ServeAsync_TwoListenersOfOneScheme_EachExchangeNoteNamesItsOwnListenUrl()
    {
        var first = new ListenUrl("http", "127.0.0.1", 8080);
        var second = new ListenUrl("http", "127.0.0.1", 8081);
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var engine = CreateEngine(factory, new ManualTimeProvider(), logs, new FakeConnectionProtocolServer("http"));
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([first, second], stop.Token);
        var onFirst = new InMemoryConnection([]);
        var onSecond = new InMemoryConnection([]);

        factory.ListenerFor(first).Connect(onFirst);
        await WaitUntilDisposedAsync(onFirst);
        factory.ListenerFor(second).Connect(onSecond);
        await WaitUntilDisposedAsync(onSecond);
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        StringAssert.StartsWith(logs.LogOf(1).Notes[0], "Exchange 1 opened: http on http://127.0.0.1:8080/ from ");
        StringAssert.StartsWith(logs.LogOf(2).Notes[0], "Exchange 2 opened: http on http://127.0.0.1:8081/ from ");
    }

    [TestMethod]
    public async Task ServeAsync_DatagramFlow_OpenedNoteNamesTheBoundListenUrl()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var engine = CreateEngine(factory, new ManualTimeProvider(), logs, new FakeDatagramProtocolServer("tftp"));
        var flow = new FakeDatagramFlow(ReadRequest);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Tftp], stop.Token);

        factory.DatagramListenerFor(Tftp).Open(flow);
        await flow.WhenDisposed.WaitAsync(Patience.Timeout);
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        StringAssert.StartsWith(logs.LogOf(1).Notes[0], "Exchange 1 opened: tftp on tftp://127.0.0.1:40000/ from ");
    }
}
