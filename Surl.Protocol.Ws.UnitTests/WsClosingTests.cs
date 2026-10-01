using System.Net;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ws.WsServerHarness;

namespace Surl.Protocol.Ws;

/// <summary>
/// How the server ends an exchange: the refusal's one-second write deadline, and the lingering
/// close of at most one second after it shuts down its sending side (ADR-0071 decisions 1 and 5).
/// </summary>
[TestClass]
public sealed class WsClosingTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ClientThatKeepsTheConnectionOpen_After101_IsClosedAfterOneSecond()
    {
        var clock = new ManualTimeProvider(Now);
        var connection = new InMemoryConnection([RecordedFixture.ReadRequestBytes("upgrade-101")], peerHalfClosesWhenExhausted: false);
        var log = new RecordingExchangeLog();
        var serving = Server().ServeAsync(connection, Context(log, clock, TestContext.CancellationToken));

        await EndLingeringCloseAsync(clock, connection, serving);

        Assert.AreEqual(Recorded101Response, Latin1(connection.WrittenBytes));
        Assert.AreEqual("Stopped reading what the client still sent at the 1-second lingering close.", log.Notes[^1]);
    }

    [TestMethod]
    public async Task BytesTheClientStillSends_AreReadAndThrownAway()
    {
        var request = RecordedFixture.ReadRequestBytes("upgrade-101");

        var (connection, _) = await ServeAsync([request, new byte[] { 0x8A, 0x80, 1, 2, 3, 4 }], TestContext.CancellationToken);

        Assert.AreEqual(Recorded101Response, Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ConnectionFailure_DuringTheLingeringClose_IsNoted()
    {
        var connection = new ResettingConnection(RecordedFixture.ReadRequestBytes("upgrade-101"));
        var log = new RecordingExchangeLog();

        await Server().ServeAsync(connection, Context(log, new ManualTimeProvider(Now), TestContext.CancellationToken));

        Assert.AreEqual("The connection failed during the lingering close (reset by the client).", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ExchangeCancelled_DuringTheLingeringClose_EndsTheExchange()
    {
        var clock = new ManualTimeProvider(Now);
        var connection = new InMemoryConnection([RecordedFixture.ReadRequestBytes("upgrade-101")], peerHalfClosesWhenExhausted: false);
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = Server().ServeAsync(connection, Context(new RecordingExchangeLog(), clock, exchange.Token));

        await WaitForLingeringCloseAsync(clock, connection);
        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    [TestMethod]
    public async Task Refusal_NotWrittenWithinOneSecond_IsGivenUp()
    {
        var clock = new ManualTimeProvider(Now);
        var connection = new StalledWriteConnection(RecordedFixture.ReadRequestBytes("head-405"));
        var log = new RecordingExchangeLog();
        var serving = Server().ServeAsync(connection, Context(log, clock, TestContext.CancellationToken));

        await WaitForTimersAsync(clock, 1);
        clock.Advance(WebSocketUpgradeResponder.RefusalWriteDeadline);
        await serving;

        Assert.IsFalse(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("The 405 was not written within its 1-second write deadline; the connection is closed without it.", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ExchangeCancelled_WhileARefusalIsWritten_EndsTheExchange()
    {
        var clock = new ManualTimeProvider(Now);
        var connection = new StalledWriteConnection(RecordedFixture.ReadRequestBytes("head-405"));
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = Server().ServeAsync(connection, Context(new RecordingExchangeLog(), clock, exchange.Token));

        await WaitForTimersAsync(clock, 1);
        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    /// <summary>
    /// A connection whose client sends one request, then resets the connection.
    /// </summary>
    private sealed class ResettingConnection(byte[] request) : IConnection
    {
        private bool requestRead;

        public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 18301);

        public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

        public TlsSession? TlsSession => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (requestRead)
            {
                throw new IOException("reset by the client");
            }

            requestRead = true;
            request.CopyTo(buffer);

            return ValueTask.FromResult(request.Length);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public void Abort()
        {
        }

        public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("This connection never upgrades.");

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
