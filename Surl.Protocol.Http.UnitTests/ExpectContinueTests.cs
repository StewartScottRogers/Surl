using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class ExpectContinueTests
{
    private const string Continue = "HTTP/1.1 100 Continue\r\n\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task GetWithExpectContinue_Sends100ContinueBeforeReadingTheBody()
    {
        var request = RecordedFixture.ReadRequestBytes("expect-continue-get");
        var headLength = Latin1(request).IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4;

        var (connection, bytesReadBeforeAnswer) = await ServeCountingReadsAsync([request.AsMemory(0, headLength), request.AsMemory(headLength)], TestContext.CancellationToken);

        Assert.AreEqual(16, request.Length - headLength, "The recording holds curl's 16-byte body after the head.");
        CollectionAssert.AreEqual(RecordedResponse("expect-continue-get"), connection.WrittenBytes);
        StringAssert.StartsWith(Latin1(connection.WrittenBytes), Continue + "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual(headLength, bytesReadBeforeAnswer, "100 Continue went out before a body byte was read.");
    }

    [TestMethod]
    public async Task HeadWithExpectContinue_Sends100ContinueBeforeTheResponse()
    {
        var request = "HEAD /file.txt HTTP/1.1\r\nHost: h\r\nExpect: 100-continue\r\nContent-Length: 3\r\n\r\nabc";

        var (connection, _) = await ServeAsync([Ascii(request)], TestContext.CancellationToken);

        Assert.AreEqual(Continue + Latin1(RecordedResponse("head-file")), Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow("Expect: 100-CONTINUE")]
    [DataRow("Expect: something-else, 100-continue")]
    [DataRow("Expect: \t100-continue ")]
    public async Task ChunkedGetWithTheExpectationInAnyCaseOrList_Sends100Continue(string expectField)
    {
        var request = $"GET /file.txt HTTP/1.1\r\nHost: h\r\n{expectField}\r\nTransfer-Encoding: chunked\r\n\r\n3\r\nabc\r\n0\r\n\r\n";

        var (connection, _) = await ServeAsync([Ascii(request)], TestContext.CancellationToken);

        Assert.AreEqual(Continue + Latin1(RecordedResponse("get-file")), Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow("GET /file.txt HTTP/1.0\r\nExpect: 100-continue\r\nContent-Length: 3\r\n\r\nabc")]
    [DataRow("GET /file.txt HTTP/1.1\r\nHost: h\r\nContent-Length: 3\r\n\r\nabc")]
    [DataRow("GET /file.txt HTTP/1.1\r\nHost: h\r\nExpect: 100-continue\r\n\r\n")]
    [DataRow("GET /file.txt HTTP/1.1\r\nHost: h\r\nExpect: 100-continue\r\nContent-Length: 0\r\n\r\n")]
    [DataRow("GET /file.txt HTTP/1.1\r\nHost: h\r\nExpect: 200-ok\r\nContent-Length: 3\r\n\r\nabc")]
    public async Task Http10NoExpectationOrNoBody_SendsNo100Continue(string request)
    {
        var (connection, _) = await ServeAsync([Ascii(request)], TestContext.CancellationToken);

        StringAssert.StartsWith(Latin1(connection.WrittenBytes), "HTTP/1.1 200 OK\r\n");
        Assert.DoesNotContain("100 Continue", Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task UnframableBodyWithExpectContinue_SendsNo100Continue()
    {
        var request = "GET /file.txt HTTP/1.1\r\nHost: h\r\nExpect: 100-continue\r\nTransfer-Encoding: gzip\r\n\r\n";

        var (connection, _) = await ServeAsync([Ascii(request)], TestContext.CancellationToken, peerHalfCloses: false);

        StringAssert.StartsWith(Latin1(connection.WrittenBytes), "HTTP/1.1 200 OK\r\n");
        Assert.DoesNotContain("100 Continue", Latin1(connection.WrittenBytes));
    }
}
