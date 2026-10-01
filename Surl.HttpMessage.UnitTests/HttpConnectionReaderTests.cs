using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.HttpMessage;

[TestClass]
public sealed partial class HttpConnectionReaderTests
{
    private const string RecordedHost = "127.0.0.1:18017";

    private const int Limit = 102_400;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullConnection_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpConnectionReader(null!, 0));
    }

    [TestMethod]
    [DataRow("default-get", "GET", "/", 1, false)]
    [DataRow("default-get", "GET", "/", 1, true)]
    [DataRow("head", "HEAD", "/file.txt", 1, false)]
    [DataRow("head", "HEAD", "/file.txt", 1, true)]
    [DataRow("http10", "GET", "/file.txt", 0, false)]
    [DataRow("http10", "GET", "/file.txt", 0, true)]
    [DataRow("path-as-is", "GET", "/a/../b", 1, false)]
    [DataRow("path-as-is", "GET", "/a/../b", 1, true)]
    [DataRow("query-string", "GET", "/file.txt?x=1&y=%20", 1, false)]
    [DataRow("query-string", "GET", "/file.txt?x=1&y=%20", 1, true)]
    public async Task ReadRequestHeadAsync_RecordedUpstreamCurlHead_ParsesItsLineAndFields(
        string caseName, string method, string requestTarget, int minorVersion, bool oneBytePerRead)
    {
        var reader = ReaderOver(RecordedFixture.ReadRequestBytes(caseName), oneBytePerRead);

        var result = await reader.ReadRequestHeadAsync(TestContext.CancellationToken);

        AssertCurlHead(result, method, requestTarget, minorVersion);
        Assert.AreEqual(HttpRequestHeadReadOutcome.ConnectionClosed, (await reader.ReadRequestHeadAsync(TestContext.CancellationToken)).Outcome);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReadRequestHeadAsync_RecordedCustomHeader_KeepsItLastWithItsInnerSpace(bool oneBytePerRead)
    {
        var reader = ReaderOver(RecordedFixture.ReadRequestBytes("custom-header"), oneBytePerRead);

        var result = await reader.ReadRequestHeadAsync(TestContext.CancellationToken);

        AssertCurlHead(result, "GET", "/file.txt", 1, new HttpRequestField("X-Custom", "a b"));
        CollectionAssert.AreEqual(new[] { "a b" }, result.Head!.GetFieldValues("x-custom").ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReadRequestHeadAsync_RecordedTwoHeadsOnOneConnection_ParsesThemInSequence(bool oneBytePerRead)
    {
        var reader = ReaderOver(RecordedFixture.ReadRequestBytes("two-urls"), oneBytePerRead);

        var first = await reader.ReadRequestHeadAsync(TestContext.CancellationToken);
        var second = await reader.ReadRequestHeadAsync(TestContext.CancellationToken);
        var third = await reader.ReadRequestHeadAsync(TestContext.CancellationToken);

        AssertCurlHead(first, "GET", "/one", 1);
        AssertCurlHead(second, "GET", "/two", 1);
        Assert.AreEqual(HttpRequestHeadReadOutcome.ConnectionClosed, third.Outcome);
    }

    [TestMethod]
    public void RecordedFixtures_EveryCase_UpstreamCurlExitedZero()
    {
        string[] cases = ["default-get", "head", "http10", "custom-header", "path-as-is", "query-string", "two-urls"];

        foreach (var caseName in cases)
        {
            Assert.AreEqual("0", Encoding.ASCII.GetString(RecordedFixture.ReadBytes(caseName, "exitcode.txt")), caseName);
            Assert.AreEqual(caseName == "head" ? 38 : 0, RecordedFixture.ReadBytes(caseName, "stdout.bin").Length, caseName);
            Assert.IsNotEmpty(RecordedFixture.ReadBytes(caseName, "stderr.txt"));
        }
    }

    [TestMethod]
    [DataRow("GET/ HTTP/1.1\r\n\r\n", HttpRequestHeadReadOutcome.MalformedRequestLine)]
    [DataRow("GET / HTTP/2.0\r\nHost: x\r\n\r\n", HttpRequestHeadReadOutcome.UnsupportedVersion)]
    [DataRow("GET / HTTP/1.1\r\nHost : x\r\n\r\n", HttpRequestHeadReadOutcome.WhitespaceBeforeColon)]
    [DataRow("GET / HTTP/1.1\r\nHost: x\r\n folded\r\n\r\n", HttpRequestHeadReadOutcome.MalformedHeaderField)]
    public async Task ReadRequestHeadAsync_MalformedHead_ReturnsTheNamedFailureWithoutAHead(string request, HttpRequestHeadReadOutcome expected)
    {
        var result = await ReadOneHeadAsync(request);

        Assert.AreEqual(expected, result.Outcome);
        Assert.IsNull(result.Head);
    }

    [TestMethod]
    public async Task ReadRequestHeadAsync_HigherMinorVersion_ReadsItAsHttp11()
    {
        var result = await ReadOneHeadAsync("GET / HTTP/1.2\r\n\r\n");

        Assert.AreEqual(new Version(1, 1), result.Head!.Version);
    }

    [TestMethod]
    [DataRow("GET / HTTP/1.1\r\nHost: x\r\n")]
    [DataRow("GET / HTTP/1.1\r\nHost: x")]
    [DataRow("GET")]
    [DataRow("\r\nGE")]
    public async Task ReadRequestHeadAsync_ClosedPartWayThroughHead_ReturnsConnectionClosedBeforeHeadEnded(string request)
    {
        var result = await ReadOneHeadAsync(request);

        Assert.AreEqual(HttpRequestHeadReadOutcome.ConnectionClosedBeforeHeadEnded, result.Outcome);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("\r\n")]
    [DataRow("\r\n\n")]
    public async Task ReadRequestHeadAsync_ClosedBeforeAnyByteOrAfterOnlyEmptyLines_ReturnsConnectionClosed(string request)
    {
        var result = await ReadOneHeadAsync(request);

        Assert.AreEqual(HttpRequestHeadReadOutcome.ConnectionClosed, result.Outcome);
        Assert.IsNull(result.Head);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReadRequestHeadAsync_HeadOfExactlyTheMaximum_ReadsIt(bool oneBytePerRead)
    {
        var request = HeadOfLength(Limit);

        var result = await ReaderOver(request, oneBytePerRead).ReadRequestHeadAsync(TestContext.CancellationToken);

        Assert.AreEqual(HttpRequestHeadReadOutcome.HeadRead, result.Outcome);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReadRequestHeadAsync_HeadOneByteOverTheMaximum_ReturnsHeadTooLarge(bool oneBytePerRead)
    {
        var request = HeadOfLength(Limit + 1);

        var result = await ReaderOver(request, oneBytePerRead).ReadRequestHeadAsync(TestContext.CancellationToken);

        Assert.AreEqual(HttpRequestHeadReadOutcome.HeadTooLarge, result.Outcome);
    }

    [TestMethod]
    public async Task ReadRequestHeadAsync_LineThatNeverEndsPastTheMaximum_ReturnsHeadTooLargeWithoutWaitingForMore()
    {
        var bytes = Enumerable.Repeat((byte)'a', Limit).ToArray();
        var connection = new InMemoryConnection([bytes], peerHalfClosesWhenExhausted: false);

        var result = await new HttpConnectionReader(connection, Limit).ReadRequestHeadAsync(TestContext.CancellationToken);

        Assert.AreEqual(HttpRequestHeadReadOutcome.HeadTooLarge, result.Outcome);
    }

    [TestMethod]
    public async Task ReadRequestHeadAsync_BareLineFeeds_AreLineEndings()
    {
        var result = await ReadOneHeadAsync("GET / HTTP/1.1\nHost: x\n\n");

        Assert.AreEqual(HttpRequestHeadReadOutcome.HeadRead, result.Outcome);
        CollectionAssert.AreEqual(new[] { "x" }, result.Head!.GetFieldValues("Host").ToArray());
    }

    [TestMethod]
    public async Task ReadRequestHeadAsync_EmptyLinesBeforeRequestLine_AreSkipped()
    {
        var result = await ReadOneHeadAsync("\r\n\nGET / HTTP/1.1\r\n\r\n");

        Assert.AreEqual(HttpRequestHeadReadOutcome.HeadRead, result.Outcome);
        Assert.IsEmpty(result.Head!.Fields);
    }

    [TestMethod]
    public async Task ReadRequestHeadAsync_FieldValueAroundWhitespaceAndObsText_TrimsAndKeepsLatin1()
    {
        var bytes = Encoding.Latin1.GetBytes("GET / HTTP/1.1\r\nX:\t café \t\r\nEmpty:\r\n\r\n");

        var result = await ReaderOver(bytes, oneBytePerRead: false).ReadRequestHeadAsync(TestContext.CancellationToken);

        Assert.AreEqual("café", result.Head!.Fields[0].Value);
        Assert.AreEqual(string.Empty, result.Head.Fields[1].Value);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReadAsync_AfterHead_ReturnsTheBytesThatFollowIt(bool oneBytePerRead)
    {
        var reader = ReaderOver(Encoding.ASCII.GetBytes("POST / HTTP/1.1\r\nContent-Length: 4\r\n\r\nBODY"), oneBytePerRead);
        await reader.ReadRequestHeadAsync(TestContext.CancellationToken);

        var body = await ReadToEndAsync(reader);

        Assert.AreEqual("BODY", body);
    }

    [TestMethod]
    public async Task ReadAsync_DestinationSmallerThanBuffered_CopiesWhatFitsAndKeepsTheRest()
    {
        var reader = ReaderOver(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n\r\nABC"), oneBytePerRead: false);
        await reader.ReadRequestHeadAsync(TestContext.CancellationToken);
        var destination = new byte[2];

        var count = await reader.ReadAsync(destination, TestContext.CancellationToken);

        Assert.AreEqual(2, count);
        Assert.AreEqual("AB", Encoding.ASCII.GetString(destination));
        Assert.AreEqual("C", await ReadToEndAsync(reader));
    }

    [TestMethod]
    public async Task ReadAsync_EmptyDestinationWithBytesBuffered_Throws()
    {
        var reader = ReaderOver(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n\r\nABC"), oneBytePerRead: false);
        await reader.ReadRequestHeadAsync(TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () => await reader.ReadAsync(Memory<byte>.Empty, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadAsync_CancelledWithBytesBuffered_Throws()
    {
        var reader = ReaderOver(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n\r\nABC"), oneBytePerRead: false);
        await reader.ReadRequestHeadAsync(TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await reader.ReadAsync(new byte[1], new CancellationToken(canceled: true)));
    }

    [TestMethod]
    public async Task ReadRequestHeadAsync_CancelledWhileWaiting_Throws()
    {
        var connection = new InMemoryConnection([Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n")], peerHalfClosesWhenExhausted: false);
        var reader = new HttpConnectionReader(connection, Limit);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var read = reader.ReadRequestHeadAsync(cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await read);
    }

    [TestMethod]
    public void Constructor_NegativeLimit_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new HttpConnectionReader(new InMemoryConnection([]), -1));
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(long.MaxValue)]
    public async Task ReadRequestHeadAsync_NoLimitOrOneLargerThanAnArray_ReadsA200KiBHead(long maxRequestHeadBytes)
    {
        var reader = new HttpConnectionReader(new InMemoryConnection([HeadOfLength(200 * 1024)]), maxRequestHeadBytes);

        var result = await reader.ReadRequestHeadAsync(TestContext.CancellationToken);

        Assert.AreEqual(HttpRequestHeadReadOutcome.HeadRead, result.Outcome);
    }

    [TestMethod]
    public async Task ReadRequestHeadAsync_HeadPastTheLimit_TakesNoMoreThanTheLimitFromTheConnection()
    {
        var bytes = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nX: " + new string('a', 60));
        var connection = new InMemoryConnection([bytes], peerHalfClosesWhenExhausted: false);
        var reader = new HttpConnectionReader(connection, 20);

        var result = await reader.ReadRequestHeadAsync(TestContext.CancellationToken);

        var rest = new byte[100];
        var restCount = await connection.ReadAsync(rest, TestContext.CancellationToken);
        Assert.AreEqual(HttpRequestHeadReadOutcome.HeadTooLarge, result.Outcome);
        Assert.AreEqual(bytes.Length - 20, restCount, "Only the 20 bytes of the limit were taken from the connection.");
    }

    [TestMethod]
    public async Task HasReceivedHeadBytes_SaysWhetherAnyByteOfTheHeadArrived()
    {
        var connection = new InMemoryConnection([Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: x\r\n\r\nGE")], peerHalfClosesWhenExhausted: false);
        var reader = new HttpConnectionReader(connection, Limit);
        Assert.IsFalse(reader.HasReceivedHeadBytes);

        await reader.ReadRequestHeadAsync(TestContext.CancellationToken);

        Assert.IsTrue(reader.HasReceivedHeadBytes, "Two bytes of the next head are buffered.");
    }

    [TestMethod]
    public async Task HasReceivedHeadBytes_AfterAHeadCutOffPartWay_IsTrue()
    {
        var connection = new InMemoryConnection([Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n")], peerHalfClosesWhenExhausted: false);
        var reader = new HttpConnectionReader(connection, Limit);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var read = reader.ReadRequestHeadAsync(cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await read);
        Assert.IsTrue(reader.HasReceivedHeadBytes);
    }

    [TestMethod]
    public async Task WaitForBytesAsync_BytesBufferedOrArriving_ReturnsTrue()
    {
        var reader = ReaderOver(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: x\r\n\r\nX"), oneBytePerRead: false);
        await reader.ReadRequestHeadAsync(TestContext.CancellationToken);

        Assert.IsTrue(await reader.WaitForBytesAsync(TestContext.CancellationToken), "One byte is buffered.");
        Assert.IsTrue(await ReaderOver([1], oneBytePerRead: false).WaitForBytesAsync(TestContext.CancellationToken), "One byte arrives.");
    }

    [TestMethod]
    public async Task WaitForBytesAsync_ClientHalfCloses_ReturnsFalse()
    {
        Assert.IsFalse(await ReaderOver([], oneBytePerRead: false).WaitForBytesAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReadLineAsync_LinesAfterAHead_ReturnsEachWithoutItsLineFeedButWithItsCarriageReturn(bool oneBytePerRead)
    {
        var reader = ReaderOver(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: x\r\n\r\n5;x=y\r\nab\n\r\nrest"), oneBytePerRead);
        await reader.ReadRequestHeadAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual("5;x=y\r"u8.ToArray(), await reader.ReadLineAsync(10, TestContext.CancellationToken));
        CollectionAssert.AreEqual("ab"u8.ToArray(), await reader.ReadLineAsync(10, TestContext.CancellationToken));
        CollectionAssert.AreEqual("\r"u8.ToArray(), await reader.ReadLineAsync(10, TestContext.CancellationToken));
        Assert.AreEqual("rest", await ReadToEndAsync(reader));
    }

    [TestMethod]
    [DataRow("0123456789\n", 10)]
    [DataRow("0123456789", 10)]
    [DataRow("01234", 10)]
    public async Task ReadLineAsync_LineTooLongOrNeverEnded_ReturnsNull(string bytes, int maxLineBytes)
    {
        var reader = ReaderOver(Encoding.ASCII.GetBytes(bytes), oneBytePerRead: true);

        Assert.IsNull(await reader.ReadLineAsync(maxLineBytes, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadLineAsync_LongLineAlreadyBuffered_ReturnsNullAndConsumesIt()
    {
        var reader = ReaderOver(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: x\r\n\r\n0123456789\nnext\n"), oneBytePerRead: false);
        await reader.ReadRequestHeadAsync(TestContext.CancellationToken);

        Assert.IsNull(await reader.ReadLineAsync(5, TestContext.CancellationToken));
        CollectionAssert.AreEqual("next"u8.ToArray(), await reader.ReadLineAsync(5, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadLineAsync_LineThatNeverEndsPastTheLimit_ReturnsNullWithoutWaitingForMore()
    {
        var connection = new InMemoryConnection([Encoding.ASCII.GetBytes("0123456789")], peerHalfClosesWhenExhausted: false);

        Assert.IsNull(await new HttpConnectionReader(connection, Limit).ReadLineAsync(10, TestContext.CancellationToken));
    }

    [TestMethod]
    public void Constructor_NullProtocol_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpConnectionReader(new InMemoryConnection([]), Limit, null!));
    }

    [TestMethod]
    public async Task ReadRequestHeadAsync_RtspHeadWhenGivenRtsp10_ReadsIt()
    {
        var connection = new InMemoryConnection([Encoding.ASCII.GetBytes("OPTIONS * RTSP/1.0\r\nCSeq: 1\r\n\r\n")]);
        var reader = new HttpConnectionReader(connection, Limit, HttpMessageProtocol.Rtsp10);

        var result = await reader.ReadRequestHeadAsync(TestContext.CancellationToken);

        Assert.AreEqual(HttpRequestHeadReadOutcome.HeadRead, result.Outcome);
        Assert.AreSame(HttpMessageProtocol.Rtsp10, result.Head!.Protocol);
        Assert.AreEqual("OPTIONS", result.Head.Method);
        CollectionAssert.AreEqual(new[] { "1" }, result.Head.GetFieldValues("CSeq").ToArray());
    }

    [TestMethod]
    public async Task ReadRequestHeadAsync_RtspHeadWhenGivenHttp11_ReturnsMalformedRequestLine()
    {
        var result = await ReadOneHeadAsync("OPTIONS * RTSP/1.0\r\nCSeq: 1\r\n\r\n");

        Assert.AreEqual(HttpRequestHeadReadOutcome.MalformedRequestLine, result.Outcome);
    }

    [TestMethod]
    public async Task ReadRequestHeadAsync_HttpHeadWhenGivenRtsp10_ReturnsMalformedRequestLine()
    {
        var connection = new InMemoryConnection([Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: x\r\n\r\n")]);
        var reader = new HttpConnectionReader(connection, Limit, HttpMessageProtocol.Rtsp10);

        var result = await reader.ReadRequestHeadAsync(TestContext.CancellationToken);

        Assert.AreEqual(HttpRequestHeadReadOutcome.MalformedRequestLine, result.Outcome);
    }

    private static HttpConnectionReader ReaderOver(byte[] bytes, bool oneBytePerRead) =>
        new(new InMemoryConnection(oneBytePerRead ? RecordedFixture.OneBytePerRead(bytes) : RecordedFixture.Whole(bytes)), Limit);

    private async Task<HttpRequestHeadReadResult> ReadOneHeadAsync(string request) =>
        await ReaderOver(Encoding.Latin1.GetBytes(request), oneBytePerRead: false).ReadRequestHeadAsync(TestContext.CancellationToken);

    private async Task<string> ReadToEndAsync(HttpConnectionReader reader)
    {
        var text = new StringBuilder();
        var destination = new byte[16];
        int count;
        while ((count = await reader.ReadAsync(destination, TestContext.CancellationToken)) > 0)
        {
            text.Append(Encoding.ASCII.GetString(destination, 0, count));
        }

        return text.ToString();
    }

    private static byte[] HeadOfLength(int length)
    {
        const string Start = "GET / HTTP/1.1\r\nX: ";
        const string End = "\r\n\r\n";

        return Encoding.ASCII.GetBytes(Start + new string('a', length - Start.Length - End.Length) + End);
    }

    private static void AssertCurlHead(HttpRequestHeadReadResult result, string method, string requestTarget, int minorVersion, params HttpRequestField[] extraFields)
    {
        Assert.AreEqual(HttpRequestHeadReadOutcome.HeadRead, result.Outcome);
        var head = result.Head!;
        Assert.AreEqual(method, head.Method);
        Assert.AreEqual(requestTarget, head.RequestTarget);
        Assert.AreEqual(new Version(1, minorVersion), head.Version);

        (string Name, string Value)[] expected =
        [
            ("Host", RecordedHost),
            ("User-Agent", "curl/8.21.0"),
            ("Accept", "*/*"),
            .. extraFields.Select(field => (field.Name, field.Value)),
        ];
        CollectionAssert.AreEqual(expected, head.Fields.Select(field => (field.Name, field.Value)).ToArray());
        CollectionAssert.AreEqual(new[] { RecordedHost }, head.GetFieldValues("HOST").ToArray());
    }
}
