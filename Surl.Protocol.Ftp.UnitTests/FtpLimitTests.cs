using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ftp.FtpTestExchange;

namespace Surl.Protocol.Ftp;

/// <summary>
/// ADR-0006 section 5's FTP column, as ADR-0052 decision 10 words it: a line past the line
/// limit, a line past the head timeout, and a connection past a connection limit.
/// </summary>
[TestClass]
public sealed class FtpLimitTests
{
    private static readonly TimeSpan HeadTimeout = ExchangeLimits.Default.HeadTimeout;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task LineOfExactly8192Bytes_IsAnswered()
    {
        var written = await ServeAsync(UserLine(8192), TestContext.CancellationToken);

        Assert.AreEqual(Greeting + "331 Password required\r\n", written);
    }

    [TestMethod]
    public async Task LineOf8193Bytes_Answers500AndClosesWithoutReadingOn()
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(Ascii(UserLine(8193) + "QUIT\r\n"));

        await Server().ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log));

        Assert.AreEqual(Greeting + LineTooLongReply, Text(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        CollectionAssert.AreEqual(new[] { "A command line was longer than 8192 bytes; answered 500 and closed." }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task PartialLine_AfterHeadTimeout_Answers421AndCloses()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(Ascii("USER anon"), peerHalfClosesWhenExhausted: false);

        var serving = Server().ServeAsync(connection, Context(clock, TestContext.CancellationToken, log: log));
        clock.Advance(HeadTimeout - TimeSpan.FromTicks(1));
        Assert.IsFalse(serving.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));
        await serving;

        Assert.AreEqual(Greeting + HeadTimedOutReply, Text(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        CollectionAssert.AreEqual(
            new[] { "A command line was not complete within the head timeout; answered 421 and closed." },
            log.Notes.ToArray());
    }

    [TestMethod]
    public async Task WaitBetweenCommands_IsNotCutOffByTheHeadTimeout()
    {
        var clock = new ManualTimeProvider();
        using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var connection = new InMemoryConnection(Ascii("NOOP\r\n"), peerHalfClosesWhenExhausted: false);

        var serving = Server().ServeAsync(connection, Context(clock, idleTimeout.Token));
        clock.Advance(HeadTimeout * 4);

        Assert.IsFalse(serving.IsCompleted);
        Assert.AreEqual(Greeting + "200 NOOP ok\r\n", Text(connection.WrittenBytes));

        await idleTimeout.CancelAsync();
        await serving;
        Assert.AreEqual(Greeting + "200 NOOP ok\r\n" + ExchangeCancelledReply, Text(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ExchangeCancelledWhileWaitingForACommand_Answers421TimeoutClosingAndCloses()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var connection = new InMemoryConnection(Ascii(AnonymousLogin), peerHalfClosesWhenExhausted: false);

        var serving = Server().ServeAsync(connection, Context(clock, idleTimeout.Token, log: log));
        Assert.IsFalse(serving.IsCompleted);
        await idleTimeout.CancelAsync();
        await serving;

        Assert.AreEqual(Greeting + AnonymousLoginReplies + "421 Timeout, closing\r\n", Text(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("The exchange was cancelled; answered 421 and closed.", log.Notes[^1]);
    }

    [TestMethod]
    public async Task CancellationReplyNotTakenWithinTheWriteDeadline_ClosesWithoutAborting()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var connection = new WriteStallingConnection([], writesBeforeStalling: 1);

        var serving = Server().ServeAsync(connection, Context(clock, idleTimeout.Token, log: log));
        await idleTimeout.CancelAsync();
        await connection.WriteStalled.WaitAsync(TestContext.CancellationToken);
        Assert.IsFalse(serving.IsCompleted);
        clock.Advance(FtpProtocolServer.LimitReplyWriteDeadline);
        await serving;

        Assert.AreEqual(Greeting, Text(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("The reply was not written within the one-second write deadline; the connection was closed.", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ExchangeCancelledAtShutdownWhileWaitingForACommand_ThrowsWithNoFarewell()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var connection = new InMemoryConnection(Ascii(AnonymousLogin), peerHalfClosesWhenExhausted: false);

        var serving = Server().ServeAsync(connection, Context(clock, shutdown.Token, log: log, shutdownToken: shutdown.Token));
        Assert.IsFalse(serving.IsCompleted);
        await shutdown.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.AreEqual(Greeting + AnonymousLoginReplies, Text(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted);
        Assert.IsEmpty(log.Notes);
    }

    [TestMethod]
    public async Task ShutdownWhileALimitReplyIsWritten_Throws()
    {
        var clock = new ManualTimeProvider();
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var connection = new WriteStallingConnection([], writesBeforeStalling: 1);

        var serving = Server().ServeAsync(connection, Context(clock, shutdown.Token, shutdownToken: shutdown.Token));
        clock.Advance(HeadTimeout);
        await connection.WriteStalled.WaitAsync(TestContext.CancellationToken);
        await shutdown.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.AreEqual(Greeting, Text(connection.WrittenBytes));
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    public async Task CancellationThrownWhileTheExchangeIsNotCancelled_PropagatesWithoutAReply()
    {
        var fileSystem = new UnitTestThrowingContentFileSystem(new OperationCanceledException(), failsStatus: true);
        var connection = new InMemoryConnection(Ascii(AnonymousLogin + "SIZE a.txt\r\n"));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => Server(contentStore: fileSystem.ContentStore())
            .ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken)));

        Assert.AreEqual(Greeting + AnonymousLoginReplies, Text(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ReadFailure_PropagatesWithoutAReply()
    {
        var connection = new ReadFailingConnection(Ascii("NOOP\r\n"));

        await Assert.ThrowsExactlyAsync<IOException>(
            () => Server().ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken)));

        Assert.AreEqual(Greeting + "200 NOOP ok\r\n", Text(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task LimitReplyNotTakenWithinTheWriteDeadline_ClosesWithoutAborting()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new WriteStallingConnection([], writesBeforeStalling: 1);

        var serving = Server().ServeAsync(connection, Context(clock, TestContext.CancellationToken, log: log));
        clock.Advance(HeadTimeout);
        await connection.WriteStalled.WaitAsync(TestContext.CancellationToken);
        Assert.IsFalse(serving.IsCompleted);
        clock.Advance(FtpProtocolServer.LimitReplyWriteDeadline);
        await serving;

        Assert.AreEqual(Greeting, Text(connection.WrittenBytes));
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("The reply was not written within the one-second write deadline; the connection was closed.", log.Notes[1]);
    }

    [TestMethod]
    public async Task ExchangeCancelledWhileTheLimitReplyIsWritten_StillClosesAtTheWriteDeadline()
    {
        var clock = new ManualTimeProvider();
        using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var connection = new WriteStallingConnection([], writesBeforeStalling: 1);

        var serving = Server().ServeAsync(connection, Context(clock, idleTimeout.Token));
        clock.Advance(HeadTimeout);
        await connection.WriteStalled.WaitAsync(TestContext.CancellationToken);
        await idleTimeout.CancelAsync();
        Assert.IsFalse(serving.IsCompleted);
        clock.Advance(FtpProtocolServer.LimitReplyWriteDeadline);
        await serving;

        Assert.AreEqual(Greeting, Text(connection.WrittenBytes));
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    [DataRow(ConnectionRefusal.TooManyConnections)]
    [DataRow(ConnectionRefusal.TooManyConnectionsFromAddress)]
    public async Task WriteRefusalAsync_Answers421InPlaceOfTheGreetingAndCloses(ConnectionRefusal refusal)
    {
        var connection = new InMemoryConnection([]);

        await Server().WriteRefusalAsync(connection, refusal, TestContext.CancellationToken);

        Assert.AreEqual("421 Too many connections\r\n", Text(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_NullConnection_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => Server().WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnections, TestContext.CancellationToken).AsTask());
    }

    // "USER " and CRLF around a name that makes the line lineBytes long.
    private static string UserLine(int lineBytes) => "USER " + new string('x', lineBytes - 7) + "\r\n";
}
