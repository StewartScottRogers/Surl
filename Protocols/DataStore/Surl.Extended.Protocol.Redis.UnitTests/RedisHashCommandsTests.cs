using static Surl.Extended.Protocol.Redis.RedisTestExchanges;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The hash commands through the server (BL-491; ADR-0106 decisions 1 and 2), every byte written
/// by hand from the RESP page and each command's reference page.
/// </summary>
[TestClass]
public sealed class RedisHashCommandsTests
{
    private const string Resp2Null = "$-1\r\n";
    private const string Resp3Null = "_\r\n";
    private const string WrongType = "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
    private const string NotAnInteger = "-ERR value is not an integer or out of range\r\n";

    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public async Task HSet_NewAndChangedFields_AnswersTheNumberAddedAndHGetTheValues()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("HSET", "myhash", "field1", "Hello"),
            Command("HSET", "myhash", "field1", "Hi", "field2", "World"),
            Command("HGET", "myhash", "field1"),
            Command("HGET", "myhash", "nosuch"),
            Command("HGET", "nokey", "field1"));

        Assert.AreEqual(":1\r\n:1\r\n$2\r\nHi\r\n" + Resp2Null + Resp2Null, written);
    }

    [TestMethod]
    public async Task HSet_FieldWithoutAValue_IsTheWrongNumberOfArguments()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(Token, Command("HSET", "h", "f1", "v1", "f2"), Command("HSET", "h", "f1"), Command("EXISTS", "h"));

        Assert.AreEqual("-ERR wrong number of arguments for 'hset' command\r\n-ERR wrong number of arguments for 'hset' command\r\n:0\r\n", written);
    }

    [TestMethod]
    public async Task HGetAll_IsAFlatArrayInRespTwoAndAMapInRespThree()
    {
        var server = new RedisTestServer();
        await server.SendAsync(Token, Command("HSET", "myhash", "field1", "Hello", "field2", "World"));

        var resp2 = await server.SendAsync(Token, Command("HGETALL", "myhash"), Command("HGETALL", "nokey"));
        var resp3 = await server.SendAsync(Token, Command("HELLO", "3"), Command("HGETALL", "myhash"), Command("HGETALL", "nokey"));

        Assert.AreEqual("*4\r\n$6\r\nfield1\r\n$5\r\nHello\r\n$6\r\nfield2\r\n$5\r\nWorld\r\n*0\r\n", resp2);
        Assert.EndsWith("%2\r\n$6\r\nfield1\r\n$5\r\nHello\r\n$6\r\nfield2\r\n$5\r\nWorld\r\n%0\r\n", resp3);
    }

    [TestMethod]
    public async Task HMGet_MissingFieldsAndKey_AreNullsInTheConnectionsVersion()
    {
        var server = new RedisTestServer();
        await server.SendAsync(Token, Command("HSET", "myhash", "field1", "Hello"));

        var resp2 = await server.SendAsync(Token, Command("HMGET", "myhash", "field1", "nofield"), Command("HMGET", "nokey", "field1"));
        var resp3 = await server.SendAsync(Token, Command("HELLO", "3"), Command("HMGET", "myhash", "nofield", "field1"));

        Assert.AreEqual("*2\r\n$5\r\nHello\r\n" + Resp2Null + "*1\r\n" + Resp2Null, resp2);
        Assert.EndsWith("*2\r\n" + Resp3Null + "$5\r\nHello\r\n", resp3);
    }

    [TestMethod]
    public async Task HDel_AnswersTheFieldsRemovedAndTheLastRemovesTheKey()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("HSET", "myhash", "field1", "foo", "field2", "bar"),
            Command("HDEL", "myhash", "field1", "nofield"),
            Command("HLEN", "myhash"),
            Command("HDEL", "myhash", "field2"),
            Command("EXISTS", "myhash"),
            Command("HDEL", "nokey", "field1"));

        Assert.AreEqual(":2\r\n:1\r\n:1\r\n:1\r\n:0\r\n:0\r\n", written);
    }

    [TestMethod]
    public async Task HExistsHLenHKeysHVals_AnswerTheFieldsInTheOrderAdded()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("HSET", "myhash", "field1", "Hello", "field2", "World"),
            Command("HEXISTS", "myhash", "field1"),
            Command("HEXISTS", "myhash", "nofield"),
            Command("HEXISTS", "nokey", "field1"),
            Command("HLEN", "myhash"),
            Command("HLEN", "nokey"),
            Command("HKEYS", "myhash"),
            Command("HVALS", "myhash"),
            Command("HKEYS", "nokey"),
            Command("HVALS", "nokey"));

        Assert.AreEqual(
            ":2\r\n:1\r\n:0\r\n:0\r\n:2\r\n:0\r\n*2\r\n$6\r\nfield1\r\n$6\r\nfield2\r\n*2\r\n$5\r\nHello\r\n$5\r\nWorld\r\n*0\r\n*0\r\n",
            written);
    }

    [TestMethod]
    public async Task HIncrBy_AbsentFieldCountsFromZero()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("HSET", "myhash", "field", "5"),
            Command("HINCRBY", "myhash", "field", "1"),
            Command("HINCRBY", "myhash", "field", "-1"),
            Command("HINCRBY", "myhash", "field", "-10"),
            Command("HINCRBY", "other", "new", "7"),
            Command("HGET", "myhash", "field"));

        Assert.AreEqual(":1\r\n:6\r\n:5\r\n:-5\r\n:7\r\n$2\r\n-5\r\n", written);
    }

    [TestMethod]
    public async Task HIncrBy_NonIntegerIncrementValueOrOverflow_IsRefusedAndChangesNothing()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("HSET", "h", "text", "abc", "big", "9223372036854775807", "small", "-9223372036854775808"),
            Command("HINCRBY", "h", "text", "x"),
            Command("HINCRBY", "h", "text", "1"),
            Command("HINCRBY", "h", "big", "1"),
            Command("HINCRBY", "h", "small", "-1"),
            Command("HINCRBY", "absent", "f", "x"),
            Command("EXISTS", "absent"),
            Command("HGET", "h", "big"));

        Assert.AreEqual(
            ":3\r\n" + NotAnInteger + "-ERR hash value is not an integer\r\n"
            + "-ERR increment or decrement would overflow\r\n-ERR increment or decrement would overflow\r\n"
            + NotAnInteger + ":0\r\n$19\r\n9223372036854775807\r\n",
            written);
    }

    [TestMethod]
    public async Task HSetNx_SetsOnlyAnAbsentField()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("HSETNX", "myhash", "field", "Hello"),
            Command("HSETNX", "myhash", "field", "World"),
            Command("HGET", "myhash", "field"));

        Assert.AreEqual(":1\r\n:0\r\n$5\r\nHello\r\n", written);
    }

    [TestMethod]
    public async Task HashCommands_OnAString_AreWrongType()
    {
        var server = new RedisTestServer();
        await server.SendAsync(Token, Command("SET", "s", "v"));

        var written = await server.SendAsync(
            Token,
            Command("HSET", "s", "f", "v"),
            Command("HGET", "s", "f"),
            Command("HDEL", "s", "f"),
            Command("HGETALL", "s"),
            Command("HINCRBY", "s", "f", "1"),
            Command("GET", "s"));

        Assert.AreEqual(string.Concat(Enumerable.Repeat(WrongType, 5)) + "$1\r\nv\r\n", written);
    }

    [TestMethod]
    public async Task Type_HashListAndSet_AreNamedByType()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("HSET", "h", "f", "v"),
            Command("RPUSH", "l", "a"),
            Command("SADD", "s", "m"),
            Command("TYPE", "h"),
            Command("TYPE", "l"),
            Command("TYPE", "s"));

        Assert.AreEqual(":1\r\n:1\r\n:1\r\n+hash\r\n+list\r\n+set\r\n", written);
    }
}
