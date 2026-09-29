using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Dict;

[TestClass]
public sealed class DictLineReaderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadLineAsync_SeveralLinesInOneRead_ReturnsThemInOrder()
    {
        var reader = Reader(["one\r\ntwo\nthree\r\n"], maxLineBytes: 0);

        Assert.AreEqual(DictLineReadResult.Read("one"), await reader.ReadLineAsync(TestContext.CancellationToken));
        Assert.AreEqual(DictLineReadResult.Read("two"), await reader.ReadLineAsync(TestContext.CancellationToken));
        Assert.AreEqual(DictLineReadResult.Read("three"), await reader.ReadLineAsync(TestContext.CancellationToken));
        Assert.AreEqual(DictLineReadResult.NoLine(DictLineReadOutcome.ConnectionClosed), await reader.ReadLineAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadLineAsync_LineSplitAcrossReads_ReturnsItWhole()
    {
        var reader = Reader(["DEF", "INE ! h", "ello\r", "\n"], maxLineBytes: 8192);

        Assert.AreEqual(DictLineReadResult.Read("DEFINE ! hello"), await reader.ReadLineAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadLineAsync_Utf8Line_IsDecoded()
    {
        var connection = new InMemoryConnection([Encoding.UTF8.GetBytes("DEFINE ! café\r\n")]);
        var reader = new DictLineReader(connection, 0);

        Assert.AreEqual(DictLineReadResult.Read("DEFINE ! café"), await reader.ReadLineAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadLineAsync_LineOfExactlyTheLimit_IsRead()
    {
        var reader = Reader([new string('a', 8190) + "\r\n"], maxLineBytes: 8192);

        Assert.AreEqual(DictLineReadResult.Read(new string('a', 8190)), await reader.ReadLineAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(100)]
    public async Task ReadLineAsync_LineOneByteOverTheLimit_IsTooLong(int bytesPerRead)
    {
        var line = new string('a', 8191) + "\r\n";
        var chunks = Enumerable.Range(0, (line.Length + bytesPerRead - 1) / bytesPerRead)
            .Select(index => line.Substring(index * bytesPerRead, Math.Min(bytesPerRead, line.Length - (index * bytesPerRead))));
        var reader = Reader(chunks, maxLineBytes: 8192);

        Assert.AreEqual(DictLineReadResult.NoLine(DictLineReadOutcome.LineTooLong), await reader.ReadLineAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadLineAsync_OverLongLineWithoutLineFeed_IsTooLong()
    {
        var reader = Reader([new string('a', 20), "never read\r\n"], maxLineBytes: 16);

        Assert.AreEqual(DictLineReadResult.NoLine(DictLineReadOutcome.LineTooLong), await reader.ReadLineAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadLineAsync_NoLimit_ReadsA16KibLine()
    {
        var reader = Reader([new string('a', 16384) + "\r\n"], maxLineBytes: 0);

        Assert.AreEqual(DictLineReadResult.Read(new string('a', 16384)), await reader.ReadLineAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadLineAsync_ClosedPartWayThroughALine_SaysSo()
    {
        var reader = Reader(["QUIT\r\nDEF"], maxLineBytes: 0);

        await reader.ReadLineAsync(TestContext.CancellationToken);

        Assert.AreEqual(DictLineReadResult.NoLine(DictLineReadOutcome.ConnectionClosedMidLine), await reader.ReadLineAsync(TestContext.CancellationToken));
    }

    private static DictLineReader Reader(IEnumerable<string> chunks, long maxLineBytes) =>
        new(new InMemoryConnection(chunks.Select(chunk => new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes(chunk))).ToList()), maxLineBytes);
}
