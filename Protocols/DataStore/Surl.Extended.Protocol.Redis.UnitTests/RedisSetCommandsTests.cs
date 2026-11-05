using static Surl.Extended.Protocol.Redis.RedisTestExchanges;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The set commands through the server (BL-491; ADR-0106 decisions 1 and 2), every byte written
/// by hand from the RESP page and each command's reference page.
/// </summary>
[TestClass]
public sealed class RedisSetCommandsTests
{
    private const string Resp2Null = "$-1\r\n";
    private const string Resp3Null = "_\r\n";
    private const string WrongType = "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";

    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public async Task SAddThenSMembers_IsAnArrayInRespTwoAndASetInRespThree()
    {
        var server = new RedisTestServer();

        var resp2 = await server.SendAsync(
            Token,
            Command("SADD", "myset", "Hello"),
            Command("SADD", "myset", "World"),
            Command("SADD", "myset", "World"),
            Command("SMEMBERS", "myset"),
            Command("SMEMBERS", "nokey"));
        var resp3 = await server.SendAsync(Token, Command("HELLO", "3"), Command("SMEMBERS", "myset"), Command("SMEMBERS", "nokey"));

        Assert.AreEqual(":1\r\n:1\r\n:0\r\n*2\r\n$5\r\nHello\r\n$5\r\nWorld\r\n*0\r\n", resp2);
        Assert.EndsWith("~2\r\n$5\r\nHello\r\n$5\r\nWorld\r\n~0\r\n", resp3);
    }

    [TestMethod]
    public async Task SRem_AnswersTheMembersRemovedAndTheLastRemovesTheKey()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("SADD", "myset", "one", "two"),
            Command("SREM", "myset", "one", "four"),
            Command("SREM", "myset", "two"),
            Command("EXISTS", "myset"),
            Command("SREM", "nokey", "one"));

        Assert.AreEqual(":2\r\n:1\r\n:1\r\n:0\r\n:0\r\n", written);
    }

    [TestMethod]
    public async Task SIsMemberSMIsMemberSCard_AnswerIntegers()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("SADD", "myset", "one"),
            Command("SISMEMBER", "myset", "one"),
            Command("SISMEMBER", "myset", "two"),
            Command("SISMEMBER", "nokey", "one"),
            Command("SMISMEMBER", "myset", "one", "notamember"),
            Command("SCARD", "myset"),
            Command("SCARD", "nokey"));

        Assert.AreEqual(":1\r\n:1\r\n:0\r\n:0\r\n*2\r\n:1\r\n:0\r\n:1\r\n:0\r\n", written);
    }

    [TestMethod]
    public async Task SPop_WithoutACount_AnswersAMemberOrNull()
    {
        var server = new RedisTestServer();

        var resp2 = await server.SendAsync(
            Token,
            Command("SADD", "myset", "one"),
            Command("SPOP", "myset"),
            Command("EXISTS", "myset"),
            Command("SPOP", "myset"));
        var resp3 = await server.SendAsync(Token, Command("HELLO", "3"), Command("SPOP", "nokey"));

        Assert.AreEqual(":1\r\n$3\r\none\r\n:0\r\n" + Resp2Null, resp2);
        Assert.EndsWith(Resp3Null, resp3);
    }

    [TestMethod]
    public async Task SPop_WithACount_AnswersAnArrayOrSetAndTheNullArrayOnAMissingKey()
    {
        var server = new RedisTestServer();
        await server.SendAsync(Token, Command("SADD", "myset", "one", "two", "three"));

        var resp2 = await server.SendAsync(Token, Command("SPOP", "myset", "2"), Command("SPOP", "nokey", "2"));
        var resp3 = await server.SendAsync(Token, Command("HELLO", "3"), Command("SPOP", "myset", "5"), Command("SPOP", "nokey", "1"), Command("EXISTS", "myset"));

        Assert.AreEqual("*2\r\n$3\r\none\r\n$3\r\ntwo\r\n*-1\r\n", resp2);
        Assert.EndsWith("~1\r\n$5\r\nthree\r\n" + Resp3Null + ":0\r\n", resp3);
    }

    [TestMethod]
    public async Task SPop_BadCountOrTooManyArguments_IsRefused()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("SPOP", "myset", "-1"),
            Command("SPOP", "myset", "x"),
            Command("SPOP", "myset", "1", "2"));

        Assert.AreEqual(
            "-ERR value is out of range, must be positive\r\n-ERR value is not an integer or out of range\r\n-ERR syntax error\r\n",
            written);
    }

    [TestMethod]
    public async Task SInterSUnionSDiff_AnswerThePagesExamplesAsArraysOrSets()
    {
        var server = new RedisTestServer();
        await server.SendAsync(Token, Command("SADD", "key1", "a", "b", "c"), Command("SADD", "key2", "c", "d", "e"));

        var resp2 = await server.SendAsync(
            Token,
            Command("SINTER", "key1", "key2"),
            Command("SUNION", "key1", "key2"),
            Command("SDIFF", "key1", "key2"));
        var resp3 = await server.SendAsync(Token, Command("HELLO", "3"), Command("SINTER", "key1", "key2"));

        Assert.AreEqual(
            "*1\r\n$1\r\nc\r\n*5\r\n$1\r\na\r\n$1\r\nb\r\n$1\r\nc\r\n$1\r\nd\r\n$1\r\ne\r\n*2\r\n$1\r\na\r\n$1\r\nb\r\n",
            resp2);
        Assert.EndsWith("~1\r\n$1\r\nc\r\n", resp3);
    }

    [TestMethod]
    public async Task SInterSUnionSDiff_MissingKeysAreEmptySets()
    {
        var server = new RedisTestServer();
        await server.SendAsync(Token, Command("SADD", "key1", "a", "b"));

        var written = await server.SendAsync(
            Token,
            Command("SINTER", "key1", "nokey"),
            Command("SUNION", "key1", "nokey"),
            Command("SDIFF", "nokey", "key1"),
            Command("SDIFF", "key1", "nokey"));

        Assert.AreEqual("*0\r\n*2\r\n$1\r\na\r\n$1\r\nb\r\n*0\r\n*2\r\n$1\r\na\r\n$1\r\nb\r\n", written);
    }

    [TestMethod]
    public async Task SetCommands_OnAString_AreWrongType()
    {
        var server = new RedisTestServer();
        await server.SendAsync(Token, Command("SET", "s", "v"), Command("SADD", "key1", "a"));

        var written = await server.SendAsync(
            Token,
            Command("SADD", "s", "m"),
            Command("SMEMBERS", "s"),
            Command("SPOP", "s"),
            Command("SINTER", "key1", "s"));

        Assert.AreEqual(string.Concat(Enumerable.Repeat(WrongType, 4)), written);
    }
}
