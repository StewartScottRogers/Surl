using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class HeadTimeoutTests
{
    private static readonly TimeSpan HeadTimeout = ExchangeLimits.Default.HeadTimeout;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PartialHead_AfterHeadTimeout_Answers408AndCloses()
    {
        var clock = new ManualTimeProvider(Now - HeadTimeout);
        var connection = new InMemoryConnection([Ascii("GET /file.txt HTTP/1.1\r\nHost:")], peerHalfClosesWhenExhausted: false);
        var log = new RecordingExchangeLog();
        var serving = Server().ServeAsync(connection, Context(log, clock, TestContext.CancellationToken));

        clock.Advance(HeadTimeout - TimeSpan.FromSeconds(1));
        Assert.IsFalse(serving.IsCompleted, "One second of the head timeout is left.");
        clock.Advance(TimeSpan.FromSeconds(1));
        await AdvanceUntilCompletedAsync(clock, serving);

        CollectionAssert.AreEqual(RecordedResponse("head-timeout-408"), connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("No request head was read: HeadTimedOut; answered 408 and closed.", log.Notes[0]);
        Assert.AreEqual("Stopped draining the unread request bytes at the 1-second drain limit.", log.Notes[1]);
    }

    [TestMethod]
    public async Task NoBytes_AfterHeadTimeout_ClosesWithNoBytes()
    {
        var clock = new ManualTimeProvider(Now);
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);
        var log = new RecordingExchangeLog();
        var serving = Server().ServeAsync(connection, Context(log, clock, TestContext.CancellationToken));

        clock.Advance(HeadTimeout);
        await serving;

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsFalse(connection.Aborted);
        CollectionAssert.AreEqual(new[] { "No request head was read: HeadTimedOutBeforeAnyByte; closed with no bytes." }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task KeptAliveConnection_IdleBeforeTheNextHead_IsNotCutOffByTheHeadTimeout()
    {
        var clock = new ManualTimeProvider(Now);
        var connection = new InMemoryConnection([RecordedFixture.ReadRequestBytes("get-file")], peerHalfClosesWhenExhausted: false);
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = Server().ServeAsync(connection, Context(new RecordingExchangeLog(), clock, exchange.Token));

        CollectionAssert.AreEqual(RecordedResponse("get-file"), connection.WrittenBytes, "The first request was answered.");
        Assert.AreEqual(0, clock.ActiveTimerCount, "No head timer runs while the connection waits for the next head.");
        clock.Advance(TimeSpan.FromHours(1));
        Assert.IsFalse(serving.IsCompleted);

        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        CollectionAssert.AreEqual(RecordedResponse("get-file"), connection.WrittenBytes);
    }

    [TestMethod]
    public async Task KeptAliveConnection_NextHeadIsTimedFromItsFirstByte()
    {
        var clock = new ManualTimeProvider(Now - HeadTimeout);
        var connection = new InMemoryConnection(
            [RecordedFixture.ReadRequestBytes("get-file"), Ascii("GET /file.txt")],
            peerHalfClosesWhenExhausted: false);
        var serving = Server().ServeAsync(connection, Context(new RecordingExchangeLog(), clock, TestContext.CancellationToken));

        Assert.AreEqual(1, clock.ActiveTimerCount, "The second head's first bytes started its timer.");
        clock.Advance(HeadTimeout);
        await AdvanceUntilCompletedAsync(clock, serving);

        var recorded408 = RecordedResponse("head-timeout-408");
        CollectionAssert.AreEqual(recorded408, connection.WrittenBytes[^recorded408.Length..]);
    }

    [TestMethod]
    public async Task ExchangeCancelledDuringAHead_ThrowsInsteadOfAnswering408()
    {
        var clock = new ManualTimeProvider(Now);
        var connection = new InMemoryConnection([Ascii("GET /")], peerHalfClosesWhenExhausted: false);
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = Server().ServeAsync(connection, Context(new RecordingExchangeLog(), clock, exchange.Token));

        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.IsEmpty(connection.WrittenBytes);
    }

    [TestMethod]
    [DataRow(-1.0)]
    [DataRow(365.0)]
    public async Task InfiniteOrLongerThanATimerCanWaitHeadTimeout_NeverCutsAHeadOff(double days)
    {
        var clock = new ManualTimeProvider(Now);
        var connection = new InMemoryConnection([Ascii("GET /")], peerHalfClosesWhenExhausted: false);
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var limits = ExchangeLimits.Default with { HeadTimeout = days < 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromDays(days) };
        var serving = Server().ServeAsync(connection, Context(new RecordingExchangeLog(), clock, exchange.Token, limits));

        clock.Advance(TimeSpan.FromDays(400));
        Assert.IsFalse(serving.IsCompleted);
        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.IsEmpty(connection.WrittenBytes);
    }
}
