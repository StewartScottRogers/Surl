using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class RequestHeadLimitTests
{
    private static readonly int Limit = (int)ExchangeLimits.Default.MaxRequestHeadBytes;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task HeadOfExactlyTheLimit_IsAnswered(bool oneBytePerRead)
    {
        var head = HeadOfLength(Limit);

        var (connection, _) = await ServeAsync(oneBytePerRead ? RecordedFixture.OneBytePerRead(head) : RecordedFixture.Whole(head), TestContext.CancellationToken);

        Assert.AreEqual(102_400, head.Length);
        CollectionAssert.AreEqual(RecordedResponse("get-file"), connection.WrittenBytes);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task HeadOneByteOverTheLimit_Answers431AndCloses(bool oneBytePerRead)
    {
        var head = HeadOfLength(Limit + 1);

        var (connection, log) = await ServeAsync(
            oneBytePerRead ? RecordedFixture.OneBytePerRead(head) : RecordedFixture.Whole(head), TestContext.CancellationToken, peerHalfCloses: false);

        CollectionAssert.AreEqual(RecordedResponse("head-too-large-431"), connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("No request head was read: HeadTooLarge; answered 431 and closed.", log.Notes[0]);
    }

    [TestMethod]
    public async Task HeadPastTheLimit_IsNotReadPastTheLimit()
    {
        var head = HeadOfLength(Limit + 1000);

        var (connection, _) = await ServeAsync([head], TestContext.CancellationToken, peerHalfCloses: false);

        Assert.HasCount(1000, await ReadWhatIsLeftAsync(connection, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ZeroLimit_Accepts200KiBHead()
    {
        var limits = ExchangeLimits.Default with { MaxRequestHeadBytes = 0 };

        var (connection, _) = await ServeAsync([HeadOfLength(200 * 1024)], TestContext.CancellationToken, limits);

        CollectionAssert.AreEqual(RecordedResponse("get-file"), connection.WrittenBytes);
    }

    [TestMethod]
    public async Task SmallerLimit_IsTheOneEnforced()
    {
        var limits = ExchangeLimits.Default with { MaxRequestHeadBytes = 100 };

        var (connection, _) = await ServeAsync([HeadOfLength(101)], TestContext.CancellationToken, limits, peerHalfCloses: false);

        CollectionAssert.AreEqual(RecordedResponse("head-too-large-431"), connection.WrittenBytes);
    }

    [TestMethod]
    public async Task HeadPastTheLimitAlreadyReadWithAChunkedBody_Answers431()
    {
        var limits = ExchangeLimits.Default with { MaxRequestHeadBytes = 150 };
        var firstHead = Ascii("GET /file.txt HTTP/1.1\r\nHost: h\r\nTransfer-Encoding: chunked\r\n\r\n");
        var lastChunkAndNextHead = Ascii("0\r\n\r\n").Concat(HeadOfLength(300)).ToArray();

        var (connection, _) = await ServeAsync([firstHead, lastChunkAndNextHead], TestContext.CancellationToken, limits, peerHalfCloses: false);

        var recorded431 = RecordedResponse("head-too-large-431");
        CollectionAssert.AreEqual(recorded431, connection.WrittenBytes[^recorded431.Length..]);
    }

    // A GET of /file.txt padded with one field to exactly length bytes, its CRLF CRLF included.
    private static byte[] HeadOfLength(int length)
    {
        const string Start = "GET /file.txt HTTP/1.1\r\nHost: h\r\nX-Pad: ";
        const string End = "\r\n\r\n";

        return Ascii(Start + new string('a', length - Start.Length - End.Length) + End);
    }
}
