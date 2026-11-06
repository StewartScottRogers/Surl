using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Redis.RedisTestExchanges;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The server's command loop, unknown commands and limits (BL-489; ADR-0106 decisions 1, 5 and 6),
/// every byte written by hand from the RESP page and the command reference.
/// </summary>
[TestClass]
public sealed class RedisServerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Schemes_AreRedisAndRediss()
    {
        CollectionAssert.AreEqual(new[] { "redis", "rediss" }, new RedisServer(Anonymous()).Schemes.ToArray());
    }

    [TestMethod]
    public async Task NullArguments_Throw()
    {
        var server = new RedisServer(Anonymous());

        Assert.ThrowsExactly<ArgumentNullException>(() => new RedisServer(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new RedisServer(null!, new MessageBus.MessageBus(TimeProvider.System)));
        Assert.ThrowsExactly<ArgumentNullException>(() => new RedisServer(Anonymous(), null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => server.ServeAsync(null!, Context(TestContext.CancellationToken)));
        Assert.ThrowsExactly<ArgumentNullException>(() => server.ServeAsync(Connection([]), null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnections, TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task ServeAsync_UnknownCommand_IsTheUnknownCommandErrorAndTheConnectionStaysOpen()
    {
        var written = await ServeAsync(Anonymous(), TestContext.CancellationToken, Command("FOO", "a", "b"), Command("PING"));

        Assert.AreEqual("-ERR unknown command 'FOO', with args beginning with: 'a' 'b' \r\n+PONG\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_UnknownCommandBeforeLogin_IsTheUnknownCommandError()
    {
        var written = await ServeAsync(LoginRequired(), TestContext.CancellationToken, Command("NOSUCH"));

        Assert.AreEqual("-ERR unknown command 'NOSUCH', with args beginning with: \r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_NameLongerThanAnyServedCommand_IsUnknown()
    {
        var written = await ServeAsync(Anonymous(), TestContext.CancellationToken, Command("PINGPINGPINGPINGPING"));

        Assert.AreEqual("-ERR unknown command 'PINGPINGPINGPINGPING', with args beginning with: \r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_CommandNamesInAnyCase_AreTheSameCommand()
    {
        var written = await ServeAsync(Anonymous(), TestContext.CancellationToken, "ping\r\n", "PiNg\r\n", Command("eChO", "x"));

        Assert.AreEqual("+PONG\r\n+PONG\r\n$1\r\nx\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_PipelinedCommands_AreAnsweredInOrder()
    {
        var written = await ServeAsync(Anonymous(), TestContext.CancellationToken, Command("ECHO", "1") + Command("ECHO", "2") + Command("PING"));

        Assert.AreEqual("$1\r\n1\r\n$1\r\n2\r\n+PONG\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_FramingError_AnswersTheProtocolErrorThenCloses()
    {
        var log = new RecordingExchangeLog();
        var connection = Connection(["*1\r\n$x\r\nPING\r\n"]);

        await new RedisServer(Anonymous()).ServeAsync(connection, Context(TestContext.CancellationToken, log: log));

        Assert.AreEqual("-ERR Protocol error: invalid bulk length\r\n", Written(connection));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual("A command could not be framed; answered the protocol error and closed.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_MoreThanTenElementsBeforeLogin_IsTheUnauthenticatedMultibulkError()
    {
        var written = await ServeAsync(LoginRequired(), TestContext.CancellationToken, Command([.. Enumerable.Repeat("x", 11)]));

        Assert.AreEqual("-ERR Protocol error: unauthenticated multibulk length\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_BulkStringOver16KiBBeforeLogin_IsTheUnauthenticatedBulkError()
    {
        var written = await ServeAsync(LoginRequired(), TestContext.CancellationToken, Command("AUTH", new string('p', 16385)));

        Assert.AreEqual("-ERR Protocol error: unauthenticated bulk length\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_ElevenElementsAfterLogin_AreRead()
    {
        var written = await ServeAsync(LoginRequired(), TestContext.CancellationToken, Command("AUTH", "hunter2"), Command([.. Enumerable.Repeat("x", 11)]));

        StringAssert.StartsWith(written, "+OK\r\n-ERR unknown command 'x', with args beginning with: 'x' ");
    }

    [TestMethod]
    public async Task ServeAsync_ElevenElementsUnderAllowAnonymous_AreRead()
    {
        var written = await ServeAsync(Anonymous(), TestContext.CancellationToken, Command([.. Enumerable.Repeat("x", 11)]));

        StringAssert.StartsWith(written, "-ERR unknown command 'x'");
    }

    [TestMethod]
    public async Task ServeAsync_CommandPastMaxFilesizeAndFraming_ClosesWithNoBytes()
    {
        var log = new RecordingExchangeLog();
        var limits = ExchangeLimits.Default with { MaxUploadBytes = 8 };
        var connection = Connection([Command([.. Enumerable.Repeat("12345678", 5000)])]);

        await new RedisServer(Anonymous()).ServeAsync(connection, Context(TestContext.CancellationToken, limits: limits, log: log));

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual("A command passed --max-filesize plus 64 KiB; closed with no reply.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_FirstCommandPastTheHeadTimeout_ClosesWithNoBytes()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = Connection(["*1\r\n$4\r\nPI"], peerHalfCloses: false);

        var serving = new RedisServer(Anonymous()).ServeAsync(connection, Context(TestContext.CancellationToken, clock, log: log));
        clock.Advance(ExchangeLimits.Default.HeadTimeout);
        await serving;

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual("No complete command within the head timeout; closed with no reply.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_LaterCommandIncompleteWithinTheHeadTimeoutOfItsFirstByte_ClosesWithNoMoreBytes()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = Connection(["PING\r\n*1\r\n"], peerHalfCloses: false);

        var serving = new RedisServer(Anonymous()).ServeAsync(connection, Context(TestContext.CancellationToken, clock, log: log));
        clock.Advance(ExchangeLimits.Default.HeadTimeout);
        await serving;

        Assert.AreEqual("+PONG\r\n", Written(connection));
        Assert.AreEqual("No complete command within the head timeout; closed with no reply.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_QuietBetweenCommands_IsNotBoundByTheHeadTimeout()
    {
        var clock = new ManualTimeProvider();
        using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var connection = Connection(["PING\r\n"], peerHalfCloses: false);

        var serving = new RedisServer(Anonymous()).ServeAsync(connection, Context(idleTimeout.Token, clock));
        clock.Advance(TimeSpan.FromDays(1));
        Assert.IsFalse(serving.IsCompleted);
        await idleTimeout.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.AreEqual("+PONG\r\n", Written(connection));
    }

    [TestMethod]
    public async Task ServeAsync_InfiniteHeadTimeout_WaitsUntilTheExchangeIsCancelled()
    {
        var clock = new ManualTimeProvider();
        using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var limits = ExchangeLimits.Default with { HeadTimeout = Timeout.InfiniteTimeSpan };
        var connection = Connection(["PING\r\n*1\r\n"], peerHalfCloses: false);

        var serving = new RedisServer(Anonymous()).ServeAsync(connection, Context(idleTimeout.Token, clock, limits));
        clock.Advance(TimeSpan.FromDays(1));
        Assert.IsFalse(serving.IsCompleted);
        await idleTimeout.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.AreEqual("+PONG\r\n", Written(connection));
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledBeforeTheFirstCommand_IsNotTakenForAHeadTimeout()
    {
        var log = new RecordingExchangeLog();
        using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var connection = Connection(["*1\r\n"], peerHalfCloses: false);

        var serving = new RedisServer(Anonymous()).ServeAsync(connection, Context(idleTimeout.Token, log: log));
        await idleTimeout.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.IsEmpty(log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_ClientClosesWithoutACommand_EndsWithNoBytes()
    {
        var written = await ServeAsync(LoginRequired(), TestContext.CancellationToken);

        Assert.IsEmpty(written);
    }

    [TestMethod]
    public async Task ServeAsync_TlsConnection_PassesItsSessionToThePolicy()
    {
        var policy = LoginRequired();
        var connection = Connection([Command("AUTH", "hunter2")], tlsSession: InMemoryConnection.DefaultUpgradeTlsSession);

        await new RedisServer(policy).ServeAsync(connection, Context(TestContext.CancellationToken));

        Assert.IsTrue(policy.Logins.All(login => login.TlsSession == InMemoryConnection.DefaultUpgradeTlsSession));
    }

    [TestMethod]
    [DataRow(ConnectionRefusal.TooManyConnections)]
    [DataRow(ConnectionRefusal.TooManyConnectionsFromAddress)]
    public async Task WriteRefusalAsync_AnyRefusal_IsMaxNumberOfClientsReachedThenClose(ConnectionRefusal refusal)
    {
        var connection = Connection([]);

        await new RedisServer(Anonymous()).WriteRefusalAsync(connection, refusal, TestContext.CancellationToken);

        Assert.AreEqual("-ERR max number of clients reached\r\n", Encoding.Latin1.GetString(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    private static UnitTestRedisAuthenticationPolicy LoginRequired() => new(false);

    private static UnitTestRedisAuthenticationPolicy Anonymous() => new(true);
}
