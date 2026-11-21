using System.Text;

namespace Surl.McpServing;

[TestClass]
public sealed class McpInputLineReaderTests
{
    [TestMethod]
    public async Task ReadAsync_LinesSpanningSeveralReads_ReturnsEachWholeLine()
    {
        McpInputLineReader reader = Reader("abcdefgh\nijklmnop\n", maximumMessageBytes: 16, bufferBytes: 3);

        string?[] lines = await ReadAllAsync(reader);

        CollectionAssert.AreEqual(new[] { "abcdefgh", "ijklmnop" }, lines);
    }

    [TestMethod]
    public async Task ReadAsync_LineExactlyAtTheLimitEndingInCarriageReturn_ReturnsItWithoutTheCarriageReturn()
    {
        McpInputLineReader reader = Reader("abcd\r\nef\n", maximumMessageBytes: 4, bufferBytes: 2);

        string?[] lines = await ReadAllAsync(reader);

        CollectionAssert.AreEqual(new[] { "abcd", "ef" }, lines);
    }

    [TestMethod]
    public async Task ReadAsync_LineOneByteOverTheLimit_ReturnsAnEmptyMessage()
    {
        McpInputLineReader reader = Reader("abcde\nef\n", maximumMessageBytes: 4, bufferBytes: 2);

        string?[] lines = await ReadAllAsync(reader);

        CollectionAssert.AreEqual(new[] { string.Empty, "ef" }, lines);
    }

    [TestMethod]
    public async Task ReadAsync_LineFarOverTheLimitAcrossReads_DiscardsItAndReturnsAnEmptyMessage()
    {
        McpInputLineReader reader = Reader(new string('x', 50) + "\nef\n", maximumMessageBytes: 4, bufferBytes: 3);

        string?[] lines = await ReadAllAsync(reader);

        CollectionAssert.AreEqual(new[] { string.Empty, "ef" }, lines);
    }

    [TestMethod]
    public async Task ReadAsync_UnterminatedLastLineOverTheLimit_ReturnsAnEmptyMessageThenTheEnd()
    {
        McpInputLineReader reader = Reader("ab\n" + new string('x', 50), maximumMessageBytes: 4, bufferBytes: 3);

        string?[] lines = await ReadAllAsync(reader);

        CollectionAssert.AreEqual(new[] { "ab", string.Empty }, lines);
    }

    [TestMethod]
    [DataRow("""{"id":7,"params":{"p":"0123456789012345678901234567890123456789"}}""", 3, "7")]
    [DataRow("""{"params":{"p":"\"id\":1 0123456789012345678901234567890123456789"},"id":"abc"}""", 3, "\"abc\"")]
    [DataRow("""{"params":{"p":"0123456789012345678901234567890123456789"},"id":"a\"bc"}""", 1, "\"a\\\"bc\"")]
    [DataRow("""{"id":5}""", 2, "5")]
    [DataRow("""{"params":{"id":5,"p":"0123456789012345678901234567890123456789"}}""", 5, null)]
    public async Task ReadAsync_LineOverTheLimit_ReportsTheIdItCouldRead(string line, int bufferBytes, string? expected)
    {
        McpInputLineReader reader = Reader(line + "\nef\n", maximumMessageBytes: line.Length == 8 ? 7 : 30, bufferBytes: bufferBytes);

        byte[]? discarded = await reader.ReadAsync(CancellationToken.None);

        Assert.IsNotNull(discarded);
        Assert.IsEmpty(discarded);
        Assert.AreEqual(expected, reader.DiscardedRequestId?.Value.GetRawText());
    }

    [TestMethod]
    public async Task ReadAsync_UnterminatedLastLineOverTheLimit_ReportsItsId()
    {
        McpInputLineReader reader = Reader("""{"id":3,"p":"0123456789"}""", maximumMessageBytes: 10, bufferBytes: 4);

        await reader.ReadAsync(CancellationToken.None);

        Assert.AreEqual("3", reader.DiscardedRequestId?.Value.GetRawText());
    }

    [TestMethod]
    public async Task ReadAsync_IdLongerThanTheScannerKeeps_ReportsNoIdAndStillDiscardsTheLine()
    {
        string longId = new('a', McpDiscardedRequestIdScanner.MaximumIdBytes + 10);
        McpInputLineReader reader = Reader("{\"id\":\"" + longId + "\"}\nef\n", maximumMessageBytes: 20, bufferBytes: 7);

        string?[] lines = await ReadAllAsync(reader);

        CollectionAssert.AreEqual(new[] { string.Empty, "ef" }, lines);
        Assert.IsNull(reader.DiscardedRequestId);
    }

    [TestMethod]
    public async Task ReadAsync_AfterAnOverLongLineWithAnId_NextOverLongLineWithNoneReportsNull()
    {
        McpInputLineReader reader = Reader("{\"id\":1,\"p\":\"" + new string('x', 40) + "\"}\n" + new string('y', 40) + "\n", maximumMessageBytes: 20, bufferBytes: 6);

        await reader.ReadAsync(CancellationToken.None);
        Assert.IsNotNull(reader.DiscardedRequestId);
        await reader.ReadAsync(CancellationToken.None);

        Assert.IsNull(reader.DiscardedRequestId);
    }

    [TestMethod]
    public async Task ReadAsync_AfterTheEnd_KeepsReturningNull()
    {
        McpInputLineReader reader = Reader(string.Empty, maximumMessageBytes: 4, bufferBytes: 3);

        byte[]? first = await reader.ReadAsync(CancellationToken.None);
        byte[]? second = await reader.ReadAsync(CancellationToken.None);

        Assert.IsNull(first);
        Assert.IsNull(second);
    }

    private static McpInputLineReader Reader(string input, int maximumMessageBytes, int bufferBytes) =>
        new(new MemoryStream(Encoding.UTF8.GetBytes(input)), maximumMessageBytes, bufferBytes);

    private static async Task<string?[]> ReadAllAsync(McpInputLineReader reader)
    {
        List<string?> lines = [];
        while (await reader.ReadAsync(CancellationToken.None) is { } line)
        {
            lines.Add(Encoding.UTF8.GetString(line));
        }

        return [.. lines];
    }
}
