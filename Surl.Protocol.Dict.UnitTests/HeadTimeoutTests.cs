using Surl.Protocol.Abstractions;
using static Surl.Protocol.Dict.DictTestExchange;

namespace Surl.Protocol.Dict;

[TestClass]
public sealed class HeadTimeoutTests
{
    private static readonly TimeSpan HeadTimeout = ExchangeLimits.Default.HeadTimeout;
    private static readonly TimeSpan JustUnderHeadTimeout = HeadTimeout - TimeSpan.FromTicks(1);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PartialLine_AfterHeadTimeout_Answers420AndCloses()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(Ascii("DEFINE ! hel"), peerHalfClosesWhenExhausted: false);

        var serving = Server().ServeAsync(connection, Context(clock, TestContext.CancellationToken, log: log));
        clock.Advance(JustUnderHeadTimeout);
        Assert.IsFalse(serving.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));
        await serving;

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("head-timeout", "stdout.bin"), connection.WrittenBytes);
        Assert.AreEqual(Banner + HeadTimedOutReply, Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("A command line was not complete within the head timeout; answered 420 and closed.", log.Notes[0]);
    }

    [TestMethod]
    public async Task NoCommand_AfterHeadTimeout_Answers420AndCloses()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);

        var serving = Server().ServeAsync(connection, Context(clock, TestContext.CancellationToken));
        clock.Advance(JustUnderHeadTimeout);
        Assert.IsFalse(serving.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));
        await serving;

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("head-timeout", "stdout.bin"), connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    public void RecordedHeadTimeout_WasAcceptedByUpstreamCurl()
    {
        Assert.AreEqual("0", Utf8(RecordedFixture.ReadBytes("head-timeout", "exitcode.txt")));
        Assert.IsEmpty(RecordedFixture.ReadBytes("head-timeout", "stderr.txt"));
    }

    [TestMethod]
    public async Task WaitBetweenCommands_IsNotCutOffByTheHeadTimeout()
    {
        var clock = new ManualTimeProvider();
        using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var connection = new InMemoryConnection(Ascii("STATUS\r\n"), peerHalfClosesWhenExhausted: false);

        var serving = Server().ServeAsync(connection, Context(clock, idleTimeout.Token));
        clock.Advance(HeadTimeout * 4);

        Assert.IsFalse(serving.IsCompleted);
        Assert.AreEqual(0, clock.PendingTimerCount);
        Assert.AreEqual(Banner + "210 status ok\r\n", Utf8(connection.WrittenBytes));

        await idleTimeout.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "The next line's first byte arrives in a later read")]
    [DataRow(true, DisplayName = "The next line's first byte arrived behind the previous line")]
    public async Task NextLine_HeadTimeoutRunsFromItsFirstByte(bool sameRead)
    {
        var clock = new ManualTimeProvider();
        var chunks = sameRead ? Ascii("STATUS\r\nDEF") : Ascii("STATUS\r\n", "DEF");
        var connection = new InMemoryConnection(chunks, peerHalfClosesWhenExhausted: false);

        var serving = Server().ServeAsync(connection, Context(clock, TestContext.CancellationToken));
        clock.Advance(JustUnderHeadTimeout);
        Assert.IsFalse(serving.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));
        await serving;

        Assert.AreEqual(Banner + "210 status ok\r\n" + HeadTimedOutReply, Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task InfiniteHeadTimeout_NeverAnswers420()
    {
        var clock = new ManualTimeProvider();
        using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var limits = ExchangeLimits.Default with { HeadTimeout = Timeout.InfiniteTimeSpan };
        var connection = new InMemoryConnection(Ascii("DEF"), peerHalfClosesWhenExhausted: false);

        var serving = Server().ServeAsync(connection, Context(clock, idleTimeout.Token, limits));
        clock.Advance(TimeSpan.FromDays(1));

        Assert.IsFalse(serving.IsCompleted);
        Assert.AreEqual(0, clock.PendingTimerCount);

        await idleTimeout.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.AreEqual(Banner, Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ExchangeCancelledMidLine_IsNotTakenForAHeadTimeout()
    {
        var clock = new ManualTimeProvider();
        using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var connection = new InMemoryConnection(Ascii("DEF"), peerHalfClosesWhenExhausted: false);

        var serving = Server().ServeAsync(connection, Context(clock, idleTimeout.Token));
        await idleTimeout.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.AreEqual(Banner, Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ReplyNotTakenWithinTheWriteDeadline_ClosesWithoutAborting()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new WriteStallingConnection([], writesBeforeStalling: 1);

        var serving = Server().ServeAsync(connection, Context(clock, TestContext.CancellationToken, log: log));
        clock.Advance(HeadTimeout);
        await connection.WriteStalled.WaitAsync(TestContext.CancellationToken);
        Assert.IsFalse(serving.IsCompleted);
        clock.Advance(DictProtocolServer.LimitReplyWriteDeadline);
        await serving;

        Assert.AreEqual(Banner, Utf8(connection.WrittenBytes));
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("The reply was not written within the one-second write deadline; the connection was closed.", log.Notes[1]);
    }

    [TestMethod]
    public async Task ExchangeCancelledWhileTheReplyIsWritten_PropagatesTheCancellation()
    {
        var clock = new ManualTimeProvider();
        using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var connection = new WriteStallingConnection([], writesBeforeStalling: 1);

        var serving = Server().ServeAsync(connection, Context(clock, idleTimeout.Token));
        clock.Advance(HeadTimeout);
        await connection.WriteStalled.WaitAsync(TestContext.CancellationToken);
        await idleTimeout.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.IsFalse(connection.Aborted);
    }
}
