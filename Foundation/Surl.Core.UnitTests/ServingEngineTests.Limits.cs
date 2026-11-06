using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Core.InMemoryConnectionWaits;

namespace Surl.Core;

// The connection limits and exchange timeouts ADR-0006 assigns to the serving engine.
public sealed partial class ServingEngineTests
{
    private static readonly TimeSpan Tick = TimeSpan.FromTicks(1);
    private static readonly ListenUrl Https = new("https", "127.0.0.1", 8443);
    private static readonly ConnectionLimits OneConnection = new(1, 0, TimeSpan.Zero, TimeSpan.Zero);

    [TestMethod]
    public void Constructor_NullConnectionLimits_Throws()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new ServingEngine(
            new FakeListenerFactory(), [], new FakeExchangeLogFactory(), new ManualTimeProvider(), GracePeriod, null!));

        Assert.AreEqual("connectionLimits", exception.ParamName);
    }

    [TestMethod]
    public async Task ServeAsync_DefaultLimits_RefuseThe1025thConnection_AndLeaveTheOthersRunning()
    {
        using var harness = new Harness(limits: null, "http");
        var admitted = Enumerable.Range(0, 1024).Select(peer => HeldConnection(PeerAddress(peer))).ToArray();

        foreach (var connection in admitted)
        {
            harness.Listener.Connect(connection);
        }

        var exchanges = await harness.NextExchangesAsync(1024);
        var refused = HeldConnection(PeerAddress(5000));
        harness.Listener.Connect(refused);
        var refusal = await harness.Server.NextRefusalAsync();
        await WaitUntilDisposedAsync(refused);

        Assert.AreEqual(ConnectionRefusal.TooManyConnections, refusal.Refusal);
        Assert.AreEqual("refused:TooManyConnections", Encoding.ASCII.GetString(refused.WrittenBytes));
        Assert.IsFalse(refused.Aborted);
        Assert.IsFalse(harness.Server.Inner.TryTakeExchange(out _));
        Assert.IsFalse(exchanges.Any(exchange => exchange.Context.CancellationToken.IsCancellationRequested));
        Assert.IsFalse(admitted.Any(connection => connection.Disposed));
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task ServeAsync_DefaultLimits_RefuseThe101stConnectionFromOneAddress_AndAdmitAnotherAddress()
    {
        using var harness = new Harness(limits: null, "http");
        var sameAddress = IPAddress.Parse("192.0.2.1");
        var admitted = Enumerable.Range(0, 100).Select(port => HeldConnection(new IPEndPoint(sameAddress, 50000 + port))).ToArray();

        foreach (var connection in admitted)
        {
            harness.Listener.Connect(connection);
        }

        var exchanges = await harness.NextExchangesAsync(100);
        var refused = HeldConnection(new IPEndPoint(sameAddress, 60000));
        harness.Listener.Connect(refused);
        var refusal = await harness.Server.NextRefusalAsync();
        await WaitUntilDisposedAsync(refused);
        harness.Listener.Connect(HeldConnection(new IPEndPoint(IPAddress.Parse("192.0.2.2"), 50000)));
        var otherAddress = await harness.Server.Inner.NextExchangeAsync();

        Assert.AreEqual(ConnectionRefusal.TooManyConnectionsFromAddress, refusal.Refusal);
        Assert.AreEqual("refused:TooManyConnectionsFromAddress", Encoding.ASCII.GetString(refused.WrittenBytes));
        Assert.IsFalse(refused.Aborted);
        Assert.AreEqual(101, otherAddress.Context.ExchangeId);
        Assert.IsFalse(exchanges.Any(exchange => exchange.Context.CancellationToken.IsCancellationRequested));
        Assert.IsFalse(admitted.Any(connection => connection.Disposed));
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task ServeAsync_ConnectionAfterAnExchangeEnds_IsAdmittedWithTheNextExchangeId()
    {
        using var harness = new Harness(OneConnection, "http");
        var first = HeldConnection(PeerAddress(1));
        harness.Listener.Connect(first);
        await harness.Server.Inner.NextExchangeAsync();
        var refused = HeldConnection(PeerAddress(2));
        harness.Listener.Connect(refused);
        await harness.Server.NextRefusalAsync();
        await WaitUntilDisposedAsync(refused);

        harness.EndExchange(1);
        await WaitUntilDisposedAsync(first);
        var next = await harness.ConnectUntilAdmittedAsync(PeerAddress(3));

        Assert.AreEqual(2, next.Context.ExchangeId);
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task ServeAsync_ServerThatWritesNoRefusal_HasTheConnectionClosedWithNoBytes()
    {
        var factory = new FakeListenerFactory();
        var release = new TaskCompletionSource();
        var server = new FakeConnectionProtocolServer((_, context) => release.Task.WaitAsync(context.CancellationToken), "http");
        var engine = new ServingEngine(factory, [server], new FakeExchangeLogFactory(), new ManualTimeProvider(), GracePeriod, OneConnection);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);
        factory.ListenerFor(Http).Connect(HeldConnection(PeerAddress(1)));
        await server.NextExchangeAsync();

        var refused = HeldConnection(PeerAddress(2));
        factory.ListenerFor(Http).Connect(refused);
        await WaitUntilDisposedAsync(refused);

        Assert.IsEmpty(refused.WrittenBytes);
        Assert.IsFalse(refused.Aborted);
        Assert.IsFalse(server.TryTakeExchange(out _));
        release.SetResult();
        await stop.CancelAsync();
        Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience.Timeout));
    }

    [TestMethod]
    [DataRow(ConnectionRefusal.TooManyConnections, "192.0.2.2", "Refused a connection from 192.0.2.2:50001: past --max-connections 1.")]
    [DataRow(ConnectionRefusal.TooManyConnectionsFromAddress, "192.0.2.1", "Refused a connection from 192.0.2.1:50001: past --max-connections-per-address 1.")]
    public async Task ServeAsync_ConnectionPastALimit_LogsOneNoteOutsideAnyExchange_NamingTheRemoteEndPointAndTheLimit(
        ConnectionRefusal expectedRefusal, string refusedAddress, string expectedNote)
    {
        var limits = expectedRefusal == ConnectionRefusal.TooManyConnections
            ? new ConnectionLimits(1, 0, TimeSpan.Zero, TimeSpan.Zero)
            : new ConnectionLimits(2, 1, TimeSpan.Zero, TimeSpan.Zero);
        using var harness = new Harness(limits, "http");
        harness.Listener.Connect(HeldConnection(new IPEndPoint(IPAddress.Parse("192.0.2.1"), 50000)));
        await harness.Server.Inner.NextExchangeAsync();

        var refused = HeldConnection(new IPEndPoint(IPAddress.Parse(refusedAddress), 50001));
        harness.Listener.Connect(refused);
        var refusal = await harness.Server.NextRefusalAsync();
        await WaitUntilDisposedAsync(refused);

        Assert.AreEqual(expectedRefusal, refusal.Refusal);
        Assert.AreEqual(expectedNote, harness.Logs.NotesOutsideExchanges.Single());
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task ServeAsync_ConnectionPastALimit_WhoseNoteTheLogFailsToTake_IsStillRefusedAndClosed()
    {
        using var harness = new Harness(OneConnection, "http");
        harness.Logs.NoteOutsideExchangeFailure = new IOException("stderr is gone");
        harness.Listener.Connect(HeldConnection(PeerAddress(1)));
        await harness.Server.Inner.NextExchangeAsync();

        var refused = HeldConnection(PeerAddress(2));
        harness.Listener.Connect(refused);
        var refusal = await harness.Server.NextRefusalAsync();
        await WaitUntilDisposedAsync(refused);

        Assert.AreEqual(ConnectionRefusal.TooManyConnections, refusal.Refusal);
        Assert.AreEqual("refused:TooManyConnections", Encoding.ASCII.GetString(refused.WrittenBytes));
        Assert.IsFalse(refused.Aborted);
        Assert.IsEmpty(harness.Logs.NotesOutsideExchanges);
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task ServeAsync_ImplicitTlsListener_ClosesAConnectionPastTheLimitWithNoRefusal()
    {
        using var harness = new Harness(OneConnection, "https");
        var admitted = HeldConnection(PeerAddress(1));
        harness.Listener.Connect(admitted);
        await harness.Server.Inner.NextExchangeAsync();
        Assert.IsTrue(admitted.UpgradeRequested);

        var refused = HeldConnection(PeerAddress(2));
        harness.Listener.Connect(refused);
        await WaitUntilDisposedAsync(refused);

        Assert.IsFalse(harness.Server.TryTakeRefusal(out _));
        Assert.IsEmpty(refused.WrittenBytes);
        Assert.IsFalse(refused.UpgradeRequested);
        Assert.IsFalse(refused.Aborted);
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task ServeAsync_RefusalStillBeingWrittenAfterOneSecond_IsCutOffAndTheConnectionClosed()
    {
        using var harness = new Harness(
            OneConnection, "http", (_, _, cancellationToken) => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));
        harness.Listener.Connect(HeldConnection(PeerAddress(1)));
        await harness.Server.Inner.NextExchangeAsync();
        var refused = HeldConnection(PeerAddress(2));
        harness.Listener.Connect(refused);
        var refusal = await harness.Server.NextRefusalAsync();

        harness.Time.Advance(TimeSpan.FromSeconds(1) - Tick);

        Assert.IsFalse(refusal.CancellationToken.IsCancellationRequested);

        harness.Time.Advance(Tick);
        await WaitUntilDisposedAsync(refused);

        Assert.IsTrue(refusal.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(refused.Aborted);
        Assert.AreEqual(TimeSpan.FromSeconds(1), ServingEngine.RefusalWriteDeadline);
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task ServeAsync_RefusalWriterThatIgnoresTheDeadline_IsLeftBehindAndTheConnectionClosedAfterOneSecond()
    {
        using var harness = new Harness(OneConnection, "http", (_, _, _) => new TaskCompletionSource().Task);
        harness.Listener.Connect(HeldConnection(PeerAddress(1)));
        await harness.Server.Inner.NextExchangeAsync();
        var refused = HeldConnection(PeerAddress(2));
        harness.Listener.Connect(refused);
        await harness.Server.NextRefusalAsync();

        harness.Time.Advance(TimeSpan.FromSeconds(1));
        await WaitUntilDisposedAsync(refused);

        Assert.IsFalse(refused.Aborted);
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task ServeAsync_RefusalWriterThatThrows_HasTheConnectionAbortedAndClosed()
    {
        using var harness = new Harness(
            OneConnection, "http", (_, _, _) => Task.FromException(new IOException("The refusal could not be written.")));
        harness.Listener.Connect(HeldConnection(PeerAddress(1)));
        await harness.Server.Inner.NextExchangeAsync();

        var refused = HeldConnection(PeerAddress(2));
        harness.Listener.Connect(refused);
        await WaitUntilDisposedAsync(refused);

        Assert.IsTrue(refused.Aborted);
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task ServeAsync_DefaultIdleTimeout_CancelsAnIdleExchangeAt120SecondsAndNotBefore()
    {
        using var harness = new Harness(limits: null, "http", serve: ReadOnceAsync);
        var connection = HeldConnection(PeerAddress(1));
        harness.Listener.Connect(connection);
        var exchange = await harness.Server.Inner.NextExchangeAsync();

        harness.Time.Advance(TimeSpan.FromSeconds(120) - Tick);

        Assert.IsFalse(exchange.Context.CancellationToken.IsCancellationRequested);

        harness.Time.Advance(Tick);
        await WaitUntilDisposedAsync(connection);

        Assert.IsTrue(exchange.Context.IsCancelledForALimit);
        Assert.IsFalse(connection.Aborted);
        Assert.IsEmpty(connection.WrittenBytes);
        CollectionAssert.Contains(
            harness.Logs.LogOf(1).Notes.ToList(), "Exchange 1 cancelled: no byte moved for the idle timeout of 120 s.");
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task ServeAsync_BytesReadOrWritten_RestartTheIdleClock()
    {
        var readGate = new TaskCompletionSource();
        var readDone = new TaskCompletionSource();
        var writeGate = new TaskCompletionSource();
        var writeDone = new TaskCompletionSource();
        using var harness = new Harness(
            new ConnectionLimits(0, 0, TimeSpan.FromSeconds(10), TimeSpan.Zero),
            "http",
            serve: async (connection, context) =>
            {
                var buffer = new byte[16];
                await readGate.Task;
                await connection.ReadAsync(buffer, context.CancellationToken);
                readDone.SetResult();
                await writeGate.Task;
                await connection.WriteAsync("pong"u8.ToArray(), context.CancellationToken);
                writeDone.SetResult();
                await connection.ReadAsync(buffer, context.CancellationToken);
            });
        var connection = new InMemoryConnection(["ping"u8.ToArray()], remoteEndPoint: PeerAddress(1), peerHalfClosesWhenExhausted: false);
        harness.Listener.Connect(connection);
        var exchange = await harness.Server.Inner.NextExchangeAsync();

        harness.Time.Advance(TimeSpan.FromSeconds(9));
        readGate.SetResult();
        await readDone.Task.WaitAsync(Patience.Timeout);
        harness.Time.Advance(TimeSpan.FromSeconds(9));

        Assert.IsFalse(exchange.Context.CancellationToken.IsCancellationRequested);

        writeGate.SetResult();
        await writeDone.Task.WaitAsync(Patience.Timeout);
        harness.Time.Advance(TimeSpan.FromSeconds(10) - Tick);

        Assert.IsFalse(exchange.Context.CancellationToken.IsCancellationRequested);

        harness.Time.Advance(Tick);
        await WaitUntilDisposedAsync(connection);

        Assert.IsTrue(exchange.Context.IsCancelledForALimit);
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task ServeAsync_DefaultMaxExchangeDuration_CancelsTheExchangeAt3600SecondsAndNotBefore()
    {
        using var harness = new Harness(ConnectionLimits.Default with { IdleTimeout = TimeSpan.Zero }, "http", serve: ReadOnceAsync);
        var connection = HeldConnection(PeerAddress(1));
        harness.Listener.Connect(connection);
        var exchange = await harness.Server.Inner.NextExchangeAsync();

        harness.Time.Advance(TimeSpan.FromSeconds(3600) - Tick);

        Assert.IsFalse(exchange.Context.CancellationToken.IsCancellationRequested);

        harness.Time.Advance(Tick);
        await WaitUntilDisposedAsync(connection);

        Assert.IsTrue(exchange.Context.IsCancelledForALimit);
        Assert.IsFalse(connection.Aborted);
        Assert.IsEmpty(connection.WrittenBytes);
        CollectionAssert.Contains(
            harness.Logs.LogOf(1).Notes.ToList(),
            "Exchange 1 cancelled: it reached the maximum exchange duration of 3600 s.");
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task ServeAsync_ServerThatReturnsQuietlyAfterTheIdleTimeout_StillHasItNotedAndClosesGracefully()
    {
        using var harness = new Harness(
            new ConnectionLimits(0, 0, TimeSpan.FromSeconds(10), TimeSpan.Zero),
            "http",
            serve: async (_, context) =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                }
            });
        var connection = HeldConnection(PeerAddress(1));
        harness.Listener.Connect(connection);
        await harness.Server.Inner.NextExchangeAsync();

        harness.Time.Advance(TimeSpan.FromSeconds(10));
        await WaitUntilDisposedAsync(connection);

        Assert.IsFalse(connection.Aborted);
        CollectionAssert.Contains(
            harness.Logs.LogOf(1).Notes.ToList(), "Exchange 1 cancelled: no byte moved for the idle timeout of 10 s.");
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task ServeAsync_ServerThatThrowsSomethingElseAfterTheIdleTimeout_HasItsConnectionAborted()
    {
        using var harness = new Harness(
            new ConnectionLimits(0, 0, TimeSpan.FromSeconds(10), TimeSpan.Zero),
            "http",
            serve: async (_, context) =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw new IOException("The peer went away.");
                }
            });
        var connection = HeldConnection(PeerAddress(1));
        harness.Listener.Connect(connection);
        await harness.Server.Inner.NextExchangeAsync();

        harness.Time.Advance(TimeSpan.FromSeconds(10));
        await WaitUntilDisposedAsync(connection);

        Assert.IsTrue(connection.Aborted);
        CollectionAssert.Contains(
            harness.Logs.LogOf(1).Notes.ToList(),
            "Exchange 1 ended because the protocol server threw IOException: The peer went away.");
        await harness.StopAsync();
    }

    private static Task ReadOnceAsync(IConnection connection, ExchangeContext context) =>
        connection.ReadAsync(new byte[1], context.CancellationToken).AsTask();

    private static IPEndPoint PeerAddress(int peer) => new(new IPAddress([10, 0, (byte)(peer >> 8), (byte)peer]), 50000);

    // A connection whose peer sends nothing and never half-closes, so a read waits until the
    // exchange is cancelled.
    private static InMemoryConnection HeldConnection(EndPoint remoteEndPoint) =>
        new([], remoteEndPoint: remoteEndPoint, peerHalfClosesWhenExhausted: false);

    /// <summary>
    /// One engine serving <see cref="Http"/> or <see cref="Https"/> with a refusal-writing
    /// fake server, on a manual clock. Unless the test scripts otherwise, each exchange waits
    /// until the test ends it or the engine cancels it.
    /// </summary>
    private sealed class Harness : IDisposable
    {
        private readonly Dictionary<long, TaskCompletionSource> exchangeEnds = [];
        private readonly Lock gate = new();
        private bool stopping;
        private readonly CancellationTokenSource stop = new();
        private readonly Task<SurlExitCode> serving;

        public Harness(
            ConnectionLimits? limits,
            string scheme,
            Func<IConnection, ConnectionRefusal, CancellationToken, Task>? writeRefusal = null,
            Func<IConnection, ExchangeContext, Task>? serve = null)
        {
            var factory = new FakeListenerFactory();
            var listenUrl = scheme == "https" ? Https : Http;
            Server = new FakeRefusalWritingProtocolServer(
                new FakeConnectionProtocolServer(serve ?? WaitUntilEndedAsync, scheme), writeRefusal);
            var engine = limits is null
                ? new ServingEngine(factory, [Server], Logs, Time, GracePeriod)
                : new ServingEngine(factory, [Server], Logs, Time, GracePeriod, limits);
            serving = engine.ServeAsync([listenUrl], stop.Token);
            Listener = factory.ListenerFor(listenUrl);
        }

        public ManualTimeProvider Time { get; } = new();

        public FakeExchangeLogFactory Logs { get; } = new();

        public FakeRefusalWritingProtocolServer Server { get; }

        public FakeConnectionListener Listener { get; }

        public async Task<ServedExchange[]> NextExchangesAsync(int count)
        {
            var exchanges = new ServedExchange[count];

            for (var index = 0; index < count; index++)
            {
                exchanges[index] = await Server.Inner.NextExchangeAsync();
            }

            return exchanges;
        }

        public void EndExchange(long exchangeId) => EndOf(exchangeId).TrySetResult();

        // The engine stops counting a connection just after disposing it, so a connection
        // made the moment the test sees the dispose may still be refused; retry until one is
        // admitted.
        public async Task<ServedExchange> ConnectUntilAdmittedAsync(EndPoint remoteEndPoint)
        {
            var deadline = DateTime.UtcNow + Patience.Timeout;
            var admitted = Server.Inner.NextExchangeAsync();

            while (DateTime.UtcNow < deadline)
            {
                Listener.Connect(HeldConnection(remoteEndPoint));
                var refused = Server.NextRefusalAsync();

                if (await Task.WhenAny(admitted, refused) == admitted)
                {
                    return await admitted;
                }
            }

            throw new AssertFailedException("No connection was admitted.");
        }

        public async Task StopAsync()
        {
            lock (gate)
            {
                stopping = true;

                foreach (var end in exchangeEnds.Values)
                {
                    end.TrySetResult();
                }
            }

            await stop.CancelAsync();
            Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience.Timeout));
        }

        public void Dispose() => stop.Dispose();

        private Task WaitUntilEndedAsync(IConnection connection, ExchangeContext context) =>
            EndOf(context.ExchangeId).Task.WaitAsync(context.CancellationToken);

        private TaskCompletionSource EndOf(long exchangeId)
        {
            lock (gate)
            {
                if (!exchangeEnds.TryGetValue(exchangeId, out var end))
                {
                    end = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    exchangeEnds[exchangeId] = end;

                    // An exchange that reaches its server after StopAsync ends at once.
                    if (stopping)
                    {
                        end.TrySetResult();
                    }
                }

                return end;
            }
        }
    }
}
