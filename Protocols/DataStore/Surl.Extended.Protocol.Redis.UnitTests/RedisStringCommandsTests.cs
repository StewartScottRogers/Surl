using static Surl.Extended.Protocol.Redis.RedisTestExchanges;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The string commands through the server (BL-490; ADR-0106 decisions 1 to 3), every byte written
/// by hand from the RESP page and each command's reference page.
/// </summary>
[TestClass]
public sealed class RedisStringCommandsTests
{
    private const string Ok = "+OK\r\n";
    private const string Resp2Null = "$-1\r\n";
    private const string Resp3Null = "_\r\n";
    private const string WrongType = "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
    private const string NotAnInteger = "-ERR value is not an integer or out of range\r\n";
    private const string Overflow = "-ERR increment or decrement would overflow\r\n";
    private const string SyntaxError = "-ERR syntax error\r\n";
    private const string InvalidSetExpireTime = "-ERR invalid expire time in 'set' command\r\n";

    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public async Task Get_MissingKey_IsNullBulkStringInRespTwoAndNullInRespThree()
    {
        var server = new RedisTestServer();

        var resp2 = await server.SendAsync(Token, Command("GET", "nosuch"));
        var resp3 = await server.SendAsync(Token, Command("HELLO", "3"), Command("GET", "nosuch"));

        Assert.AreEqual(Resp2Null, resp2);
        Assert.EndsWith(Resp3Null, resp3);
    }

    [TestMethod]
    public async Task SetThenGet_AnswersOkThenTheValueOnAnotherConnection()
    {
        var server = new RedisTestServer();

        var set = await server.SendAsync(Token, Command("SET", "mykey", "Hello"));
        var get = await server.SendAsync(Token, Command("get", "mykey"));

        Assert.AreEqual(Ok, set);
        Assert.AreEqual("$5\r\nHello\r\n", get);
    }

    [TestMethod]
    public async Task Set_NxOnAPresentKeyOrXxOnAnAbsentOne_IsTheNullReplyAndSetsNothing()
    {
        var server = new RedisTestServer();

        var resp2 = await server.SendAsync(
            Token,
            Command("SET", "k", "v1"),
            Command("SET", "k", "v2", "NX"),
            Command("SET", "absent", "v", "XX"),
            Command("GET", "k"),
            Command("EXISTS", "absent"));
        var resp3 = await server.SendAsync(Token, Command("HELLO", "3"), Command("SET", "k", "v3", "nx"));

        Assert.AreEqual(Ok + Resp2Null + Resp2Null + "$2\r\nv1\r\n:0\r\n", resp2);
        Assert.EndsWith(Resp3Null, resp3);
    }

    [TestMethod]
    public async Task Set_NxOnAnAbsentKeyAndXxOnAPresentOne_SetsTheValue()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("SET", "k", "v1", "NX", "NX"),
            Command("SET", "k", "v2", "XX"),
            Command("GET", "k"));

        Assert.AreEqual(Ok + Ok + "$2\r\nv2\r\n", written);
    }

    [TestMethod]
    public async Task Set_WithGet_AnswersTheOldValueOrNull()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("SET", "k", "v1", "GET"),
            Command("SET", "k", "v2", "GET"),
            Command("SET", "k", "v3", "NX", "GET"),
            Command("GET", "k"));

        Assert.AreEqual(Resp2Null + "$2\r\nv1\r\n$2\r\nv2\r\n$2\r\nv2\r\n", written);
    }

    [TestMethod]
    public async Task Set_WithGetOnAKeyOfAnotherType_IsWrongTypeAndSetsNothing()
    {
        var server = new RedisTestServer();
        server.Keyspace[0].Put("h", new UnitTestRedisValue(), server.NowMilliseconds);

        var written = await server.SendAsync(Token, Command("SET", "h", "v", "GET"), Command("TYPE", "h"));

        Assert.AreEqual(WrongType + "+hash\r\n", written);
    }

    [TestMethod]
    public async Task Set_OnAKeyOfAnotherTypeWithoutGet_ReplacesItWithAString()
    {
        var server = new RedisTestServer();
        server.Keyspace[0].Put("h", new UnitTestRedisValue(), server.NowMilliseconds);

        var written = await server.SendAsync(Token, Command("SET", "h", "v", "NX"), Command("SET", "h", "v"), Command("TYPE", "h"));

        Assert.AreEqual(Resp2Null + Ok + "+string\r\n", written);
    }

    [TestMethod]
    public async Task SetPx100_KeyIsAbsentOnceTheClockHasAdvanced100Milliseconds()
    {
        var server = new RedisTestServer();
        await server.SendAsync(Token, Command("SET", "k", "v", "PX", "100"));

        server.Clock.Advance(TimeSpan.FromMilliseconds(99));
        var before = await server.SendAsync(Token, Command("GET", "k"), Command("PTTL", "k"));
        server.Clock.Advance(TimeSpan.FromMilliseconds(1));
        var after = await server.SendAsync(Token, Command("GET", "k"), Command("DBSIZE"));

        Assert.AreEqual("$1\r\nv\r\n:1\r\n", before);
        Assert.AreEqual(Resp2Null + ":0\r\n", after);
    }

    [TestMethod]
    public async Task Set_ExExatAndPxat_SetTheExpiry()
    {
        var server = new RedisTestServer();
        var now = server.NowMilliseconds;

        var written = await server.SendAsync(
            Token,
            Command("SET", "ex", "v", "EX", "10", "EX", "20"),
            Command("SET", "exat", "v", "EXAT", ((now / 1000) + 30).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Command("SET", "pxat", "v", "pxat", (now + 4000).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Command("TTL", "ex"),
            Command("TTL", "exat"),
            Command("PTTL", "pxat"));

        Assert.AreEqual(Ok + Ok + Ok + ":20\r\n:30\r\n:4000\r\n", written);
    }

    [TestMethod]
    public async Task Set_WithoutKeepTtlClearsTheExpiryAndWithKeepTtlKeepsIt()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("SET", "a", "v", "EX", "100"),
            Command("SET", "a", "w"),
            Command("SET", "b", "v", "EX", "100"),
            Command("SET", "b", "w", "KEEPTTL"),
            Command("TTL", "a"),
            Command("TTL", "b"));

        Assert.AreEqual(Ok + Ok + Ok + Ok + ":-1\r\n:100\r\n", written);
    }

    [TestMethod]
    [DataRow("EX", "0")]
    [DataRow("PX", "-5")]
    [DataRow("EXAT", "0")]
    [DataRow("PXAT", "-1")]
    [DataRow("EX", "9223372036854775")]
    [DataRow("PX", "9223372036854775807")]
    public async Task Set_ExpiryTimeNotPositiveOrOverflowing_IsInvalidExpireTime(string option, string time)
    {
        var written = await new RedisTestServer().SendAsync(Token, Command("SET", "k", "v", option, time), Command("EXISTS", "k"));

        Assert.AreEqual(InvalidSetExpireTime + ":0\r\n", written);
    }

    [TestMethod]
    public async Task Set_ExpiryTimeNotAnInteger_IsNotAnInteger()
    {
        var written = await new RedisTestServer().SendAsync(Token, Command("SET", "k", "v", "EX", "ten"));

        Assert.AreEqual(NotAnInteger, written);
    }

    [TestMethod]
    [DataRow("NX XX")]
    [DataRow("XX NX")]
    [DataRow("EX 10 PX 100")]
    [DataRow("KEEPTTL EX 10")]
    [DataRow("EX 10 KEEPTTL")]
    [DataRow("EX")]
    [DataRow("BOGUS")]
    public async Task Set_ConflictingUnknownOrIncompleteOptions_IsSyntaxError(string options)
    {
        var written = await new RedisTestServer().SendAsync(Token, Command(["SET", "k", "v", .. options.Split(' ')]));

        Assert.AreEqual(SyntaxError, written);
    }

    [TestMethod]
    public async Task GetAndStrLenAndAppendAndIncr_OnAKeyOfAnotherType_AreWrongType()
    {
        var server = new RedisTestServer();
        server.Keyspace[0].Put("h", new UnitTestRedisValue(), server.NowMilliseconds);

        var written = await server.SendAsync(
            Token,
            Command("GET", "h"),
            Command("STRLEN", "h"),
            Command("APPEND", "h", "x"),
            Command("INCR", "h"),
            Command("GETDEL", "h"));

        Assert.AreEqual(WrongType + WrongType + WrongType + WrongType + WrongType, written);
    }

    [TestMethod]
    public async Task GetDel_AnswersTheValueAndDeletesTheKey()
    {
        var written = await new RedisTestServer().SendAsync(
            Token,
            Command("SET", "k", "v"),
            Command("GETDEL", "k"),
            Command("GETDEL", "k"));

        Assert.AreEqual(Ok + "$1\r\nv\r\n" + Resp2Null, written);
    }

    [TestMethod]
    public async Task MGet_AnswersANullForEachMissingOrNonStringKey()
    {
        var server = new RedisTestServer();
        server.Keyspace[0].Put("h", new UnitTestRedisValue(), server.NowMilliseconds);

        var resp2 = await server.SendAsync(Token, Command("MSET", "a", "1", "b", "2"), Command("MGET", "a", "nosuch", "h", "b"));
        var resp3 = await server.SendAsync(Token, Command("HELLO", "3"), Command("MGET", "a", "nosuch"));

        Assert.AreEqual(Ok + "*4\r\n$1\r\n1\r\n$-1\r\n$-1\r\n$1\r\n2\r\n", resp2);
        Assert.EndsWith("*2\r\n$1\r\n1\r\n_\r\n", resp3);
    }

    [TestMethod]
    public async Task MSet_OddArgumentCount_IsWrongNumberOfArguments()
    {
        var written = await new RedisTestServer().SendAsync(Token, Command("MSET", "a", "1", "b"));

        Assert.AreEqual("-ERR wrong number of arguments for 'mset' command\r\n", written);
    }

    [TestMethod]
    public async Task MSet_ClearsAnExpiry()
    {
        var written = await new RedisTestServer().SendAsync(Token, Command("SET", "a", "v", "EX", "5"), Command("MSET", "a", "w"), Command("TTL", "a"));

        Assert.AreEqual(Ok + Ok + ":-1\r\n", written);
    }

    [TestMethod]
    public async Task IncrFamily_OnAMissingKey_CountsFromZero()
    {
        var written = await new RedisTestServer().SendAsync(
            Token,
            Command("INCR", "a"),
            Command("DECR", "b"),
            Command("INCRBY", "c", "5"),
            Command("DECRBY", "d", "7"),
            Command("INCRBY", "a", "-3"),
            Command("GET", "a"));

        Assert.AreEqual(":1\r\n:-1\r\n:5\r\n:-7\r\n:-2\r\n$2\r\n-2\r\n", written);
    }

    [TestMethod]
    public async Task Incr_OnANonIntegerValue_IsNotAnInteger()
    {
        var written = await new RedisTestServer().SendAsync(
            Token,
            Command("SET", "k", "1.5"),
            Command("INCR", "k"),
            Command("INCRBY", "k", "x"),
            Command("DECRBY", "k", "01"));

        Assert.AreEqual(Ok + NotAnInteger + NotAnInteger + NotAnInteger, written);
    }

    [TestMethod]
    public async Task Incr_PastTheLargestInteger_IsRefusedAndTheValueUnchanged()
    {
        var written = await new RedisTestServer().SendAsync(
            Token,
            Command("SET", "k", "9223372036854775807"),
            Command("INCR", "k"),
            Command("SET", "m", "-9223372036854775808"),
            Command("DECR", "m"),
            Command("DECRBY", "z", "-9223372036854775808"),
            Command("GET", "k"));

        Assert.AreEqual(Ok + Overflow + Ok + Overflow + Overflow + "$19\r\n9223372036854775807\r\n", written);
    }

    [TestMethod]
    public async Task Incr_KeepsTheExpiry()
    {
        var written = await new RedisTestServer().SendAsync(Token, Command("SET", "k", "1", "EX", "50"), Command("INCR", "k"), Command("TTL", "k"));

        Assert.AreEqual(Ok + ":2\r\n:50\r\n", written);
    }

    [TestMethod]
    public async Task AppendThenStrLen_AnswerTheNewLengthAndZeroWhenMissing()
    {
        var written = await new RedisTestServer().SendAsync(
            Token,
            Command("STRLEN", "k"),
            Command("APPEND", "k", "Hello"),
            Command("APPEND", "k", " World"),
            Command("STRLEN", "k"),
            Command("GET", "k"));

        Assert.AreEqual(":0\r\n:5\r\n:11\r\n:11\r\n$11\r\nHello World\r\n", written);
    }

    [TestMethod]
    public async Task Set_WhenTheKeyspaceIsFull_IsOutOfMemoryButReadsAndDeletesRun()
    {
        var server = new RedisTestServer(new RedisKeyspace(2, 1024));

        var written = await server.SendAsync(
            Token,
            Command("SET", "a", "1"),
            Command("SET", "b", "2"),
            Command("SET", "c", "3"),
            Command("APPEND", "a", "x"),
            Command("GET", "a"),
            Command("DEL", "a"),
            Command("SET", "c", "3"));

        Assert.AreEqual(
            Ok + Ok + "-OOM command not allowed when used memory > 'maxmemory'.\r\n"
            + "-OOM command not allowed when used memory > 'maxmemory'.\r\n$1\r\n1\r\n:1\r\n" + Ok,
            written);
    }

    [TestMethod]
    public async Task Set_WhenOnlyExpiredKeysFillTheKeyspace_RemovesThemAndSets()
    {
        var server = new RedisTestServer(new RedisKeyspace(1, 1024));
        server.Keyspace[5].Put("old", new RedisStringValue([1]), server.NowMilliseconds).ExpiresAtMilliseconds = server.NowMilliseconds + 10;

        server.Clock.Advance(TimeSpan.FromMilliseconds(20));
        var written = await server.SendAsync(Token, Command("SET", "b", "1"));

        Assert.AreEqual(Ok, written);
        Assert.AreEqual(1, server.Keyspace.KeyCount);
    }

    [TestMethod]
    public async Task Set_PastTheByteBound_IsOutOfMemory()
    {
        var server = new RedisTestServer(new RedisKeyspace(100, 10));

        var written = await server.SendAsync(Token, Command("SET", "k", "123456789"), Command("SET", "j", "v"));

        Assert.AreEqual(Ok + "-OOM command not allowed when used memory > 'maxmemory'.\r\n", written);
        Assert.AreEqual(10, server.Keyspace.ByteCount);
    }
}
