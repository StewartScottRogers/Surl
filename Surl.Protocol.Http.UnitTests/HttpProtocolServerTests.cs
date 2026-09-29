using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class HttpProtocolServerTests
{
    private const string DateAndServer = "Date: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\n";
    private const string FileBody = "Hello from Surl.\n";
    private const string OkHead =
        "HTTP/1.1 200 OK\r\n" + DateAndServer
        + "Last-Modified: Tue, 01 Sep 2026 08:30:00 GMT\r\n"
        + "Content-Type: application/octet-stream\r\n"
        + "Content-Length: 17\r\n";

    private const string NotFound = "HTTP/1.1 404 Not Found\r\n" + DateAndServer + "Content-Length: 0\r\n\r\n";
    private const string BadRequest = "HTTP/1.1 400 Bad Request\r\n" + DateAndServer + "Content-Length: 0\r\nConnection: close\r\n\r\n";

    private static readonly string Root = Path.Join(Path.GetTempPath(), "surl-http-tests");
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FileTime = new(2026, 9, 1, 8, 30, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullContentStore_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpProtocolServer(null!));
    }

    [TestMethod]
    public void Schemes_IsHttpOnly()
    {
        var server = new HttpProtocolServer(new ContentStore(Root, StandardFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot));

        CollectionAssert.AreEqual(new[] { "http" }, server.Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_NullArguments_Throw()
    {
        var server = new HttpProtocolServer(new ContentStore(Root, StandardFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot));
        var connection = new InMemoryConnection([]);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, Context(new RecordingExchangeLog())));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(connection, null!));
    }

    [TestMethod]
    [DataRow("get-file", false)]
    [DataRow("get-file", true)]
    [DataRow("custom-header", false)]
    [DataRow("query-string", false)]
    public async Task ServeAsync_RecordedGetOfAFile_SendsTheRecordedResponseAndKeepsTheConnection(string caseName, bool oneBytePerRead)
    {
        var (connection, log) = await ServeAsync(RecordedFixture.ReadRequestBytes(caseName), oneBytePerRead: oneBytePerRead);

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("get-file", "response.bin"), connection.WrittenBytes);
        Assert.AreEqual(OkHead + "\r\n" + FileBody, Latin1(connection.WrittenBytes));
        Assert.IsFalse(connection.Aborted);
        Assert.Contains($"200, 17 bytes of {Path.Join(Root, "file.txt")}", log.Notes[0]);
    }

    [TestMethod]
    [DataRow("head-file")]
    [DataRow("head")]
    public async Task ServeAsync_RecordedHeadOfAFile_SendsTheRecordedHeadWithNoBody(string caseName)
    {
        var (connection, _) = await ServeAsync(RecordedFixture.ReadRequestBytes(caseName));

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("head-file", "response.bin"), connection.WrittenBytes);
        Assert.AreEqual(OkHead + "\r\n", Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_RecordedMissingFile_SendsTheRecorded404AndKeepsTheConnection()
    {
        var (connection, log) = await ServeAsync(RecordedFixture.ReadRequestBytes("not-found-fail"));

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("not-found-fail", "response.bin"), connection.WrittenBytes);
        Assert.AreEqual(NotFound, Latin1(connection.WrittenBytes));
        Assert.Contains($"404, nothing exists at {Path.Join(Root, "missing.txt")}", log.Notes[0]);
        Assert.AreEqual("The client closed the connection: ConnectionClosed.", log.Notes[1]);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedHttp10Get_SendsTheFileWithConnectionCloseAndHalfCloses()
    {
        var (connection, log) = await ServeAsync(RecordedFixture.ReadRequestBytes("http10"), peerHalfCloses: false);

        Assert.AreEqual(OkHead + "Connection: close\r\n\r\n" + FileBody, Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        CollectionAssert.AreEqual(
            new[] { "GET /file.txt: 200, 17 bytes of " + Path.Join(Root, "file.txt"), "Stopped draining the unread request bytes at the 1-second drain limit." },
            log.Notes.ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ServeAsync_RecordedTwoRequests_AnswersBothOnOneConnection(bool oneBytePerRead)
    {
        var fileSystem = StandardFileSystem()
            .AddFile(Path.Join(Root, "one"), "1"u8.ToArray(), FileTime)
            .AddFile(Path.Join(Root, "two"), "22"u8.ToArray(), FileTime);

        var (connection, log) = await ServeAsync(RecordedFixture.ReadRequestBytes("two-urls"), fileSystem, oneBytePerRead);

        Assert.AreEqual(OkResponse(1, "1") + OkResponse(2, "22"), Latin1(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted, "Neither response closed the connection; only the client's close ended it.");
        Assert.HasCount(3, log.Notes);
    }

    [TestMethod]
    [DataRow("GET /%2e%2e/x HTTP/1.1\r\nHost: h\r\n\r\n", "404, the path was refused (DotSegment)")]
    [DataRow("GET /a%2Fb HTTP/1.1\r\nHost: h\r\n\r\n", "404, the path was refused (SeparatorInSegment)")]
    [DataRow("GET / HTTP/1.1\r\nHost: h\r\n\r\n", "is a directory")]
    [DataRow("GET /sub/ HTTP/1.1\r\nHost: h\r\n\r\n", "is a directory")]
    [DataRow("HEAD /nothing HTTP/1.1\r\nHost: h\r\n\r\n", "404, nothing exists at")]
    public async Task ServeAsync_PathWithNoFileToServe_Answers404AndKeepsTheConnection(string request, string note)
    {
        var (connection, log) = await ServeAsync(Encoding.ASCII.GetBytes(request));

        Assert.AreEqual(NotFound, Latin1(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted);
        Assert.Contains(note, log.Notes[0]);
    }

    [TestMethod]
    [DataRow("http://127.0.0.1:18018/file.txt", "file.txt")]
    [DataRow("HTTP://h/file.txt?x=/y", "file.txt")]
    [DataRow("http://h", "")]
    [DataRow("http://h?x=/file.txt", "")]
    public async Task ServeAsync_AbsoluteFormTarget_IsServedByItsPath(string requestTarget, string expectedFile)
    {
        var (_, log) = await ServeAsync(Encoding.ASCII.GetBytes($"HEAD {requestTarget} HTTP/1.1\r\nHost: h\r\n\r\n"));

        var expectedLocation = expectedFile.Length == 0 ? Root : Path.Join(Root, expectedFile);
        Assert.Contains(expectedLocation, log.Notes[0]);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedPathAsIsDotSegment_Answers404()
    {
        var (connection, log) = await ServeAsync(RecordedFixture.ReadRequestBytes("path-as-is"));

        Assert.AreEqual(NotFound, Latin1(connection.WrittenBytes));
        Assert.Contains("(DotSegment)", log.Notes[0]);
    }

    [TestMethod]
    [DataRow("POST")]
    [DataRow("PUT")]
    [DataRow("DELETE")]
    [DataRow("CONNECT")]
    [DataRow("OPTIONS")]
    [DataRow("TRACE")]
    [DataRow("PATCH")]
    public async Task ServeAsync_KnownMethodTheStoreRefuses_Answers405WithAllowAndCloses(string method)
    {
        var request = $"{method} /file.txt HTTP/1.1\r\nHost: h\r\nContent-Length: 3\r\n\r\nabc";

        var (connection, log) = await ServeAsync(Encoding.ASCII.GetBytes(request), peerHalfCloses: false);

        Assert.AreEqual(
            "HTTP/1.1 405 Method Not Allowed\r\n" + DateAndServer + "Allow: GET, HEAD\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual($"{method} /file.txt: the method is not served; answered 405 and closed.", log.Notes[0]);
    }

    [TestMethod]
    [DataRow("BREW")]
    [DataRow("get")]
    public async Task ServeAsync_UnknownMethod_Answers501AndCloses(string method)
    {
        var (connection, _) = await ServeAsync(Encoding.ASCII.GetBytes($"{method} /file.txt HTTP/1.1\r\nHost: h\r\n\r\n"), peerHalfCloses: false);

        Assert.AreEqual(
            "HTTP/1.1 501 Not Implemented\r\n" + DateAndServer + "Content-Length: 0\r\nConnection: close\r\n\r\n",
            Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    [DataRow("GET /file.txt HTTP/1.1\r\nHost: h\r\nBad Field\r\n\r\n", "MalformedHeaderField")]
    [DataRow("GET /file.txt HTTP/1.1\r\nHost : h\r\n\r\n", "WhitespaceBeforeColon")]
    [DataRow("GET /file.txt\r\n\r\n", "MalformedRequestLine")]
    public async Task ServeAsync_MalformedHead_Answers400AndCloses(string request, string outcome)
    {
        var (connection, log) = await ServeAsync(Encoding.ASCII.GetBytes(request), peerHalfCloses: false);

        Assert.AreEqual(BadRequest, Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual($"No request head was read: {outcome}; answered 400 and closed.", log.Notes[0]);
    }

    [TestMethod]
    public async Task ServeAsync_UnsupportedVersion_Answers505AndCloses()
    {
        var (connection, _) = await ServeAsync("GET / HTTP/2.0\r\n\r\n"u8.ToArray(), peerHalfCloses: false);

        Assert.AreEqual(
            "HTTP/1.1 505 HTTP Version Not Supported\r\n" + DateAndServer + "Content-Length: 0\r\nConnection: close\r\n\r\n",
            Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_HeadTooLarge_Answers431AndCloses()
    {
        var request = "GET / HTTP/1.1\r\nX-Big: " + new string('a', (int)ExchangeLimits.Default.MaxRequestHeadBytes) + "\r\n\r\n";

        var (connection, _) = await ServeAsync(Encoding.ASCII.GetBytes(request), peerHalfCloses: false);

        Assert.AreEqual(
            "HTTP/1.1 431 Request Header Fields Too Large\r\n" + DateAndServer + "Content-Length: 0\r\nConnection: close\r\n\r\n",
            Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    [DataRow("", "ConnectionClosed")]
    [DataRow("\r\n", "ConnectionClosed")]
    [DataRow("GET /file.txt HTTP/1.1\r\nHost:", "ConnectionClosedBeforeHeadEnded")]
    public async Task ServeAsync_ClientClosesBeforeAHead_SendsNothing(string request, string outcome)
    {
        var (connection, log) = await ServeAsync(Encoding.ASCII.GetBytes(request));

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsFalse(connection.WritesCompleted);
        CollectionAssert.AreEqual(new[] { $"The client closed the connection: {outcome}." }, log.Notes.ToArray());
    }

    [TestMethod]
    [DataRow("GET /file.txt HTTP/1.1\r\n\r\n")]
    [DataRow("GET /file.txt HTTP/1.1\r\nHost: a\r\nHost: b\r\n\r\n")]
    [DataRow("GET /file.txt HTTP/1.0\r\nHost: a\r\nHost: b\r\n\r\n")]
    public async Task ServeAsync_HostMissingOrRepeated_Answers400AndCloses(string request)
    {
        var (connection, log) = await ServeAsync(Encoding.ASCII.GetBytes(request), peerHalfCloses: false);

        Assert.AreEqual(BadRequest, Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.Contains("at most one Host field, and an HTTP/1.1 request needs one", log.Notes[0]);
    }

    [TestMethod]
    public async Task ServeAsync_Http10WithoutHost_IsServed()
    {
        var (connection, _) = await ServeAsync("GET /file.txt HTTP/1.0\r\n\r\n"u8.ToArray(), peerHalfCloses: false);

        Assert.AreEqual(OkHead + "Connection: close\r\n\r\n" + FileBody, Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow("Connection: close\r\n")]
    [DataRow("Connection: keep-alive, Close\r\n")]
    [DataRow("Transfer-Encoding: gzip\r\n")]
    public async Task ServeAsync_Http11RequestThatEndsTheConnection_SaysCloseAndHalfCloses(string field)
    {
        var (connection, _) = await ServeAsync(Encoding.ASCII.GetBytes($"GET /file.txt HTTP/1.1\r\nHost: h\r\n{field}\r\n"), peerHalfCloses: false);

        Assert.AreEqual(OkHead + "Connection: close\r\n\r\n" + FileBody, Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_Http11WithContentLengthZero_KeepsTheConnection()
    {
        var request = "GET /file.txt HTTP/1.1\r\nHost: h\r\nContent-Length: 0\r\n\r\nHEAD /file.txt HTTP/1.1\r\nHost: h\r\n\r\n"u8.ToArray();

        var (connection, _) = await ServeAsync(request);

        Assert.AreEqual(OkHead + "\r\n" + FileBody + OkHead + "\r\n", Latin1(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_Http10WithKeepAlive_SaysKeepAliveAndReadsTheNextRequest()
    {
        var request = "GET /file.txt HTTP/1.0\r\nConnection: Keep-Alive\r\n\r\nGET /missing HTTP/1.0\r\n\r\n"u8.ToArray();

        var (connection, _) = await ServeAsync(request, peerHalfCloses: false);

        Assert.AreEqual(
            OkHead + "Connection: keep-alive\r\n\r\n" + FileBody
            + "HTTP/1.1 404 Not Found\r\n" + DateAndServer + "Content-Length: 0\r\nConnection: close\r\n\r\n",
            Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_FileModifiedAfterNow_SendsLastModifiedEqualToDate()
    {
        var fileSystem = StandardFileSystem().AddFile(Path.Join(Root, "future"), "x"u8.ToArray(), Now.AddDays(1));

        var (connection, _) = await ServeAsync("HEAD /future HTTP/1.1\r\nHost: h\r\n\r\n"u8.ToArray(), fileSystem);

        Assert.Contains("Last-Modified: Mon, 28 Sep 2026 12:00:00 GMT\r\n", Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_EmptyFile_SendsContentLengthZero()
    {
        var fileSystem = StandardFileSystem().AddFile(Path.Join(Root, "empty"), [], FileTime);

        var (connection, _) = await ServeAsync("GET /empty HTTP/1.1\r\nHost: h\r\n\r\n"u8.ToArray(), fileSystem);

        Assert.AreEqual(OkResponse(0, string.Empty), Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_FileShrinksWhileSent_AbortsTheConnection()
    {
        var fileSystem = StandardFileSystem().AddFile(Path.Join(Root, "shrinks"), "abc"u8.ToArray(), FileTime, reportedLength: 5);

        var (connection, log) = await ServeAsync("GET /shrinks HTTP/1.1\r\nHost: h\r\n\r\nGET /file.txt HTTP/1.1\r\nHost: h\r\n\r\n"u8.ToArray(), fileSystem);

        Assert.AreEqual(OkResponse(5, "abc"), Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.Aborted);
        Assert.AreEqual($"{Path.Join(Root, "shrinks")} shrank to 3 bytes while it was sent; the connection was aborted.", log.Notes[1]);
    }

    private static string OkResponse(int length, string body) =>
        "HTTP/1.1 200 OK\r\n" + DateAndServer
        + "Last-Modified: Tue, 01 Sep 2026 08:30:00 GMT\r\n"
        + "Content-Type: application/octet-stream\r\n"
        + $"Content-Length: {length}\r\n\r\n" + body;

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    private static InMemoryContentFileSystem StandardFileSystem() => new InMemoryContentFileSystem()
        .AddDirectory(Root)
        .AddDirectory(Path.Join(Root, "sub"))
        .AddFile(Path.Join(Root, "file.txt"), Encoding.ASCII.GetBytes(FileBody), FileTime);

    private static ExchangeContext Context(IExchangeLog log, CancellationToken cancellationToken = default, TimeProvider? timeProvider = null) => new(
        1,
        new ListenUrl("http", "127.0.0.1", 18018).WithBoundPort(18018),
        new IPEndPoint(IPAddress.Loopback, 18018),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        timeProvider ?? new FixedTimeProvider(Now),
        cancellationToken);

    private async Task<(InMemoryConnection Connection, RecordingExchangeLog Log)> ServeAsync(
        byte[] request, InMemoryContentFileSystem? fileSystem = null, bool oneBytePerRead = false, bool peerHalfCloses = true)
    {
        var server = new HttpProtocolServer(new ContentStore(Root, fileSystem ?? StandardFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot));
        var chunks = oneBytePerRead ? RecordedFixture.OneBytePerRead(request) : RecordedFixture.Whole(request);
        var connection = new InMemoryConnection(chunks, peerHalfClosesWhenExhausted: peerHalfCloses);
        var log = new RecordingExchangeLog();
        var clock = new ManualTimeProvider(Now);

        var serving = server.ServeAsync(connection, Context(log, TestContext.CancellationToken, clock));
        await (peerHalfCloses ? serving : HttpServerHarness.AdvanceUntilCompletedAsync(clock, serving));

        return (connection, log);
    }
}
