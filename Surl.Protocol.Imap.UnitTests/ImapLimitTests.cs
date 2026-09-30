using Surl.Protocol.Abstractions;
using static Surl.Protocol.Imap.ImapTestExchange;

namespace Surl.Protocol.Imap;

/// <summary>
/// ADR-0055 decisions 9 and 12: literals, and ADR-0006's limits in IMAP's words.
/// </summary>
[TestClass]
public sealed class ImapLimitTests
{
    private static readonly ExchangeLimits ThirtyByteLines = ExchangeLimits.Default with { MaxLineBytes = 30 };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_FirstLineTooLong_SaysByeAndCloses()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(Server(AnonymousStore(clock)), "a NOOP " + new string('x', 40) + "\r\nb NOOP\r\n", clock, TestContext.CancellationToken, ThirtyByteLines, log);

        Assert.AreEqual("* BYE surl Command line too long, closing\r\n", AfterGreeting(connection));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("A command was longer than 30 bytes; answered and closed.", log.Notes.Single());
    }

    [TestMethod]
    [DataRow("a LOGIN {3}\r\nabc " + "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\r\n")]
    [DataRow("a LOGIN {3}\r\nabc xxxxxxxxxxxxxx\r\n")]
    public async Task ServeAsync_CommandPastTheLineLimitAfterALiteral_AnswersBadWithTheTagAndCloses(string request)
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(Server(AnonymousStore(clock)), request + "b NOOP\r\n", clock, TestContext.CancellationToken, ThirtyByteLines);

        Assert.AreEqual("+ Ready for literal data\r\na BAD Command line too long\r\n", AfterGreeting(connection));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    [DataRow("a LOGIN {17}\r\n")]
    [DataRow("a X {99999999999999999999}\r\n")]
    public async Task ServeAsync_LiteralPastTheBound_AnswersLiteralTooLongAndGoesOn(string request)
    {
        var responses = await ResponsesAsync(request + "b NOOP\r\n", TestContext.CancellationToken, limits: ThirtyByteLines);

        Assert.AreEqual("a BAD Literal too long\r\nb OK NOOP completed\r\n", responses);
    }

    [TestMethod]
    public async Task ServeAsync_LiteralPastArrayBoundsWithNoLineLimit_AnswersLiteralTooLong()
    {
        var limits = ExchangeLimits.Default with { MaxLineBytes = 0 };

        var responses = await ResponsesAsync("a LOGIN {2147483648}\r\nb LOGIN {1}\r\nu p\r\n", TestContext.CancellationToken, limits: limits);

        Assert.AreEqual("a BAD Literal too long\r\n+ Ready for literal data\r\nb OK LOGIN completed\r\n", responses);
    }

    [TestMethod]
    public async Task ServeAsync_NonSynchronizingLiteral_AnswersBadAndCloses()
    {
        var clock = new ManualTimeProvider();

        var connection = await ServeAsync(Server(AnonymousStore(clock)), "a LOGIN {1+}\r\nu p\r\nb NOOP\r\n", clock, TestContext.CancellationToken);

        Assert.AreEqual("a BAD Non-synchronizing literals are not supported\r\n", AfterGreeting(connection));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_LiteralAfterAnInvalidTag_AnswersInvalidTagWithoutAContinuation()
    {
        var responses = await ResponsesAsync("+a LOGIN {3}\r\nb NOOP\r\n", TestContext.CancellationToken);

        Assert.AreEqual("* BAD Invalid tag\r\nb OK NOOP completed\r\n", responses);
    }

    [TestMethod]
    public async Task ServeAsync_PeerClosesInsideALiteral_EndsTheSession()
    {
        var responses = await ResponsesAsync("a LOGIN {10}\r\nabc", TestContext.CancellationToken);

        Assert.AreEqual("+ Ready for literal data\r\n", responses);
    }

    [TestMethod]
    public async Task ServeAsync_NoCommandWithinTheHeadTimeout_SaysByeAndCloses()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(Bytes("a NOO"), peerHalfClosesWhenExhausted: false);

        var serving = Server(AnonymousStore(clock)).ServeAsync(connection, Context(clock, TestContext.CancellationToken, log: log));
        clock.Advance(ExchangeLimits.Default.HeadTimeout - TimeSpan.FromTicks(1));
        Assert.IsFalse(serving.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));
        await serving;

        Assert.AreEqual("* BYE surl Timeout waiting for a command, closing\r\n", AfterGreeting(connection));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual("A command was not complete within the head timeout; answered * BYE and closed.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_LiteralNotCompleteWithinTheHeadTimeout_SaysByeAndCloses()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection(Bytes("a LOGIN {10}\r\nabc"), peerHalfClosesWhenExhausted: false);

        var serving = Server(AnonymousStore(clock)).ServeAsync(connection, Context(clock, TestContext.CancellationToken));
        while (clock.PendingTimerCount < 1 || !Utf8(connection.WrittenBytes).Contains('+', StringComparison.Ordinal))
        {
            await Task.Yield();
        }

        clock.Advance(ExchangeLimits.Default.HeadTimeout);
        await serving;

        Assert.AreEqual("+ Ready for literal data\r\n* BYE surl Timeout waiting for a command, closing\r\n", AfterGreeting(connection));
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledInsideALiteral_Throws()
    {
        var clock = new ManualTimeProvider();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var connection = new InMemoryConnection(Bytes("a LOGIN {10}\r\nabc"), peerHalfClosesWhenExhausted: false);

        var serving = Server(AnonymousStore(clock)).ServeAsync(connection, Context(clock, cancellation.Token));
        while (!Utf8(connection.WrittenBytes).Contains('+', StringComparison.Ordinal))
        {
            await Task.Yield();
        }

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    [TestMethod]
    public async Task ServeAsync_LimitResponseNotReadWithinTheDeadline_ClosesAndNotesIt()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new WriteStallingConnection(Bytes("a NOOP " + new string('x', 40)), writesBeforeStalling: 1);

        var serving = Server(AnonymousStore(clock)).ServeAsync(connection, Context(clock, TestContext.CancellationToken, ThirtyByteLines, log));
        await connection.WriteStalled;
        clock.Advance(ImapProtocolServer.LimitReplyWriteDeadline);
        await serving;

        Assert.AreEqual(Greeting, Utf8(connection.WrittenBytes));
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("The response was not written within the one-second write deadline; the connection was closed.", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledWhileALimitResponseWaits_Throws()
    {
        var clock = new ManualTimeProvider();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var connection = new WriteStallingConnection(Bytes("a NOOP " + new string('x', 40)), writesBeforeStalling: 1);

        var serving = Server(AnonymousStore(clock)).ServeAsync(connection, Context(clock, cancellation.Token, ThirtyByteLines));
        await connection.WriteStalled;
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }
}
