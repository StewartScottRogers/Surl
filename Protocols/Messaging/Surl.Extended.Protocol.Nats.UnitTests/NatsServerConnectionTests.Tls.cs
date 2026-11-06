using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Nats.UnitTests;

/// <summary>
/// <c>natss</c> (BL-527; ADR-0113 decision 6): TLS first or INFO first, told apart by the client's
/// first byte, and plain text where TLS is required refused with the protocol page's <c>-ERR</c>.
/// The handshake is the in-memory stand-in the mail servers' tests use.
/// </summary>
public sealed partial class NatsServerConnectionTests
{
    [TestMethod]
    public async Task ServeAsync_NatssInfoFirst_SendsInfoWithTlsRequiredThenUpgradesAndAcceptsConnectOverTls()
    {
        var policy = new UnitTestNatsAuthenticationPolicy();
        var log = new RecordingExchangeLog();
        var connection = new UnitTestTlsClientConnection(false, UnitTestTlsClientConnection.ClientHello, [VerboseAliceConnect, "PING\r\n"], new ManualTimeProvider());

        await Server(policy).ServeAsync(connection, TlsContext(new ManualTimeProvider(), log));

        Assert.IsTrue(connection.UpgradeRequested);
        StringAssert.Contains(connection.WrittenBeforeTls, "\"tls_required\":true,\"tls_verify\":false,\"tls_available\":false}");
        Assert.AreEqual(connection.WrittenBeforeTls + "+OK\r\nPONG\r\n", connection.Written);
        Assert.AreEqual("natss", policy.Logins[0].Scheme);
        Assert.AreEqual(InMemoryConnection.DefaultUpgradeTlsSession, policy.Logins[0].TlsSession);
        CollectionAssert.Contains(log.Notes.ToList(), "TLS Tls13 in force, INFO first.");
    }

    [TestMethod]
    public async Task ServeAsync_NatssTlsFirst_UpgradesBeforeSendingInfoInsideTls()
    {
        var log = new RecordingExchangeLog();
        var connection = new UnitTestTlsClientConnection(true, UnitTestTlsClientConnection.ClientHello, [VerboseAliceConnect, "PING\r\n"], new ManualTimeProvider());

        await Server(new UnitTestNatsAuthenticationPolicy()).ServeAsync(connection, TlsContext(new ManualTimeProvider(), log));

        Assert.AreEqual(string.Empty, connection.WrittenBeforeTls);
        StringAssert.Contains(connection.Written, "\"tls_required\":true,");
        Assert.AreEqual("+OK\r\nPONG\r\n", AfterInfo(connection.Written));
        CollectionAssert.Contains(log.Notes.ToList(), "TLS Tls13 in force, TLS first.");
    }

    [TestMethod]
    public async Task ServeAsync_NatssPlainConnectAfterInfo_IsSecureConnectionTlsRequiredAndClosed()
    {
        var policy = new UnitTestNatsAuthenticationPolicy();
        var log = new RecordingExchangeLog();
        var connection = new UnitTestTlsClientConnection(false, Encoding.UTF8.GetBytes(AliceConnect), ["PING\r\n"], new ManualTimeProvider());

        await Server(policy).ServeAsync(connection, TlsContext(new ManualTimeProvider(), log));

        Assert.IsFalse(connection.UpgradeRequested);
        StringAssert.Contains(connection.Written, "\"tls_required\":true,");
        Assert.AreEqual("-ERR 'Secure Connection - TLS Required'\r\n", AfterInfo(connection.Written));
        Assert.IsEmpty(policy.Logins);
        CollectionAssert.Contains(log.Notes.ToList(), "Plain text arrived where TLS is required; answered -ERR 'Secure Connection - TLS Required' and closed.");
    }

    [TestMethod]
    public async Task ServeAsync_NatssAnyByteWithinTheTlsFirstWait_StartsTheHandshakeBeforeInfo()
    {
        var connection = new UnitTestTlsClientConnection(true, Encoding.UTF8.GetBytes(AliceConnect), [], new ManualTimeProvider());

        await Server(new UnitTestNatsAuthenticationPolicy()).ServeAsync(connection, TlsContext(new ManualTimeProvider()));

        Assert.IsTrue(connection.UpgradeRequested);
        Assert.AreEqual(string.Empty, connection.WrittenBeforeTls);
    }

    [TestMethod]
    public async Task ServeAsync_NatssClientClosesBeforeTls_SendsOnlyInfo()
    {
        var log = new RecordingExchangeLog();
        var connection = new UnitTestTlsClientConnection(false, [], [], new ManualTimeProvider());

        await Server(new UnitTestNatsAuthenticationPolicy()).ServeAsync(connection, TlsContext(new ManualTimeProvider(), log));

        Assert.IsFalse(connection.UpgradeRequested);
        Assert.AreEqual(string.Empty, AfterInfo(connection.Written));
        CollectionAssert.Contains(log.Notes.ToList(), "The client closed before starting TLS.");
    }

    [TestMethod]
    public async Task ServeAsync_NatssNoHandshakeWithinTheHeadTimeout_IsAuthorizationTimeoutAndClosed()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new UnitTestTlsClientConnection(true, [], [], clock, closesWhenExhausted: false);
        var serving = Server(new UnitTestNatsAuthenticationPolicy()).ServeAsync(connection, TlsContext(clock, log));
        await WaitUntilAsync(() => clock.PendingTimerCount == 2);

        clock.Advance(NatsServerOptions.TlsFirstWait);
        await WaitUntilAsync(() => connection.Written.Length > 0 && clock.PendingTimerCount == 2);
        clock.Advance(TimeSpan.FromSeconds(30) - NatsServerOptions.TlsFirstWait);
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        StringAssert.Contains(connection.Written, "\"tls_required\":true,");
        Assert.AreEqual("-ERR 'Authorization Timeout'\r\n", AfterInfo(connection.Written));
        CollectionAssert.Contains(log.Notes.ToList(), "No TLS handshake began within the head timeout; answered -ERR 'Authorization Timeout' and closed.");
    }

    [TestMethod]
    public async Task ServeAsync_NatssHandshakeUnfinishedWithinTheHeadTimeout_ClosesWithNoBytes()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new UnitTestTlsClientConnection(true, UnitTestTlsClientConnection.ClientHello, [AliceConnect], clock, handshakeHangs: true);
        var serving = Server(new UnitTestNatsAuthenticationPolicy()).ServeAsync(connection, TlsContext(clock, log));
        await WaitUntilAsync(() => connection.UpgradeRequested);

        clock.Advance(TimeSpan.FromSeconds(30));
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.AreEqual(string.Empty, connection.Written);
        CollectionAssert.Contains(log.Notes.ToList(), "The TLS handshake did not finish within the head timeout; closed.");
    }

    [TestMethod]
    public async Task ServeAsync_NatssHandshakeFails_ThrowsWithNoBytesWritten()
    {
        var connection = new UnitTestTlsClientConnection(true, UnitTestTlsClientConnection.ClientHello, [AliceConnect], new ManualTimeProvider(), upgradeFails: true);

        await Assert.ThrowsExactlyAsync<TlsHandshakeException>(
            () => Server(new UnitTestNatsAuthenticationPolicy()).ServeAsync(connection, TlsContext(new ManualTimeProvider())));

        Assert.AreEqual(string.Empty, connection.Written);
    }

    [TestMethod]
    public async Task ServeAsync_NatssWithoutHeadTimeout_StillWaitsForTheHandshake()
    {
        var connection = new UnitTestTlsClientConnection(false, UnitTestTlsClientConnection.ClientHello, [VerboseAliceConnect], new ManualTimeProvider());
        var context = TlsContext(new ManualTimeProvider()) with { Limits = ExchangeLimits.Default with { HeadTimeout = Timeout.InfiniteTimeSpan } };

        await Server(new UnitTestNatsAuthenticationPolicy()).ServeAsync(connection, context);

        Assert.IsTrue(connection.UpgradeRequested);
        Assert.AreEqual(connection.WrittenBeforeTls + "+OK\r\n", connection.Written);
    }

    [TestMethod]
    public async Task ServeAsync_NatssExchangeCancelledBeforeTls_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        var clock = new ManualTimeProvider();
        var connection = new UnitTestTlsClientConnection(true, [], [], clock, closesWhenExhausted: false);
        var serving = Server(new UnitTestNatsAuthenticationPolicy()).ServeAsync(connection, TlsContext(clock, cancellation: cancellation.Token));
        await WaitUntilAsync(() => clock.PendingTimerCount == 2);

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.AreEqual(string.Empty, connection.Written);
    }

    [TestMethod]
    public async Task ServeAsync_NatsWithAClientHello_NeverOffersOrStartsTls()
    {
        var connection = new UnitTestTlsClientConnection(true, UnitTestTlsClientConnection.ClientHello, [], new ManualTimeProvider());

        await Server(new UnitTestNatsAuthenticationPolicy()).ServeAsync(connection, Context(new ManualTimeProvider(), cancellation: TestContext.CancellationToken));

        Assert.IsFalse(connection.UpgradeRequested);
        StringAssert.Contains(connection.Written, "\"tls_required\":false,\"tls_verify\":false,\"tls_available\":false}");
        Assert.AreEqual(string.Empty, AfterInfo(connection.Written));
    }

    private static NatsServer Server(IAuthenticationPolicy policy) => new(policy, UnitTestMessageBus.Create());

    private ExchangeContext TlsContext(TimeProvider clock, IExchangeLog? log = null, CancellationToken? cancellation = null) => new(
        1,
        new ListenUrl("natss", "127.0.0.1", 4222).WithBoundPort(4222),
        new IPEndPoint(IPAddress.Loopback, 4222),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log ?? new RecordingExchangeLog(),
        clock,
        cancellation ?? TestContext.CancellationToken);
}
