using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// The HTTP server half of upstream test cases (ADR-0080 decision 2), driven with inline cases
/// through <see cref="InMemoryConnection"/>: the bytes it sends and the bytes it records.
/// </summary>
[TestClass]
public sealed class HttpUpstreamTestCaseServerTests
{
    private const string Data = "HTTP/1.1 200 OK\r\nContent-Length: 6\r\n\r\n-foo-\n";

    private const string Data2 = "HTTP/1.1 200 two\r\nContent-Length: 0\r\n\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task OneRequest_SendsTheDataPartVerbatimAndRecordsTheRequest()
    {
        const string request = "GET /1 HTTP/1.1\r\nHost: h\r\n\r\n";
        var replies = Replies(("data", Data));

        var (connection, _) = await ServeAsync(replies, [Ascii(request)]);

        Assert.AreEqual(Data, Latin1(connection.WrittenBytes));
        Assert.AreEqual(request, Latin1(replies.ReceivedBytes));
        Assert.IsFalse(connection.WritesCompleted, "Without swsclose the connection stays open until the client closes it.");
    }

    [TestMethod]
    public async Task TwoRequestsOnOneConnection_TheSecondPathsNumberPicksDataNum()
    {
        const string requests = "GET /1220 HTTP/1.1\r\nHost: h\r\n\r\nGET /12200002 HTTP/1.1\r\nHost: h\r\n\r\n";
        var replies = Replies(("data", Data), ("data2", Data2));

        var (connection, _) = await ServeAsync(replies, [Ascii(requests)]);

        Assert.AreEqual(Data + Data2, Latin1(connection.WrittenBytes));
        Assert.AreEqual(requests, Latin1(replies.ReceivedBytes));
    }

    [TestMethod]
    public async Task Connect_IsAnsweredWithTheConnectPart()
    {
        var replies = Replies(("data", Data), ("connect", "HTTP/1.1 200 Mighty fine indeed\r\n\r\n"));

        var (connection, _) = await ServeAsync(replies, [Ascii("CONNECT test.1059:443 HTTP/1.1\r\nHost: test.1059:443\r\n\r\n")]);

        Assert.AreEqual("HTTP/1.1 200 Mighty fine indeed\r\n\r\n", Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task SwsbounceInAPart_TheNextRequestGetsTheNextPartWhateverItAsks()
    {
        var replies = Replies(("data", "HTTP/1.1 401 swsbounce\r\nContent-Length: 0\r\n\r\n"), ("data1", Data));

        var (connection, _) = await ServeAsync(replies, [Ascii("GET /5 HTTP/1.1\r\nHost: h\r\n\r\nGET /5 HTTP/1.1\r\nHost: h\r\n\r\n")]);

        Assert.AreEqual("HTTP/1.1 401 swsbounce\r\nContent-Length: 0\r\n\r\n" + Data, Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task SwscloseServerCommand_ClosesAfterTheReplyAndReadsNoMoreRequests()
    {
        var replies = Replies([("data", Data)], "swsclose");

        var (connection, _) = await ServeAsync(replies, [Ascii("GET /1 HTTP/1.1\r\nHost: h\r\n\r\nGET /1 HTTP/1.1\r\nHost: h\r\n\r\n")]);

        Assert.AreEqual(Data, Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task SwscloseInThePart_ClosesAfterTheReply()
    {
        const string data = "HTTP/1.1 200 OK\r\nFunny-head: swsclose\r\n\r\nbody";
        var replies = Replies(("data", data));

        var (connection, _) = await ServeAsync(replies, [Ascii("GET /1 HTTP/1.1\r\nHost: h\r\n\r\n")]);

        Assert.AreEqual(data, Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task Idle_SendsNothingAndWaitsForTheClientToClose()
    {
        var replies = Replies([("data", Data)], "idle");

        var (connection, _) = await ServeAsync(replies, [Ascii("GET /1 HTTP/1.1\r\nHost: h\r\n\r\n"), Ascii("late")]);

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual("GET /1 HTTP/1.1\r\nHost: h\r\n\r\nlate", Latin1(replies.ReceivedBytes));
    }

    [TestMethod]
    public async Task WriteDelay_PausesOnTheExchangeClockBetweenTheReplysLines()
    {
        var replies = Replies([("data", "A\nB\nC")], "writedelay: 10");
        var connection = new InMemoryConnection([Ascii("GET /1 HTTP/1.1\r\nHost: h\r\n\r\n")]);
        var clock = new ManualTimeProvider(Now);

        var serving = new HttpUpstreamTestCaseServer(replies).ServeAsync(connection, Context(new RecordingExchangeLog(), clock, TestContext.CancellationToken));
        await WaitForTimersAsync(clock, 1);
        Assert.AreEqual("A\n", Latin1(connection.WrittenBytes));
        clock.Advance(TimeSpan.FromMilliseconds(9));
        Assert.AreEqual("A\n", Latin1(connection.WrittenBytes), "Nothing more before the 10 ms are up.");
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await WaitForTimersAsync(clock, 1);
        Assert.AreEqual("A\nB\n", Latin1(connection.WrittenBytes));
        clock.Advance(TimeSpan.FromMilliseconds(10));
        await serving;

        Assert.AreEqual("A\nB\nC", Latin1(connection.WrittenBytes));
    }


    [TestMethod]
    [DataRow("POST", "skip: 4", 6)]
    [DataRow("PUT", "skip: 10", 0)]
    [DataRow("POST", "skip: 99", 0)]
    [DataRow("GET", "skip: 4", 10)]
    [DataRow("POST", "auth_required", 0)]
    [DataRow("PUT", "auth_required", 0)]
    [DataRow("GET", "auth_required", 10)]
    [DataRow("POST", "no-expect", 10)]
    public async Task BodyServerCommands_AnswerAfterReadingOnlyTheBodyBytesTheyLeave(string method, string serverCommand, int bodyBytesRead)
    {
        var head = $"{method} /1 HTTP/1.1\r\nHost: h\r\nContent-Length: 10\r\n\r\n";
        var replies = Replies([("data", Data)], serverCommand);

        var (connection, bytesReadBeforeAnswer) = await ServeCountingReadsAsync(replies, [Ascii(head), Ascii("1234567890")]);

        Assert.AreEqual(Data, Latin1(connection.WrittenBytes));
        Assert.AreEqual(head.Length + bodyBytesRead, bytesReadBeforeAnswer);
        Assert.AreEqual(head + "1234567890", Latin1(replies.ReceivedBytes), "What the next head read takes is recorded too.");
    }

    [TestMethod]
    public async Task AuthRequired_PostWithAuthorization_ReadsItsBodyFirst()
    {
        const string head = "POST /1 HTTP/1.1\r\nHost: h\r\nAuthorization: Basic eDp5\r\nContent-Length: 3\r\n\r\n";
        var replies = Replies([("data", Data)], "auth_required");

        var (connection, bytesReadBeforeAnswer) = await ServeCountingReadsAsync(replies, [Ascii(head), Ascii("abc")]);

        Assert.AreEqual(Data, Latin1(connection.WrittenBytes));
        Assert.AreEqual(head.Length + 3, bytesReadBeforeAnswer);
    }

    [TestMethod]
    public async Task NoExpect_RequestWithExpect_IsAnsweredWithoutReadingItsBodyOrA100Continue()
    {
        const string head = "PUT /1 HTTP/1.1\r\nHost: h\r\nExpect: 100-continue\r\nContent-Length: 3\r\n\r\n";
        var replies = Replies([("data", Data)], "no-expect");

        var (connection, bytesReadBeforeAnswer) = await ServeCountingReadsAsync(replies, [Ascii(head), Ascii("abc")]);

        Assert.AreEqual(Data, Latin1(connection.WrittenBytes));
        Assert.AreEqual(head.Length, bytesReadBeforeAnswer);
    }

    [TestMethod]
    public async Task ExpectContinue_WritesNo100ContinueAndAnswersOnceTheBodyIsRead()
    {
        const string request = "POST /1 HTTP/1.1\r\nHost: h\r\nExpect: 100-continue\r\nContent-Length: 3\r\n\r\nabc";
        var replies = Replies(("data", Data));

        var (connection, _) = await ServeAsync(replies, [Ascii(request)]);

        Assert.AreEqual(Data, Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ChunkedBody_IsReadToItsTrailerBeforeTheReply()
    {
        const string request = "POST /1 HTTP/1.1\r\nHost: h\r\nTransfer-Encoding: chunked\r\n\r\n3;x=y\r\nabc\r\n0\r\nTrailer: t\r\n\r\nGET /1 HTTP/1.1\r\nHost: h\r\n\r\n";
        var replies = Replies(("data", Data));

        var (connection, _) = await ServeAsync(replies, [Ascii(request)]);

        Assert.AreEqual(Data + Data, Latin1(connection.WrittenBytes));
        Assert.AreEqual(request, Latin1(replies.ReceivedBytes));
    }

    [TestMethod]
    [DataRow("zz\r\n")]
    [DataRow("FFFFFFFFFFFFFFFF\r\n")]
    [DataRow("5\r\nab")]
    [DataRow("3\r\nabc\r\n")]
    [DataRow("0\r\nTrailer: t\r\n")]
    [DataRow("")]
    public async Task ChunkedBodyThatEndsEarlyOrIsMalformed_StopsReadingAndStillAnswers(string body)
    {
        var replies = Replies(("data", Data));

        var (connection, _) = await ServeAsync(replies, [Ascii("POST /1 HTTP/1.1\r\nHost: h\r\nTransfer-Encoding: chunked\r\n\r\n" + body)]);

        Assert.AreEqual(Data, Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ContentLengthBodyThatEndsEarly_StillAnswers()
    {
        var replies = Replies(("data", Data));

        var (connection, _) = await ServeAsync(replies, [Ascii("POST /1 HTTP/1.1\r\nHost: h\r\nContent-Length: 70000\r\n\r\nabc")]);

        Assert.AreEqual(Data, Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task UnreadableBody_IsNotRead()
    {
        var replies = Replies(("data", Data));

        var (connection, _) = await ServeAsync(replies, [Ascii("POST /1 HTTP/1.1\r\nHost: h\r\nTransfer-Encoding: gzip\r\n\r\n")]);

        Assert.AreEqual(Data, Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task EmptyPart_SendsNothingAndLeavesTheConnectionOpen()
    {
        var replies = Replies(("data", string.Empty));

        var (connection, _) = await ServeAsync(replies, [Ascii("GET /1548 HTTP/1.0\r\nHost: h\r\n\r\n")]);

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsFalse(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task EmptyPart_OnAnHttp11Request_SendsNothingAndClosesTheConnection()
    {
        var replies = Replies(("data", string.Empty));

        var (connection, _) = await ServeAsync(replies, [Ascii("GET /1297 HTTP/1.1\r\nHost: h\r\n\r\n")]);

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task MissingPart_ClosesWithNoBytesAndNotesThePart()
    {
        var replies = Replies(("data", Data));

        var (connection, log) = await ServeAsync(replies, [Ascii("GET /10010003 HTTP/1.1\r\nHost: h\r\n\r\n")]);

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsTrue(log.Notes.Any(note => note.Contains("<data3>", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task MalformedHead_ClosesWithNoBytes()
    {
        var replies = Replies(("data", Data));

        var (connection, log) = await ServeAsync(replies, [Ascii("NOT A REQUEST\r\n\r\n")]);

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsTrue(log.Notes.Any(note => note.StartsWith("No request head was read", StringComparison.Ordinal)));
    }

    // The headers pinned upstream curl 8.21.0 (Git for Windows build) sent for upstream cases
    // 1455 (--haproxy-protocol), 3201 and 3220 (--haproxy-clientip), measured by BL-681.
    [TestMethod]
    [DataRow("PROXY TCP4 127.0.0.1 127.0.0.1 53211 47681\r\n")]
    [DataRow("PROXY TCP4 192.168.1.1 192.168.1.1 53212 47681\r\n")]
    [DataRow("PROXY TCP6 2a04:4e42::347 2a04:4e42::347 53213 47681\r\n")]
    public async Task ProxyHeaderBeforeTheRequest_IsRecordedAsTheFirstLineAndTheRequestAnswered(string proxyHeader)
    {
        const string request = "GET /1455 HTTP/1.1\r\nHost: 127.0.0.1:47681\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nTestno: 1455\r\n\r\n";
        var replies = Replies(("data", Data));

        var (connection, log) = await ServeAsync(replies, [Ascii(proxyHeader + request)]);

        Assert.AreEqual(Data, Latin1(connection.WrittenBytes));
        Assert.AreEqual(proxyHeader + request, Latin1(replies.ReceivedBytes));
        Assert.IsTrue(log.Notes.Contains($"Read a PROXY protocol header of {proxyHeader.Length} bytes."));
    }

    [TestMethod]
    public async Task ProxyHeaderSplitAcrossReads_IsReadPastAndTheRequestAnswered()
    {
        const string bytes = "PROXY TCP4 127.0.0.1 127.0.0.1 1 2\r\nGET /1 HTTP/1.1\r\nHost: h\r\n\r\n";
        var replies = Replies(("data", Data));

        var (connection, _) = await ServeAsync(replies, [Ascii("PRO"), Ascii("XY TCP4 127.0.0.1"), Ascii(bytes[20..])]);

        Assert.AreEqual(Data, Latin1(connection.WrittenBytes));
        Assert.AreEqual(bytes, Latin1(replies.ReceivedBytes));
    }

    [TestMethod]
    public async Task ProxyHeaderOfExactly107Bytes_IsReadPast()
    {
        var proxyHeader = "PROXY UNKNOWN " + new string('x', HttpUpstreamTestCaseProxyHeader.MaxHeaderBytes - 16) + "\r\n";
        var replies = Replies(("data", Data));

        var (connection, _) = await ServeAsync(replies, [Ascii(proxyHeader), Ascii("GET /1 HTTP/1.1\r\nHost: h\r\n\r\n")]);

        Assert.AreEqual(107, proxyHeader.Length);
        Assert.AreEqual(Data, Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ProxyHeaderOf108Bytes_IsNotTakenAsARequestAndClosesWithNoBytes()
    {
        var proxyHeader = "PROXY UNKNOWN " + new string('x', HttpUpstreamTestCaseProxyHeader.MaxHeaderBytes - 15) + "\r\n";
        var replies = Replies(("data", Data));

        var (connection, log) = await ServeAsync(replies, [Ascii(proxyHeader + "GET /1 HTTP/1.1\r\nHost: h\r\n\r\n")]);

        Assert.AreEqual(108, proxyHeader.Length);
        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsTrue(log.Notes.Any(note => note.StartsWith("A PROXY protocol header had no CRLF within 107 bytes", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("PROXY TCP4 127.0.0.1 127.0.0.1 1 2")]
    [DataRow("PROXY TCP4 127.0.0.1 127.0.0.1 1 2\nGET /1 HTTP/1.1\r\nHost: h\r\n\r\n")]
    public async Task MalformedProxyHeader_IsNotTakenAsARequestAndClosesWithNoBytes(string bytes)
    {
        var replies = Replies(("data", Data));

        var (connection, log) = await ServeAsync(replies, [Ascii(bytes)]);

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsTrue(log.Notes.Any(note => note.StartsWith("A PROXY protocol header had no CRLF within 107 bytes", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RequestWhoseFirstBytesCouldStartAProxyHeader_IsAnsweredWithEveryByteRead()
    {
        const string request = "POST /1 HTTP/1.1\r\nHost: h\r\nContent-Length: 0\r\n\r\n";
        var replies = Replies(("data", Data));

        var (connection, _) = await ServeAsync(replies, [Ascii("P"), Ascii("O"), Ascii(request[2..])]);

        Assert.AreEqual(Data, Latin1(connection.WrittenBytes));
        Assert.AreEqual(request, Latin1(replies.ReceivedBytes));
    }

    [TestMethod]
    public async Task ClientThatSendsPartOfTheSignatureAndCloses_GetsNoBytes()
    {
        var replies = Replies(("data", Data));

        var (connection, log) = await ServeAsync(replies, [Ascii("PROX")]);

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsTrue(log.Notes.Any(note => note.StartsWith("No request head was read", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Schemes_IsHttp()
    {
        CollectionAssert.AreEqual(new[] { "http" }, new HttpUpstreamTestCaseServer(Replies()).Schemes.ToArray());
    }

    [TestMethod]
    public async Task NullArguments_Throw()
    {
        var server = new HttpUpstreamTestCaseServer(Replies());
        var context = Context(new RecordingExchangeLog(), new ManualTimeProvider(Now), TestContext.CancellationToken);

        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpUpstreamTestCaseServer(null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, context));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(new InMemoryConnection([]), null!));
    }

    private static HttpUpstreamTestCaseReplies Replies(params (string Name, string Text)[] parts) => Replies(parts, []);

    private static HttpUpstreamTestCaseReplies Replies((string Name, string Text)[] parts, params string[] serverCommands) =>
        new(parts.ToDictionary(part => part.Name, part => Ascii(part.Text)), serverCommands);

    // The client sends every chunk and then half-closes, which ends the exchange.
    private async Task<(InMemoryConnection Connection, RecordingExchangeLog Log)> ServeAsync(
        HttpUpstreamTestCaseReplies replies, IEnumerable<ReadOnlyMemory<byte>> chunks)
    {
        var connection = new InMemoryConnection(chunks);
        var log = new RecordingExchangeLog();

        await new HttpUpstreamTestCaseServer(replies).ServeAsync(connection, Context(log, new ManualTimeProvider(Now), TestContext.CancellationToken));

        return (connection, log);
    }

    // As ServeAsync, saying how many bytes the server had read when it first wrote.
    private async Task<(InMemoryConnection Connection, long BytesReadBeforeAnswer)> ServeCountingReadsAsync(
        HttpUpstreamTestCaseReplies replies, IEnumerable<ReadOnlyMemory<byte>> chunks)
    {
        var connection = new InMemoryConnection(chunks);
        var counting = new ReadCountingConnection(connection);

        await new HttpUpstreamTestCaseServer(replies).ServeAsync(counting, Context(new RecordingExchangeLog(), new ManualTimeProvider(Now), TestContext.CancellationToken));

        return (connection, counting.BytesReadBeforeFirstWrite);
    }
}
