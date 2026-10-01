using Surl.Protocol.Abstractions;
using static Surl.Protocol.Pop3.Pop3TestExchange;

namespace Surl.Protocol.Pop3;

/// <summary>
/// ADR-0056 decision 9: ADR-0006's limits in POP3's words.
/// </summary>
[TestClass]
public sealed class Pop3LimitTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_LineTooLong_AnswersErrAndCloses()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var limits = ExchangeLimits.Default with { MaxLineBytes = 16 };

        var connection = await ServeAsync(AccountStore(clock), "NOOP " + new string('x', 40) + "\r\nNOOP\r\n", clock, TestContext.CancellationToken, limits: limits, log: log);

        Assert.AreEqual(Greeting + "-ERR Command line too long, closing\r\n", Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("A command line was longer than 16 bytes; answered -ERR and closed.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_NoCommandWithinTheHeadTimeout_AnswersErrAndCloses()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(Ascii("NOO"), peerHalfClosesWhenExhausted: false);

        var serving = Server(AccountStore(clock)).ServeAsync(connection, Context(clock, TestContext.CancellationToken, log: log));
        clock.Advance(ExchangeLimits.Default.HeadTimeout - TimeSpan.FromTicks(1));
        Assert.IsFalse(serving.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));
        await serving;

        Assert.AreEqual(Greeting + "-ERR Timeout waiting for a command, closing\r\n", Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual("A command line was not complete within the head timeout; answered -ERR and closed.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_LimitReplyNotReadWithinTheDeadline_ClosesAndNotesIt()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var limits = ExchangeLimits.Default with { MaxLineBytes = 16 };
        var connection = new WriteStallingConnection(Ascii("NOOP " + new string('x', 40)), writesBeforeStalling: 1);

        var serving = Server(AccountStore(clock)).ServeAsync(connection, Context(clock, TestContext.CancellationToken, limits, log));
        await connection.WriteStalled;
        clock.Advance(Pop3ProtocolServer.LimitReplyWriteDeadline);
        await serving;

        Assert.AreEqual(Greeting, Utf8(connection.WrittenBytes));
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("The reply was not written within the one-second write deadline; the connection was closed.", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledWhileALimitReplyWaits_Throws()
    {
        var clock = new ManualTimeProvider();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var limits = ExchangeLimits.Default with { MaxLineBytes = 16 };
        var connection = new WriteStallingConnection(Ascii("NOOP " + new string('x', 40)), writesBeforeStalling: 1);

        var serving = Server(AccountStore(clock)).ServeAsync(connection, Context(clock, cancellation.Token, limits));
        await connection.WriteStalled;
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    [TestMethod]
    [DataRow(ConnectionRefusal.TooManyConnections)]
    [DataRow(ConnectionRefusal.TooManyConnectionsFromAddress)]
    public async Task WriteRefusalAsync_AnyRefusal_AnswersTooManyConnectionsAndCloses(ConnectionRefusal refusal)
    {
        var connection = new InMemoryConnection([]);

        await Server(AccountStore(new ManualTimeProvider())).WriteRefusalAsync(connection, refusal, TestContext.CancellationToken);

        Assert.AreEqual("-ERR surl Too many connections, closing\r\n", Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_NullConnection_Throws()
    {
        var server = Server(AccountStore(new ManualTimeProvider()));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => server.WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnections, TestContext.CancellationToken).AsTask());
    }
}
