using Surl.Protocol.Abstractions;
using static Surl.Protocol.HttpProxy.ProxyTestKit;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// Forwarding absolute-form requests (ADR-0088 decision 3): what the origin receives, what
/// curl receives, and which answers the proxy makes itself.
/// </summary>
[TestClass]
public sealed class HttpProxyExchangeTests
{
    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";

    private const string RelayedOk = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nVia: 1.1 surl\r\n\r\nok";

    private static string Get(string target = "http://origin.test/a", string fields = "") =>
        $"GET {target} HTTP/1.1\r\nHost: origin.test\r\n{fields}\r\n";

    private static (InMemoryOriginConnector Origins, InMemoryConnection Origin) OneOrigin(params string[] response)
    {
        var origin = Connection(response);

        return (new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin), origin);
    }

    [TestMethod]
    [DataRow(HttpRequestHeadReadOutcomeCase.TooLarge, 431, "Request Header Fields Too Large")]
    [DataRow(HttpRequestHeadReadOutcomeCase.Malformed, 400, "Bad Request")]
    [DataRow(HttpRequestHeadReadOutcomeCase.Version, 505, "HTTP Version Not Supported")]
    public async Task ServeAsync_HeadThatCannotBeRead_AnswersAndCloses(HttpRequestHeadReadOutcomeCase head, int code, string reason)
    {
        var request = head switch
        {
            HttpRequestHeadReadOutcomeCase.TooLarge => Get(fields: "X-Big: " + new string('a', 200) + "\r\n"),
            HttpRequestHeadReadOutcomeCase.Malformed => "GET\r\n\r\n",
            _ => "GET http://origin.test/ HTTP/2.0\r\n\r\n",
        };
        var limits = ExchangeLimits.Default with { MaxRequestHeadBytes = 100 };

        var written = await ServeAsync(Connection(request), new InMemoryOriginConnector(), limits: limits);

        Assert.AreEqual(Answer(code, reason, closes: true), written);
    }

    [TestMethod]
    public async Task ServeAsync_ClientThatSendsNothing_ClosesWithoutAnswering()
    {
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(Connection(), new InMemoryOriginConnector(), log);

        Assert.AreEqual(string.Empty, written);
        CollectionAssert.Contains(log.Notes.ToList(), "No request head was read: ConnectionClosed; the connection closes.");
    }

    [TestMethod]
    public async Task ServeAsync_TwoHostFields_Answers400AndCloses()
    {
        var written = await ServeAsync(Connection(Get(fields: "Host: other.test\r\n")), new InMemoryOriginConnector());

        Assert.AreEqual(Answer(400, "Bad Request", closes: true), written);
    }

    [TestMethod]
    [DataRow("/a")]
    [DataRow("*")]
    [DataRow("http:///a")]
    public async Task ServeAsync_TargetNotInAbsoluteForm_Answers400AndCloses(string target)
    {
        var origins = new InMemoryOriginConnector();

        var written = await ServeAsync(Connection(Get(target)), origins);

        Assert.AreEqual(Answer(400, "Bad Request", closes: true), written);
        Assert.IsEmpty(origins.Requests);
    }

    [TestMethod]
    [DataRow("ftp://origin.test/a.txt")]
    [DataRow("https://origin.test/a")]
    public async Task ServeAsync_SchemeOtherThanHttp_Answers501AndKeepsTheConnection(string target)
    {
        var (origins, _) = OneOrigin(Ok);

        var written = await ServeAsync(Connection(Get(target), Get()), origins);

        Assert.AreEqual(Answer(501, "Not Implemented") + RelayedOk, written);
    }

    [TestMethod]
    [DataRow("Transfer-Encoding: gzip\r\n", 501, "Not Implemented")]
    [DataRow("Content-Length: x\r\n", 400, "Bad Request")]
    [DataRow("Content-Length: 11\r\n", 413, "Content Too Large")]
    public async Task ServeAsync_BodyTheProxyWillNotForward_AnswersAndCloses(string field, int code, string reason)
    {
        var limits = ExchangeLimits.Default with { MaxUploadBytes = 10 };

        var written = await ServeAsync(Connection(Get(fields: field)), new InMemoryOriginConnector(), limits: limits);

        Assert.AreEqual(Answer(code, reason, closes: true), written);
    }

    [TestMethod]
    public async Task ServeAsync_OptionsWithMaxForwardsZero_IsAnsweredByTheProxy()
    {
        var origins = new InMemoryOriginConnector();

        var written = await ServeAsync(Connection("OPTIONS http://origin.test/ HTTP/1.1\r\nHost: origin.test\r\nMax-Forwards: 0\r\n\r\n"), origins);

        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{DateField}Server: surl\r\nAllow: CONNECT, DELETE, GET, HEAD, OPTIONS, PATCH, POST, PUT, TRACE\r\nContent-Length: 0\r\n\r\n",
            written);
        Assert.IsEmpty(origins.Requests);
    }

    [TestMethod]
    public async Task ServeAsync_TraceWithMaxForwardsZero_EchoesTheRequestAndClosesForHttp10()
    {
        var request = "TRACE http://origin.test/ HTTP/1.0\r\nHost: origin.test\r\nMax-Forwards: 0\r\n\r\n";

        var written = await ServeAsync(Connection(request), new InMemoryOriginConnector());

        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{DateField}Server: surl\r\nContent-Type: message/http\r\nContent-Length: {request.Length}\r\nConnection: close\r\n\r\n{request}",
            written);
    }

    [TestMethod]
    public async Task ServeAsync_TraceWithMaxForwardsTwo_IsForwardedWithOne()
    {
        var (origins, origin) = OneOrigin(Ok);

        await ServeAsync(Connection("TRACE http://origin.test/ HTTP/1.1\r\nHost: origin.test\r\nMax-Forwards: 2\r\n\r\n"), origins);

        Assert.AreEqual("TRACE / HTTP/1.1\r\nHost: origin.test\r\nMax-Forwards: 1\r\nVia: 1.1 surl\r\n\r\n", Latin1(origin.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_GetWithMaxForwards_IsForwardedUnchanged()
    {
        var (origins, origin) = OneOrigin(Ok);

        await ServeAsync(Connection(Get(fields: "Max-Forwards: 0\r\n")), origins);

        Assert.AreEqual("GET /a HTTP/1.1\r\nHost: origin.test\r\nMax-Forwards: 0\r\nVia: 1.1 surl\r\n\r\n", Latin1(origin.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_PostWithContentLength_StreamsTheBodyThrough()
    {
        var (origins, origin) = OneOrigin(Ok);

        var written = await ServeAsync(Connection("POST http://origin.test/up HTTP/1.1\r\nHost: origin.test\r\nContent-Length: 5\r\n\r\nhel", "lo"), origins);

        Assert.AreEqual("POST /up HTTP/1.1\r\nHost: origin.test\r\nContent-Length: 5\r\nVia: 1.1 surl\r\n\r\nhello", Latin1(origin.WrittenBytes));
        Assert.AreEqual(RelayedOk, written);
    }

    [TestMethod]
    public async Task ServeAsync_PostWithAChunkedBody_SendsItOnChunked()
    {
        var (origins, origin) = OneOrigin(Ok);
        var request = "POST http://origin.test/up HTTP/1.1\r\nHost: origin.test\r\nTransfer-Encoding: chunked\r\n\r\n3;x=y\r\nhel\r\n2\r\nlo\r\n0\r\nX-Trailer: 1\r\n\r\n";

        await ServeAsync(Connection(request), origins);

        Assert.AreEqual(
            "POST /up HTTP/1.1\r\nHost: origin.test\r\nVia: 1.1 surl\r\nTransfer-Encoding: chunked\r\n\r\n3\r\nhel\r\n2\r\nlo\r\n0\r\n\r\n",
            Latin1(origin.WrittenBytes));
    }

    [TestMethod]
    [DataRow("5\r\nhello\r\n6\r\n world\r\n0\r\n\r\n", 413, "Content Too Large")]
    [DataRow("zz\r\n", 400, "Bad Request")]
    [DataRow("3\r\nhelXX\r\n0\r\n\r\n", 400, "Bad Request")]
    [DataRow("3\r\nhelX\n0\r\n\r\n", 400, "Bad Request")]
    [DataRow("3\r\nhe", 400, "Bad Request")]
    [DataRow("0\r\n", 400, "Bad Request")]
    [DataRow("3\r\nhel", 400, "Bad Request")]
    [DataRow("", 400, "Bad Request")]
    [DataRow("0000000000000001\r\nx\r\n0\r\n\r\n", 400, "Bad Request")]
    public async Task ServeAsync_ChunkedBodyTheProxyCannotSendOn_AnswersAndCloses(string body, int code, string reason)
    {
        var (origins, origin) = OneOrigin(Ok);
        var limits = ExchangeLimits.Default with { MaxUploadBytes = 10 };

        var written = await ServeAsync(Connection("POST http://origin.test/ HTTP/1.1\r\nHost: origin.test\r\nTransfer-Encoding: chunked\r\n\r\n" + body), origins, limits: limits);

        Assert.AreEqual(Answer(code, reason, closes: true), written);
        Assert.IsTrue(origin.Disposed);
    }

    [TestMethod]
    public async Task ServeAsync_ContentLengthBodyCutShort_Answers400AndCloses()
    {
        var (origins, _) = OneOrigin(Ok);

        var written = await ServeAsync(Connection("PUT http://origin.test/ HTTP/1.1\r\nHost: origin.test\r\nContent-Length: 9\r\n\r\nabc"), origins);

        Assert.AreEqual(Answer(400, "Bad Request", closes: true), written);
    }

    [TestMethod]
    public async Task ServeAsync_ExpectContinueAnswered100_RelaysTheInterimThenSendsTheBody()
    {
        var (origins, origin) = OneOrigin("HTTP/1.1 100 Continue\r\n\r\n", Ok);
        var request = "PUT http://origin.test/f HTTP/1.1\r\nHost: origin.test\r\nContent-Length: 2\r\nExpect: 100-continue\r\n\r\nhi";

        var written = await ServeAsync(Connection(request), origins);

        Assert.AreEqual("HTTP/1.1 100 Continue\r\nVia: 1.1 surl\r\n\r\n" + RelayedOk, written);
        Assert.EndsWith("Expect: 100-continue\r\nVia: 1.1 surl\r\n\r\nhi", Latin1(origin.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_ExpectContinueAnsweredFinal_RelaysItWithoutTheBodyAndCloses()
    {
        var (origins, origin) = OneOrigin("HTTP/1.1 417 Expectation Failed\r\nContent-Length: 0\r\n\r\n");
        var request = "PUT http://origin.test/f HTTP/1.1\r\nHost: origin.test\r\nContent-Length: 2\r\nExpect: 100-continue\r\n\r\nhi";

        var written = await ServeAsync(Connection(request), origins);

        Assert.AreEqual("HTTP/1.1 417 Expectation Failed\r\nContent-Length: 0\r\nVia: 1.1 surl\r\nConnection: close\r\n\r\n", written);
        Assert.EndsWith("Via: 1.1 surl\r\n\r\n", Latin1(origin.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_ExpectContinueToAnOriginThatSendsNothing_Answers502AndCloses()
    {
        var (origins, _) = OneOrigin();
        var request = "PUT http://origin.test/f HTTP/1.1\r\nHost: origin.test\r\nContent-Length: 2\r\nExpect: 100-continue\r\n\r\nhi";

        var written = await ServeAsync(Connection(request), origins);

        Assert.AreEqual(Answer(502, "Bad Gateway", closes: true), written);
    }

    [TestMethod]
    public async Task ServeAsync_BareLineFeedsFromBothSides_AreRead()
    {
        var (origins, origin) = OneOrigin("HTTP/1.1 200 OK\nTransfer-Encoding: chunked\n\n2\nok\n0\n\n");
        var request = "POST http://origin.test/ HTTP/1.1\r\nHost: origin.test\r\nTransfer-Encoding: chunked\r\n\r\n2\nhi\n0\n\n";

        var written = await ServeAsync(Connection(request), origins);

        Assert.AreEqual("HTTP/1.1 200 OK\r\nVia: 1.1 surl\r\nTransfer-Encoding: chunked\r\n\r\n2\r\nok\r\n0\r\n\r\n", written);
        Assert.EndsWith("\r\n\r\n2\r\nhi\r\n0\r\n\r\n", Latin1(origin.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_InterimResponseBeforeTheFinal_IsRelayed()
    {
        var (origins, _) = OneOrigin("HTTP/1.1 103 Early Hints\r\nLink: </s.css>\r\n\r\n" + Ok);

        var written = await ServeAsync(Connection(Get()), origins);

        Assert.AreEqual("HTTP/1.1 103 Early Hints\r\nLink: </s.css>\r\nVia: 1.1 surl\r\n\r\n" + RelayedOk, written);
    }

    [TestMethod]
    public async Task ServeAsync_UpgradeAnswered101_RelaysTheHeadThenBytesBothWays()
    {
        var (origins, origin) = OneOrigin("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n\r\nfrom-origin");
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(Connection(Get(fields: "Upgrade: websocket\r\nConnection: Upgrade\r\n"), "from-curl"), origins, log);

        Assert.AreEqual("HTTP/1.1 101 Switching Protocols\r\nVia: 1.1 surl\r\nConnection: Upgrade\r\nUpgrade: websocket\r\n\r\nfrom-origin", written);
        Assert.AreEqual("GET /a HTTP/1.1\r\nHost: origin.test\r\nVia: 1.1 surl\r\nConnection: Upgrade\r\nUpgrade: websocket\r\n\r\nfrom-curl", Latin1(origin.WrittenBytes));
        CollectionAssert.Contains(log.Notes.ToList(), "The upgraded connection to origin.test:80 closed.");
    }

    [TestMethod]
    public async Task ServeAsync_UpgradedConnectionThatFails_AbortsBoth()
    {
        var origin = new FailingConnection(Connection("HTTP/1.1 101 Switching Protocols\r\nUpgrade: x\r\nConnection: Upgrade\r\n\r\n"), resetsInsteadOfClosing: true);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin);
        var client = HeldOpenConnection(Get(fields: "Upgrade: x\r\nConnection: Upgrade\r\n"));
        var log = new RecordingExchangeLog();

        await new HttpProxyProtocolServer(origins).ServeAsync(client, Context(log));

        Assert.IsTrue(client.Aborted);
        CollectionAssert.Contains(log.Notes.ToList(), "The upgraded connection to origin.test:80 failed; both connections were aborted.");
    }

    [TestMethod]
    public async Task ServeAsync_TwoRequestsToTheSameOrigin_UseOneOriginConnection()
    {
        var (origins, origin) = OneOrigin(Ok + Ok);

        var written = await ServeAsync(Connection(Get(), Get("http://ORIGIN.test:80/b")), origins);

        Assert.AreEqual(RelayedOk + RelayedOk, written);
        Assert.HasCount(1, origins.Requests);
        Assert.AreEqual(Get().Replace("http://origin.test/a", "/a").Replace("\r\n\r\n", "\r\nVia: 1.1 surl\r\n\r\n") + "GET /b HTTP/1.1\r\nHost: ORIGIN.test:80\r\nVia: 1.1 surl\r\n\r\n", Latin1(origin.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_RequestToAnotherOrigin_ReplacesTheOriginConnection()
    {
        var first = Connection(Ok);
        var second = Connection(Ok);
        var origins = new InMemoryOriginConnector()
            .ScriptConnection("origin.test", 80, first)
            .ScriptConnection("other.test", 8080, second);

        var written = await ServeAsync(Connection(Get(), Get("http://other.test:8080/")), origins);

        Assert.AreEqual(RelayedOk + RelayedOk, written);
        Assert.IsTrue(first.Disposed);
        Assert.IsTrue(second.Disposed);
    }

    [TestMethod]
    public async Task ServeAsync_KeptOriginClosedBetweenRequests_ReconnectsOnceForAGet()
    {
        var first = Connection(Ok);
        var second = Connection(Ok);
        var origins = new InMemoryOriginConnector()
            .ScriptConnection("origin.test", 80, first)
            .ScriptConnection("origin.test", 80, second);
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(Connection(Get(), Get()), origins, log);

        Assert.AreEqual(RelayedOk + RelayedOk, written);
        Assert.HasCount(2, origins.Requests);
        CollectionAssert.Contains(log.Notes.ToList(), "Origin origin.test:80 closed the kept connection; reconnecting once.");
    }

    [TestMethod]
    public async Task ServeAsync_KeptOriginClosedBeforeAPost_Answers502()
    {
        var (origins, _) = OneOrigin(Ok);

        var written = await ServeAsync(Connection(Get(), "POST http://origin.test/ HTTP/1.1\r\nHost: origin.test\r\nContent-Length: 1\r\n\r\nx"), origins);

        Assert.AreEqual(RelayedOk + Answer(502, "Bad Gateway", closes: true), written);
    }

    [TestMethod]
    public async Task ServeAsync_KeptOriginThatFailsOnWrite_ReconnectsOnce()
    {
        var first = new FailingConnection(HeldOpenConnection(Ok), writesBeforeFailure: 1);
        var second = Connection(Ok);
        var origins = new InMemoryOriginConnector()
            .ScriptConnection("origin.test", 80, first)
            .ScriptConnection("origin.test", 80, second);

        var written = await ServeAsync(Connection(Get(), Get()), origins);

        Assert.AreEqual(RelayedOk + RelayedOk, written);
        Assert.HasCount(2, origins.Requests);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 2000 OK\r\n\r\n")]
    [DataRow("SSH-2.0-OpenSSH\r\n")]
    [DataRow("HTTP/1.1 200 OK\r\nBad Field\r\n\r\n")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nContent-Length: 3\r\n\r\nok")]
    [DataRow("")]
    [DataRow("HTTP/1.1 200 OK")]
    [DataRow("HTTP/1.1 200 OK\r\nX\n\r\n")]
    public async Task ServeAsync_OriginResponseTheProxyCannotRead_Answers502(string response)
    {
        var (origins, origin) = OneOrigin(response);

        var written = await ServeAsync(Connection(Get()), origins);

        Assert.AreEqual(Answer(502, "Bad Gateway"), written);
        Assert.IsTrue(origin.Disposed);
    }

    [TestMethod]
    public async Task ServeAsync_OriginResponseHeadOverTheLimit_Answers502()
    {
        var (origins, _) = OneOrigin("HTTP/1.1 200 OK\r\nX-Big: " + new string('a', 200) + "\r\n\r\n");
        var limits = ExchangeLimits.Default with { MaxRequestHeadBytes = 120 };

        var written = await ServeAsync(Connection(Get()), origins, limits: limits);

        Assert.AreEqual(Answer(502, "Bad Gateway"), written);
    }

    [TestMethod]
    public async Task ServeAsync_OriginResponseHeadFillingTheLimitWithItsStatusLine_Answers502()
    {
        var statusLine = "HTTP/1.1 200 " + new string('x', 25) + "\r\n";
        var origins = new InMemoryOriginConnector().ScriptConnection("o", 80, Connection(statusLine + "\r\n"));
        var limits = ExchangeLimits.Default with { MaxRequestHeadBytes = statusLine.Length };

        var written = await ServeAsync(Connection("GET http://o/ HTTP/1.1\r\n\r\n"), origins, limits: limits);

        Assert.AreEqual(Answer(502, "Bad Gateway"), written);
    }

    [TestMethod]
    public async Task ServeAsync_NoHeadLimit_ReadsTheOriginResponse()
    {
        var (origins, _) = OneOrigin(Ok);
        var limits = ExchangeLimits.Default with { MaxRequestHeadBytes = 0, HeadTimeout = TimeSpan.FromDays(60) };

        var written = await ServeAsync(Connection(Get()), origins, limits: limits);

        Assert.AreEqual(RelayedOk, written);
    }

    [TestMethod]
    public async Task ServeAsync_OriginThatDoesNotAnswerInTime_Answers504()
    {
        var origin = HeldOpenConnection();
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin);
        var clock = new ManualTimeProvider(Now);
        var log = new RecordingExchangeLog();
        var client = Connection(Get());

        var serving = new HttpProxyProtocolServer(origins).ServeAsync(client, Context(log, clock: clock));
        clock.Advance(ExchangeLimits.Default.HeadTimeout);
        await serving;

        Assert.AreEqual(Answer(504, "Gateway Timeout").Replace("12:00:00", "12:00:30"), Latin1(client.WrittenBytes));
        Assert.IsTrue(origin.Disposed);
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledWhileTheOriginIsAwaited_Throws()
    {
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, HeldOpenConnection());
        using var cancellation = new CancellationTokenSource();
        var serving = new HttpProxyProtocolServer(origins).ServeAsync(Connection(Get()), Context(new RecordingExchangeLog(), cancellationToken: cancellation.Token));

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    [TestMethod]
    public async Task ServeAsync_ChunkedResponse_IsRelayedChunked()
    {
        var (origins, _) = OneOrigin("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n2\r\nok\r\n0\r\n\r\n");

        var written = await ServeAsync(Connection(Get()), origins);

        Assert.AreEqual("HTTP/1.1 200 OK\r\nVia: 1.1 surl\r\nTransfer-Encoding: chunked\r\n\r\n2\r\nok\r\n0\r\n\r\n", written);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\n\r\nall of it")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip\r\n\r\nall of it")]
    public async Task ServeAsync_CloseDelimitedResponse_IsRelayedAndClosesCurlsConnection(string response)
    {
        var (origins, origin) = OneOrigin(response);
        var client = Connection(Get(), Get());

        var written = await ServeAsync(client, origins);

        Assert.AreEqual("HTTP/1.1 200 OK\r\nVia: 1.1 surl\r\nConnection: close\r\n\r\nall of it", written);
        Assert.IsTrue(client.WritesCompleted);
        Assert.IsTrue(origin.Disposed);
    }

    [TestMethod]
    [DataRow("HEAD", "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\n", "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nVia: 1.1 surl\r\n\r\n")]
    [DataRow("GET", "HTTP/1.1 304 Not Modified\r\nContent-Length: 5\r\n\r\n", "HTTP/1.1 304 Not Modified\r\nContent-Length: 5\r\nVia: 1.1 surl\r\n\r\n")]
    [DataRow("GET", "HTTP/1.1 204 \r\n\r\n", "HTTP/1.1 204 \r\nVia: 1.1 surl\r\n\r\n")]
    public async Task ServeAsync_ResponseWithoutABody_RelaysOnlyTheHead(string method, string response, string relayed)
    {
        var (origins, _) = OneOrigin(response);

        var written = await ServeAsync(Connection($"{method} http://origin.test/ HTTP/1.1\r\nHost: origin.test\r\n\r\n"), origins);

        Assert.AreEqual(relayed, written);
    }

    [TestMethod]
    public async Task ServeAsync_OriginThatClosesAfterItsResponse_IsReconnectedForTheNextRequest()
    {
        var first = Connection("HTTP/1.0 200 OK\r\nContent-Length: 2\r\n\r\nok");
        var second = Connection("HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 2\r\n\r\nok");
        var origins = new InMemoryOriginConnector()
            .ScriptConnection("origin.test", 80, first)
            .ScriptConnection("origin.test", 80, second);

        var written = await ServeAsync(Connection(Get(), Get()), origins);

        Assert.AreEqual(RelayedOk + RelayedOk, written);
        Assert.HasCount(2, origins.Requests);
        Assert.IsTrue(first.Disposed);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nok")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nok")]
    public async Task ServeAsync_ResponseBodyCutShort_AbortsCurlsConnection(string response)
    {
        var (origins, origin) = OneOrigin(response);
        var client = Connection(Get());
        var log = new RecordingExchangeLog();

        await ServeAsync(client, origins, log);

        Assert.IsTrue(client.Aborted);
        Assert.IsTrue(origin.Disposed);
        CollectionAssert.Contains(log.Notes.ToList(), "The origin's response body ended early or was malformed; both connections closed.");
    }

    [TestMethod]
    public async Task ServeAsync_RequestAskingToClose_GetsConnectionCloseAndTheConnectionCloses()
    {
        var (origins, _) = OneOrigin(Ok);
        var client = Connection(Get(fields: "Connection: close\r\n"), Get());

        var written = await ServeAsync(client, origins);

        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nVia: 1.1 surl\r\nConnection: close\r\n\r\nok", written);
        Assert.IsTrue(client.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_ForwardedRequestToAnOriginThatTimesOutConnecting_Answers504()
    {
        var origins = new InMemoryOriginConnector().ScriptFailure("origin.test", 80, OriginConnectionFailure.TimedOut);

        var written = await ServeAsync(Connection(Get()), origins);

        Assert.AreEqual(Answer(504, "Gateway Timeout"), written);
    }
}

/// <summary>Which head-reading failure a data row exercises.</summary>
public enum HttpRequestHeadReadOutcomeCase
{
    TooLarge,
    Malformed,
    Version,
}
