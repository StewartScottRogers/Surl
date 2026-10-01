using Surl.Protocol.Abstractions;
using static Surl.Protocol.Rtsp.RtspServerHarness;

namespace Surl.Protocol.Rtsp;

[TestClass]
public sealed class RtspRefusalTests
{
    private const string NextOptions = "OPTIONS * RTSP/1.0\r\nCSeq: 9\r\n\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task UnknownMethod_Is501WithTheBytesUpstreamCurlAccepted()
    {
        var (connection, log) = await ServeAsync([Ascii("GET * RTSP/1.0\r\nCSeq: 1\r\n\r\n")], TestContext.CancellationToken);

        CollectionAssert.AreEqual(RecordedResponse("not-implemented-501"), connection.WrittenBytes);
        Assert.AreEqual("RTSP GET refused: 501 Not Implemented: the method is not an RTSP method", log.Notes[0]);
    }

    [TestMethod]
    [DataRow("REDIRECT")]
    [DataRow("options")]
    public async Task MethodNotOneOfTheTen_Is501AndKeepsTheConnection(string method)
    {
        var (connection, _) = await ServeAsync([Ascii($"{method} rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 1\r\n\r\n{NextOptions}")], TestContext.CancellationToken);

        Assert.StartsWith(ResponseHead("501 Not Implemented", "1") + "RTSP/1.0 200 OK\r\nCSeq: 9\r\n", Latin1(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted);
    }

    [TestMethod]
    [DataRow("/media")]
    [DataRow("http://h/media")]
    [DataRow("rtsp:/h")]
    public async Task RequestUriNeitherStarNorRtspUrl_Is400WithTheBytesUpstreamCurlAccepted(string requestUri)
    {
        var (connection, log) = await ServeAsync([Ascii($"OPTIONS {requestUri} RTSP/1.0\r\nCSeq: 1\r\n\r\n")], TestContext.CancellationToken);

        CollectionAssert.AreEqual(RecordedResponse("bad-request-400"), connection.WrittenBytes);
        Assert.IsFalse(connection.WritesCompleted);
        Assert.AreEqual("RTSP OPTIONS refused: 400 Bad Request: the Request-URI is neither * nor an rtsp:// URL", log.Notes[0]);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("CSeq: \r\n")]
    [DataRow("CSeq: 1\r\nCSeq: 2\r\n")]
    [DataRow("CSeq: 1a\r\n")]
    [DataRow("CSeq: -1\r\n")]
    [DataRow("CSeq: 1234567890\r\n")]
    public async Task WithoutExactlyOneNumericCSeq_Is400WithNoCSeqAndKeepsTheConnection(string cseqFields)
    {
        var (connection, log) = await ServeAsync([Ascii($"OPTIONS * RTSP/1.0\r\n{cseqFields}\r\n{NextOptions}")], TestContext.CancellationToken);

        Assert.StartsWith(ResponseHead("400 Bad Request", null) + "RTSP/1.0 200 OK\r\nCSeq: 9\r\n", Latin1(connection.WrittenBytes));
        Assert.AreEqual("RTSP OPTIONS refused: 400 Bad Request: the request needs exactly one CSeq of 1 to 9 digits", log.Notes[0]);
    }

    [TestMethod]
    public async Task WithoutCSeqButWithABody_Is400WithNoCSeqAndCloses()
    {
        var (connection, _) = await ServeAsync([Ascii("OPTIONS * RTSP/1.0\r\nContent-Length: 4\r\n\r\nbody"), Ascii(NextOptions)], TestContext.CancellationToken);

        Assert.AreEqual(ResponseHead("400 Bad Request", null), Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsEmpty(await ReadWhatIsLeftAsync(connection, TestContext.CancellationToken), "What the client still sent was drained.");
    }

    [TestMethod]
    [DataRow("Transfer-Encoding: chunked\r\n", "RTSP requests carry no Transfer-Encoding")]
    [DataRow("Transfer-Encoding: gzip\r\nContent-Length: 4\r\n", "RTSP requests carry no Transfer-Encoding")]
    [DataRow("Content-Length: 4x\r\n", "the Content-Length is not one field of decimal digits")]
    [DataRow("Content-Length: 4\r\nContent-Length: 4\r\n", "the Content-Length is not one field of decimal digits")]
    public async Task BodyFramingTheServerDoesNotRead_Is400WithTheCSeqAndCloses(string framingFields, string check)
    {
        var (connection, log) = await ServeAsync([Ascii($"OPTIONS * RTSP/1.0\r\nCSeq: 3\r\n{framingFields}\r\nbody{NextOptions}")], TestContext.CancellationToken);

        Assert.AreEqual(ResponseHead("400 Bad Request", "3"), Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual($"RTSP OPTIONS refused: 400 Bad Request: {check}", log.Notes[0]);
    }

    [TestMethod]
    public async Task BodyPastTheUploadLimit_Is413BeforeItIsReadAndCloses()
    {
        var limits = ExchangeLimits.Default with { MaxUploadBytes = 4 };

        var (connection, log) = await ServeAsync([Ascii("ANNOUNCE rtsp://h/a RTSP/1.0\r\nCSeq: 2\r\nContent-Length: 5\r\n\r\n12345")], TestContext.CancellationToken, limits);

        Assert.AreEqual(ResponseHead("413 Request Entity Too Large", "2"), Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual("RTSP ANNOUNCE refused: 413 Request Entity Too Large: the 5-byte body is past the upload limit of 4 bytes", log.Notes[0]);
    }

    [TestMethod]
    public async Task BodyAtTheUploadLimit_IsReadAndTheRequestAnswered()
    {
        var limits = ExchangeLimits.Default with { MaxUploadBytes = 4 };

        var (connection, _) = await ServeAsync([Ascii("OPTIONS * RTSP/1.0\r\nCSeq: 2\r\nContent-Length: 4\r\n\r\n1234")], TestContext.CancellationToken, limits);

        Assert.StartsWith("RTSP/1.0 200 OK\r\nCSeq: 2\r\n", Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task BodyThatEndsEarly_Is400AndCloses()
    {
        var (connection, log) = await ServeAsync([Ascii("OPTIONS * RTSP/1.0\r\nCSeq: 2\r\nContent-Length: 10\r\n\r\n1234")], TestContext.CancellationToken);

        Assert.AreEqual(ResponseHead("400 Bad Request", "2"), Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual("RTSP OPTIONS refused: 400 Bad Request: the body ended before its Content-Length", log.Notes[0]);
    }

    [TestMethod]
    [DataRow("OPTIONS * RTSP/2.0\r\nCSeq: 1\r\n\r\n", "505 RTSP Version Not Supported", "UnsupportedVersion")]
    [DataRow("OPTIONS * HTTP/1.1\r\nCSeq: 1\r\n\r\n", "400 Bad Request", "MalformedRequestLine")]
    [DataRow("OPTIONS *\r\nCSeq: 1\r\n\r\n", "400 Bad Request", "MalformedRequestLine")]
    [DataRow("OPTIONS * RTSP/1.0\r\nCSeq 1\r\n\r\n", "400 Bad Request", "MalformedHeaderField")]
    [DataRow("OPTIONS * RTSP/1.0\r\nCSeq : 1\r\n\r\n", "400 Bad Request", "WhitespaceBeforeColon")]
    public async Task HeadThatCannotBeRead_IsRefusedWithNoCSeqAndCloses(string request, string statusLine, string outcome)
    {
        var (connection, log) = await ServeAsync([Ascii(request + NextOptions)], TestContext.CancellationToken);

        Assert.AreEqual(ResponseHead(statusLine, null), Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual($"RTSP request refused: {statusLine}: no request head was read ({outcome})", log.Notes[0]);
    }

    [TestMethod]
    public async Task RtspMinorVersionAboveZero_IsReadAsRtsp10()
    {
        var (connection, _) = await ServeAsync([Ascii("OPTIONS * RTSP/1.1\r\nCSeq: 1\r\n\r\n")], TestContext.CancellationToken);

        Assert.StartsWith("RTSP/1.0 200 OK\r\nCSeq: 1\r\n", Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task HeadPastTheRequestHeadLimit_Is431WithNoCSeqAndCloses()
    {
        var limits = ExchangeLimits.Default with { MaxRequestHeadBytes = 128 };
        var request = $"OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nX-Long: {new string('a', 200)}\r\n\r\n";

        var (connection, log) = await ServeAsync([Ascii(request)], TestContext.CancellationToken, limits);

        Assert.AreEqual(ResponseHead("431 Request Header Fields Too Large", null), Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual("RTSP request refused: 431 Request Header Fields Too Large: no request head was read (HeadTooLarge)", log.Notes[0]);
    }

    [TestMethod]
    public async Task ClientClosesPartWayThroughAHead_GetsNoBytes()
    {
        var (connection, log) = await ServeAsync([Ascii("OPTIONS * RTSP/1.0\r\nCSeq:")], TestContext.CancellationToken);

        Assert.IsEmpty(connection.WrittenBytes);
        CollectionAssert.AreEqual(new[] { "The client closed the connection: ConnectionClosedBeforeHeadEnded." }, log.Notes.ToArray());
    }
}
