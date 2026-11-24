using Surl.Protocol.Abstractions;

namespace Surl.Core;

// An Accept-stage delay's hold ends when the client leaves, so its connection slot is free for
// the next client (ADR-0141, amendment 2, decisions 1, 3 and 4; BL-1521).
public sealed partial class ServingEngineTests
{
    private static readonly byte[] HeldRequest = "GET / HTTP/1.1\r\n\r\n"u8.ToArray();

    private static ServingEngine CreateOnePerAddressEngine(
        FakeListenerFactory factory, ManualTimeProvider time, FakeExchangeLogFactory logs, FakeConnectionProtocolServer server)
    {
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Delay) { DelayMilliseconds = 60000 });

        return new ServingEngine(
            factory, [server], logs, time, GracePeriod, ConnectionLimits.None with { MaxConnectionsPerAddress = 1 },
            RefusingDataConnectionOpener.Instance, ExchangeLimits.Default, DiscardingSurlEventPublisher.Instance, plan);
    }

    // The slot is released just after the connection is disposed, so a connection made in that gap is
    // refused (and disposed); connect again until one is held under the delay. A connection counts as
    // admitted only once its hold's timer exists (BL-1621), so advancing the clock cannot miss the hold
    // and a slow runner cannot make a not-yet-refused connection look admitted.
    private static async Task ConnectUntilAdmittedAsync(FakeListenerFactory factory, ManualTimeProvider time)
    {
        var deadline = DateTime.UtcNow + Patience.Timeout;

        while (DateTime.UtcNow < deadline)
        {
            var before = time.TimerCount;
            var next = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);
            factory.ListenerFor(Http).Connect(next);

            while (!next.Disposed && time.TimerCount == before && DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(5));
            }

            if (!next.Disposed && time.TimerCount > before)
            {
                return;
            }
        }

        Assert.Fail("No connection was admitted after the client left.");
    }

    [TestMethod]
    public async Task ServeAsync_ClientHalfClosesDuringAnAcceptDelay_EndsTheExchangeAtOnce_AndFreesItsSlot()
    {
        var factory = new FakeListenerFactory();
        var time = new ManualTimeProvider();
        var logs = new FakeExchangeLogFactory();
        var server = new FakeConnectionProtocolServer("http");
        var engine = CreateOnePerAddressEngine(factory, time, logs, server);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);
        var leaver = new InMemoryConnection([HeldRequest]);

        factory.ListenerFor(Http).Connect(leaver);
        await InMemoryConnectionWaits.WaitUntilDisposedAsync(leaver);
        await ConnectUntilAdmittedAsync(factory, time);
        time.Advance(TimeSpan.FromMilliseconds(60000));
        await server.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsFalse(leaver.Aborted);

        CollectionAssert.Contains(
            logs.LogOf(1).Notes.ToList(), "Exchange 1: the client closed the connection during the hold by fault rule f1.");
    }

    [TestMethod]
    public async Task ServeAsync_ClientResetsDuringAnAcceptDelay_AbortsTheExchangeAtOnce_AndFreesItsSlot()
    {
        var factory = new FakeListenerFactory();
        var time = new ManualTimeProvider();
        var logs = new FakeExchangeLogFactory();
        var server = new FakeConnectionProtocolServer("http");
        var engine = CreateOnePerAddressEngine(factory, time, logs, server);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);
        var leaver = new InMemoryConnection([], peerResetsWhenExhausted: true);

        factory.ListenerFor(Http).Connect(leaver);
        await InMemoryConnectionWaits.WaitUntilDisposedAsync(leaver);
        await ConnectUntilAdmittedAsync(factory, time);
        time.Advance(TimeSpan.FromMilliseconds(60000));
        await server.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsTrue(leaver.Aborted);

        CollectionAssert.Contains(
            logs.LogOf(1).Notes.ToList(), "Exchange 1: the client reset the connection during the hold by fault rule f1.");
    }

    [TestMethod]
    public async Task ServeAsync_ClientStaysDuringAnAcceptDelay_IsServedAfterItWithEveryByteItSent()
    {
        var factory = new FakeListenerFactory();
        var time = new ManualTimeProvider();
        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = new ReadingConnectionServer(received);
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Delay) { DelayMilliseconds = 60000 });
        var engine = new ServingEngine(
            factory, [server], new FakeExchangeLogFactory(), time, GracePeriod, ConnectionLimits.None,
            RefusingDataConnectionOpener.Instance, ExchangeLimits.Default, DiscardingSurlEventPublisher.Instance, plan);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(new InMemoryConnection([HeldRequest], peerHalfClosesWhenExhausted: false));
        await time.TimersCreated(1).WaitAsync(Patience.Timeout);
        var servedEarly = received.Task.IsCompleted;
        time.Advance(TimeSpan.FromMilliseconds(60000));
        var bytes = await received.Task.WaitAsync(Patience.Timeout);
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsFalse(servedEarly);
        CollectionAssert.AreEqual(HeldRequest, bytes);
    }

    [TestMethod]
    public async Task ServeAsync_AcceptDelayWhoseClientFloodsTheReadAheadBuffer_IsHeldForTheFullDelay()
    {
        var factory = new FakeListenerFactory();
        var time = new ManualTimeProvider();
        var server = new FakeConnectionProtocolServer("http");
        var engine = CreateOnePerAddressEngine(factory, time, new FakeExchangeLogFactory(), server);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(new InMemoryConnection([new byte[64 * 1024 + 1]], peerHalfClosesWhenExhausted: false));
        await time.TimersCreated(1).WaitAsync(Patience.Timeout);
        var servedEarly = server.TryTakeExchange(out _);
        time.Advance(TimeSpan.FromMilliseconds(60000));
        await server.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsFalse(servedEarly);
    }

    [TestMethod]
    public async Task ServeAsync_AcceptDelayWhoseWatchThrowsIOException_EndsTheExchangeAsAReset()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var server = new FakeConnectionProtocolServer("http");
        var engine = CreateOnePerAddressEngine(factory, new ManualTimeProvider(), logs, server);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);
        var inner = new InMemoryConnection([]);

        factory.ListenerFor(Http).Connect(new ThrowingWatchConnection(inner));
        await InMemoryConnectionWaits.WaitUntilDisposedAsync(inner);
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsTrue(inner.Aborted);
        CollectionAssert.Contains(
            logs.LogOf(1).Notes.ToList(), "Exchange 1: the client reset the connection during the hold by fault rule f1.");
    }

    [TestMethod]
    public async Task ServeAsync_AcceptDelayOutlastingTheMaximumExchangeDuration_CancelsTheExchangeDuringTheHold()
    {
        var factory = new FakeListenerFactory();
        var time = new ManualTimeProvider();
        var logs = new FakeExchangeLogFactory();
        var server = new FakeConnectionProtocolServer("http");
        var plan = new FaultPlan();
        plan.Add(new FaultRule("x", FaultKind.Delay) { DelayMilliseconds = 600000 });
        var engine = new ServingEngine(
            factory, [server], logs, time, GracePeriod, new ConnectionLimits(0, 0, TimeSpan.Zero, TimeSpan.FromSeconds(100)),
            RefusingDataConnectionOpener.Instance, ExchangeLimits.Default, DiscardingSurlEventPublisher.Instance, plan);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);

        factory.ListenerFor(Http).Connect(connection);
        await time.TimersCreated(1).WaitAsync(Patience.Timeout);
        time.Advance(TimeSpan.FromSeconds(100));
        await InMemoryConnectionWaits.WaitUntilDisposedAsync(connection);
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsFalse(server.TryTakeExchange(out _));
    }
    private sealed class ThrowingWatchConnection(InMemoryConnection inner) : IConnection
    {
        public System.Net.EndPoint LocalEndPoint => inner.LocalEndPoint;

        public System.Net.EndPoint RemoteEndPoint => inner.RemoteEndPoint;

        public TlsSession? TlsSession => inner.TlsSession;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            inner.ReadAsync(buffer, cancellationToken);

        public ValueTask<PeerWatchOutcome> WatchForPeerCloseAsync(int maxBufferedBytes, CancellationToken cancellationToken) =>
            throw new IOException("reset");

        public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) =>
            inner.WriteAsync(bytes, cancellationToken);

        public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => inner.CompleteWritesAsync(cancellationToken);

        public void Abort() => inner.Abort();

        public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => inner.UpgradeToTlsAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
    private sealed class ReadingConnectionServer(TaskCompletionSource<byte[]> received) : IConnectionProtocolServer
    {
        public IReadOnlyList<string> Schemes { get; } = ["http"];

        public async Task ServeAsync(IConnection connection, ExchangeContext context)
        {
            var buffer = new byte[HeldRequest.Length];
            var count = await connection.ReadAsync(buffer, context.CancellationToken);
            received.SetResult(buffer[..count]);
        }
    }
}
