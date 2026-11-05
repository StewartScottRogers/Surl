using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading.Channels;
using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// <see cref="UdpDatagramListener"/> under the serving engine, as an <c>https</c> listener serving
/// QUIC connections rides it (ADR-0082, decision 5): each remote endpoint is one flow, so one
/// connection, and a flow past ADR-0006's limits is refused and noted outside any exchange
/// (ADR-0028). Fake sockets, no network.
/// </summary>
[TestClass]
public sealed class UdpDatagramListenerConnectionLimitTests
{
    private static readonly ListenUrl Https = new("https", "127.0.0.1", 0);
    private static readonly IPEndPoint FirstAddressFirstPort = new(IPAddress.Parse("192.0.2.1"), 50000);
    private static readonly IPEndPoint FirstAddressSecondPort = new(IPAddress.Parse("192.0.2.1"), 50001);
    private static readonly IPEndPoint SecondAddress = new(IPAddress.Parse("192.0.2.2"), 50000);
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_DatagramsFromTwoRemoteEndPoints_AreServedAsTwoConnections()
    {
        var harness = new EngineHarness(perAddressLimit: 1);
        var serving = harness.Engine.ServeAsync([Https], harness.Stop.Token);
        var listenSocket = await harness.ListenSocketAsync();

        listenSocket.Arrive("initial-a", FirstAddressFirstPort);
        var first = await harness.Server.NextFlowAsync();
        listenSocket.Arrive("initial-b", SecondAddress);
        var second = await harness.Server.NextFlowAsync();
        listenSocket.Arrive("handshake-a", FirstAddressFirstPort);
        var laterFromFirst = await first.ReceiveAsync(TestContext.CancellationToken);

        Assert.AreEqual(FirstAddressFirstPort, first.RemoteEndPoint);
        Assert.AreEqual(SecondAddress, second.RemoteEndPoint);
        Assert.AreEqual("initial-a", Encoding.ASCII.GetString(first.FirstDatagram.Span));
        Assert.AreEqual("initial-b", Encoding.ASCII.GetString(second.FirstDatagram.Span));
        Assert.AreEqual("handshake-a", Encoding.ASCII.GetString(laterFromFirst.Span));
        Assert.IsEmpty(harness.Logs.NotesOutsideExchanges);
        await harness.StopAsync(serving);
    }

    [TestMethod]
    public async Task ServeAsync_DatagramPastMaxConnectionsPerAddress_IsRefusedThroughTheServerAndNotedOutsideAnyExchange()
    {
        var harness = new EngineHarness(perAddressLimit: 1);
        var serving = harness.Engine.ServeAsync([Https], harness.Stop.Token);
        var listenSocket = await harness.ListenSocketAsync();

        listenSocket.Arrive("initial-a", FirstAddressFirstPort);
        await harness.Server.NextFlowAsync();
        listenSocket.Arrive("initial-c", FirstAddressSecondPort);
        var refusal = await harness.Server.NextRefusalAsync();
        await harness.Logs.NoteWrittenAsync().WaitAsync(Patience, TestContext.CancellationToken);

        Assert.AreEqual(ConnectionRefusal.TooManyConnectionsFromAddress, refusal);
        Assert.AreEqual(("refused:TooManyConnectionsFromAddress", (EndPoint)FirstAddressSecondPort), listenSocket.Sent.Single());
        Assert.AreEqual(
            $"Refused a flow from {FirstAddressSecondPort}: past --max-connections-per-address 1.",
            harness.Logs.NotesOutsideExchanges.Single());
        Assert.AreEqual(1, harness.Logs.ExchangesOpened);
        await harness.StopAsync(serving);
    }

    [TestMethod]
    public async Task ServeAsync_Stopped_DisposesTheListenSocketOnceTheFlowsHaveEnded()
    {
        var harness = new EngineHarness(perAddressLimit: 1);
        var serving = harness.Engine.ServeAsync([Https], harness.Stop.Token);
        var listenSocket = await harness.ListenSocketAsync();

        listenSocket.Arrive("initial-a", FirstAddressFirstPort);
        await harness.Server.NextFlowAsync();
        await harness.StopAsync(serving);

        Assert.AreEqual(1, listenSocket.DisposeCount);
    }

    /// <summary>
    /// A serving engine over a <see cref="UdpDatagramListener"/> on fake sockets, with one
    /// datagram server for <c>https</c> and a per-address limit.
    /// </summary>
    private sealed class EngineHarness
    {
        public EngineHarness(int perAddressLimit)
        {
            Engine = new ServingEngine(
                new FakeSocketListenerFactory(Binder),
                [Server],
                Logs,
                new ManualTimeProvider(),
                TimeSpan.Zero,
                new ConnectionLimits(0, perAddressLimit, TimeSpan.Zero, TimeSpan.Zero));
        }

        public FakeDatagramSocketBinder Binder { get; } = new();

        public HeldFlowServer Server { get; } = new();

        public NoteRecordingLogFactory Logs { get; } = new();

        public ServingEngine Engine { get; }

        public CancellationTokenSource Stop { get; } = new();

        public async Task<FakeDatagramSocket> ListenSocketAsync()
        {
            while (Binder.Bound.Count == 0)
            {
                await Task.Yield();
            }

            return Binder.Bound[0];
        }

        public async Task StopAsync(Task<SurlExitCode> serving)
        {
            await Stop.CancelAsync();

            Assert.AreEqual(SurlExitCode.Ok, await serving.WaitAsync(Patience));
            Stop.Dispose();
        }
    }

    private sealed class FakeSocketListenerFactory(FakeDatagramSocketBinder binder) : IListenerFactory
    {
        public ValueTask<IConnectionListener> StartConnectionListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public async ValueTask<IDatagramListener> StartDatagramListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken) =>
            await UdpDatagramListener.StartAsync(
                listenUrl, (_, _) => Task.FromResult(Array.Empty<IPAddress>()), binder.Bind, cancellationToken);
    }

    /// <summary>
    /// Holds every flow it serves until the exchange is cancelled, and answers a refusal with
    /// one datagram naming it.
    /// </summary>
    private sealed class HeldFlowServer : IDatagramProtocolServer, IDatagramRefusalWriter
    {
        private readonly Channel<IDatagramFlow> flows = Channel.CreateUnbounded<IDatagramFlow>();
        private readonly Channel<ConnectionRefusal> refusals = Channel.CreateUnbounded<ConnectionRefusal>();

        public IReadOnlyList<string> Schemes { get; } = ["https"];

        public async Task ServeAsync(IDatagramFlow flow, ExchangeContext context)
        {
            flows.Writer.TryWrite(flow);

            await Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken).ContinueWith(_ => { }, TaskScheduler.Default);
        }

        public async ValueTask WriteRefusalAsync(IDatagramFlow flow, ConnectionRefusal refusal, CancellationToken cancellationToken)
        {
            await flow.SendAsync(Encoding.ASCII.GetBytes($"refused:{refusal}"), cancellationToken);
            refusals.Writer.TryWrite(refusal);
        }

        public async Task<IDatagramFlow> NextFlowAsync() => await flows.Reader.ReadAsync().AsTask().WaitAsync(Patience);

        public async Task<ConnectionRefusal> NextRefusalAsync() => await refusals.Reader.ReadAsync().AsTask().WaitAsync(Patience);
    }

    private sealed class NoteRecordingLogFactory : IExchangeLogFactory
    {
        private readonly TaskCompletionSource noteWritten = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int exchangesOpened;

        public ConcurrentQueue<string> NotesOutsideExchanges { get; } = new();

        public int ExchangesOpened => Volatile.Read(ref exchangesOpened);

        public Task NoteWrittenAsync() => noteWritten.Task;

        public IExchangeLog Create(long exchangeId, EndPoint remoteEndPoint)
        {
            Interlocked.Increment(ref exchangesOpened);

            return new SilentExchangeLog();
        }

        public void NoteOutsideExchange(string text)
        {
            NotesOutsideExchanges.Enqueue(text);
            noteWritten.TrySetResult();
        }
    }

    private sealed class SilentExchangeLog : IExchangeLog
    {
        public void BytesReceived(ReadOnlySpan<byte> bytes)
        {
        }

        public void BytesSent(ReadOnlySpan<byte> bytes)
        {
        }

        public void Note(string text)
        {
        }
    }
}
