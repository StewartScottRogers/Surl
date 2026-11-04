using System.Net;
using Surl.Protocol.Abstractions;
using static Surl.Core.InMemoryConnectionWaits;

namespace Surl.Core;

// The FTP data connections ADR-0052 decision 9 has the serving engine hand each exchange.
public sealed partial class ServingEngineTests
{
    private static readonly ListenUrl Ftp = new("ftp", "127.0.0.1", 2121);
    private static readonly IPEndPoint CurlDataPort = new(IPAddress.Loopback, 50001);

    [TestMethod]
    public void Constructor_NullDataConnectionOpener_Throws()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new ServingEngine(
            new FakeListenerFactory(), [], new FakeExchangeLogFactory(), new ManualTimeProvider(), GracePeriod,
            ConnectionLimits.Default, null!));

        Assert.AreEqual("dataConnectionOpener", exception.ParamName);
    }

    [TestMethod]
    public async Task ServeAsync_EngineGivenNoOpener_HandsEachExchangeDataConnectionsThatRefuse()
    {
        var failure = new TaskCompletionSource<DataConnectionFailure>();
        var server = new FakeConnectionProtocolServer(
            async (connection, context) =>
            {
                try
                {
                    await context.DataConnections.StartPassiveListenerAsync(
                        connection.LocalEndPoint, connection.RemoteEndPoint, context.CancellationToken);
                }
                catch (DataConnectionException refused)
                {
                    failure.SetResult(refused.Failure);
                }
            },
            "ftp");
        var factory = new FakeListenerFactory();
        using var stop = new CancellationTokenSource();
        var serving = new ServingEngine(factory, [server], new FakeExchangeLogFactory(), new ManualTimeProvider(), GracePeriod)
            .ServeAsync([Ftp], stop.Token);

        factory.ListenerFor(Ftp).Connect(new InMemoryConnection([]));

        Assert.AreEqual(DataConnectionFailure.Unavailable, await failure.Task.WaitAsync(Patience.Timeout));
        await stop.CancelAsync();
        Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience.Timeout));
    }

    [TestMethod]
    public async Task ServeAsync_BytesOnADataConnection_PostponeTheControlConnectionsIdleTimeout()
    {
        var readGate = new TaskCompletionSource();
        var readDone = new TaskCompletionSource();
        var opener = new InMemoryDataConnections().ScriptActiveConnection(
            new InMemoryConnection(["data"u8.ToArray()], remoteEndPoint: CurlDataPort, peerHalfClosesWhenExhausted: false));
        await using var engine = new DataConnectionEngine(
            opener,
            new ConnectionLimits(0, 0, TimeSpan.FromSeconds(10), TimeSpan.Zero),
            async (connection, context) =>
            {
                var data = await context.DataConnections.ConnectActiveAsync(
                    connection.RemoteEndPoint, CurlDataPort, TimeSpan.FromSeconds(30), context.CancellationToken);
                await readGate.Task;
                await data.ReadAsync(new byte[16], context.CancellationToken);
                readDone.SetResult();
                await data.ReadAsync(new byte[16], context.CancellationToken);
            });
        var control = HeldConnection(PeerAddress(1));
        engine.Listener.Connect(control);
        var exchange = await engine.Server.NextExchangeAsync();

        engine.Time.Advance(TimeSpan.FromSeconds(9));
        readGate.SetResult();
        await readDone.Task.WaitAsync(Patience.Timeout);
        engine.Time.Advance(TimeSpan.FromSeconds(10) - Tick);

        Assert.IsFalse(exchange.Context.CancellationToken.IsCancellationRequested);

        engine.Time.Advance(Tick);
        await WaitUntilDisposedAsync(control);

        Assert.IsTrue(exchange.Context.CancellationToken.IsCancellationRequested);
    }

    [TestMethod]
    public async Task ServeAsync_DataConnectionBytes_GoToTheExchangesLogBetweenItsOpenedAndClosedNotes()
    {
        var opener = new InMemoryDataConnections().ScriptActiveConnection(
            new InMemoryConnection(["data"u8.ToArray()], remoteEndPoint: CurlDataPort));
        await using var engine = new DataConnectionEngine(
            opener,
            ConnectionLimits.Default,
            async (connection, context) =>
            {
                await using var data = await context.DataConnections.ConnectActiveAsync(
                    connection.RemoteEndPoint, CurlDataPort, TimeSpan.FromSeconds(30), context.CancellationToken);
                await data.WriteAsync("file"u8.ToArray(), context.CancellationToken);
                await data.ReadAsync(new byte[16], context.CancellationToken);
            });
        var control = new InMemoryConnection([], remoteEndPoint: PeerAddress(1));

        engine.Listener.Connect(control);
        await WaitUntilDisposedAsync(control);

        CollectionAssert.AreEqual(
            new[]
            {
                "Note::Exchange 1 opened: ftp from 10.0.0.1:50000.",
                "Note::Data connection opened: active to 127.0.0.1:50001.",
                "BytesSent:" + Convert.ToHexString("file"u8) + ":",
                "BytesReceived:" + Convert.ToHexString("data"u8) + ":",
                "Note::Data connection closed.",
                "Note::Exchange 1 ended; closing the connection.",
            },
            engine.Logs.LogOf(1).Entries.Select(entry => $"{entry.Kind}:{Convert.ToHexString(entry.Bytes)}:{entry.Text}").ToList());
    }

    [TestMethod]
    public async Task ServeAsync_DataConnectionAndListenerStillOpenWhenTheExchangeEnds_AreDisposedBeforeItsEndNote()
    {
        var data = new InMemoryConnection([], remoteEndPoint: CurlDataPort, peerHalfClosesWhenExhausted: false);
        var opener = new InMemoryDataConnections()
            .ScriptActiveConnection(data)
            .ScriptPassiveListener(new IPEndPoint(IPAddress.Loopback, 40000), null);
        await using var engine = new DataConnectionEngine(
            opener,
            ConnectionLimits.Default,
            async (connection, context) =>
            {
                await context.DataConnections.ConnectActiveAsync(
                    connection.RemoteEndPoint, CurlDataPort, TimeSpan.FromSeconds(30), context.CancellationToken);
                await context.DataConnections.StartPassiveListenerAsync(
                    connection.LocalEndPoint, connection.RemoteEndPoint, context.CancellationToken);
            });
        var control = new InMemoryConnection([], remoteEndPoint: PeerAddress(1));

        engine.Listener.Connect(control);
        await WaitUntilDisposedAsync(control);

        Assert.IsTrue(data.Disposed);
        Assert.IsTrue(opener.PassiveListeners.Single().Disposed);
        Assert.IsFalse(control.Aborted);
        CollectionAssert.AreEqual(
            new[] { "Data connection closed.", "Exchange 1 ended; closing the connection." },
            engine.Logs.LogOf(1).Notes.TakeLast(2).ToList());
    }

    [TestMethod]
    public async Task ServeAsync_OpenDataConnection_DoesNotCountAgainstMaxConnections()
    {
        var dataOpened = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var opener = new InMemoryDataConnections().ScriptActiveConnection(
            new InMemoryConnection([], remoteEndPoint: CurlDataPort, peerHalfClosesWhenExhausted: false));
        await using var engine = new DataConnectionEngine(
            opener,
            new ConnectionLimits(2, 0, TimeSpan.Zero, TimeSpan.Zero),
            async (connection, context) =>
            {
                if (context.ExchangeId == 1)
                {
                    await context.DataConnections.ConnectActiveAsync(
                        connection.RemoteEndPoint, CurlDataPort, TimeSpan.FromSeconds(30), context.CancellationToken);
                    dataOpened.SetResult();
                }

                await release.Task.WaitAsync(context.CancellationToken);
            });
        engine.Listener.Connect(HeldConnection(PeerAddress(1)));
        await dataOpened.Task.WaitAsync(Patience.Timeout);

        engine.Listener.Connect(HeldConnection(PeerAddress(2)));
        await engine.Server.NextExchangeAsync();
        var second = await engine.Server.NextExchangeAsync();

        Assert.AreEqual(2, second.Context.ExchangeId);
        Assert.IsEmpty(engine.Logs.NotesOutsideExchanges);
        release.SetResult();
    }

    /// <summary>
    /// One engine serving <see cref="Ftp"/> with a fake server and the given data-connection
    /// opener, on a manual clock; disposing it stops the engine.
    /// </summary>
    private sealed class DataConnectionEngine : IAsyncDisposable
    {
        private readonly CancellationTokenSource stop = new();
        private readonly Task<SurlExitCode> serving;

        public DataConnectionEngine(
            IDataConnectionOpener opener, ConnectionLimits limits, Func<IConnection, ExchangeContext, Task> serve)
        {
            var factory = new FakeListenerFactory();
            Server = new FakeConnectionProtocolServer(serve, "ftp");
            serving = new ServingEngine(factory, [Server], Logs, Time, GracePeriod, limits, opener).ServeAsync([Ftp], stop.Token);
            Listener = factory.ListenerFor(Ftp);
        }

        public ManualTimeProvider Time { get; } = new();

        public FakeExchangeLogFactory Logs { get; } = new();

        public FakeConnectionProtocolServer Server { get; }

        public FakeConnectionListener Listener { get; }

        public async ValueTask DisposeAsync()
        {
            await stop.CancelAsync();
            Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience.Timeout));
            stop.Dispose();
        }
    }
}
