using static Surl.Extended.Protocol.Redis.RedisTestExchanges;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The key commands through the server (BL-490; ADR-0106 decisions 1 and 3), every byte written by
/// hand from the RESP page and each command's reference page.
/// </summary>
[TestClass]
public sealed class RedisKeyCommandsTests
{
    private const string Ok = "+OK\r\n";
    private const string SyntaxError = "-ERR syntax error\r\n";

    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public async Task DelUnlinkAndExists_CountTheKeysAndExistsCountsARepeatedKeyTwice()
    {
        var written = await new RedisTestServer().SendAsync(
            Token,
            Command("MSET", "a", "1", "b", "2", "c", "3"),
            Command("EXISTS", "a", "a", "nosuch"),
            Command("DEL", "a", "b", "nosuch"),
            Command("UNLINK", "c", "c"),
            Command("EXISTS", "a", "b", "c"));

        Assert.AreEqual(Ok + ":2\r\n:2\r\n:1\r\n:0\r\n", written);
    }

    [TestMethod]
    public async Task Ttl_MissingKeyIsMinusTwoAndKeyWithoutExpiryMinusOne()
    {
        var written = await new RedisTestServer().SendAsync(
            Token,
            Command("SET", "k", "v"),
            Command("TTL", "nosuch"),
            Command("PTTL", "nosuch"),
            Command("TTL", "k"),
            Command("PTTL", "k"));

        Assert.AreEqual(Ok + ":-2\r\n:-2\r\n:-1\r\n:-1\r\n", written);
    }

    [TestMethod]
    public async Task Ttl_RoundsTheRemainingMillisecondsToTheNearestSecond()
    {
        var server = new RedisTestServer();
        await server.SendAsync(Token, Command("SET", "k", "v", "PX", "10000"));

        server.Clock.Advance(TimeSpan.FromMilliseconds(1600));
        var written = await server.SendAsync(Token, Command("TTL", "k"), Command("PTTL", "k"));

        Assert.AreEqual(":8\r\n:8400\r\n", written);
    }

    [TestMethod]
    public async Task ExpireAndPExpire_SetTheExpiryAndAnswerOneOrZeroForAMissingKey()
    {
        var written = await new RedisTestServer().SendAsync(
            Token,
            Command("SET", "k", "v"),
            Command("EXPIRE", "k", "10"),
            Command("TTL", "k"),
            Command("PEXPIRE", "k", "2500"),
            Command("PTTL", "k"),
            Command("EXPIRE", "nosuch", "10"));

        Assert.AreEqual(Ok + ":1\r\n:10\r\n:1\r\n:2500\r\n:0\r\n", written);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("-10")]
    public async Task Expire_TimeAtOrBelowZero_DeletesTheKeyAndAnswersOne(string seconds)
    {
        var written = await new RedisTestServer().SendAsync(Token, Command("SET", "k", "v"), Command("EXPIRE", "k", seconds), Command("EXISTS", "k"));

        Assert.AreEqual(Ok + ":1\r\n:0\r\n", written);
    }

    [TestMethod]
    [DataRow("NX", ":1\r\n", ":0\r\n")]
    [DataRow("XX", ":0\r\n", ":1\r\n")]
    [DataRow("GT", ":0\r\n", ":1\r\n")]
    [DataRow("LT", ":1\r\n", ":0\r\n")]
    public async Task Expire_Conditions_SetOnlyAsTheExpirePageSays(string condition, string onPersistent, string onLaterThanTwenty)
    {
        // "p" has no expiry; "e" expires in 20 s and is asked for 30 s.
        var written = await new RedisTestServer().SendAsync(
            Token,
            Command("SET", "p", "v"),
            Command("SET", "e", "v", "EX", "20"),
            Command("EXPIRE", "p", "30", condition),
            Command("EXPIRE", "e", "30", condition));

        Assert.AreEqual(Ok + Ok + onPersistent + onLaterThanTwenty, written);
    }

    [TestMethod]
    public async Task Expire_LtWithAnEarlierTimeAndXxGtCombined_SetTheExpiry()
    {
        var written = await new RedisTestServer().SendAsync(
            Token,
            Command("SET", "e", "v", "EX", "20"),
            Command("EXPIRE", "e", "10", "LT"),
            Command("EXPIRE", "e", "15", "xx", "gt", "GT"),
            Command("EXPIRE", "e", "5", "XX", "LT"),
            Command("TTL", "e"));

        Assert.AreEqual(Ok + ":1\r\n:1\r\n:1\r\n:5\r\n", written);
    }

    [TestMethod]
    [DataRow("NX XX")]
    [DataRow("XX NX")]
    [DataRow("NX GT")]
    [DataRow("GT LT")]
    [DataRow("SOON")]
    public async Task Expire_ConflictingOrUnknownConditions_IsSyntaxError(string conditions)
    {
        var written = await new RedisTestServer().SendAsync(Token, Command(["EXPIRE", "k", "10", .. conditions.Split(' ')]));

        Assert.AreEqual(SyntaxError, written);
    }

    [TestMethod]
    public async Task Expire_TimeNotAnIntegerOrOverflowing_IsRefused()
    {
        var written = await new RedisTestServer().SendAsync(
            Token,
            Command("EXPIRE", "k", "soon"),
            Command("EXPIRE", "k", "9223372036854775807"),
            Command("EXPIRE", "k", "-9223372036854775807"),
            Command("PEXPIRE", "k", "9223372036854775807"));

        Assert.AreEqual(
            "-ERR value is not an integer or out of range\r\n-ERR invalid expire time in 'expire' command\r\n"
            + "-ERR invalid expire time in 'expire' command\r\n-ERR invalid expire time in 'pexpire' command\r\n",
            written);
    }

    [TestMethod]
    public async Task Persist_RemovesAnExpiryAnsweringOneElseZero()
    {
        var written = await new RedisTestServer().SendAsync(
            Token,
            Command("SET", "k", "v", "EX", "10"),
            Command("PERSIST", "k"),
            Command("PERSIST", "k"),
            Command("PERSIST", "nosuch"),
            Command("TTL", "k"));

        Assert.AreEqual(Ok + ":1\r\n:0\r\n:0\r\n:-1\r\n", written);
    }

    [TestMethod]
    public async Task Type_AnswersTheTypeOrNoneAsASimpleString()
    {
        var server = new RedisTestServer();
        server.Keyspace[0].Put("h", new UnitTestRedisValue(), server.NowMilliseconds);

        var written = await server.SendAsync(Token, Command("SET", "s", "v"), Command("TYPE", "s"), Command("TYPE", "h"), Command("TYPE", "nosuch"));

        Assert.AreEqual(Ok + "+string\r\n+hash\r\n+none\r\n", written);
    }

    [TestMethod]
    public async Task Rename_MovesTheValueAndExpiryReplacingTheTarget()
    {
        var written = await new RedisTestServer().SendAsync(
            Token,
            Command("SET", "src", "v", "EX", "40"),
            Command("SET", "dst", "old"),
            Command("RENAME", "src", "dst"),
            Command("EXISTS", "src"),
            Command("GET", "dst"),
            Command("TTL", "dst"),
            Command("RENAME", "dst", "dst"),
            Command("GET", "dst"));

        Assert.AreEqual(Ok + Ok + Ok + ":0\r\n$1\r\nv\r\n:40\r\n" + Ok + "$1\r\nv\r\n", written);
    }

    [TestMethod]
    public async Task Rename_MissingSource_IsNoSuchKey()
    {
        var written = await new RedisTestServer().SendAsync(Token, Command("RENAME", "nosuch", "dst"));

        Assert.AreEqual("-ERR no such key\r\n", written);
    }

    [TestMethod]
    public async Task Keys_HQuestionMarkLlo_MatchesAsTheKeysPageSays()
    {
        var written = await new RedisTestServer().SendAsync(
            Token,
            Command("MSET", "hello", "1", "hallo", "2", "hxllo", "3", "hllo", "4", "heeello", "5"),
            Command("KEYS", "h?llo"),
            Command("KEYS", "h*llo"),
            Command("KEYS", "h[ae]llo"),
            Command("KEYS", "h[^e]llo"),
            Command("KEYS", "h[a-b]llo"));

        Assert.AreEqual(
            Ok
            + "*3\r\n$5\r\nhello\r\n$5\r\nhallo\r\n$5\r\nhxllo\r\n"
            + "*5\r\n$5\r\nhello\r\n$5\r\nhallo\r\n$5\r\nhxllo\r\n$4\r\nhllo\r\n$7\r\nheeello\r\n"
            + "*2\r\n$5\r\nhello\r\n$5\r\nhallo\r\n"
            + "*2\r\n$5\r\nhallo\r\n$5\r\nhxllo\r\n"
            + "*1\r\n$5\r\nhallo\r\n",
            written);
    }

    [TestMethod]
    public async Task Scan_FullIterationFromZeroToZero_ReturnsEveryKey()
    {
        var server = new RedisTestServer();
        await server.SendAsync(Token, Command("MSET", "a", "1", "b", "2", "c", "3", "d", "4", "e", "5"));

        var first = await server.SendAsync(Token, Command("SCAN", "0", "COUNT", "2"));
        var second = await server.SendAsync(Token, Command("SCAN", "3", "COUNT", "2"));
        var third = await server.SendAsync(Token, Command("SCAN", "5", "COUNT", "2"));

        Assert.AreEqual("*2\r\n$1\r\n3\r\n*2\r\n$1\r\na\r\n$1\r\nb\r\n", first);
        Assert.AreEqual("*2\r\n$1\r\n5\r\n*2\r\n$1\r\nc\r\n$1\r\nd\r\n", second);
        Assert.AreEqual("*2\r\n$1\r\n0\r\n*1\r\n$1\r\ne\r\n", third);
    }

    [TestMethod]
    public async Task Scan_DefaultCountAndCountEqualToTheKeysLeft_EndsWithCursorZero()
    {
        var server = new RedisTestServer();
        await server.SendAsync(Token, Command("MSET", "a", "1", "b", "2"));

        var written = await server.SendAsync(Token, Command("SCAN", "0"), Command("SCAN", "0", "COUNT", "2"), Command("SCAN", "18446744073709551615"));

        Assert.AreEqual(
            "*2\r\n$1\r\n0\r\n*2\r\n$1\r\na\r\n$1\r\nb\r\n*2\r\n$1\r\n0\r\n*2\r\n$1\r\na\r\n$1\r\nb\r\n*2\r\n$1\r\n0\r\n*0\r\n",
            written);
    }

    [TestMethod]
    public async Task Scan_MatchAndType_FilterTheKeysLookedAt()
    {
        var server = new RedisTestServer();
        server.Keyspace[0].Put("hash1", new UnitTestRedisValue(), server.NowMilliseconds);

        var written = await server.SendAsync(
            Token,
            Command("MSET", "key1", "1", "other", "2"),
            Command("SCAN", "0", "MATCH", "*1"),
            Command("SCAN", "0", "TYPE", "STRING", "match", "*"));

        Assert.AreEqual(
            Ok + "*2\r\n$1\r\n0\r\n*2\r\n$5\r\nhash1\r\n$4\r\nkey1\r\n"
            + "*2\r\n$1\r\n0\r\n*2\r\n$4\r\nkey1\r\n$5\r\nother\r\n",
            written);
    }

    [TestMethod]
    public async Task Scan_BadCursorOrOptions_IsRefused()
    {
        var written = await new RedisTestServer().SendAsync(
            Token,
            Command("SCAN", "-1"),
            Command("SCAN", "abc"),
            Command("SCAN", "0", "COUNT", "0"),
            Command("SCAN", "0", "COUNT", "many"),
            Command("SCAN", "0", "MATCH"),
            Command("SCAN", "0", "LIMIT", "5"));

        Assert.AreEqual(
            "-ERR invalid cursor\r\n-ERR invalid cursor\r\n" + SyntaxError
            + "-ERR value is not an integer or out of range\r\n" + SyntaxError + SyntaxError,
            written);
    }

    [TestMethod]
    public async Task Select_IsolatesDatabases()
    {
        var written = await new RedisTestServer().SendAsync(
            Token,
            Command("SET", "k", "zero"),
            Command("SELECT", "1"),
            Command("GET", "k"),
            Command("SET", "k", "one"),
            Command("DBSIZE"),
            Command("SELECT", "0"),
            Command("GET", "k"));

        Assert.AreEqual(Ok + Ok + "$-1\r\n" + Ok + ":1\r\n" + Ok + "$4\r\nzero\r\n", written);
    }

    [TestMethod]
    public async Task FlushDbAndFlushAll_EmptyTheSelectedDatabaseAndThenEveryOne()
    {
        var written = await new RedisTestServer().SendAsync(
            Token,
            Command("SET", "a", "1"),
            Command("SELECT", "2"),
            Command("SET", "b", "2"),
            Command("FLUSHDB", "ASYNC"),
            Command("DBSIZE"),
            Command("SET", "b", "2"),
            Command("SELECT", "0"),
            Command("DBSIZE"),
            Command("FLUSHALL", "sync"),
            Command("DBSIZE"),
            Command("SELECT", "2"),
            Command("DBSIZE"),
            Command("FLUSHDB"));

        Assert.AreEqual(Ok + Ok + Ok + Ok + ":0\r\n" + Ok + Ok + ":1\r\n" + Ok + ":0\r\n" + Ok + ":0\r\n" + Ok, written);
    }

    [TestMethod]
    [DataRow("LAZY")]
    [DataRow("SYNC ASYNC")]
    public async Task FlushDb_UnknownMode_IsSyntaxError(string mode)
    {
        var written = await new RedisTestServer().SendAsync(Token, Command(["FLUSHDB", .. mode.Split(' ')]), Command(["FLUSHALL", .. mode.Split(' ')]));

        Assert.AreEqual(SyntaxError + SyntaxError, written);
    }

    [TestMethod]
    public async Task Server_NewServer_StartsWithEveryDatabaseEmpty()
    {
        var first = new RedisTestServer();
        await first.SendAsync(Token, Command("SET", "k", "v"));

        var written = await new RedisTestServer().SendAsync(Token, Command("GET", "k"), Command("DBSIZE"));

        Assert.AreEqual("$-1\r\n:0\r\n", written);
    }
}
