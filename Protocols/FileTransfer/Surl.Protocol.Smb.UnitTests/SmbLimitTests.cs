using Surl.Protocol.Abstractions;
using static Surl.Protocol.Smb.SmbTestExchange;

namespace Surl.Protocol.Smb;

/// <summary>
/// ADR-0073 decision 7: <c>--max-message</c>, the head timeout, and the idle timeout and maximum
/// duration told from shutdown as ADR-0059 says.
/// </summary>
[TestClass]
public sealed class SmbLimitTests
{
    private static readonly TimeSpan HeadTimeout = ExchangeLimits.Default.HeadTimeout;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AMessagePastMaxMessage_IsServerErrorOnceItsHeaderIsInAndTheSessionGoesOn()
    {
        var log = new RecordingExchangeLog();
        var limits = ExchangeLimits.Default with { MaxMessageBytes = 200 };
        var tooLarge = RequestHex(SmbCommand.WriteAndX, 1, SmbSession.UserId, new string('0', 2 * 300));

        var written = await ServeAsync(TestContext.CancellationToken, [NegotiateHex, SessionSetupHex, TreeConnectHex("share"), tooLarge, TreeDisconnectHex(1)], log: log, limits: limits);

        var prefix = NegotiateResponseHex.Replace("FFFF0100", "C8000000", StringComparison.Ordinal) + SessionSetupResponseHex + TreeConnectResponseHex(1);
        Assert.AreEqual(
            prefix + ErrorHex(SmbCommand.WriteAndX, SmbStatus.ServerError) + ResponseHex(SmbCommand.TreeDisconnect, 0, 1, SmbSession.UserId),
            written);
        Assert.Contains("SMB message of 332 bytes is past --max-message 200", log.Notes);
    }

    [TestMethod]
    public async Task AMessagePastMaxMessageThatIsNotSmb_IsNotedAndClosesTheConnection()
    {
        var log = new RecordingExchangeLog();
        var limits = ExchangeLimits.Default with { MaxMessageBytes = 200 };

        var written = await ServeAsync(TestContext.CancellationToken, [NegotiateHex, "000000D2" + new string('0', 2 * 210), SessionSetupHex], log: log, limits: limits);

        Assert.AreEqual(NegotiateResponseHex.Replace("FFFF0100", "C8000000", StringComparison.Ordinal), written);
        CollectionAssert.AreEqual(new[] { "SMB message of 210 bytes is past --max-message 200" }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task AMessagePastMaxMessageShorterThanAnSmbHeader_IsNotedAndClosesTheConnection()
    {
        var log = new RecordingExchangeLog();
        var limits = ExchangeLimits.Default with { MaxMessageBytes = 10 };

        var written = await ServeAsync(TestContext.CancellationToken, ["00000014" + new string('0', 2 * 20), NegotiateHex], log: log, limits: limits);

        Assert.IsEmpty(written);
        CollectionAssert.AreEqual(new[] { "SMB message of 20 bytes is past --max-message 10" }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task NoNegotiateWithinTheHeadTimeout_ClosesWithNothingSent()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([Hex("85000000")], peerHalfClosesWhenExhausted: false);

        var serving = Server().ServeAsync(connection, Context(clock, TestContext.CancellationToken, log: log));
        clock.Advance(HeadTimeout - TimeSpan.FromTicks(1));
        Assert.IsFalse(serving.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));
        await serving;

        Assert.IsEmpty(connection.WrittenBytes);
        CollectionAssert.AreEqual(new[] { "SMB connection closed: head timeout" }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task WaitAfterTheNegotiate_IsNotCutOffByTheHeadTimeout()
    {
        var clock = new ManualTimeProvider();
        using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([Hex(NegotiateHex)], peerHalfClosesWhenExhausted: false);

        var serving = Server().ServeAsync(connection, Context(clock, idleTimeout.Token, log: log));
        clock.Advance(HeadTimeout * 4);
        Assert.IsFalse(serving.IsCompleted);

        await idleTimeout.CancelAsync();
        await serving;

        Assert.AreEqual(NegotiateResponseHex, Convert.ToHexString(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted);
        CollectionAssert.AreEqual(new[] { "SMB connection closed: idle timeout or maximum duration" }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ALimitWhileARequestIsAnswered_AnswersServerErrorAndCloses()
    {
        using var maximumDuration = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var policy = new NeverAnsweringPolicy();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([Hex(NegotiateHex + SessionSetupHex)], peerHalfClosesWhenExhausted: false);

        var serving = Server(policy).ServeAsync(connection, Context(new ManualTimeProvider(), maximumDuration.Token, log: log));
        await policy.Asked;
        await maximumDuration.CancelAsync();
        await serving;

        Assert.AreEqual(NegotiateResponseHex + ErrorHex(SmbCommand.SessionSetupAndX, SmbStatus.ServerError, 0, 0), Convert.ToHexString(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        CollectionAssert.AreEqual(new[] { "SMB connection closed: idle timeout or maximum duration" }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ALimitWhoseErrorIsNotWrittenWithinTheDeadline_ClosesAnyway()
    {
        var clock = new ManualTimeProvider();
        using var maximumDuration = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var log = new RecordingExchangeLog();
        var connection = new WriteStallingConnection([Hex(NegotiateHex + SessionSetupHex)], writesBeforeStalling: 1);

        var serving = Server().ServeAsync(connection, Context(clock, maximumDuration.Token, log: log));
        await connection.WriteStalled;
        await maximumDuration.CancelAsync();
        while (clock.PendingTimerCount == 0)
        {
            await Task.Yield();
        }

        clock.Advance(SmbProtocolServer.LimitReplyWriteDeadline);
        await serving;

        Assert.AreEqual(NegotiateResponseHex, Convert.ToHexString(connection.WrittenBytes));
        CollectionAssert.AreEqual(
            new[] { "Login accepted: ntlmv1 alice", "SMB connection closed: idle timeout or maximum duration", "The reply was not written within the one-second write deadline; the connection was closed." },
            log.Notes.ToArray());
    }

    [TestMethod]
    public async Task Shutdown_PropagatesWithNothingSent()
    {
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([Hex(NegotiateHex)], peerHalfClosesWhenExhausted: false);

        var serving = Server().ServeAsync(connection, Context(new ManualTimeProvider(), shutdown.Token, log: log, shutdownToken: shutdown.Token));
        await shutdown.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.AreEqual(NegotiateResponseHex, Convert.ToHexString(connection.WrittenBytes));
        Assert.IsEmpty(log.Notes);
    }

    [TestMethod]
    public async Task ShutdownBeforeTheNegotiate_PropagatesRatherThanReadingAsAHeadTimeout()
    {
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);

        var serving = Server().ServeAsync(connection, Context(new ManualTimeProvider(), shutdown.Token, log: log, shutdownToken: shutdown.Token));
        await shutdown.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.IsEmpty(log.Notes);
    }
}
