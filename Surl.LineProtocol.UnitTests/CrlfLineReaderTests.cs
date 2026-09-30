using Surl.Protocol.Abstractions;
using static Surl.LineProtocol.Wire;

namespace Surl.LineProtocol;

[TestClass]
public sealed class CrlfLineReaderTests
{
    private static readonly TimeSpan HeadTimeout = ExchangeLimits.Default.HeadTimeout;
    private static readonly TimeSpan JustUnderHeadTimeout = HeadTimeout - TimeSpan.FromTicks(1);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadLineAsync_LinesSplitAtEveryByte_AreReadWholeAndInOrder()
    {
        foreach (var chunks in Splits("EHLO a\r\nNOOP\r\n"))
        {
            using var reader = Reader(Connection(chunks));

            Assert.AreEqual("EHLO a", await ReadTextAsync(reader), string.Join("|", chunks));
            Assert.AreEqual("NOOP", await ReadTextAsync(reader), string.Join("|", chunks));
            Assert.AreEqual(CrlfLineReadOutcome.Closed, (await reader.ReadLineAsync(TestContext.CancellationToken)).Outcome);
        }
    }

    [TestMethod]
    public async Task ReadLineAsync_PipelinedLinesInOneRead_AreEachReturned()
    {
        using var reader = Reader(Connection("MAIL FROM:<a@b>\r\nRCPT TO:<c@d>\r\nDATA\r\n"));

        Assert.AreEqual("MAIL FROM:<a@b>", await ReadTextAsync(reader));
        Assert.AreEqual("RCPT TO:<c@d>", await ReadTextAsync(reader));
        Assert.AreEqual("DATA", await ReadTextAsync(reader));
    }

    [TestMethod]
    [DataRow("A\nB\r\n", "A\nB", DisplayName = "A bare LF is part of the line")]
    [DataRow("A\rB\r\n", "A\rB", DisplayName = "A bare CR is part of the line")]
    [DataRow("A\r\r\n", "A\r", DisplayName = "A CR before CRLF is part of the line")]
    [DataRow("\r\n", "", DisplayName = "An empty line")]
    public async Task ReadLineAsync_OnlyCrlfEndsALine(string input, string expected)
    {
        using var reader = Reader(Connection(input));

        Assert.AreEqual(expected, await ReadTextAsync(reader));
    }

    [TestMethod]
    public async Task ReadLineAsync_BareLfOnly_NeverEndsTheLine()
    {
        using var reader = Reader(Connection("QUIT\n"));

        Assert.AreEqual(CrlfLineReadOutcome.Closed, (await reader.ReadLineAsync(TestContext.CancellationToken)).Outcome);
    }

    [TestMethod]
    public async Task ReadLineAsync_LineExactlyAtTheLimit_IsRead()
    {
        var limits = ExchangeLimits.Default with { MaxLineBytes = 6 };
        using var reader = Reader(Connection("NOOP\r\nX"), limits);

        Assert.AreEqual("NOOP", await ReadTextAsync(reader));
    }

    [TestMethod]
    public async Task ReadLineAsync_LineOneBytePastTheLimit_IsTooLongAndNoBytePastTheLimitIsRead()
    {
        var limits = ExchangeLimits.Default with { MaxLineBytes = 6 };
        var connection = Connection("NOOPS\r\nNEXT");
        using var reader = Reader(connection, limits);

        var result = await reader.ReadLineAsync(TestContext.CancellationToken);

        Assert.AreEqual(CrlfLineReadOutcome.LineTooLong, result.Outcome);
        Assert.IsNull(result.Line);
        Assert.AreEqual("\nNEXT", await RemainingTextAsync(connection, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadLineAsync_SecondLineOverTheLimitBehindAFirstLine_IsTooLong()
    {
        var limits = ExchangeLimits.Default with { MaxLineBytes = 6 };
        var connection = Connection("NOOP\r\nABCDEFG");
        using var reader = Reader(connection, limits);

        Assert.AreEqual("NOOP", await ReadTextAsync(reader));
        Assert.AreEqual(CrlfLineReadOutcome.LineTooLong, (await reader.ReadLineAsync(TestContext.CancellationToken)).Outcome);
        Assert.AreEqual("G", await RemainingTextAsync(connection, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadLineAsync_LimitOfZero_ReadsALineLongerThanTheBuffer()
    {
        var limits = ExchangeLimits.Default with { MaxLineBytes = 0 };
        var longLine = new string('x', 5000);
        using var reader = Reader(Connection(longLine[..2500], longLine[2500..] + "\r\n"), limits);

        Assert.AreEqual(longLine, await ReadTextAsync(reader));
    }

    [TestMethod]
    public async Task ReadLineAsync_PeerClosesMidLine_IsClosedWithNoPartialLine()
    {
        using var reader = Reader(Connection("NOO"));

        var result = await reader.ReadLineAsync(TestContext.CancellationToken);

        Assert.AreEqual(CrlfLineReadOutcome.Closed, result.Outcome);
        Assert.IsNull(result.Line);
    }

    [TestMethod]
    public async Task ReadLineAsync_FirstLineNotCompleteWithinTheHeadTimeoutFromCreation_TimesOut()
    {
        var clock = new ManualTimeProvider();
        using var reader = Reader(OpenConnection(), clock: clock);

        var reading = reader.ReadLineAsync(TestContext.CancellationToken).AsTask();
        clock.Advance(JustUnderHeadTimeout);
        Assert.IsFalse(reading.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));

        Assert.AreEqual(CrlfLineReadOutcome.HeadTimedOut, (await reading).Outcome);
    }

    [TestMethod]
    public async Task ReadLineAsync_HeadTimeoutRunsFromCreationNotFromTheCall()
    {
        var clock = new ManualTimeProvider();
        using var reader = Reader(OpenConnection("EHL"), clock: clock);

        clock.Advance(JustUnderHeadTimeout);
        var reading = reader.ReadLineAsync(TestContext.CancellationToken).AsTask();
        Assert.IsFalse(reading.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));

        Assert.AreEqual(CrlfLineReadOutcome.HeadTimedOut, (await reading).Outcome);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "The next line's first byte arrives in a later read")]
    [DataRow(true, DisplayName = "The next line's first byte arrived behind the previous line")]
    public async Task ReadLineAsync_LaterLine_HeadTimeoutRunsFromItsFirstByte(bool sameRead)
    {
        var clock = new ManualTimeProvider();
        var connection = sameRead ? OpenConnection("NOOP\r\nQU") : OpenConnection("NOOP\r\n", "QU");
        using var reader = Reader(connection, clock: clock);

        Assert.AreEqual("NOOP", await ReadTextAsync(reader));
        Assert.AreEqual(0, clock.PendingTimerCount);
        var reading = reader.ReadLineAsync(TestContext.CancellationToken).AsTask();
        clock.Advance(JustUnderHeadTimeout);
        Assert.IsFalse(reading.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));

        Assert.AreEqual(CrlfLineReadOutcome.HeadTimedOut, (await reading).Outcome);
    }

    [TestMethod]
    public async Task ReadLineAsync_WaitBetweenLines_IsNotCutOffByTheHeadTimeout()
    {
        var clock = new ManualTimeProvider();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var reader = Reader(OpenConnection("NOOP\r\n"), clock: clock);

        Assert.AreEqual("NOOP", await ReadTextAsync(reader));
        var reading = reader.ReadLineAsync(cancellation.Token).AsTask();
        clock.Advance(HeadTimeout * 4);
        Assert.IsFalse(reading.IsCompleted);

        await cancellation.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => reading);
    }

    [TestMethod]
    public async Task ReadLineAsync_InfiniteHeadTimeout_NeverTimesOut()
    {
        var clock = new ManualTimeProvider();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var limits = ExchangeLimits.Default with { HeadTimeout = Timeout.InfiniteTimeSpan };
        using var reader = Reader(OpenConnection("EHL"), limits, clock);

        var reading = reader.ReadLineAsync(cancellation.Token).AsTask();
        clock.Advance(TimeSpan.FromDays(1));
        Assert.IsFalse(reading.IsCompleted);
        Assert.AreEqual(0, clock.PendingTimerCount);

        await cancellation.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => reading);
    }

    [TestMethod]
    public async Task ReadLineAsync_ExchangeCancelledMidLine_ThrowsRatherThanTimingOut()
    {
        var clock = new ManualTimeProvider();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var reader = Reader(OpenConnection("EHL"), clock: clock);

        var reading = reader.ReadLineAsync(cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => reading);
    }

    [TestMethod]
    public void Dispose_StopsTheHeadTimeoutClock()
    {
        var clock = new ManualTimeProvider();
        var reader = Reader(OpenConnection(), clock: clock);
        Assert.AreEqual(1, clock.PendingTimerCount);

        reader.Dispose();

        Assert.AreEqual(0, clock.PendingTimerCount);
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        var connection = Connection();
        var clock = new ManualTimeProvider();

        Assert.ThrowsExactly<ArgumentNullException>(() => new CrlfLineReader(null!, ExchangeLimits.Default, clock));
        Assert.ThrowsExactly<ArgumentNullException>(() => new CrlfLineReader(connection, null!, clock));
        Assert.ThrowsExactly<ArgumentNullException>(() => new CrlfLineReader(connection, ExchangeLimits.Default, null!));
    }

    [TestMethod]
    public async Task DiscardBuffered_AfterStartTls_ThrowsAwayPipelinedBytesAndCountsThem()
    {
        var connection = Connection("STARTTLS\r\nMAIL FROM:<x@y>\r\n", "EHLO after\r\n");
        using var reader = Reader(connection);

        Assert.AreEqual("STARTTLS", await ReadTextAsync(reader));
        Assert.AreEqual(17, reader.DiscardBuffered());
        Assert.AreEqual(0, reader.DiscardBuffered());

        Assert.AreEqual("EHLO after", await ReadTextAsync(reader));
    }

    [TestMethod]
    public async Task ReadCountedRunAsync_TakesBufferedBytesThenReadsTheRest_AndLeavesWhatFollows()
    {
        foreach (var chunks in Splits("A1 APPEND x {5}\r\nHello)\r\n"))
        {
            using var reader = Reader(Connection(chunks));
            var literal = new MemoryStream();

            Assert.AreEqual("A1 APPEND x {5}", await ReadTextAsync(reader));
            Assert.IsTrue(await reader.ReadCountedRunAsync(5, literal, TestContext.CancellationToken));
            Assert.AreEqual("Hello", Text(literal.ToArray()), string.Join("|", chunks));
            Assert.AreEqual(")", await ReadTextAsync(reader));
        }
    }

    [TestMethod]
    public async Task ReadCountedRunAsync_ZeroBytes_ReadsNothing()
    {
        using var reader = Reader(OpenConnection());

        Assert.IsTrue(await reader.ReadCountedRunAsync(0, new MemoryStream(), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadCountedRunAsync_PeerClosesFirst_IsFalse()
    {
        using var reader = Reader(Connection("Hel"));

        Assert.IsFalse(await reader.ReadCountedRunAsync(5, new MemoryStream(), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadCountedRunAsync_IsNotUnderTheHeadTimeout()
    {
        var clock = new ManualTimeProvider();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var reader = Reader(OpenConnection("Hel"), clock: clock);

        var reading = reader.ReadCountedRunAsync(5, new MemoryStream(), cancellation.Token).AsTask();
        clock.Advance(HeadTimeout * 2);
        Assert.IsFalse(reading.IsCompleted);

        await cancellation.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => reading);
    }

    [TestMethod]
    public async Task ReadCountedRunAsync_BadArguments_Throw()
    {
        using var reader = Reader(Connection());

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            () => reader.ReadCountedRunAsync(-1, new MemoryStream(), TestContext.CancellationToken).AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => reader.ReadCountedRunAsync(1, null!, TestContext.CancellationToken).AsTask());
    }

    private async Task<string?> ReadTextAsync(CrlfLineReader reader)
    {
        var result = await reader.ReadLineAsync(TestContext.CancellationToken);

        Assert.AreEqual(CrlfLineReadOutcome.LineRead, result.Outcome);
        return Text(result.Line);
    }
}
