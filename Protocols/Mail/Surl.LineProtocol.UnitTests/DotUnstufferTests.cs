using Surl.Protocol.Abstractions;
using static Surl.LineProtocol.Wire;

namespace Surl.LineProtocol;

[TestClass]
public sealed class DotUnstufferTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadDotStuffedBodyAsync_StuffedLinesAndTerminatorSplitAtEveryByte_AreUnstuffed()
    {
        foreach (var chunks in Splits("..one\r\ntwo.\r\n...\r\n.\r\nQUIT\r\n"))
        {
            using var reader = Reader(Connection(chunks));
            var body = new MemoryStream();

            var result = await reader.ReadDotStuffedBodyAsync(body, TestContext.CancellationToken);

            var context = string.Join("|", chunks);
            Assert.AreEqual(DotStuffedBodyReadOutcome.BodyRead, result.Outcome, context);
            Assert.AreEqual(".one\r\ntwo.\r\n..\r\n", Text(body.ToArray()), context);
            Assert.AreEqual(body.Length, result.BytesWritten, context);
            Assert.AreEqual("QUIT", Text((await reader.ReadLineAsync(TestContext.CancellationToken)).Line), context);
        }
    }

    [TestMethod]
    [DataRow(".\r\n", "", DisplayName = "An empty body")]
    [DataRow("a\n.\nb\r\n.\r\n", "a\n.\nb\r\n", DisplayName = "LF . LF is body, not an end")]
    [DataRow("a\r.\r\r\n.\r\n", "a\r.\r\r\n", DisplayName = "CR . CR is body, not an end")]
    [DataRow(".x\r\n.\r\n", "x\r\n", DisplayName = "A dot starting the first line is removed")]
    [DataRow(".\rx\r\n.\r\n", "\rx\r\n", DisplayName = "Dot CR then not LF keeps the CR")]
    [DataRow(".\r\r\n.\r\n", "\r\r\n", DisplayName = "Dot CR CR LF is an unstuffed CR line")]
    [DataRow("\r\n.\r\n", "\r\n", DisplayName = "An empty first line")]
    [DataRow(".\n.\r\n.\r\n", "\n.\r\n", DisplayName = "Dot then a bare LF is body")]
    public async Task ReadDotStuffedBodyAsync_UnstuffsOnlyAfterCrlf(string input, string expected)
    {
        using var reader = Reader(Connection(input));
        var body = new MemoryStream();

        var result = await reader.ReadDotStuffedBodyAsync(body, TestContext.CancellationToken);

        Assert.AreEqual(DotStuffedBodyReadOutcome.BodyRead, result.Outcome);
        Assert.AreEqual(expected, Text(body.ToArray()));
    }

    [TestMethod]
    public async Task ReadDotStuffedBodyAsync_PeerClosesBeforeTheEnd_IsClosed()
    {
        using var reader = Reader(Connection("line\r\n."));
        var body = new MemoryStream();

        var result = await reader.ReadDotStuffedBodyAsync(body, TestContext.CancellationToken);

        Assert.AreEqual(DotStuffedBodyReadOutcome.Closed, result.Outcome);
        Assert.AreEqual(6, result.BytesWritten);
    }

    [TestMethod]
    public async Task ReadDotStuffedBodyAsync_BodyExactlyAtTheUploadLimit_IsRead()
    {
        var limits = ExchangeLimits.Default with { MaxUploadBytes = 5 };
        using var reader = Reader(Connection("abc\r\n.\r\n"), limits);

        var result = await reader.ReadDotStuffedBodyAsync(new MemoryStream(), TestContext.CancellationToken);

        Assert.AreEqual(new DotStuffedBodyReadResult(DotStuffedBodyReadOutcome.BodyRead, 5), result);
    }

    [TestMethod]
    public async Task ReadDotStuffedBodyAsync_BodyPastTheUploadLimit_IsTooLargeAndNothingMoreIsRead()
    {
        var limits = ExchangeLimits.Default with { MaxUploadBytes = 5 };
        var connection = Connection("abc\r\n", "d\r\n.\r\n", "QUIT\r\n");
        using var reader = Reader(connection, limits);
        var body = new MemoryStream();

        var result = await reader.ReadDotStuffedBodyAsync(body, TestContext.CancellationToken);

        Assert.AreEqual(new DotStuffedBodyReadResult(DotStuffedBodyReadOutcome.BodyTooLarge, 5), result);
        Assert.AreEqual("abc\r\n", Text(body.ToArray()));
        Assert.AreEqual("QUIT\r\n", await RemainingTextAsync(connection, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadDotStuffedBodyAsync_UploadLimitOfZero_MeansNoLimit()
    {
        var limits = ExchangeLimits.Default with { MaxUploadBytes = 0 };
        var text = new string('x', 3000) + "\r\n";
        using var reader = Reader(Connection(text, ".\r\n"), limits);

        var result = await reader.ReadDotStuffedBodyAsync(new MemoryStream(), TestContext.CancellationToken);

        Assert.AreEqual(new DotStuffedBodyReadResult(DotStuffedBodyReadOutcome.BodyRead, text.Length), result);
    }

    [TestMethod]
    public async Task ReadDotStuffedBodyAsync_NullDestination_Throws()
    {
        using var reader = Reader(Connection());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => reader.ReadDotStuffedBodyAsync(null!, TestContext.CancellationToken).AsTask());
    }
}
