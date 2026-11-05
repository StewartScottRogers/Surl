using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Redis.RedisTestExchanges;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The connection commands through the server (BL-489; ADR-0106 decisions 1, 2 and 4), every
/// byte written by hand from the RESP page and each command's reference page.
/// </summary>
[TestClass]
public sealed class RedisConnectionCommandsTests
{
    private const string Resp3Hello =
        "%7\r\n$6\r\nserver\r\n$5\r\nredis\r\n$7\r\nversion\r\n$5\r\n7.4.0\r\n$5\r\nproto\r\n:3\r\n$2\r\nid\r\n:1\r\n"
        + "$4\r\nmode\r\n$10\r\nstandalone\r\n$4\r\nrole\r\n$6\r\nmaster\r\n$7\r\nmodules\r\n*0\r\n";

    private const string Resp2Hello =
        "*14\r\n$6\r\nserver\r\n$5\r\nredis\r\n$7\r\nversion\r\n$5\r\n7.4.0\r\n$5\r\nproto\r\n:2\r\n$2\r\nid\r\n:1\r\n"
        + "$4\r\nmode\r\n$10\r\nstandalone\r\n$4\r\nrole\r\n$6\r\nmaster\r\n$7\r\nmodules\r\n*0\r\n";

    private const string WrongPass = "-WRONGPASS invalid username-password pair or user is disabled.\r\n";

    private const string NoAuth = "-NOAUTH Authentication required.\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_CommandBeforeAuth_IsNoAuthAndTheConnectionStaysOpen()
    {
        var written = await ServeAsync(LoginRequired(), TestContext.CancellationToken, Command("ECHO", "hi"), Command("SELECT", "1"), Command("PING"));

        Assert.AreEqual(NoAuth + NoAuth + NoAuth, written);
    }

    [TestMethod]
    public async Task ServeAsync_AuthWithTheGoodPassword_IsOkAndLogsInAsDefault()
    {
        var policy = LoginRequired();

        var written = await ServeAsync(policy, TestContext.CancellationToken, Command("AUTH", "hunter2"), Command("ECHO", "hi"));

        Assert.AreEqual("+OK\r\n$2\r\nhi\r\n", written);
        Assert.AreEqual("default", policy.Logins[^1].UserName);
        Assert.AreEqual("redis", policy.Logins[^1].Scheme);
    }

    [TestMethod]
    public async Task ServeAsync_AuthWithABadPassword_IsWrongPassAndLeavesTheConnectionLoggedOut()
    {
        var log = new RecordingExchangeLog();
        var connection = Connection([Command("AUTH", "nope"), Command("ECHO", "hi")]);

        await new RedisServer(LoginRequired()).ServeAsync(connection, Context(TestContext.CancellationToken, log: log));

        Assert.AreEqual(WrongPass + NoAuth, Written(connection));
        Assert.AreEqual("A login was refused (RefusedCredentials); answered -WRONGPASS.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_AuthWithUserAndPassword_LogsInAsThatUser()
    {
        var policy = LoginRequired();

        var written = await ServeAsync(policy, TestContext.CancellationToken, Command("auth", "alice", "secret"), Command("ECHO", "hi"));

        Assert.AreEqual("+OK\r\n$2\r\nhi\r\n", written);
        Assert.AreEqual("alice", policy.Logins[^1].UserName);
    }

    [TestMethod]
    public async Task ServeAsync_RefusedLoginAfterAnAcceptedOne_KeepsTheEarlierLogin()
    {
        var written = await ServeAsync(LoginRequired(), TestContext.CancellationToken, Command("AUTH", "hunter2"), Command("AUTH", "alice", "wrong"), Command("ECHO", "hi"));

        Assert.AreEqual("+OK\r\n" + WrongPass + "$2\r\nhi\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_PlaintextLoginThePolicyRefuses_IsWrongPass()
    {
        var policy = new UnitTestRedisAuthenticationPolicy(false) { RefusalVerdict = PasswordLoginVerdict.RefusedPlaintext };

        var written = await ServeAsync(policy, TestContext.CancellationToken, Command("AUTH", "bob", "pw"));

        Assert.AreEqual(WrongPass, written);
    }

    [TestMethod]
    public async Task ServeAsync_AuthWithTooManyOrNoArguments_IsSyntaxErrorOrWrongArgumentCount()
    {
        var written = await ServeAsync(LoginRequired(), TestContext.CancellationToken, Command("AUTH", "a", "b", "c"), Command("AUTH"));

        Assert.AreEqual("-ERR syntax error\r\n-ERR wrong number of arguments for 'auth' command\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_AnonymousAllowed_ServesCommandsWithoutLoginAndAcceptsAuthUnchecked()
    {
        var written = await ServeAsync(Anonymous(), TestContext.CancellationToken, Command("ECHO", "hi"), Command("AUTH", "anyone", "anything"));

        Assert.AreEqual("$2\r\nhi\r\n+OK\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_Hello3_AnswersARespThreeMapAndLaterRepliesAreRespThree()
    {
        var written = await ServeAsync(Anonymous(), TestContext.CancellationToken, Command("HELLO", "3"), Command("CLIENT", "GETNAME"));

        Assert.AreEqual(Resp3Hello + "_\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_Hello3ThenHello2_SwitchesBackToRespTwo()
    {
        var written = await ServeAsync(Anonymous(), TestContext.CancellationToken, Command("HELLO", "3"), Command("HELLO", "2"), Command("CLIENT", "GETNAME"));

        Assert.AreEqual(Resp3Hello + Resp2Hello + "$-1\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_HelloWithoutAVersion_KeepsTheVersionAndAnswersItsFields()
    {
        var written = await ServeAsync(Anonymous(), TestContext.CancellationToken, Command("HELLO"));

        Assert.AreEqual(Resp2Hello, written);
    }

    [TestMethod]
    [DataRow("4")]
    [DataRow("1")]
    [DataRow("three")]
    public async Task ServeAsync_HelloWithAnUnsupportedVersion_IsNoProtoAndKeepsTheVersion(string version)
    {
        var written = await ServeAsync(Anonymous(), TestContext.CancellationToken, Command("HELLO", version), Command("CLIENT", "GETNAME"));

        Assert.AreEqual("-NOPROTO unsupported protocol version\r\n$-1\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_Hello3WithAuth_LogsInAndSwitches()
    {
        var policy = LoginRequired();

        var written = await ServeAsync(policy, TestContext.CancellationToken, Command("HELLO", "3", "AUTH", "alice", "secret"), Command("ECHO", "hi"));

        Assert.AreEqual(Resp3Hello + "$2\r\nhi\r\n", written);
        Assert.AreEqual("alice", policy.Logins[^1].UserName);
    }

    [TestMethod]
    public async Task ServeAsync_Hello3WithRefusedAuth_IsWrongPassAndKeepsRespTwo()
    {
        var written = await ServeAsync(
            LoginRequired(),
            TestContext.CancellationToken,
            Command("HELLO", "3", "AUTH", "alice", "wrong"),
            Command("AUTH", "hunter2"),
            Command("CLIENT", "GETNAME"));

        Assert.AreEqual(WrongPass + "+OK\r\n$-1\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_HelloWithoutAuthBeforeALogin_IsHellosNoAuth()
    {
        var written = await ServeAsync(LoginRequired(), TestContext.CancellationToken, Command("HELLO", "3"));

        Assert.AreEqual(
            "-NOAUTH HELLO must be called with the client already authenticated, otherwise the HELLO <proto> AUTH <user> <pass> option can be used to authenticate the client and select the RESP protocol version at the same time\r\n",
            written);
    }

    [TestMethod]
    public async Task ServeAsync_HelloWithSetName_NamesTheConnection()
    {
        var written = await ServeAsync(Anonymous(), TestContext.CancellationToken, Command("HELLO", "2", "setname", "conn1"), Command("CLIENT", "GETNAME"));

        Assert.AreEqual(Resp2Hello + "$5\r\nconn1\r\n", written);
    }

    [TestMethod]
    [DataRow("SETNAME", "a b", "-ERR Client names cannot contain spaces, newlines or special characters.")]
    [DataRow("FOO", "x", "-ERR syntax error")]
    [DataRow("AUTH", "alice", "-ERR syntax error")]
    public async Task ServeAsync_HelloWithABadOption_IsRefusedAndChangesNothing(string option, string value, string expected)
    {
        var written = await ServeAsync(Anonymous(), TestContext.CancellationToken, Command("HELLO", "3", option, value), Command("CLIENT", "GETNAME"));

        Assert.AreEqual(expected + "\r\n$-1\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_SelectInRange_IsOk()
    {
        var written = await ServeAsync(Anonymous(), TestContext.CancellationToken, Command("SELECT", "0"), Command("SELECT", "15"));

        Assert.AreEqual("+OK\r\n+OK\r\n", written);
    }

    [TestMethod]
    [DataRow("16", "-ERR DB index is out of range")]
    [DataRow("-1", "-ERR DB index is out of range")]
    [DataRow("abc", "-ERR value is not an integer or out of range")]
    [DataRow("4294967296", "-ERR value is not an integer or out of range")]
    [DataRow("-4294967296", "-ERR value is not an integer or out of range")]
    public async Task ServeAsync_SelectOutOfRangeOrNotAnInteger_IsRefused(string index, string expected)
    {
        var written = await ServeAsync(Anonymous(), TestContext.CancellationToken, Command("SELECT", index));

        Assert.AreEqual(expected + "\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_PingInRespTwo_IsPongOrTheMessage()
    {
        var written = await ServeAsync(Anonymous(), TestContext.CancellationToken, Command("PING"), Command("PING", "hello"));

        Assert.AreEqual("+PONG\r\n$5\r\nhello\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_PingInRespThree_IsPongOrTheMessage()
    {
        var written = await ServeAsync(Anonymous(), TestContext.CancellationToken, Command("HELLO", "3"), Command("PING"), Command("PING", "hello"));

        Assert.AreEqual(Resp3Hello + "+PONG\r\n$5\r\nhello\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_PingWithTwoArguments_IsWrongArgumentCount()
    {
        var written = await ServeAsync(Anonymous(), TestContext.CancellationToken, Command("PING", "a", "b"));

        Assert.AreEqual("-ERR wrong number of arguments for 'ping' command\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_Echo_IsTheMessage()
    {
        var written = await ServeAsync(Anonymous(), TestContext.CancellationToken, Command("ECHO", "Hello World"));

        Assert.AreEqual("$11\r\nHello World\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_Quit_IsOkThenTheConnectionCloses()
    {
        var connection = Connection([Command("QUIT") + Command("PING")]);

        await new RedisServer(LoginRequired()).ServeAsync(connection, Context(TestContext.CancellationToken));

        Assert.AreEqual("+OK\r\n", Written(connection));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_Reset_LogsOutClearsTheNameAndReturnsToRespTwo()
    {
        var written = await ServeAsync(
            LoginRequired(),
            TestContext.CancellationToken,
            Command("HELLO", "3", "AUTH", "alice", "secret", "SETNAME", "n"),
            Command("SELECT", "3"),
            Command("RESET"),
            Command("ECHO", "x"),
            Command("AUTH", "hunter2"),
            Command("CLIENT", "GETNAME"));

        Assert.AreEqual(Resp3Hello + "+OK\r\n+RESET\r\n" + NoAuth + "+OK\r\n$-1\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_ClientId_IsOnePerConnectionInServeOrder()
    {
        var server = new RedisServer(Anonymous());
        var first = Connection([Command("CLIENT", "ID")]);
        var second = Connection([Command("client", "id")]);

        await server.ServeAsync(first, Context(TestContext.CancellationToken));
        await server.ServeAsync(second, Context(TestContext.CancellationToken));

        Assert.AreEqual(":1\r\n", Written(first));
        Assert.AreEqual(":2\r\n", Written(second));
    }

    [TestMethod]
    public async Task ServeAsync_ClientSetNameThenGetName_AnswersTheNameAndAnEmptyNameClearsIt()
    {
        var written = await ServeAsync(
            Anonymous(),
            TestContext.CancellationToken,
            Command("CLIENT", "SETNAME", "worker-1"),
            Command("CLIENT", "GETNAME"),
            Command("CLIENT", "SETNAME", string.Empty),
            Command("CLIENT", "GETNAME"));

        Assert.AreEqual("+OK\r\n$8\r\nworker-1\r\n+OK\r\n$-1\r\n", written);
    }

    [TestMethod]
    [DataRow("a b")]
    [DataRow("a\nb")]
    [DataRow("café")]
    public async Task ServeAsync_ClientSetNameWithSpacesOrSpecialCharacters_IsRefused(string name)
    {
        var written = await ServeAsync(Anonymous(), TestContext.CancellationToken, Command("CLIENT", "SETNAME", name), Command("CLIENT", "GETNAME"));

        Assert.AreEqual("-ERR Client names cannot contain spaces, newlines or special characters.\r\n$-1\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_ClientSetInfo_IsOkForTheLibraryAttributesAndASyntaxErrorOtherwise()
    {
        var written = await ServeAsync(
            Anonymous(),
            TestContext.CancellationToken,
            Command("CLIENT", "SETINFO", "lib-name", "redis-py"),
            Command("CLIENT", "SETINFO", "LIB-VER", "5.0"),
            Command("CLIENT", "SETINFO", "colour", "red"));

        Assert.AreEqual("+OK\r\n+OK\r\n-ERR syntax error\r\n", written);
    }

    [TestMethod]
    public async Task ServeAsync_ClientWithAnUnknownSubcommandOrWrongCount_IsRefusedAsRedisDoes()
    {
        var written = await ServeAsync(
            Anonymous(),
            TestContext.CancellationToken,
            Command("CLIENT", "KILL", "x"),
            Command("CLIENT"),
            Command("CLIENT", "GETNAME", "extra"));

        Assert.AreEqual(
            "-ERR unknown subcommand 'KILL'. Try CLIENT HELP.\r\n"
            + "-ERR wrong number of arguments for 'client' command\r\n"
            + "-ERR wrong number of arguments for 'client|getname' command\r\n",
            written);
    }

    [TestMethod]
    public async Task ServeAsync_ClientBeforeLogin_IsNoAuth()
    {
        var written = await ServeAsync(LoginRequired(), TestContext.CancellationToken, Command("CLIENT", "ID"));

        Assert.AreEqual(NoAuth, written);
    }

    [TestMethod]
    public void IsValidClientName_PrintableAsciiWithoutSpace_IsValid()
    {
        Assert.IsTrue(RedisConnectionCommands.IsValidClientName("!~a-Z_0"u8.ToArray()));
        Assert.IsTrue(RedisConnectionCommands.IsValidClientName([]));
        Assert.IsFalse(RedisConnectionCommands.IsValidClientName([0x7F]));
    }

    private static UnitTestRedisAuthenticationPolicy LoginRequired() => new(false);

    private static UnitTestRedisAuthenticationPolicy Anonymous() => new(true);
}
