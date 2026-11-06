using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.HttpMessage;

public sealed partial class HttpConnectionReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan HeadTimeout = ExchangeLimits.Default.HeadTimeout;

    [TestMethod]
    public async Task ReadNextRequestHeadAsync_NullContext_Throws()
    {
        var reader = new HttpConnectionReader(new InMemoryConnection([]), Limit);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => reader.ReadNextRequestHeadAsync(null!, isFirstHead: true));
    }

    [TestMethod]
    public async Task ReadNextRequestHeadAsync_FirstHeadWithinTheTimeout_ReturnsItAndStopsItsTimer()
    {
        var clock = new ManualTimeProvider(Now);
        var reader = new HttpConnectionReader(new InMemoryConnection([Ascii("GET / HTTP/1.1\r\nHost: x\r\n\r\n")]), Limit);

        var result = await reader.ReadNextRequestHeadAsync(Context(clock, TestContext.CancellationToken), isFirstHead: true);

        Assert.AreEqual(HttpRequestHeadReadOutcome.HeadRead, result.Outcome);
        Assert.AreEqual(0, clock.ActiveTimerCount);
    }

    [TestMethod]
    public async Task ReadNextRequestHeadAsync_LaterHeadAfterTheClientHalfCloses_ReturnsConnectionClosedWithNoTimer()
    {
        var clock = new ManualTimeProvider(Now);
        var reader = new HttpConnectionReader(new InMemoryConnection([]), Limit);

        var result = await reader.ReadNextRequestHeadAsync(Context(clock, TestContext.CancellationToken), isFirstHead: false);

        Assert.AreEqual(HttpRequestHeadReadOutcome.ConnectionClosed, result.Outcome);
        Assert.AreEqual(0, clock.ActiveTimerCount);
    }

    [TestMethod]
    public async Task ReadNextRequestHeadAsync_LaterHead_IsNotTimedUntilItsFirstByteArrives()
    {
        var clock = new ManualTimeProvider(Now);
        var reader = new HttpConnectionReader(new InMemoryConnection([], peerHalfClosesWhenExhausted: false), Limit);
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        var read = reader.ReadNextRequestHeadAsync(Context(clock, exchange.Token), isFirstHead: false);

        Assert.AreEqual(0, clock.ActiveTimerCount);
        await exchange.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => read);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ReadNextRequestHeadAsync_TimeoutAfterPartOfTheHead_ReturnsHeadTimedOut(bool isFirstHead)
    {
        var clock = new ManualTimeProvider(Now);
        var connection = new InMemoryConnection([Ascii("GET / HTTP/1.1\r\nHost:")], peerHalfClosesWhenExhausted: false);
        var reader = new HttpConnectionReader(connection, Limit);

        var read = reader.ReadNextRequestHeadAsync(Context(clock, TestContext.CancellationToken), isFirstHead);
        clock.Advance(HeadTimeout - TimeSpan.FromSeconds(1));
        Assert.IsFalse(read.IsCompleted, "One second of the head timeout is left.");
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual(HttpRequestHeadReadOutcome.HeadTimedOut, (await read).Outcome);
    }

    [TestMethod]
    public async Task ReadNextRequestHeadAsync_TimeoutBeforeAnyByte_ReturnsHeadTimedOutBeforeAnyByte()
    {
        var clock = new ManualTimeProvider(Now);
        var reader = new HttpConnectionReader(new InMemoryConnection([], peerHalfClosesWhenExhausted: false), Limit);

        var read = reader.ReadNextRequestHeadAsync(Context(clock, TestContext.CancellationToken), isFirstHead: true);
        clock.Advance(HeadTimeout);

        Assert.AreEqual(HttpRequestHeadReadOutcome.HeadTimedOutBeforeAnyByte, (await read).Outcome);
    }

    [TestMethod]
    public async Task ReadNextRequestHeadAsync_ExchangeCancelled_Throws()
    {
        var clock = new ManualTimeProvider(Now);
        var reader = new HttpConnectionReader(new InMemoryConnection([Ascii("GET /")], peerHalfClosesWhenExhausted: false), Limit);
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        var read = reader.ReadNextRequestHeadAsync(Context(clock, exchange.Token), isFirstHead: true);
        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => read);
    }

    [TestMethod]
    [DataRow(-1.0)]
    [DataRow(365.0)]
    public async Task ReadNextRequestHeadAsync_InfiniteOrLongerThanATimerCanWaitTimeout_NeverCutsAHeadOff(double days)
    {
        var clock = new ManualTimeProvider(Now);
        var reader = new HttpConnectionReader(new InMemoryConnection([Ascii("GET /")], peerHalfClosesWhenExhausted: false), Limit);
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var limits = ExchangeLimits.Default with { HeadTimeout = days < 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromDays(days) };

        var read = reader.ReadNextRequestHeadAsync(Context(clock, exchange.Token, limits), isFirstHead: true);
        clock.Advance(TimeSpan.FromDays(400));

        Assert.IsFalse(read.IsCompleted);
        await exchange.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => read);
    }

    private static ExchangeContext Context(TimeProvider clock, CancellationToken cancellationToken, ExchangeLimits? limits = null) => new(
        1,
        new ListenUrl("http", "127.0.0.1", 18050).WithBoundPort(18050),
        new IPEndPoint(IPAddress.Loopback, 18050),
        new IPEndPoint(IPAddress.Loopback, 50000),
        new RecordingExchangeLog(),
        clock,
        cancellationToken)
    {
        Limits = limits ?? ExchangeLimits.Default,
    };

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);
}
