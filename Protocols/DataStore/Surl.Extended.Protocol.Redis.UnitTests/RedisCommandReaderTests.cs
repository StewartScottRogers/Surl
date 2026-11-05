using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// <see cref="RedisCommandReader"/> on the RESP page's command framing, inline commands and
/// pipelining, and ADR-0106 decision 6's protocol errors, from bytes written by hand.
/// </summary>
[TestClass]
public sealed class RedisCommandReaderTests
{
    private static readonly ExchangeLimits SmallLimits = new(
        TimeSpan.FromSeconds(30),
        maxRequestHeadBytes: 102400,
        maxLineBytes: 16,
        maxMessageBytes: 1048576,
        maxUploadBytes: 8);

    [TestMethod]
    public async Task ReadCommandAsync_ArrayOfBulkStrings_ReadsEachArgument()
    {
        var reader = Reader("*2\r\n$4\r\nLLEN\r\n$6\r\nmylist\r\n");

        var outcome = await reader.ReadCommandAsync(CancellationToken.None);

        AssertCommand(outcome, "LLEN", "mylist");
    }

    [TestMethod]
    public async Task ReadCommandAsync_CommandSplitAcrossReads_ReadsItWhole()
    {
        var bytes = Encoding.Latin1.GetBytes("*3\r\n$3\r\nSET\r\n$3\r\nkey\r\n$5\r\nvalue\r\n");
        var reader = new RedisCommandReader(
            new InMemoryConnection(bytes.Select(single => new ReadOnlyMemory<byte>([single]))),
            ExchangeLimits.Default);

        var outcome = await reader.ReadCommandAsync(CancellationToken.None);

        AssertCommand(outcome, "SET", "key", "value");
    }

    [TestMethod]
    public async Task ReadCommandAsync_ThreePipelinedCommands_ReadsThemOneByOneInOrder()
    {
        var reader = Reader("*1\r\n$4\r\nPING\r\n*2\r\n$4\r\nECHO\r\n$2\r\nhi\r\n*2\r\n$3\r\nGET\r\n$1\r\nk\r\n");

        var first = await reader.ReadCommandAsync(CancellationToken.None);
        var second = await reader.ReadCommandAsync(CancellationToken.None);
        var third = await reader.ReadCommandAsync(CancellationToken.None);
        var after = await reader.ReadCommandAsync(CancellationToken.None);

        AssertCommand(first, "PING");
        AssertCommand(second, "ECHO", "hi");
        AssertCommand(third, "GET", "k");
        Assert.AreEqual(RedisCommandReadOutcomeKind.ConnectionClosed, after.Kind);
    }

    [TestMethod]
    public async Task ReadCommandAsync_BulkStringHoldingCrLfAndNul_ReadsItByLength()
    {
        var reader = Reader("*2\r\n$4\r\nECHO\r\n$4\r\na\r\n\0\r\n");

        var outcome = await reader.ReadCommandAsync(CancellationToken.None);

        AssertCommand(outcome, "ECHO", "a\r\n\0");
    }

    [TestMethod]
    public async Task ReadCommandAsync_EmptyBulkString_ReadsAnEmptyArgument()
    {
        var reader = Reader("*2\r\n$4\r\nECHO\r\n$0\r\n\r\n");

        var outcome = await reader.ReadCommandAsync(CancellationToken.None);

        AssertCommand(outcome, "ECHO", "");
    }

    [TestMethod]
    public async Task ReadCommandAsync_InlineCommand_SplitsItOnSpaces()
    {
        var reader = Reader("EXISTS somekey\r\n");

        var outcome = await reader.ReadCommandAsync(CancellationToken.None);

        AssertCommand(outcome, "EXISTS", "somekey");
    }

    [TestMethod]
    public async Task ReadCommandAsync_InlineCommandEndedByLfAlone_ReadsIt()
    {
        var reader = Reader("PING\n");

        var outcome = await reader.ReadCommandAsync(CancellationToken.None);

        AssertCommand(outcome, "PING");
    }

    [TestMethod]
    public async Task ReadCommandAsync_EmptyLinesAndEmptyArrays_AreSkipped()
    {
        var reader = Reader("\r\n\n   \r\n*0\r\n*-1\r\n*-5\r\nPING\r\n");

        var outcome = await reader.ReadCommandAsync(CancellationToken.None);

        AssertCommand(outcome, "PING");
    }

    [TestMethod]
    [DataRow("*2\r\n$9\r\n123456789\r\n")]
    [DataRow("*1\r\n$x\r\n")]
    [DataRow("*1\r\n$\r\n")]
    [DataRow("*1\r\n$-1\r\n")]
    [DataRow("*1\r\n$+1\r\na\r\n")]
    [DataRow("*1\r\n$01\r\na\r\n")]
    [DataRow("*1\r\n$1\na\r\n")]
    [DataRow("*1\r\n$1\r\nab\r\n")]
    [DataRow("*1\r\n$1\r\na\rx")]
    [DataRow("*1\r\n$1234567890123456\r\n")]
    public async Task ReadCommandAsync_BadBulkLengthOrMissingCrLf_IsInvalidBulkLength(string bytes)
    {
        var reader = Reader(bytes, SmallLimits);

        var outcome = await reader.ReadCommandAsync(CancellationToken.None);

        AssertProtocolError(outcome, "-ERR Protocol error: invalid bulk length\r\n");
    }

    [TestMethod]
    [DataRow("*x\r\n")]
    [DataRow("*\r\n")]
    [DataRow("*-\r\n")]
    [DataRow("*-0\r\n")]
    [DataRow("*1048577\r\n")]
    [DataRow("*99999999999999999999\r\n")]
    [DataRow("*1\n")]
    [DataRow("*123456789012345678\r\n")]
    public async Task ReadCommandAsync_BadMultibulkCount_IsInvalidMultibulkLength(string bytes)
    {
        var reader = Reader(bytes, SmallLimits with { MaxLineBytes = 22 });

        var outcome = await reader.ReadCommandAsync(CancellationToken.None);

        AssertProtocolError(outcome, "-ERR Protocol error: invalid multibulk length\r\n");
    }

    [TestMethod]
    public async Task ReadCommandAsync_MultibulkLineLongerThanMaxLine_IsInvalidMultibulkLength()
    {
        var reader = Reader("*0000000000000001\r\n", SmallLimits);

        var outcome = await reader.ReadCommandAsync(CancellationToken.None);

        AssertProtocolError(outcome, "-ERR Protocol error: invalid multibulk length\r\n");
    }

    [TestMethod]
    [DataRow("*1\r\n:1\r\n", "-ERR Protocol error: expected '$', got ':'\r\n")]
    [DataRow("*1\r\n\r\n", "-ERR Protocol error: expected '$', got ' '\r\n")]
    [DataRow("*1\r\n\n", "-ERR Protocol error: expected '$', got ' '\r\n")]
    public async Task ReadCommandAsync_ElementNotABulkString_IsExpectedDollar(string bytes, string expected)
    {
        var reader = Reader(bytes);

        var outcome = await reader.ReadCommandAsync(CancellationToken.None);

        AssertProtocolError(outcome, expected);
    }

    [TestMethod]
    [DataRow("SET key 12345678\r\n")]
    [DataRow("SET key 1234567890")]
    public async Task ReadCommandAsync_InlineLineLongerThanMaxLine_IsTooBigInlineRequest(string bytes)
    {
        var reader = Reader(bytes, SmallLimits);

        var outcome = await reader.ReadCommandAsync(CancellationToken.None);

        AssertProtocolError(outcome, "-ERR Protocol error: too big inline request\r\n");
    }

    [TestMethod]
    public async Task ReadCommandAsync_InlineLineOfExactlyMaxLine_IsRead()
    {
        var reader = Reader("ET key 1234567\r\n", SmallLimits);

        var outcome = await reader.ReadCommandAsync(CancellationToken.None);

        AssertCommand(outcome, "ET", "key", "1234567");
    }

    [TestMethod]
    public async Task ReadCommandAsync_InlineUnbalancedQuotes_IsUnbalancedQuotesInRequest()
    {
        var reader = Reader("SET \"key value\r\n");

        var outcome = await reader.ReadCommandAsync(CancellationToken.None);

        AssertProtocolError(outcome, "-ERR Protocol error: unbalanced quotes in request\r\n");
    }

    [TestMethod]
    public async Task ReadCommandAsync_CommandPastMaxFilesizeAndFraming_IsCommandTooLarge()
    {
        var bytes = new StringBuilder("*10000\r\n").Insert(8, "$4\r\nabcd\r\n", 10000).ToString();
        var reader = Reader(bytes, SmallLimits);

        var outcome = await reader.ReadCommandAsync(CancellationToken.None);

        Assert.AreEqual(RedisCommandReadOutcomeKind.CommandTooLarge, outcome.Kind);
        Assert.IsNull(outcome.ErrorReply);
        Assert.IsEmpty(outcome.Arguments);
    }

    [TestMethod]
    public async Task ReadCommandAsync_CommandWithinTheBound_IsReadAfterAnEarlierLargeOne()
    {
        var command = new StringBuilder("*6000\r\n").Insert(7, "$4\r\nabcd\r\n", 6000).ToString();
        var reader = Reader(command + command, SmallLimits);

        var first = await reader.ReadCommandAsync(CancellationToken.None);
        var second = await reader.ReadCommandAsync(CancellationToken.None);

        Assert.HasCount(6000, first.Arguments);
        Assert.HasCount(6000, second.Arguments);
    }

    [TestMethod]
    public async Task ReadCommandAsync_BulkStringLargerThanTheFirstBuffer_IsRead()
    {
        var value = new string('v', 40000);
        var reader = Reader($"*2\r\n$4\r\nECHO\r\n$40000\r\n{value}\r\n");

        var outcome = await reader.ReadCommandAsync(CancellationToken.None);

        AssertCommand(outcome, "ECHO", value);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("*2\r\n$4\r\nECHO\r\n$5\r\nhel")]
    [DataRow("*2\r\n$4\r\nECHO\r\n")]
    [DataRow("*2\r\n$4")]
    [DataRow("PING")]
    public async Task ReadCommandAsync_ClientClosesBetweenOrInsideACommand_IsConnectionClosed(string bytes)
    {
        var reader = Reader(bytes);

        var outcome = await reader.ReadCommandAsync(CancellationToken.None);

        Assert.AreSame(RedisCommandReadOutcome.ConnectionClosed, outcome);
    }

    [TestMethod]
    public async Task ReadCommandAsync_UnauthenticatedElevenElements_IsTheUnauthenticatedMultibulkError()
    {
        var reader = Reader("*11\r\n");
        reader.AppliesUnauthenticatedLimits = true;

        var outcome = await reader.ReadCommandAsync(CancellationToken.None);

        AssertProtocolError(outcome, "-ERR Protocol error: unauthenticated multibulk length\r\n");
    }

    [TestMethod]
    public async Task ReadCommandAsync_UnauthenticatedTenElementsOf16KiB_IsRead()
    {
        var bulk = $"$16384\r\n{new string('v', 16384)}\r\n";
        var reader = Reader("*10\r\n" + string.Concat(Enumerable.Repeat(bulk, 10)));
        reader.AppliesUnauthenticatedLimits = true;

        var outcome = await reader.ReadCommandAsync(CancellationToken.None);

        Assert.AreEqual(RedisCommandReadOutcomeKind.Command, outcome.Kind);
        Assert.HasCount(10, outcome.Arguments);
    }

    [TestMethod]
    public async Task ReadCommandAsync_UnauthenticatedBulkStringOver16KiB_IsTheUnauthenticatedBulkError()
    {
        var reader = Reader("*1\r\n$16385\r\n");
        reader.AppliesUnauthenticatedLimits = true;

        var outcome = await reader.ReadCommandAsync(CancellationToken.None);

        AssertProtocolError(outcome, "-ERR Protocol error: unauthenticated bulk length\r\n");
    }

    [TestMethod]
    public async Task WaitForCommandStartAsync_ByteArrived_IsTrueAndTheCommandIsStillRead()
    {
        var reader = Reader("PING\r\n");

        var started = await reader.WaitForCommandStartAsync(CancellationToken.None);
        var outcome = await reader.ReadCommandAsync(CancellationToken.None);

        Assert.IsTrue(started);
        AssertCommand(outcome, "PING");
    }

    [TestMethod]
    public async Task WaitForCommandStartAsync_ClientClosed_IsFalse()
    {
        var reader = Reader(string.Empty);

        var started = await reader.WaitForCommandStartAsync(CancellationToken.None);

        Assert.IsFalse(started);
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        var connection = new InMemoryConnection([]);

        Assert.ThrowsExactly<ArgumentNullException>(() => new RedisCommandReader(null!, ExchangeLimits.Default));
        Assert.ThrowsExactly<ArgumentNullException>(() => new RedisCommandReader(connection, null!));
    }

    private static RedisCommandReader Reader(string bytes, ExchangeLimits? limits = null) =>
        new(new InMemoryConnection([Encoding.Latin1.GetBytes(bytes)]), limits ?? ExchangeLimits.Default);

    private static void AssertCommand(RedisCommandReadOutcome outcome, params string[] expected)
    {
        Assert.AreEqual(RedisCommandReadOutcomeKind.Command, outcome.Kind);
        Assert.IsNull(outcome.ErrorReply);
        CollectionAssert.AreEqual(expected, outcome.Arguments.Select(argument => Encoding.Latin1.GetString(argument)).ToArray());
    }

    private static void AssertProtocolError(RedisCommandReadOutcome outcome, string expected)
    {
        Assert.AreEqual(RedisCommandReadOutcomeKind.ProtocolError, outcome.Kind);
        Assert.IsEmpty(outcome.Arguments);
        Assert.AreEqual(expected, Encoding.Latin1.GetString(outcome.ErrorReply!.Encode()));
    }
}
