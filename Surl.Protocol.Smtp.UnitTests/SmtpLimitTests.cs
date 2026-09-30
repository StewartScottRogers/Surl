using Surl.Protocol.Abstractions;
using static Surl.Protocol.Smtp.SmtpTestExchange;

namespace Surl.Protocol.Smtp;

/// <summary>
/// ADR-0053 decision 7: ADR-0006's limits in SMTP's words.
/// </summary>
[TestClass]
public sealed class SmtpLimitTests
{
    private const string Envelope = "EHLO c\r\nMAIL FROM:<a@x>\r\nRCPT TO:<b@y>\r\nDATA\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_LineTooLong_Answers500AndCloses()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var limits = ExchangeLimits.Default with { MaxLineBytes = 16 };

        var connection = await ServeAsync(AnonymousStore(clock), "NOOP " + new string('x', 40) + "\r\nNOOP\r\n", clock, TestContext.CancellationToken, limits, log);

        Assert.AreEqual(Greeting + "500 5.5.6 Command line too long\r\n", Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("A command line was longer than 16 bytes; answered 500 and closed.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_NoCommandWithinTheHeadTimeout_Answers421AndCloses()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(Ascii("NOO"), peerHalfClosesWhenExhausted: false);

        var serving = Server(AnonymousStore(clock)).ServeAsync(connection, Context(clock, TestContext.CancellationToken, log: log));
        clock.Advance(ExchangeLimits.Default.HeadTimeout - TimeSpan.FromTicks(1));
        Assert.IsFalse(serving.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));
        await serving;

        Assert.AreEqual(Greeting + "421 4.4.2 surl Timeout waiting for a command, closing\r\n", Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual("A command line was not complete within the head timeout; answered 421 and closed.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_LimitReplyNotReadWithinTheDeadline_ClosesAndNotesIt()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var limits = ExchangeLimits.Default with { MaxLineBytes = 16 };
        var connection = new WriteStallingConnection(Ascii("NOOP " + new string('x', 40)), writesBeforeStalling: 1);

        var serving = Server(AnonymousStore(clock)).ServeAsync(connection, Context(clock, TestContext.CancellationToken, limits, log));
        await connection.WriteStalled;
        clock.Advance(SmtpProtocolServer.LimitReplyWriteDeadline);
        await serving;

        Assert.AreEqual(Greeting, Utf8(connection.WrittenBytes));
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("The reply was not written within the one-second write deadline; the connection was closed.", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_ShutdownWhileALimitReplyWaits_Throws()
    {
        var clock = new ManualTimeProvider();
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var limits = ExchangeLimits.Default with { MaxLineBytes = 16 };
        var connection = new WriteStallingConnection(Ascii("NOOP " + new string('x', 40)), writesBeforeStalling: 1);

        var serving = Server(AnonymousStore(clock)).ServeAsync(connection, Context(clock, shutdown.Token, limits, shutdownToken: shutdown.Token));
        await connection.WriteStalled;
        await shutdown.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledForALimitWhileALimitReplyWaits_StillClosesAtTheWriteDeadline()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var limits = ExchangeLimits.Default with { MaxLineBytes = 16 };
        var connection = new WriteStallingConnection(Ascii("NOOP " + new string('x', 40)), writesBeforeStalling: 1);

        var serving = Server(AnonymousStore(clock)).ServeAsync(connection, Context(clock, cancellation.Token, limits, log));
        await connection.WriteStalled;
        await cancellation.CancelAsync();
        Assert.IsFalse(serving.IsCompleted);
        clock.Advance(SmtpProtocolServer.LimitReplyWriteDeadline);
        await serving;

        Assert.AreEqual(Greeting, Utf8(connection.WrittenBytes));
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("The reply was not written within the one-second write deadline; the connection was closed.", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_IdleTimeout_Answers421TimeoutClosingThenCompletesWrites()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var idleTimeout = TimeSpan.FromSeconds(120);
        using var idle = new CancellationTokenSource(idleTimeout, clock);
        var connection = new InMemoryConnection(Ascii("EHLO c\r\n"), peerHalfClosesWhenExhausted: false);

        var serving = Server(AnonymousStore(clock)).ServeAsync(connection, Context(clock, idle.Token, log: log));
        clock.Advance(idleTimeout - TimeSpan.FromTicks(1));
        Assert.IsFalse(serving.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));
        await serving;

        Assert.AreEqual(Greeting + EhloReply + "421 4.4.2 surl Timeout, closing\r\n", Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("The exchange was cancelled for a limit; answered 421 and closed.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_MaxExchangeDurationMidBody_Answers421TimeoutClosingStoresNothingAndCompletesWrites()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var store = AnonymousStore(clock);
        var maxExchangeDuration = TimeSpan.FromSeconds(3600);
        using var duration = new CancellationTokenSource(maxExchangeDuration, clock);
        var connection = new InMemoryConnection(Ascii(Envelope + "Subject: s\r\n\r\npart of a body"), peerHalfClosesWhenExhausted: false);

        var serving = Server(store).ServeAsync(connection, Context(clock, duration.Token, log: log));
        clock.Advance(maxExchangeDuration);
        await serving;

        StringAssert.EndsWith(Utf8(connection.WrittenBytes), "354 End data with <CR><LF>.<CR><LF>\r\n421 4.4.2 surl Timeout, closing\r\n");
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsEmpty(Inbox(store, string.Empty));
        Assert.AreEqual("The exchange was cancelled for a limit; answered 421 and closed.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_Shutdown_EndsWithNoFarewell()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var connection = new InMemoryConnection(Ascii("EHLO c\r\n"), peerHalfClosesWhenExhausted: false);

        var serving = Server(AnonymousStore(clock)).ServeAsync(connection, Context(clock, shutdown.Token, log: log, shutdownToken: shutdown.Token));
        await shutdown.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.AreEqual(Greeting + EhloReply, Utf8(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted);
        Assert.IsEmpty(log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_FarewellNotReadWithinTheWriteDeadline_ClosesAndNotesIt()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        using var idle = new CancellationTokenSource(TimeSpan.FromSeconds(10), clock);
        var connection = new WriteStallingConnection(Ascii("NOOP\r\n"), writesBeforeStalling: 2);

        var serving = Server(AnonymousStore(clock)).ServeAsync(connection, Context(clock, idle.Token, log: log));
        clock.Advance(TimeSpan.FromSeconds(10));
        await connection.WriteStalled;
        clock.Advance(SmtpProtocolServer.LimitReplyWriteDeadline);
        await serving;

        Assert.AreEqual(Greeting + "250 2.0.0 OK\r\n", Utf8(connection.WrittenBytes));
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("The reply was not written within the one-second write deadline; the connection was closed.", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_BodyPastMaxFilesizeInOneRead_Answers552StoresNothingAndCloses()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var store = AnonymousStore(clock);
        var limits = ExchangeLimits.Default with { MaxUploadBytes = 200 };

        var connection = await ServeAsync(store, Envelope + new string('x', 300) + "\r\n.\r\nNOOP\r\n", clock, TestContext.CancellationToken, limits, log);

        StringAssert.EndsWith(Utf8(connection.WrittenBytes), "354 End data with <CR><LF>.<CR><LF>\r\n552 5.3.4 Message exceeds the size limit\r\n");
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsEmpty(Inbox(store, string.Empty));
        Assert.AreEqual("Message refused: past --max-filesize after 0 bytes", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_BodyThatFitsMaxFilesizeButNotWithTheTraceFields_Answers552AndCloses()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var store = AnonymousStore(clock);
        var limits = ExchangeLimits.Default with { MaxUploadBytes = 200 };
        var chunks = Ascii(Envelope, new string('x', 60), new string('x', 60), "\r\n.\r\nNOOP\r\n");

        var connection = new InMemoryConnection(chunks);
        await Server(store).ServeAsync(connection, Context(clock, TestContext.CancellationToken, limits, log));

        StringAssert.EndsWith(Utf8(connection.WrittenBytes), "552 5.3.4 Message exceeds the size limit\r\n");
        Assert.IsEmpty(Inbox(store, string.Empty));
        Assert.AreEqual("Message refused: past --max-filesize after 122 bytes", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_TraceFieldsAlonePastMaxFilesize_RefusesEvenAnEmptyBody()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        var limits = ExchangeLimits.Default with { MaxUploadBytes = 10 };

        var connection = await ServeAsync(store, Envelope + ".\r\n", clock, TestContext.CancellationToken, limits);

        StringAssert.EndsWith(Utf8(connection.WrittenBytes), "552 5.3.4 Message exceeds the size limit\r\n");
        Assert.IsEmpty(Inbox(store, string.Empty));
    }

    [TestMethod]
    public async Task ServeAsync_BodyExactlyAtMaxFilesizeWithTheTraceFields_IsStored()
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        var body = new string('x', 30) + "\r\n";
        var limits = ExchangeLimits.Default with { MaxUploadBytes = TraceFields("a@x").Length + body.Length };

        await ServeAsync(store, Envelope + body + ".\r\n", clock, TestContext.CancellationToken, limits);

        CollectionAssert.AreEqual(new[] { TraceFields("a@x") + body }, Inbox(store, string.Empty).ToList());
    }
}
