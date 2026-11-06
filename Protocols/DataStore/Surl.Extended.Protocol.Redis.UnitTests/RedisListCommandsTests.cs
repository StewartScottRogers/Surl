using static Surl.Extended.Protocol.Redis.RedisTestExchanges;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The list commands through the server (BL-491; ADR-0106 decisions 1 and 2), every byte written
/// by hand from the RESP page and each command's reference page.
/// </summary>
[TestClass]
public sealed class RedisListCommandsTests
{
    private const string Ok = "+OK\r\n";
    private const string Resp2Null = "$-1\r\n";
    private const string Resp3Null = "_\r\n";
    private const string WrongType = "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n";
    private const string NotAnInteger = "-ERR value is not an integer or out of range\r\n";
    private const string NotPositive = "-ERR value is out of range, must be positive\r\n";

    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public async Task LPushRPush_AnswerTheNewLengthAndLRangeZeroToMinusOneIsTheWholeList()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("RPUSH", "mylist", "one", "two"),
            Command("LPUSH", "mylist", "zero", "minus"),
            Command("LRANGE", "mylist", "0", "-1"));

        Assert.AreEqual(":2\r\n:4\r\n*4\r\n$5\r\nminus\r\n$4\r\nzero\r\n$3\r\none\r\n$3\r\ntwo\r\n", written);
    }

    [TestMethod]
    [DataRow("0", "0", "*1\r\n$3\r\none\r\n")]
    [DataRow("-3", "2", "*3\r\n$3\r\none\r\n$3\r\ntwo\r\n$5\r\nthree\r\n")]
    [DataRow("-100", "100", "*3\r\n$3\r\none\r\n$3\r\ntwo\r\n$5\r\nthree\r\n")]
    [DataRow("1", "-1", "*2\r\n$3\r\ntwo\r\n$5\r\nthree\r\n")]
    [DataRow("5", "10", "*0\r\n")]
    [DataRow("2", "1", "*0\r\n")]
    [DataRow("0", "-100", "*0\r\n")]
    public async Task LRange_IndexesFromEitherEnd_AreClampedToTheList(string start, string stop, string expected)
    {
        var server = new RedisTestServer();
        await server.SendAsync(Token, Command("RPUSH", "mylist", "one", "two", "three"));

        var written = await server.SendAsync(Token, Command("LRANGE", "mylist", start, stop));

        Assert.AreEqual(expected, written);
    }

    [TestMethod]
    public async Task LRange_MissingKeyOrNonIntegerIndex_IsEmptyOrRefused()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("LRANGE", "nokey", "0", "-1"),
            Command("LRANGE", "nokey", "a", "-1"),
            Command("LRANGE", "nokey", "0", "b"));

        Assert.AreEqual("*0\r\n" + NotAnInteger + NotAnInteger, written);
    }

    [TestMethod]
    public async Task LPopRPop_WithoutACount_AnswerOneElementAndTheLastRemovesTheKey()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("RPUSH", "mylist", "one", "two"),
            Command("LPOP", "mylist"),
            Command("RPOP", "mylist"),
            Command("EXISTS", "mylist"),
            Command("LPOP", "mylist"));

        Assert.AreEqual(":2\r\n$3\r\none\r\n$3\r\ntwo\r\n:0\r\n" + Resp2Null, written);
    }

    [TestMethod]
    public async Task LPopRPop_WithACount_AnswerAnArrayAndAnEmptiedListLosesItsKey()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("RPUSH", "mylist", "one", "two", "three", "four"),
            Command("RPOP", "mylist", "2"),
            Command("LPOP", "mylist", "0"),
            Command("LPOP", "mylist", "5"),
            Command("EXISTS", "mylist"));

        Assert.AreEqual(":4\r\n*2\r\n$4\r\nfour\r\n$5\r\nthree\r\n*0\r\n*2\r\n$3\r\none\r\n$3\r\ntwo\r\n:0\r\n", written);
    }

    [TestMethod]
    public async Task LPop_MissingKey_IsNullWithoutACountAndTheNullArrayWithOne()
    {
        var server = new RedisTestServer();

        var resp2 = await server.SendAsync(Token, Command("LPOP", "nokey"), Command("LPOP", "nokey", "2"));
        var resp3 = await server.SendAsync(Token, Command("HELLO", "3"), Command("LPOP", "nokey"), Command("RPOP", "nokey", "2"));

        Assert.AreEqual(Resp2Null + "*-1\r\n", resp2);
        Assert.EndsWith(Resp3Null + Resp3Null, resp3);
    }

    [TestMethod]
    public async Task LPop_BadCountOrTooManyArguments_IsRefused()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("RPUSH", "l", "a"),
            Command("LPOP", "l", "-1"),
            Command("LPOP", "l", "x"),
            Command("RPOP", "l", "1", "2"),
            Command("LLEN", "l"));

        Assert.AreEqual(":1\r\n" + NotPositive + NotAnInteger + "-ERR wrong number of arguments for 'rpop' command\r\n:1\r\n", written);
    }

    [TestMethod]
    public async Task LLenAndLIndex_AnswerTheLengthAndTheElementOrNull()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("LPUSH", "mylist", "World", "Hello"),
            Command("LLEN", "mylist"),
            Command("LLEN", "nokey"),
            Command("LINDEX", "mylist", "0"),
            Command("LINDEX", "mylist", "-1"),
            Command("LINDEX", "mylist", "3"),
            Command("LINDEX", "mylist", "-3"),
            Command("LINDEX", "nokey", "0"),
            Command("LINDEX", "mylist", "x"));

        Assert.AreEqual(
            ":2\r\n:2\r\n:0\r\n$5\r\nHello\r\n$5\r\nWorld\r\n" + Resp2Null + Resp2Null + Resp2Null + NotAnInteger,
            written);
    }

    [TestMethod]
    public async Task LSet_ReplacesAnElementOrRefusesAMissingKeyOrIndex()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("LSET", "nokey", "0", "x"),
            Command("RPUSH", "mylist", "one", "two", "three"),
            Command("LSET", "mylist", "0", "four"),
            Command("LSET", "mylist", "-2", "five"),
            Command("LSET", "mylist", "3", "six"),
            Command("LSET", "mylist", "x", "six"),
            Command("LRANGE", "mylist", "0", "-1"));

        Assert.AreEqual(
            "-ERR no such key\r\n:3\r\n" + Ok + Ok + "-ERR index out of range\r\n" + NotAnInteger
            + "*3\r\n$4\r\nfour\r\n$4\r\nfive\r\n$5\r\nthree\r\n",
            written);
    }

    [TestMethod]
    public async Task LRem_NegativeCount_RemovesFromTheTail()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("RPUSH", "mylist", "hello", "hello", "foo", "hello"),
            Command("LREM", "mylist", "-2", "hello"),
            Command("LRANGE", "mylist", "0", "-1"));

        Assert.AreEqual(":4\r\n:2\r\n*2\r\n$5\r\nhello\r\n$3\r\nfoo\r\n", written);
    }

    [TestMethod]
    public async Task LRem_PositiveZeroAndUnmetCounts_RemoveFromTheHeadAllOrWhatThereIs()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("RPUSH", "l", "a", "x", "a", "x", "a"),
            Command("LREM", "l", "1", "a"),
            Command("LRANGE", "l", "0", "-1"),
            Command("LREM", "l", "-5", "x"),
            Command("LREM", "l", "5", "zzz"),
            Command("LREM", "l", "0", "a"),
            Command("EXISTS", "l"),
            Command("LREM", "nokey", "0", "a"),
            Command("LREM", "nokey", "y", "a"));

        Assert.AreEqual(
            ":5\r\n:1\r\n*4\r\n$1\r\nx\r\n$1\r\na\r\n$1\r\nx\r\n$1\r\na\r\n:2\r\n:0\r\n:2\r\n:0\r\n:0\r\n" + NotAnInteger,
            written);
    }

    [TestMethod]
    public async Task LTrim_KeepsTheRangeAndAnEmptyRangeRemovesTheKey()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("RPUSH", "mylist", "one", "two", "three"),
            Command("LTRIM", "mylist", "1", "-1"),
            Command("LRANGE", "mylist", "0", "-1"),
            Command("LTRIM", "mylist", "5", "10"),
            Command("EXISTS", "mylist"),
            Command("LTRIM", "nokey", "0", "1"),
            Command("LTRIM", "nokey", "a", "1"),
            Command("LTRIM", "nokey", "0", "b"));

        Assert.AreEqual(":3\r\n" + Ok + "*2\r\n$3\r\ntwo\r\n$5\r\nthree\r\n" + Ok + ":0\r\n" + Ok + NotAnInteger + NotAnInteger, written);
    }

    [TestMethod]
    public async Task LPush_OnAString_IsWrongTypeAndLeavesTheString()
    {
        var server = new RedisTestServer();

        var written = await server.SendAsync(
            Token,
            Command("SET", "s", "v"),
            Command("LPUSH", "s", "a"),
            Command("LRANGE", "s", "0", "-1"),
            Command("LPOP", "s"),
            Command("GET", "s"));

        Assert.AreEqual(Ok + WrongType + WrongType + WrongType + "$1\r\nv\r\n", written);
    }
}
