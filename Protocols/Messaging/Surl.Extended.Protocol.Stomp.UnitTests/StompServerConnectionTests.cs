using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Stomp.UnitTests;

/// <summary>
/// CONNECT, CONNECTED and the ERROR-then-close answers (BL-531; ADR-0118 decisions 1, 2, 3, 6 and
/// 8), every client frame and every expected answer written by hand from the STOMP 1.2
/// specification's "Connecting", "Protocol Negotiation", "ERROR" and "Size Limits". CONNECTED's
/// random <c>session</c> is compared as <c>session:*</c>.
/// </summary>
[TestClass]
public sealed partial class StompServerConnectionTests
{
    private const string AliceConnect = "CONNECT\naccept-version:1.0,1.1,1.2\nhost:localhost\nlogin:alice\npasscode:secret\n\n\0";
    private const string Connected12 = "CONNECTED\nversion:1.2\nheart-beat:1000,30000\nsession:*\nserver:surl\n\n\0";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_ConnectAcceptingEveryVersion_IsAnswered12()
    {
        var policy = new UnitTestStompAuthenticationPolicy();

        var (answers, log) = await ServeAsync(policy, AliceConnect);

        Assert.AreEqual(Connected12, answers);
        Assert.AreEqual("stomp", policy.Logins.Single().Scheme);
        Assert.AreEqual("alice", policy.Logins[0].UserName);
        Assert.AreEqual("secret", UnitTestStompAuthenticationPolicy.PasswordOf(policy.Logins[0]));
        CollectionAssert.Contains(log.Notes.ToList(), "CONNECT accepted: version 1.2, host localhost.");
    }

    [TestMethod]
    public async Task ServeAsync_Session_Is32LowerCaseHexDigitsDrawnPerConnection()
    {
        var first = await WrittenAsync(new UnitTestStompAuthenticationPolicy(), AliceConnect);
        var second = await WrittenAsync(new UnitTestStompAuthenticationPolicy(), AliceConnect);

        Assert.MatchesRegex(SessionHeader(), first);
        Assert.AreNotEqual(SessionHeader().Match(first).Value, SessionHeader().Match(second).Value);
    }

    [TestMethod]
    public async Task ServeAsync_StompFrameAccepting11_IsAnswered11()
    {
        var (answers, _) = await ServeAsync(
            new UnitTestStompAuthenticationPolicy(), "STOMP\naccept-version:1.1\nhost:h\nlogin:alice\npasscode:secret\nheart-beat:0,0\n\n\0");

        Assert.AreEqual("CONNECTED\nversion:1.1\nheart-beat:1000,30000\nsession:*\nserver:surl\n\n\0", answers);
    }

    [TestMethod]
    public async Task ServeAsync_ConnectWithoutAcceptVersion_IsAnswered10WithoutHeartBeat()
    {
        var (answers, log) = await ServeAsync(
            new UnitTestStompAuthenticationPolicy(), "CONNECT\nlogin:alice\npasscode:secret\nheart-beat:not checked in 1.0\n\n\0");

        Assert.AreEqual("CONNECTED\nversion:1.0\nsession:*\nserver:surl\n\n\0", answers);
        CollectionAssert.Contains(log.Notes.ToList(), "CONNECT accepted: version 1.0, host (none).");
    }

    [TestMethod]
    public async Task ServeAsync_HeartBeatWanted_FollowsTheIdleTimeout()
    {
        var options = new StompServerOptions { IdleTimeout = TimeSpan.FromSeconds(8) };

        var (answers, _) = await ServeAsync(new UnitTestStompAuthenticationPolicy(), options, AliceConnect);

        Assert.AreEqual("CONNECTED\nversion:1.2\nheart-beat:1000,2000\nsession:*\nserver:surl\n\n\0", answers);
    }

    [TestMethod]
    [DataRow("CONNECT\naccept-version:2.0,2.1\nlogin:alice\npasscode:secret\n\n\0")]
    [DataRow("STOMP\naccept-version:1.0\nlogin:alice\npasscode:secret\n\n\0")]
    [DataRow("STOMP\nlogin:alice\npasscode:secret\n\n\0")]
    public async Task ServeAsync_NoCommonVersion_IsVersionErrorAndClosed(string connect)
    {
        var policy = new UnitTestStompAuthenticationPolicy();

        var (answers, _) = await ServeAsync(policy, connect + "DISCONNECT\n\n\0");

        Assert.AreEqual(
            "ERROR\nmessage:Unsupported protocol version\nversion:1.0,1.1,1.2\ncontent-type:text/plain\ncontent-length:43\n\nSupported protocol versions are 1.0 1.1 1.2\0",
            answers);
        Assert.IsEmpty(policy.Logins);
    }

    [TestMethod]
    [DataRow("CONNECT\naccept-version:1.2\nlogin:alice\npasscode:wrong\n\n\0", "RefusedCredentials")]
    [DataRow("CONNECT\naccept-version:1.2\npasscode:secret\n\n\0", "RefusedAnonymous")]
    public async Task ServeAsync_BadCredentials_IsAuthenticationFailedAndClosed(string connect, string verdict)
    {
        var (answers, log) = await ServeAsync(new UnitTestStompAuthenticationPolicy(), connect + "DISCONNECT\nreceipt:1\n\n\0");

        Assert.AreEqual("ERROR\nmessage:Authentication failed\n\n\0", answers);
        CollectionAssert.Contains(log.Notes.ToList(), $"CONNECT's login was refused ({verdict}); answered ERROR 'Authentication failed' and closed.");
    }

    [TestMethod]
    public async Task ServeAsync_RefusedPlaintext_IsAuthenticationFailedAndClosed()
    {
        var policy = new UnitTestStompAuthenticationPolicy { Verdict = PasswordLoginVerdict.RefusedPlaintext };

        var (answers, _) = await ServeAsync(policy, AliceConnect);

        Assert.AreEqual("ERROR\nmessage:Authentication failed\n\n\0", answers);
    }

    [TestMethod]
    public async Task ServeAsync_AcceptedUncheckedWithoutLogin_IsServedAsAnonymous()
    {
        var policy = new UnitTestStompAuthenticationPolicy { Verdict = PasswordLoginVerdict.AcceptedUnchecked };

        var (answers, _) = await ServeAsync(policy, "CONNECT\naccept-version:1.2\npasscode:ignored\n\n\0");

        Assert.AreEqual(Connected12, answers);
        Assert.IsNull(policy.Logins.Single().UserName);
        Assert.IsNull(policy.Logins[0].Password);
    }

    [TestMethod]
    public async Task ServeAsync_LoginWithoutPasscode_IsJudgedWithAnEmptyPassword()
    {
        var policy = new UnitTestStompAuthenticationPolicy();

        await ServeAsync(policy, "CONNECT\naccept-version:1.2\nlogin:alice\n\n\0");

        Assert.AreEqual(string.Empty, UnitTestStompAuthenticationPolicy.PasswordOf(policy.Logins.Single()));
    }

    [TestMethod]
    public async Task ServeAsync_ConnectHeaders_AreReadUnescaped()
    {
        var policy = new UnitTestStompAuthenticationPolicy { Verdict = PasswordLoginVerdict.Accepted };

        await ServeAsync(policy, "CONNECT\naccept-version:1.2\nlogin:a\\cb\npasscode:x:y\\n\n\n\0");

        Assert.AreEqual("a\\cb", policy.Logins.Single().UserName);
        Assert.AreEqual("x:y\\n", UnitTestStompAuthenticationPolicy.PasswordOf(policy.Logins[0]));
    }

    [TestMethod]
    [DataRow("CONNECT\naccept-version:1.2\nlogin:alice\npasscode:secret\nheart-beat:x\n\n\0")]
    [DataRow("CONNECT\naccept-version:1.1\nlogin:alice\npasscode:secret\nheart-beat:1,2,3\n\n\0")]
    public async Task ServeAsync_InvalidHeartBeat_IsInvalidHeartBeatAndClosed(string connect)
    {
        var (answers, _) = await ServeAsync(new UnitTestStompAuthenticationPolicy(), connect);

        Assert.AreEqual("ERROR\nmessage:Invalid heart-beat\n\n\0", answers);
    }

    [TestMethod]
    public async Task ServeAsync_FrameBeforeConnect_IsNotConnectedAndClosed()
    {
        var (answers, _) = await ServeAsync(new UnitTestStompAuthenticationPolicy(), "SEND\ndestination:/queue/a\nreceipt:r\\c1\n\nhi\0" + AliceConnect);

        Assert.AreEqual("ERROR\nmessage:Not connected\nreceipt-id:r\\c1\n\n\0", answers);
    }

    [TestMethod]
    public async Task ServeAsync_ConnectWithABody_IsMalformedFrameAndClosed()
    {
        var (answers, _) = await ServeAsync(new UnitTestStompAuthenticationPolicy(), "CONNECT\naccept-version:1.2\nreceipt:1\n\nbody\0");

        Assert.AreEqual("ERROR\nmessage:Malformed frame\nreceipt-id:1\n\n\0", answers);
    }

    [TestMethod]
    public async Task ServeAsync_SecondConnect_IsAlreadyConnectedAndClosed()
    {
        var (answers, _) = await ServeAsync(new UnitTestStompAuthenticationPolicy(), AliceConnect, "STOMP\naccept-version:1.2\nreceipt:1\n\n\0", "DISCONNECT\n\n\0");

        Assert.AreEqual(Connected12 + "ERROR\nmessage:Already connected\n\n\0", answers);
    }

    [TestMethod]
    public async Task ServeAsync_UnknownCommand_IsUnknownCommandWithTheReceiptEscapedByVersion()
    {
        var (answers, log) = await ServeAsync(new UnitTestStompAuthenticationPolicy(), AliceConnect + "FLY\nreceipt:a:b\n\n\0");

        Assert.AreEqual(Connected12 + "ERROR\nmessage:Unknown command\nreceipt-id:a\\cb\n\n\0", answers);
        CollectionAssert.Contains(log.Notes.ToList(), "The command FLY is not served; answered ERROR 'Unknown command' and closed.");
    }

    [TestMethod]
    public async Task ServeAsync_FrameOverTheSizeLimit_IsFrameTooLargeAndClosed()
    {
        var limits = new ExchangeLimits(TimeSpan.FromSeconds(30), 102400, 8192, maxMessageBytes: 4, 104857600);

        var (answers, _) = await ServeAsync(
            new UnitTestStompAuthenticationPolicy(), StompServerOptions.Default, limits, AliceConnect + "SEND\ndestination:/queue/a\ncontent-length:5\n\nhello\0");

        Assert.AreEqual(Connected12 + "ERROR\nmessage:Frame too large\n\n\0", answers);
    }

    [TestMethod]
    public async Task ServeAsync_HeaderLineOverTheLineLimit_IsHeaderLineTooLongAndClosed()
    {
        var (answers, _) = await ServeAsync(new UnitTestStompAuthenticationPolicy(), "CONNECT\nhost:" + new string('h', 9000) + "\n\n\0");

        Assert.AreEqual("ERROR\nmessage:Header line too long\n\n\0", answers);
    }

    [TestMethod]
    public async Task ServeAsync_DisconnectWithReceipt_IsAnsweredReceiptAndClosed()
    {
        var connection = Connection(AliceConnect + "DISCONNECT\nreceipt:77\n\n\0SEND\n\n\0");

        await new StompServer(new UnitTestStompAuthenticationPolicy()).ServeAsync(connection, Context(new ManualTimeProvider()));

        Assert.AreEqual(Connected12 + "RECEIPT\nreceipt-id:77\n\n\0", Masked(connection));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_DisconnectWithoutReceipt_IsClosedWithNoBytes()
    {
        var (answers, log) = await ServeAsync(new UnitTestStompAuthenticationPolicy(), AliceConnect + "\n\nDISCONNECT\n\n\0");

        Assert.AreEqual(Connected12, answers);
        CollectionAssert.Contains(log.Notes.ToList(), "DISCONNECT; closed.");
    }

    [TestMethod]
    public async Task ServeAsync_DisconnectWithABody_IsMalformedFrameAndClosed()
    {
        var (answers, _) = await ServeAsync(new UnitTestStompAuthenticationPolicy(), AliceConnect + "DISCONNECT\n\nx\0");

        Assert.AreEqual(Connected12 + "ERROR\nmessage:Malformed frame\n\n\0", answers);
    }

    [TestMethod]
    public async Task ServeAsync_ClientClosesAfterConnected_EndsQuietly()
    {
        var connection = Connection(AliceConnect);

        await new StompServer(new UnitTestStompAuthenticationPolicy()).ServeAsync(connection, Context(new ManualTimeProvider()));

        Assert.AreEqual(Connected12, Masked(connection));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_ClientClosesBeforeConnect_EndsWithNoBytes()
    {
        var (answers, _) = await ServeAsync(new UnitTestStompAuthenticationPolicy(), "CONN");

        Assert.AreEqual(string.Empty, answers);
    }

    [TestMethod]
    public async Task ServeAsync_NoConnectWithinTheHeadTimeout_IsClosedWithNoBytes()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new UnitTestStompClientConnection();
        var serving = new StompServer(new UnitTestStompAuthenticationPolicy()).ServeAsync(connection, Context(clock, log));
        connection.Send("CONNECT\n");
        await WaitUntilAsync(() => clock.PendingTimerCount == 1);

        clock.Advance(TimeSpan.FromSeconds(30));
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.AreEqual(string.Empty, connection.Written);
        CollectionAssert.Contains(log.Notes.ToList(), "No CONNECT arrived within the head timeout; closed.");
    }

    [TestMethod]
    public async Task ServeAsync_CancelledBeforeConnect_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        var connection = new UnitTestStompClientConnection();
        var serving = new StompServer(new UnitTestStompAuthenticationPolicy()).ServeAsync(connection, Context(new ManualTimeProvider(), cancellation: cancellation.Token));

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ServeAsync_ClientStillSendingAfterAnError_IsReadForOneSecondThenClosed()
    {
        var clock = new ManualTimeProvider();
        var connection = new UnitTestStompClientConnection();
        var serving = new StompServer(new UnitTestStompAuthenticationPolicy()).ServeAsync(connection, Context(clock));
        connection.Send("FLY\n\n\0");
        await WaitUntilAsync(() => connection.Written.Length > 0 && clock.PendingTimerCount == 1);
        connection.Send("more the server discards");

        clock.Advance(StompServerConnection.Linger);
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.AreEqual("ERROR\nmessage:Not connected\n\n\0", connection.Written);
    }

    [TestMethod]
    public async Task ServeAsync_CancelledWhileLingering_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        var clock = new ManualTimeProvider();
        var connection = new UnitTestStompClientConnection();
        var serving = new StompServer(new UnitTestStompAuthenticationPolicy()).ServeAsync(connection, Context(clock, cancellation: cancellation.Token));
        connection.Send("FLY\n\n\0");
        await WaitUntilAsync(() => connection.Written.Length > 0 && clock.PendingTimerCount == 1);

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
    }

    [GeneratedRegex("session:[0-9a-f]{32}\n")]
    private static partial Regex SessionHeader();

    private Task<(string Answers, RecordingExchangeLog Log)> ServeAsync(IAuthenticationPolicy policy, params string[] chunks) =>
        ServeAsync(policy, StompServerOptions.Default, ExchangeLimits.Default, chunks);

    private Task<(string Answers, RecordingExchangeLog Log)> ServeAsync(IAuthenticationPolicy policy, StompServerOptions options, params string[] chunks) =>
        ServeAsync(policy, options, ExchangeLimits.Default, chunks);

    private async Task<(string Answers, RecordingExchangeLog Log)> ServeAsync(
        IAuthenticationPolicy policy, StompServerOptions options, ExchangeLimits limits, params string[] chunks)
    {
        var log = new RecordingExchangeLog();
        var connection = Connection(chunks);

        await new StompServer(policy, options).ServeAsync(connection, Context(new ManualTimeProvider(), log, TestContext.CancellationToken) with { Limits = limits });

        return (Masked(connection), log);
    }

    private async Task<string> WrittenAsync(IAuthenticationPolicy policy, string client)
    {
        var connection = Connection(client);
        await new StompServer(policy).ServeAsync(connection, Context(new ManualTimeProvider(), cancellation: TestContext.CancellationToken));
        return Encoding.UTF8.GetString(connection.WrittenBytes);
    }

    private static InMemoryConnection Connection(params string[] chunks) =>
        new(chunks.Select(chunk => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(chunk)), new IPEndPoint(IPAddress.Loopback, 61613));

    private static string Masked(InMemoryConnection connection) =>
        SessionHeader().Replace(Encoding.UTF8.GetString(connection.WrittenBytes), "session:*\n");

    private async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("The server did not reach the expected state in time.");
            }

            await Task.Delay(5, TestContext.CancellationToken);
        }
    }

    private static ExchangeContext Context(TimeProvider clock, IExchangeLog? log = null, CancellationToken cancellation = default) => new(
        1,
        new ListenUrl("stomp", "127.0.0.1", 61613).WithBoundPort(61613),
        new IPEndPoint(IPAddress.Loopback, 61613),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log ?? new RecordingExchangeLog(),
        clock,
        cancellation);
}
