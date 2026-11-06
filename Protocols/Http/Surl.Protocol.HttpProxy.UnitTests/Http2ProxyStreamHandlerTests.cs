using Surl.Http2;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.HttpProxy.ProxyTestKit;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// One stream of an HTTP/2 connection to the HTTPS proxy (ADR-0088 Amendment 5), served by
/// <see cref="Http2ProxyStreamHandler"/> on an <see cref="Http2TestStream"/>, so each answer is
/// read back as the frames the proxy wrote. The request heads are the shapes pinned upstream
/// curl 8.21.0 sends with <c>--proxy-http2</c> (<c>Fixtures/https-proxy-h2-*</c>): a
/// <c>CONNECT</c> with only <c>:authority</c>, and a request with <c>:scheme http</c>,
/// <c>:authority</c> and <c>:path</c>.
/// </summary>
[TestClass]
public sealed class Http2ProxyStreamHandlerTests
{
    private const string OkResponse = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeStreamAsync_ConnectWithoutProxyLogin_Answers407WithEveryChallengeAndConnectsNothing()
    {
        var origins = new InMemoryOriginConnector();
        var policy = new UnitTestProxyAuthenticationPolicy();

        var (response, _) = await ServeAsync(Connect("origin.test:443"), origins, policy: policy);

        Assert.AreEqual("407", response.Status);
        CollectionAssert.AreEqual(
            UnitTestProxyAuthenticationPolicy.Challenges,
            response.Fields.Where(field => field.Name == "proxy-authenticate").Select(field => field.Value).ToArray());
        CollectionAssert.AreEqual(
            new[] { ":status", "date", "server", "proxy-authenticate", "proxy-authenticate", "content-type", "content-length" },
            response.Fields.Select(field => field.Name).ToArray());
        Assert.AreEqual("407 Proxy Authentication Required\n", response.BodyText);
        Assert.IsTrue(response.EndedOnData);
        Assert.AreEqual("origin.test:443", policy.JudgedRequests.Single().Target);
        Assert.IsEmpty(origins.Requests);
    }

    [TestMethod]
    public async Task ServeStreamAsync_PolicyForbids_Answers403()
    {
        var policy = new UnitTestProxyAuthenticationPolicy { RefusalOutcome = HttpAuthenticationOutcome.Forbidden };

        var (response, _) = await ServeAsync(Request("GET", "/a"), new InMemoryOriginConnector(), policy: policy);

        Assert.AreEqual("403", response.Status);
        Assert.AreEqual("403 Forbidden\n", response.BodyText);
    }

    [TestMethod]
    public async Task ServeStreamAsync_BasicProxyLogin_NotesTheLoginAndForwardsWithoutProxyAuthorization()
    {
        var origin = Connection(OkResponse);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin);
        var policy = new UnitTestProxyAuthenticationPolicy { CheckedLogin = new CheckedLogin("Basic", "u", true) };
        var log = new RecordingExchangeLog();

        var (response, _) = await ServeAsync(Request("GET", "/a", ("proxy-authorization", "Basic dTpw")), origins, policy: policy, log: log);

        Assert.AreEqual("200", response.Status);
        Assert.AreEqual("GET /a HTTP/1.1\r\nHost: origin.test\r\nVia: 1.1 surl\r\n\r\n", Latin1(origin.WrittenBytes));
        Assert.AreEqual("/a", policy.JudgedRequests.Single().Target);
        CollectionAssert.Contains(log.Notes.ToList(), new CheckedLogin("Basic", "u", true).Note);
    }

    [TestMethod]
    public async Task ServeStreamAsync_ConnectTargetWithoutPort_Answers400()
    {
        var (response, _) = await ServeAsync(Connect("origin.test"), new InMemoryOriginConnector());

        Assert.AreEqual("400", response.Status);
        Assert.AreEqual("400 Bad Request\n", response.BodyText);
    }

    [TestMethod]
    [DataRow(OriginConnectionFailure.Refused, "502")]
    [DataRow(OriginConnectionFailure.NotAllowed, "403")]
    [DataRow(OriginConnectionFailure.TimedOut, "504")]
    public async Task ServeStreamAsync_ConnectToAnOriginThatCannotBeUsed_AnswersDecisionTwosStatus(OriginConnectionFailure failure, string status)
    {
        var origins = new InMemoryOriginConnector().ScriptFailure("origin.test", 443, failure);
        var log = new RecordingExchangeLog();

        var (response, _) = await ServeAsync(Connect("origin.test:443"), origins, log: log);

        Assert.AreEqual(status, response.Status);
        CollectionAssert.Contains(log.Notes.ToList(), HttpProxyStatus.ForOriginFailure(failure, "origin.test", 443).LogLine);
    }

    [TestMethod]
    public async Task ServeStreamAsync_ConnectBothSidesClose_RelaysBothWaysAndHalfClosesTheOrigin()
    {
        var origin = Connection("pong");
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 443, origin);
        var log = new RecordingExchangeLog();

        var (response, stream) = await ServeAsync(Connect("origin.test:443"), origins, body: test => { test.Receive("ping"); test.End(); }, log: log);

        Assert.AreEqual("200", response.Status);
        Assert.AreEqual("pong", response.BodyText);
        Assert.IsTrue(response.EndedOnData);
        Assert.AreEqual("ping", Latin1(origin.WrittenBytes));
        Assert.IsTrue(origin.WritesCompleted);
        Assert.IsEmpty(stream.Resets);
        CollectionAssert.Contains(log.Notes.ToList(), "Stream 1: the tunnel to origin.test:443 closed.");
    }

    [TestMethod]
    public async Task ServeStreamAsync_ConnectOriginFailsToRead_ResetsConnectErrorOnceAndAbortsTheOrigin()
    {
        var inner = new InMemoryConnection([]);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 443, new FailingConnection(inner, resetsInsteadOfClosing: true));
        var log = new RecordingExchangeLog();

        var (response, stream) = await ServeAsync(Connect("origin.test:443"), origins, body: _ => { }, log: log);

        Assert.AreEqual("200", response.Status);
        Assert.AreEqual(Http2ErrorCode.ConnectError, stream.Resets.Single().Code);
        Assert.IsTrue(inner.Aborted);
        CollectionAssert.Contains(log.Notes.ToList(), "Stream 1: the tunnel to origin.test:443 failed.");
    }

    [TestMethod]
    public async Task ServeStreamAsync_ConnectOriginFailsToWrite_ResetsConnectErrorOnce()
    {
        var inner = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 443, new FailingConnection(inner, writesBeforeFailure: 0));

        var (_, stream) = await ServeAsync(Connect("origin.test:443"), origins, body: test => { test.Receive("ping"); test.End(); });

        Assert.AreEqual(Http2ErrorCode.ConnectError, stream.Resets.Single().Code);
        Assert.IsTrue(inner.Aborted);
    }

    [TestMethod]
    public async Task ServeStreamAsync_ConnectCurlAbandonsTheStream_AbortsTheOrigin()
    {
        var origin = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 443, origin);

        var (_, stream) = await ServeAsync(Connect("origin.test:443"), origins, body: test => test.Abandon());

        Assert.IsTrue(origin.Aborted);
        Assert.AreEqual(Http2ErrorCode.ConnectError, stream.Resets.Single().Code);
    }

    [TestMethod]
    public async Task ServeStreamAsync_ForwardFtpScheme_Answers501()
    {
        var (response, _) = await ServeAsync(Request("GET", "/a.txt") with { Scheme = "ftp" }, new InMemoryOriginConnector());

        Assert.AreEqual("501", response.Status);
    }

    [TestMethod]
    public async Task ServeStreamAsync_ForwardWithoutAuthority_Answers400()
    {
        var (response, _) = await ServeAsync(Request("GET", "/a") with { Authority = null }, new InMemoryOriginConnector());

        Assert.AreEqual("400", response.Status);
    }

    [TestMethod]
    public async Task ServeStreamAsync_ForwardContentLengthOverTheUploadLimit_Answers413WithoutConnecting()
    {
        var origins = new InMemoryOriginConnector();

        var (response, _) = await ServeAsync(Request("POST", "/a") with { ContentLength = 11 }, origins, body: test => test.End(), limits: ExchangeLimits.Default with { MaxUploadBytes = 10 });

        Assert.AreEqual("413", response.Status);
        Assert.IsEmpty(origins.Requests);
    }

    [TestMethod]
    public async Task ServeStreamAsync_ForwardToAnOriginThatDoesNotResolve_Answers502()
    {
        var origins = new InMemoryOriginConnector().ScriptFailure("origin.test", 80, OriginConnectionFailure.HostNotFound);

        var (response, _) = await ServeAsync(Request("GET", "/a"), origins);

        Assert.AreEqual("502", response.Status);
    }

    [TestMethod]
    public async Task ServeStreamAsync_ForwardBodyWithContentLength_SendsItAsIs()
    {
        var origin = Connection(OkResponse);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin);
        var head = Request("POST", "/a", ("content-length", "3")) with { ContentLength = 3 };

        var (response, _) = await ServeAsync(head, origins, body: test => { test.Receive("abc"); test.End(); });

        Assert.AreEqual("200", response.Status);
        Assert.AreEqual("POST /a HTTP/1.1\r\nHost: origin.test\r\ncontent-length: 3\r\nVia: 1.1 surl\r\n\r\nabc", Latin1(origin.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeStreamAsync_ForwardBodyWithoutContentLength_SendsItChunked()
    {
        var origin = Connection(OkResponse);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin);

        await ServeAsync(Request("POST", "/a"), origins, body: test => { test.Receive("ab"); test.End(); });

        Assert.AreEqual("POST /a HTTP/1.1\r\nHost: origin.test\r\nVia: 1.1 surl\r\nTransfer-Encoding: chunked\r\n\r\n2\r\nab\r\n0\r\n\r\n", Latin1(origin.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeStreamAsync_ForwardChunkedBodyOverTheUploadLimit_Answers413()
    {
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, Connection(OkResponse));

        var (response, _) = await ServeAsync(Request("POST", "/a"), origins, body: test => { test.Receive("abc"); test.End(); }, limits: ExchangeLimits.Default with { MaxUploadBytes = 2 });

        Assert.AreEqual("413", response.Status);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ServeStreamAsync_ForwardBodyWithNoUploadLimit_SendsIt(bool announcesContentLength)
    {
        var origin = Connection(OkResponse);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin);
        var head = announcesContentLength ? Request("POST", "/a", ("content-length", "3")) with { ContentLength = 3 } : Request("POST", "/a");

        var (response, _) = await ServeAsync(head, origins, body: test => { test.Receive("abc"); test.End(); }, limits: ExchangeLimits.Default with { MaxUploadBytes = 0 });

        Assert.AreEqual("200", response.Status);
        StringAssert.Contains(Latin1(origin.WrittenBytes), "abc");
    }

    [TestMethod]
    public async Task ServeStreamAsync_ForwardBodyShortOfItsContentLength_ResetsProtocolError()
    {
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, Connection(OkResponse));
        var head = Request("POST", "/a", ("content-length", "3")) with { ContentLength = 3 };

        var (_, stream) = await ServeAsync(head, origins, body: test => { test.Receive("ab"); test.End(); });

        Assert.AreEqual(Http2ErrorCode.ProtocolError, stream.Resets.Single().Code);
    }

    [TestMethod]
    public async Task ServeStreamAsync_ForwardBodyCurlAbandons_AnswersNothing()
    {
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, Connection(OkResponse));
        var log = new RecordingExchangeLog();

        var (response, stream) = await ServeAsync(Request("POST", "/a"), origins, body: test => { test.Receive("a"); test.Abandon(); }, log: log);

        Assert.IsEmpty(response.HeaderBlocks);
        Assert.IsEmpty(stream.Resets);
        CollectionAssert.Contains(log.Notes.ToList(), "Stream 1: curl ended the stream before its request body ended.");
    }

    [TestMethod]
    public async Task ServeStreamAsync_ForwardOriginClosedBeforeTheRequestWasSent_Answers502()
    {
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, new FailingConnection(Connection(OkResponse), writesBeforeFailure: 0));

        var (response, _) = await ServeAsync(Request("GET", "/a"), origins);

        Assert.AreEqual("502", response.Status);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("garbage\r\n\r\n")]
    [DataRow("HTTP/1.1 101 Switching Protocols\r\nUpgrade: h2c\r\n\r\n")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 1\r\nContent-Length: 2\r\n\r\nok")]
    public async Task ServeStreamAsync_ForwardOriginAnswersNothingHttp2CanRelay_Answers502(string originResponse)
    {
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, Connection(originResponse));

        var (response, _) = await ServeAsync(Request("GET", "/a"), origins);

        Assert.AreEqual("502", response.Status);
    }

    [TestMethod]
    public async Task ServeStreamAsync_ForwardOriginConnectionFailsBeforeAnswering_Answers502()
    {
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, new FailingConnection(Connection(), resetsInsteadOfClosing: true));

        var (response, _) = await ServeAsync(Request("GET", "/a"), origins);

        Assert.AreEqual("502", response.Status);
    }

    [TestMethod]
    public async Task ServeStreamAsync_ForwardOriginSilentPastTheHeadTimeout_Answers504()
    {
        var clock = new ManualTimeProvider(Now);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, HeldOpenConnection());

        var serving = ServeAsync(Request("GET", "/a"), origins, clock: clock);
        await Http2TestClient.WaitUntilAsync(() => clock.ActiveTimerCount > 0);
        clock.Advance(ExchangeLimits.Default.HeadTimeout);
        var (response, _) = await serving;

        Assert.AreEqual("504", response.Status);
    }

    [TestMethod]
    public async Task ServeStreamAsync_ForwardOriginSendsAnInterim100_RelaysItAsAHeaderBlockBeforeTheFinal()
    {
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, Connection("HTTP/1.1 100 Continue\r\n\r\n" + OkResponse));

        var (response, _) = await ServeAsync(Request("GET", "/a"), origins);

        CollectionAssert.AreEqual(new[] { "100", "200" }, response.HeaderBlocks.Select(block => block.Single(field => field.Name == ":status").Value).ToArray());
        Assert.AreEqual("ok", response.BodyText);
    }

    [TestMethod]
    [DataRow("HEAD", "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\n")]
    [DataRow("GET", "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n")]
    [DataRow("GET", "HTTP/1.1 204 No Content\r\n\r\n")]
    public async Task ServeStreamAsync_ForwardResponseWithoutBody_EndsTheStreamOnItsHeaders(string method, string originResponse)
    {
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, Connection(originResponse));

        var (response, _) = await ServeAsync(Request(method, "/a"), origins);

        Assert.IsTrue(response.EndedOnHeaders);
        Assert.IsEmpty(response.Body);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nConnection: keep-alive\r\n\r\n2\r\nok\r\n0\r\n\r\n")]
    [DataRow("HTTP/1.1 200 OK\r\n\r\nok")]
    public async Task ServeStreamAsync_ForwardChunkedOrCloseDelimitedResponse_RelaysTheDataWithoutHopByHopFields(string originResponse)
    {
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, Connection(originResponse));

        var (response, _) = await ServeAsync(Request("GET", "/a"), origins);

        Assert.AreEqual("ok", response.BodyText);
        Assert.IsTrue(response.EndedOnData);
        CollectionAssert.AreEqual(new[] { ":status", "via" }, response.Fields.Select(field => field.Name).ToArray());
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nok", false)]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\nzz\r\n", false)]
    [DataRow("HTTP/1.1 200 OK\r\n\r\nok", true)]
    public async Task ServeStreamAsync_ForwardResponseBodyCutShort_ResetsInternalError(string originResponse, bool resets)
    {
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, new FailingConnection(Connection(originResponse), resetsInsteadOfClosing: resets));
        var log = new RecordingExchangeLog();

        var (response, stream) = await ServeAsync(Request("GET", "/a"), origins, log: log);

        Assert.AreEqual("200", response.Status);
        Assert.AreEqual(Http2ErrorCode.InternalError, stream.Resets.Single().Code);
        CollectionAssert.Contains(log.Notes.ToList(), "Stream 1: the origin's response body ended early or was malformed; reset INTERNAL_ERROR.");
    }

    private static Http2RequestHead Connect(string authority) => new("CONNECT", authority, authority, [], null, false);

    private static Http2RequestHead Request(string method, string path, params (string Name, string Value)[] fields) =>
        new(method, path, "origin.test", fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value)).ToList(), null, false) { Scheme = "http" };

    // Serves one stream; body, when given, announces a request body and feeds it before the
    // handler runs.
    private static async Task<(Http2TestResponse Response, Http2TestStream Stream)> ServeAsync(
        Http2RequestHead head,
        InMemoryOriginConnector origins,
        Action<Http2TestStream>? body = null,
        IAuthenticationPolicy? policy = null,
        RecordingExchangeLog? log = null,
        ExchangeLimits? limits = null,
        TimeProvider? clock = null)
    {
        var stream = new Http2TestStream(head, isBodyAnnounced: body is not null, CancellationToken.None);
        body?.Invoke(stream);
        var context = Context(log ?? new RecordingExchangeLog(), limits, clock);
        using var handler = new Http2ProxyStreamHandler(context, origins, (policy ?? new AnonymousAuthenticationPolicy()).StartHttpConnection(null));

        await handler.ServeStreamAsync(stream.Stream, CancellationToken.None);

        return (Http2TestClient.Responses(stream.Connection.WrittenBytes).GetValueOrDefault(1) ?? new Http2TestResponse(), stream);
    }
}
