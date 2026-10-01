using System.Net;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ldap.LdapRequestBytes;
using static Surl.Protocol.Ldap.LdapServerExchange;

namespace Surl.Protocol.Ldap;

/// <summary>
/// ADR-0072 decision 6's limits: <c>--max-message</c>, the head timeout, the idle timeout and
/// maximum duration the engine cancels for, shutdown, and the Notice of Disconnection's write
/// deadline.
/// </summary>
[TestClass]
public sealed class LdapProtocolServerLimitTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_RecordedBindPastMaxMessage_IsRefusedByItsLengthBeforeItsBodyIsRead()
    {
        var bind = RecordedFixture.ReadRequestMessages("simple-bind-base-search")[0];
        var connection = new ReadCountingConnection(new InMemoryConnection([bind]));
        var inner = connection;
        var log = new RecordingExchangeLog();
        var limits = ExchangeLimits.Default with { MaxMessageBytes = bind.Length - 1 };
        var policy = new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted);

        await Server(policy).ServeAsync(connection, Context(TestContext.CancellationToken, limits: limits, log: log));

        Assert.AreEqual(6, inner.BytesRead);
        Assert.IsEmpty(policy.Logins);
        CollectionAssert.AreEqual(
            new[] { $"LDAP Notice of Disconnection: protocolError: a message of 33 bytes is past --max-message 32" },
            log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_RecordedBindPastMaxMessage_SendsTheNoticeOfDisconnection()
    {
        var bind = RecordedFixture.ReadRequestMessages("simple-bind-base-search")[0];
        var connection = new InMemoryConnection([bind]);
        var limits = ExchangeLimits.Default with { MaxMessageBytes = 20 };

        await Server(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted)).ServeAsync(connection, Context(TestContext.CancellationToken, limits: limits));

        Assert.AreEqual(
            "#0 extendedResponse protocolError \"a message of 33 bytes is past --max-message 20\" [10] 1.3.6.1.4.1.1466.20036",
            LdapResponseTranscript.Of(connection.WrittenBytes).Single());
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_FirstMessageNotWholeWithinTheHeadTimeout_ClosesWithNothingSent()
    {
        var time = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);

        var serving = Server(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted)).ServeAsync(connection, Context(TestContext.CancellationToken, time, log: log));
        time.Advance(ExchangeLimits.Default.HeadTimeout);
        await serving;

        Assert.IsEmpty(connection.WrittenBytes);
        CollectionAssert.AreEqual(new[] { "The first message was not complete within the head timeout; closed with no reply." }, log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_IdleTimeoutAfterTheRecordedBind_IsTheNoticeOfDisconnectionUnavailable()
    {
        using var exchange = new CancellationTokenSource();
        var log = new RecordingExchangeLog();
        var bind = RecordedFixture.ReadRequestMessages("simple-bind-base-search")[0];
        var connection = new InMemoryConnection([bind], peerHalfClosesWhenExhausted: false);

        var serving = Server(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted)).ServeAsync(connection, Context(exchange.Token, log: log));
        await exchange.CancelAsync();
        await serving;

        CollectionAssert.AreEqual(
            new[]
            {
                "#1 bindResponse success",
                "#0 extendedResponse unavailable \"idle timeout or maximum duration\" [10] 1.3.6.1.4.1.1466.20036",
            },
            LdapResponseTranscript.Of(connection.WrittenBytes).ToArray());
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual("LDAP Notice of Disconnection: unavailable: idle timeout or maximum duration", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_MaximumDurationBeforeTheFirstMessageIsWhole_IsTheNoticeOfDisconnectionUnavailable()
    {
        using var exchange = new CancellationTokenSource();
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);
        var limits = ExchangeLimits.Default with { HeadTimeout = Timeout.InfiniteTimeSpan };

        var serving = Server(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted)).ServeAsync(connection, Context(exchange.Token, limits: limits));
        await exchange.CancelAsync();
        await serving;

        Assert.AreEqual(
            "#0 extendedResponse unavailable \"idle timeout or maximum duration\" [10] 1.3.6.1.4.1.1466.20036",
            LdapResponseTranscript.Of(connection.WrittenBytes).Single());
    }

    [TestMethod]
    public async Task ServeAsync_MaximumDurationWhileTheHeadTimeoutRuns_IsTheNoticeNotAHeadTimeout()
    {
        using var exchange = new CancellationTokenSource();
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);

        var serving = Server(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted)).ServeAsync(connection, Context(exchange.Token, new ManualTimeProvider()));
        await exchange.CancelAsync();
        await serving;

        Assert.AreEqual(
            "#0 extendedResponse unavailable \"idle timeout or maximum duration\" [10] 1.3.6.1.4.1.1466.20036",
            LdapResponseTranscript.Of(connection.WrittenBytes).Single());
    }

    [TestMethod]
    public async Task ServeAsync_Shutdown_EndsWithNoFarewell()
    {
        using var shutdown = new CancellationTokenSource();
        var bind = RecordedFixture.ReadRequestMessages("simple-bind-base-search")[0];
        var connection = new InMemoryConnection([bind], peerHalfClosesWhenExhausted: false);

        var serving = Server(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted)).ServeAsync(connection, Context(shutdown.Token, shutdownToken: shutdown.Token));
        await shutdown.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.AreEqual("#1 bindResponse success", LdapResponseTranscript.Of(connection.WrittenBytes).Single());
        Assert.IsFalse(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_NoticeNotWrittenWithinTheDeadline_IsNotedAndTheExchangeEnds()
    {
        var time = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new StalledWriteConnection(new InMemoryConnection([Hex("30050201027E00")]));

        var serving = Server(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted)).ServeAsync(connection, Context(TestContext.CancellationToken, time, log: log));
        time.Advance(LdapProtocolServer.LimitReplyWriteDeadline);
        await serving;

        Assert.AreEqual(
            "The Notice of Disconnection was not written within the one-second write deadline; the connection was closed.",
            log.Notes[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_ShutdownWhileTheNoticeIsWritten_EndsWithTheCancellation()
    {
        using var shutdown = new CancellationTokenSource();
        var connection = new StalledWriteConnection(new InMemoryConnection([Hex("30050201027E00")]));

        var serving = Server(new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted))
            .ServeAsync(connection, Context(TestContext.CancellationToken, shutdownToken: shutdown.Token));
        await shutdown.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    // A connection whose every write waits until it is cancelled, as a peer that reads nothing.
    private sealed class StalledWriteConnection(InMemoryConnection inner) : IConnection
    {
        public EndPoint LocalEndPoint => inner.LocalEndPoint;

        public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

        public TlsSession? TlsSession => inner.TlsSession;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

        public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) =>
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

        public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => inner.CompleteWritesAsync(cancellationToken);

        public void Abort() => inner.Abort();

        public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => inner.UpgradeToTlsAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
