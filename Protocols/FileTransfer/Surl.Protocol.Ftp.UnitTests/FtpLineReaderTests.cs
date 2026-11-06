using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ftp;

[TestClass]
public sealed class FtpLineReaderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadLineAsync_SeveralLinesInOneRead_ReturnsThemInOrder()
    {
        using var reader = Reader(["one\r\ntwo\nthr\ree\r\n"], maxLineBytes: 0);

        Assert.AreEqual("one", await ReadTextAsync(reader));
        Assert.AreEqual("two", await ReadTextAsync(reader));
        Assert.AreEqual("thr\ree", await ReadTextAsync(reader));
        Assert.AreEqual(FtpLineReadResult.NoLine(FtpLineReadOutcome.ConnectionClosed), await reader.ReadLineAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadLineAsync_LineSplitAcrossReads_ReturnsItWhole()
    {
        using var reader = Reader(["US", "ER ano", "nymous\r", "\n"], maxLineBytes: 8192);

        Assert.AreEqual("USER anonymous", await ReadTextAsync(reader));
    }

    [TestMethod]
    public async Task ReadLineAsync_LineOfExactlyTheLimit_IsRead()
    {
        using var reader = Reader([new string('a', 8190) + "\r\n"], maxLineBytes: 8192);

        Assert.AreEqual(new string('a', 8190), await ReadTextAsync(reader));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(100)]
    public async Task ReadLineAsync_LineOneByteOverTheLimit_IsTooLong(int bytesPerRead)
    {
        var line = new string('a', 8191) + "\r\n";
        var chunks = Enumerable.Range(0, (line.Length + bytesPerRead - 1) / bytesPerRead)
            .Select(index => line.Substring(index * bytesPerRead, Math.Min(bytesPerRead, line.Length - (index * bytesPerRead))));
        using var reader = Reader(chunks, maxLineBytes: 8192);

        Assert.AreEqual(FtpLineReadResult.NoLine(FtpLineReadOutcome.LineTooLong), await reader.ReadLineAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadLineAsync_OverLongLineWithoutLineFeed_IsTooLong()
    {
        using var reader = Reader([new string('a', 20), "never read\r\n"], maxLineBytes: 16);

        Assert.AreEqual(FtpLineReadResult.NoLine(FtpLineReadOutcome.LineTooLong), await reader.ReadLineAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadLineAsync_NoLimit_ReadsA16KibLine()
    {
        using var reader = Reader([new string('a', 16384) + "\r\n"], maxLineBytes: 0);

        Assert.AreEqual(new string('a', 16384), await ReadTextAsync(reader));
    }

    [TestMethod]
    public async Task ReadLineAsync_ClosedPartWayThroughALine_SaysSo()
    {
        using var reader = Reader(["QUIT\r\nNOO"], maxLineBytes: 0);

        await reader.ReadLineAsync(TestContext.CancellationToken);

        Assert.AreEqual(FtpLineReadResult.NoLine(FtpLineReadOutcome.ConnectionClosedMidLine), await reader.ReadLineAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadLineAsync_NextLineAlreadyBuffered_RunsItsHeadTimeoutFromTheCall()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection([Encoding.ASCII.GetBytes("NOOP\r\nNO")], peerHalfClosesWhenExhausted: false);
        using var reader = new FtpLineReader(connection, 0, TimeSpan.FromSeconds(30), clock);

        Assert.AreEqual("NOOP", Encoding.ASCII.GetString((await reader.ReadLineAsync(TestContext.CancellationToken)).Line!));
        var reading = reader.ReadLineAsync(TestContext.CancellationToken).AsTask();
        clock.Advance(TimeSpan.FromSeconds(30));

        Assert.AreEqual(FtpLineReadResult.NoLine(FtpLineReadOutcome.HeadTimedOut), await reading);
    }

    private static FtpLineReader Reader(IEnumerable<string> chunks, long maxLineBytes) =>
        new(new InMemoryConnection(chunks.Select(chunk => new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes(chunk))).ToList()), maxLineBytes, Timeout.InfiniteTimeSpan, TimeProvider.System);

    private async Task<string> ReadTextAsync(FtpLineReader reader)
    {
        var result = await reader.ReadLineAsync(TestContext.CancellationToken);
        Assert.AreEqual(FtpLineReadOutcome.LineRead, result.Outcome);

        return Encoding.ASCII.GetString(result.Line!);
    }
}
