using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class UploadLimitTests
{
    private const string ChunkedGet = "GET /file.txt HTTP/1.1\r\nHost: h\r\nTransfer-Encoding: chunked\r\n\r\n";
    private const string NextHead = "HEAD /file.txt HTTP/1.1\r\nHost: h\r\n\r\n";

    private static readonly ExchangeLimits OneKiBUploads = ExchangeLimits.Default with { MaxUploadBytes = 1024 };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ContentLengthOverTheLimit_Answers413AndCloses()
    {
        var (connection, log) = await ServeAsync(
            [RecordedFixture.ReadRequestBytes("upload-too-large-413")], TestContext.CancellationToken, OneKiBUploads, peerHalfCloses: false);

        CollectionAssert.AreEqual(RecordedResponse("upload-too-large-413"), connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("POST /file.txt: the 2048-byte body is past the upload limit of 1024 bytes; answered 413 and closed.", log.Notes[0]);
    }

    [TestMethod]
    public async Task ChunkedBodyOverTheLimit_Answers413AndCloses()
    {
        var firstChunks = ChunkedGet + "400\r\n" + new string('a', 1024) + "\r\n1\r\n";

        var (connection, log) = await ServeAsync([Ascii(firstChunks), Ascii("b\r\n0\r\n\r\n")], TestContext.CancellationToken, OneKiBUploads, peerHalfCloses: false);

        CollectionAssert.AreEqual(RecordedResponse("upload-too-large-413"), connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual("GET /file.txt: the chunked body went past the upload limit of 1024 bytes; answered 413 and closed.", log.Notes[0]);
    }

    [TestMethod]
    public async Task ChunkedBodyOverTheLimit_IsAnsweredBeforeTheChunkPastTheLimitIsRead()
    {
        var firstChunks = Ascii(ChunkedGet + "400\r\n" + new string('a', 1024) + "\r\n1\r\n");

        var (_, bytesReadBeforeAnswer) = await ServeCountingReadsAsync([firstChunks, Ascii("b\r\n0\r\n\r\n")], TestContext.CancellationToken, OneKiBUploads);

        Assert.AreEqual(firstChunks.Length, bytesReadBeforeAnswer);
    }

    [TestMethod]
    public async Task ExpectContinueOverTheLimit_Answers413InsteadOf100Continue()
    {
        var body = new byte[2048];
        var head = RecordedFixture.ReadRequestBytes("expect-continue-413");

        var (connection, bytesReadBeforeAnswer) = await ServeCountingReadsAsync([head, body], TestContext.CancellationToken, OneKiBUploads);

        Assert.Contains("Expect: 100-continue\r\n", Latin1(head));
        CollectionAssert.AreEqual(RecordedResponse("expect-continue-413"), connection.WrittenBytes);
        Assert.DoesNotContain("100 Continue", Latin1(connection.WrittenBytes));
        Assert.AreEqual(head.Length, bytesReadBeforeAnswer, "No body byte was read before the answer.");
    }

    [TestMethod]
    [DataRow(1024)]
    [DataRow(3)]
    public async Task ContentLengthWithinTheLimit_IsDiscardedAndTheConnectionKept(int length)
    {
        var request = $"GET /file.txt HTTP/1.1\r\nHost: h\r\nContent-Length: {length}\r\n\r\n" + new string('x', length) + NextHead;

        var (connection, _) = await ServeAsync([Ascii(request)], TestContext.CancellationToken, OneKiBUploads);

        Assert.AreEqual(Latin1(RecordedResponse("get-file")) + Latin1(RecordedResponse("head-file")), Latin1(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ChunkedBodyWithinTheLimit_IsDiscardedWithItsExtensionsAndTrailers(bool oneBytePerRead)
    {
        var request = Ascii(ChunkedGet + "3 ; ext=1\r\nabc\r\n1a\r\n" + new string('y', 26) + "\r\n0\r\nX-Trailer: v\r\n\r\n" + NextHead);

        var (connection, _) = await ServeAsync(
            oneBytePerRead ? RecordedFixture.OneBytePerRead(request) : RecordedFixture.Whole(request), TestContext.CancellationToken, OneKiBUploads);

        Assert.AreEqual(Latin1(RecordedResponse("get-file")) + Latin1(RecordedResponse("head-file")), Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task NoUploadLimit_DiscardsBodiesOfAnySize()
    {
        var limits = ExchangeLimits.Default with { MaxUploadBytes = 0 };
        var request = "GET /file.txt HTTP/1.1\r\nHost: h\r\nContent-Length: 20000\r\n\r\n" + new string('x', 20000)
            + ChunkedGet + "4e20\r\n" + new string('x', 20000) + "\r\n0\r\n\r\n";

        var (connection, _) = await ServeAsync([Ascii(request)], TestContext.CancellationToken, limits);

        Assert.AreEqual(Latin1(RecordedResponse("get-file")) + Latin1(RecordedResponse("get-file")), Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task TrailerPastTheLimit_Answers413()
    {
        var request = ChunkedGet + "0\r\nX-Trailer: " + new string('t', 1100) + "\r\n\r\n";

        var (connection, _) = await ServeAsync([Ascii(request)], TestContext.CancellationToken, OneKiBUploads, peerHalfCloses: false);

        CollectionAssert.AreEqual(RecordedResponse("upload-too-large-413"), connection.WrittenBytes);
    }

    [TestMethod]
    [DataRow(ChunkedGet + "zz\r\n")]
    [DataRow(ChunkedGet + "\r\n")]
    [DataRow(ChunkedGet + "8000000000000000\r\n")]
    [DataRow(ChunkedGet + "3\r\nabcX\r\n")]
    [DataRow(ChunkedGet + "3\r\nab")]
    [DataRow(ChunkedGet + "3\r\nabc")]
    [DataRow(ChunkedGet + "0\r\nX-Trailer: v\r\n")]
    [DataRow(ChunkedGet + "3")]
    [DataRow(ChunkedGet + " 3\r\nabc\r\n0\r\n\r\n")]
    [DataRow(ChunkedGet + "3 \r\nabc\r\n0\r\n\r\n")]
    [DataRow(ChunkedGet + "3\nabc\r\n0\r\n\r\n")]
    [DataRow(ChunkedGet + "3\r\nabc\n0\r\n\r\n")]
    [DataRow(ChunkedGet + "0\r\nX-Trailer: v\n\r\n")]
    [DataRow(ChunkedGet + "0\r\n\n")]
    [DataRow("GET /file.txt HTTP/1.1\r\nHost: h\r\nContent-Length: 10\r\n\r\nabc")]
    public async Task BodyMalformedOrEndedEarly_Answers400AndCloses(string request)
    {
        var (connection, log) = await ServeAsync([Ascii(request)], TestContext.CancellationToken, OneKiBUploads);

        StringAssert.StartsWith(Latin1(connection.WrittenBytes), "HTTP/1.1 400 Bad Request\r\n");
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual("GET /file.txt: the body was malformed or ended early; answered 400 and closed.", log.Notes[0]);
    }

    [TestMethod]
    public async Task Http10ChunkedKeepAlive_IsServedAndClosedWithoutReadingTheBody()
    {
        var request = "GET /file.txt HTTP/1.0\r\nConnection: keep-alive\r\nTransfer-Encoding: chunked\r\n\r\n";

        var (connection, _) = await ServeAsync([Ascii(request), Ascii("0\r\n\r\nGET /smuggled HTTP/1.0\r\n\r\n")], TestContext.CancellationToken, peerHalfCloses: false);

        Assert.AreEqual(Latin1(RecordedResponse("get-file")).Replace("\r\n\r\n", "\r\nConnection: close\r\n\r\n", StringComparison.Ordinal), Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ChunkSizeLineLongerThanTheLineLimit_Answers400()
    {
        var request = ChunkedGet + "1;" + new string('e', HttpRequestBodyDiscarder.MaxChunkLineBytes) + "\r\nx\r\n0\r\n\r\n";

        var (connection, _) = await ServeAsync([Ascii(request)], TestContext.CancellationToken, peerHalfCloses: false);

        StringAssert.StartsWith(Latin1(connection.WrittenBytes), "HTTP/1.1 400 Bad Request\r\n");
    }

    [TestMethod]
    public async Task RefusedMethodWithAChunkedBody_IsAnswered405BeforeReadingItAndDrainedAfter()
    {
        var request = Ascii("POST /file.txt HTTP/1.1\r\nHost: h\r\nTransfer-Encoding: chunked\r\n\r\n");

        var (connection, bytesReadBeforeAnswer) = await ServeCountingReadsAsync([request, Ascii("5\r\nhello\r\n0\r\n\r\n")], TestContext.CancellationToken, OneKiBUploads);

        StringAssert.StartsWith(Latin1(connection.WrittenBytes), "HTTP/1.1 405 Method Not Allowed\r\n");
        Assert.AreEqual(request.Length, bytesReadBeforeAnswer);
        Assert.IsEmpty(await ReadWhatIsLeftAsync(connection, TestContext.CancellationToken));
    }
}
