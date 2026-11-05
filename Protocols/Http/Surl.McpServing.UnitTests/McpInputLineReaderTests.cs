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
