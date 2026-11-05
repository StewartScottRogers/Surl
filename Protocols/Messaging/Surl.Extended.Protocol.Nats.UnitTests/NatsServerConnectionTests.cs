using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Nats.UnitTests;

/// <summary>
/// INFO, CONNECT, keep-alive and the protocol errors (BL-525; ADR-0113 decisions 1, 2, 3, 5 and
/// 7), every client line and every expected answer written by hand from the NATS client protocol
/// page. Each exchange's answers are compared after the INFO line.
/// </summary>
[TestClass]
public sealed partial class NatsServerConnectionTests
{
    private const string AliceConnect = "CONNECT {\"user\":\"alice\",\"pass\":\"secret\"}\r\n";
    private const string VerboseAliceConnect = "CONNECT {\"verbose\":true,\"user\":\"alice\",\"pass\":\"secret\"}\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_ConnectWithGoodCredentialsUnderVerbose_IsAnsweredOk()
    {
        var policy = new UnitTestNatsAuthenticationPolicy();

        var (answers, log) = await ServeAsync(policy, "CONNECT {\"verbose\":true,\"user\":\"alice\",\"pass\":\"secret\",\"name\":\"app\",\"lang\":\"go\",\"version\":\"1.0\"}\r\n");

        Assert.AreEqual("+OK\r\n", answers);
        Assert.HasCount(1, policy.Logins);
        Assert.AreEqual("nats", policy.Logins[0].Scheme);
        Assert.AreEqual("alice", policy.Logins[0].UserName);
        CollectionAssert.Contains(log.Notes.ToList(), "CONNECT accepted: name app, lang go, version 1.0, verbose True, pedantic False.");
    }

    [TestMethod]
    public async Task ServeAsync_ConnectWithoutVerbose_IsAnsweredNothing()
    {
        var (answers, log) = await ServeAsync(new UnitTestNatsAuthenticationPolicy(), AliceConnect);

        Assert.AreEqual(string.Empty, answers);
        CollectionAssert.Contains(log.Notes.ToList(), "CONNECT accepted: name (none), lang (none), version (none), verbose False, pedantic False.");
    }

    [TestMethod]
    public async Task ServeAsync_AuthToken_LogsInAsTheEmptyNameAccount()
    {
        var policy = new UnitTestNatsAuthenticationPolicy();

        var (answers, _) = await ServeAsync(policy, "CONNECT {\"verbose\":true,\"auth_token\":\"s3cret\"}\r\n");

        Assert.AreEqual("+OK\r\n", answers);
        Assert.AreEqual(string.Empty, policy.Logins[0].UserName);
    }

    [TestMethod]
    public async Task ServeAsync_AcceptedUnchecked_IsServed()
    {
        var policy = new UnitTestNatsAuthenticationPolicy { Verdict = PasswordLoginVerdict.AcceptedUnchecked };

        var (answers, _) = await ServeAsync(policy, "CONNECT {\"verbose\":true}\r\nPING\r\n");

        Assert.AreEqual("+OK\r\nPONG\r\n", answers);
    }

    [TestMethod]
    [DataRow("CONNECT {\"verbose\":true,\"user\":\"alice\",\"pass\":\"wrong\"}\r\nPING\r\n", "RefusedCredentials")]
    [DataRow("CONNECT {\"verbose\":true}\r\nPING\r\n", "RefusedAnonymous")]
    public async Task ServeAsync_RefusedLogin_IsAuthorizationViolationAndClosed(string client, string verdict)
    {
        var (answers, log) = await ServeAsync(new UnitTestNatsAuthenticationPolicy(), client);

        Assert.AreEqual("-ERR 'Authorization Violation'\r\n", answers);
        CollectionAssert.Contains(log.Notes.ToList(), $"CONNECT's login was refused ({verdict}); answered -ERR 'Authorization Violation' and closed.");
    }

    [TestMethod]
    public async Task ServeAsync_RefusedPlaintext_IsAuthorizationViolationAndClosed()
    {
        var policy = new UnitTestNatsAuthenticationPolicy { Verdict = PasswordLoginVerdict.RefusedPlaintext };

        var (answers, _) = await ServeAsync(policy, AliceConnect);

        Assert.AreEqual("-ERR 'Authorization Violation'\r\n", answers);
    }

    [TestMethod]
    public async Task ServeAsync_NKeyLogin_IsAuthorizationViolationWithoutAskingThePolicy()
    {
        var policy = new UnitTestNatsAuthenticationPolicy();

        var (answers, _) = await ServeAsync(policy, "CONNECT {\"user\":\"alice\",\"pass\":\"secret\",\"nkey\":\"UABC\",\"sig\":\"x\"}\r\n");

        Assert.AreEqual("-ERR 'Authorization Violation'\r\n", answers);
        Assert.IsEmpty(policy.Logins);
    }

    [TestMethod]
    [DataRow("PING\r\n", "PING")]
    [DataRow("sub foo 1\r\n", "SUB")]
    public async Task ServeAsync_OperationBeforeConnect_IsAuthorizationViolationAndClosed(string client, string operation)
    {
        var (answers, log) = await ServeAsync(new UnitTestNatsAuthenticationPolicy(), client + AliceConnect);

        Assert.AreEqual("-ERR 'Authorization Violation'\r\n", answers);
        CollectionAssert.Contains(log.Notes.ToList(), $"{operation} arrived before CONNECT; answered -ERR 'Authorization Violation' and closed.");
    }

    [TestMethod]
    public async Task ServeAsync_BlankLinesAndBareLineFeeds_AreAccepted()
    {
        var (answers, _) = await ServeAsync(new UnitTestNatsAuthenticationPolicy(), "\r\n" + VerboseAliceConnect.Replace("\r\n", "\n", StringComparison.Ordinal) + "\r\nping\n");

        Assert.AreEqual("+OK\r\nPONG\r\n", answers);
    }

    [TestMethod]
    public async Task ServeAsync_ClientClosesBeforeConnect_SendsOnlyInfo()
    {
        var (answers, _) = await ServeAsync(new UnitTestNatsAuthenticationPolicy(), "CONN");

        Assert.AreEqual(string.Empty, answers);
    }

    [TestMethod]
    public async Task ServeAsync_LowerCasePing_IsAnsweredPong()
    {
        var (answers, _) = await ServeAsync(new UnitTestNatsAuthenticationPolicy(), AliceConnect + "ping\r\n");

        Assert.AreEqual("PONG\r\n", answers);
    }

    [TestMethod]
    public async Task ServeAsync_PipelinedAndSplitOperations_AreEachAnsweredInOrder()
    {
        var (answers, _) = await ServeAsync(new UnitTestNatsAuthenticationPolicy(), VerboseAliceConnect[..10], VerboseAliceConnect[10..] + "PING\r\nPI", "NG\r\nPONG\r\nPING\r\n");

        Assert.AreEqual("+OK\r\nPONG\r\nPONG\r\nPONG\r\n", answers);
    }

    [TestMethod]
    [DataRow("PING x\r\n")]
    [DataRow("PONG x\r\n")]
    public async Task ServeAsync_PingOrPongWithArguments_IsParserErrorAndClosed(string client)
    {
        var (answers, _) = await ServeAsync(new UnitTestNatsAuthenticationPolicy(), AliceConnect + client + "PING\r\n");

        Assert.AreEqual("-ERR 'Parser Error'\r\n", answers);
    }

    [TestMethod]
    [DataRow("CONNECT\r\n")]
    [DataRow("CONNECT {\"verbose\":\r\n")]
    [DataRow("CONNECT {\"verbose\":1}\r\n")]
    public async Task ServeAsync_ConnectNotAJsonObjectOfTheRightTypes_IsParserErrorAndClosed(string client)
    {
        var (answers, _) = await ServeAsync(new UnitTestNatsAuthenticationPolicy(), client + "PING\r\n");

        Assert.AreEqual("-ERR 'Parser Error'\r\n", answers);
    }

    [TestMethod]
    [DataRow(0, "+OK\r\n")]
    [DataRow(1, "+OK\r\n")]
    [DataRow(2, "-ERR 'Invalid Client Protocol'\r\n")]
    [DataRow(-1, "-ERR 'Invalid Client Protocol'\r\n")]
    public async Task ServeAsync_Protocol_ZeroAndOneAreServedAndOthersRefused(int protocol, string expected)
    {
        var (answers, _) = await ServeAsync(new UnitTestNatsAuthenticationPolicy(), $"CONNECT {{\"verbose\":true,\"protocol\":{protocol},\"user\":\"alice\",\"pass\":\"secret\"}}\r\n");

        Assert.AreEqual(expected, answers);
    }

    [TestMethod]
    [DataRow(false, "-ERR 'No Responders Requires Headers Support'\r\n")]
    [DataRow(true, "+OK\r\n")]
    public async Task ServeAsync_NoResponders_NeedsHeaders(bool headers, string expected)
    {
        var (answers, _) = await ServeAsync(
            new UnitTestNatsAuthenticationPolicy(),
            $"CONNECT {{\"verbose\":true,\"no_responders\":true,\"headers\":{(headers ? "true" : "false")},\"user\":\"alice\",\"pass\":\"secret\"}}\r\n");

        Assert.AreEqual(expected, answers);
    }

    [TestMethod]
    [DataRow("CONNECT {}\r\n")]
    [DataRow("INFO {}\r\n")]
    [DataRow("MSG foo 1 0\r\n")]
    [DataRow("HMSG foo 1 0 0\r\n")]
    [DataRow("FOO\r\n")]
    public async Task ServeAsync_UnknownOperationOrSecondConnect_IsUnknownProtocolOperationAndClosed(string client)
    {
        var (answers, _) = await ServeAsync(new UnitTestNatsAuthenticationPolicy(), AliceConnect + client + "PING\r\n");

        Assert.AreEqual("-ERR 'Unknown Protocol Operation'\r\n", answers);
    }

    [TestMethod]
    public async Task ServeAsync_ControlLineOverTheLimit_IsMaximumControlLineExceededAndClosed()
    {
        var (answers, _) = await ServeAsync(new UnitTestNatsAuthenticationPolicy(), AliceConnect + "PING " + new string('x', 9000) + "\r\nPING\r\n");

        Assert.AreEqual("-ERR 'Maximum Control Line Exceeded'\r\n", answers);
    }

    [TestMethod]
    public async Task ServeAsync_NoConnectWithinTheHeadTimeout_IsAuthorizationTimeoutAndClosed()
    {
        var clock = new ManualTimeProvider();
        var connection = new UnitTestNatsClientConnection();
        var serving = new NatsServer(new UnitTestNatsAuthenticationPolicy(), UnitTestMessageBus.Create()).ServeAsync(connection, Context(clock));
        connection.Send("CONNECT {");
        await WaitUntilAsync(() => connection.Written.Length > 0 && clock.PendingTimerCount == 1);

        clock.Advance(TimeSpan.FromSeconds(29));
        await Task.Yield();
        var beforeTimeout = AfterInfo(connection.Written);
        clock.Advance(TimeSpan.FromSeconds(1));
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.AreEqual(string.Empty, beforeTimeout);
        Assert.AreEqual("-ERR 'Authorization Timeout'\r\n", AfterInfo(connection.Written));
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledBeforeConnect_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        var connection = new UnitTestNatsClientConnection();
        var serving = new NatsServer(new UnitTestNatsAuthenticationPolicy(), UnitTestMessageBus.Create()).ServeAsync(connection, Context(new ManualTimeProvider(), cancellation: cancellation.Token));
        await WaitUntilAsync(() => connection.Written.Length > 0);

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledAfterConnect_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        var log = new RecordingExchangeLog();
        var connection = new UnitTestNatsClientConnection();
        var serving = new NatsServer(new UnitTestNatsAuthenticationPolicy(), UnitTestMessageBus.Create()).ServeAsync(connection, Context(new ManualTimeProvider(), log, cancellation.Token));
        connection.Send(AliceConnect);
        await WaitUntilAsync(() => log.Notes.Any(note => note.StartsWith("CONNECT accepted", StringComparison.Ordinal)));

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    [TestMethod]
    public async Task ServeAsync_ServerPings_AreSentEveryQuarterIdleTimeoutAndTwoUnansweredCloseAsStale()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new UnitTestNatsClientConnection();
        var serving = new NatsServer(new UnitTestNatsAuthenticationPolicy(), UnitTestMessageBus.Create()).ServeAsync(connection, Context(clock, log));
        await ConnectAsync(connection, clock, log);

        clock.Advance(TimeSpan.FromSeconds(29));
        await Task.Yield();
        var beforeInterval = AfterInfo(connection.Written);
        await AdvanceToNextWriteAsync(connection, clock, "PING\r\n");
        await AdvanceToNextWriteAsync(connection, clock, "PING\r\nPING\r\n");
        clock.Advance(TimeSpan.FromSeconds(30));
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.AreEqual(string.Empty, beforeInterval);
        Assert.AreEqual("PING\r\nPING\r\n-ERR 'Stale Connection'\r\n", AfterInfo(connection.Written));
        CollectionAssert.Contains(log.Notes.ToList(), "2 PINGs went unanswered; answered -ERR 'Stale Connection' and closed.");
    }

    [TestMethod]
    public async Task ServeAsync_ClientPong_ClearsTheUnansweredCount()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new UnitTestNatsClientConnection();
        var serving = new NatsServer(new UnitTestNatsAuthenticationPolicy(), UnitTestMessageBus.Create()).ServeAsync(connection, Context(clock, log));
        await ConnectAsync(connection, clock, log);
        clock.Advance(TimeSpan.FromSeconds(29));
        await AdvanceToNextWriteAsync(connection, clock, "PING\r\n");
        await AdvanceToNextWriteAsync(connection, clock, "PING\r\nPING\r\n");

        connection.Send("PONG\r\nPING\r\n");
        await WaitUntilAsync(() => AfterInfo(connection.Written) == "PING\r\nPING\r\nPONG\r\n");
        await AdvanceToNextWriteAsync(connection, clock, "PING\r\nPING\r\nPONG\r\nPING\r\n");
        connection.Close();
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.AreEqual("PING\r\nPING\r\nPONG\r\nPING\r\n", AfterInfo(connection.Written));
    }

    [TestMethod]
    public async Task ServeAsync_ServerPingWriteFails_StopsReadingAndThrows()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new UnitTestNatsClientConnection { FailWritesAfter = 1 };
        var serving = new NatsServer(new UnitTestNatsAuthenticationPolicy(), UnitTestMessageBus.Create()).ServeAsync(connection, Context(clock, log));
        await ConnectAsync(connection, clock, log);

        clock.Advance(TimeSpan.FromSeconds(30));

        await Assert.ThrowsExactlyAsync<IOException>(() => serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
    }

    private async Task ConnectAsync(UnitTestNatsClientConnection connection, ManualTimeProvider clock, RecordingExchangeLog log)
    {
        connection.Send(AliceConnect);
        await WaitUntilAsync(() => log.Notes.Any(note => note.StartsWith("CONNECT accepted", StringComparison.Ordinal)) && clock.PendingTimerCount == 1);
    }

    // Moves the clock the last second of an interval and waits for the written answers to be
    // the expected ones and the next interval's timer to be pending.
    private async Task AdvanceToNextWriteAsync(UnitTestNatsClientConnection connection, ManualTimeProvider clock, string expectedAnswers)
    {
        await WaitUntilAsync(() => clock.PendingTimerCount == 1);
        clock.Advance(TimeSpan.FromSeconds(1));
        await WaitUntilAsync(() => AfterInfo(connection.Written) == expectedAnswers && clock.PendingTimerCount == 1);
        clock.Advance(TimeSpan.FromSeconds(29));
    }

    private async Task<(string Answers, RecordingExchangeLog Log)> ServeAsync(IAuthenticationPolicy policy, params string[] chunks)
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(chunks.Select(chunk => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(chunk)), new IPEndPoint(IPAddress.Loopback, 4222));

        await new NatsServer(policy, UnitTestMessageBus.Create()).ServeAsync(connection, Context(new ManualTimeProvider(), log, TestContext.CancellationToken));

        return (AfterInfo(Encoding.UTF8.GetString(connection.WrittenBytes)), log);
    }

    private static string AfterInfo(string written)
    {
        Assert.StartsWith("INFO {", written);
        return written[(written.IndexOf("\r\n", StringComparison.Ordinal) + 2)..];
    }

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
        new ListenUrl("nats", "127.0.0.1", 4222).WithBoundPort(4222),
        new IPEndPoint(IPAddress.Loopback, 4222),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log ?? new RecordingExchangeLog(),
        clock,
        cancellation);
}
