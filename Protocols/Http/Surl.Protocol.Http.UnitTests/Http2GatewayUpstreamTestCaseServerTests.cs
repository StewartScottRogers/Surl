using Surl.Protocol.Abstractions;
using static Surl.Http2.Http2TestClient;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// The <c>http/2</c> gateway half of upstream test cases (ADR-0154), driven with HTTP/2 frames
/// written from RFC 9113 and case 2403's and 1701's bytes through <see cref="InMemoryConnection"/>,
/// with an <see cref="InMemoryOriginConnector"/> standing in for the case's <c>http</c> half.
/// </summary>
[TestClass]
public sealed class Http2GatewayUpstreamTestCaseServerTests
{
    private const int BackendPort = 4242;

    private const string Reply2403 =
        "HTTP/1.1 200 OK\r\nDate: Tue, 09 Nov 2010 14:49:00 GMT\r\nContent-Length: 6\r\nConnection: close\r\nContent-Type: text/html\r\nFunny-head: yesyes\r\n\r\n-foo-\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Get2403_ForwardsAnHttp11RequestAndAnswersTheStreamWithTheTranslatedReply()
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Headers(1, RequestFields("GET", "/2403", ("user-agent", "curl/8"), ("accept", "*/*")), endStream: true));
        var backend = Backend(Reply2403, peerHalfCloses: false);

        var served = await ServeAsync(request, backend);

        Assert.AreEqual(
            "GET /2403 HTTP/1.1\r\nHost: 127.0.0.1:18050\r\nUser-Agent: curl/8\r\nAccept: */*\r\nX-Forwarded-Proto: http\r\nVia: 2 nghttpx\r\n\r\n",
            Latin1(backend.WrittenBytes));
        var response = Responses(served.WrittenBytes)[1];
        Assert.AreEqual("200", response.Status);
        CollectionAssert.AreEqual(
            new[]
            {
                "date=Tue, 09 Nov 2010 14:49:00 GMT",
                "content-length=6",
                "content-type=text/html",
                "funny-head=yesyes",
                "server=nghttpx",
                "via=1.1 nghttpx",
            },
            response.Fields.Skip(1).Select(field => $"{field.Name}={field.Value}").ToArray());
        Assert.AreEqual("-foo-\n", response.BodyText);
        Assert.IsTrue(response.EndedOnData);
    }

    [TestMethod]
    public async Task PostWithABody_ForwardsTheBodyAndTheHeadAnswerEndsOnItsHeaders()
    {
        var client = new Http2TestClient();
        var request = Concat(
            Start(),
            client.Headers(1, RequestFields("POST", "/1701", ("content-length", "10")), endStream: false),
            Data(1, "datatosend", endStream: true));
        var backend = Backend("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");

        var served = await ServeAsync(request, backend);

        Assert.AreEqual(
            "POST /1701 HTTP/1.1\r\nHost: 127.0.0.1:18050\r\nContent-Length: 10\r\nX-Forwarded-Proto: http\r\nVia: 2 nghttpx\r\n\r\ndatatosend",
            Latin1(backend.WrittenBytes));
        var response = Responses(served.WrittenBytes)[1];
        Assert.AreEqual("200", response.Status);
        Assert.IsTrue(response.EndedOnHeaders);
    }

    [TestMethod]
    public async Task TwoStreamsOnOneConnection_AreEachForwardedAndAnsweredOnTheirOwnStreamId()
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Get(1, "/a"), client.Get(3, "/b"));
        var first = Backend("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok");
        var second = Backend("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok");
        var connector = new InMemoryOriginConnector()
            .ScriptConnection("127.0.0.1", BackendPort, first)
            .ScriptConnection("127.0.0.1", BackendPort, second);

        var served = await ServeAsync(request, connector, 1, 3);

        var responses = Responses(served.WrittenBytes);
        Assert.AreEqual("ok", responses[1].BodyText);
        Assert.AreEqual("ok", responses[3].BodyText);
        var forwardedPaths = new[] { first, second }.Select(backend => Latin1(backend.WrittenBytes).Split(' ')[1]).Order().ToArray();
        CollectionAssert.AreEqual(new[] { "/a", "/b" }, forwardedPaths);
    }

    [TestMethod]
    public async Task HeadRequest_AnswersHeadersOnlyWithTheBackendsContentLength()
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Headers(1, RequestFields("HEAD", "/1702"), endStream: true));
        var backend = Backend("HTTP/1.1 200 OK\r\nContent-Length: 6\r\n\r\n", peerHalfCloses: false);

        var served = await ServeAsync(request, backend);

        var response = Responses(served.WrittenBytes)[1];
        Assert.IsTrue(response.EndedOnHeaders);
        Assert.AreEqual("6", response.Fields.Single(field => field.Name == "content-length").Value);
        Assert.IsEmpty(response.Body);
    }

    [TestMethod]
    public async Task BackendThatClosesWithoutAReply_EndsTheStreamWith502BadGateway()
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Get(1, "/x"));

        var served = await ServeAsync(request, Backend(string.Empty));

        var response = Responses(served.WrittenBytes)[1];
        Assert.AreEqual("502", response.Status);
        Assert.AreEqual("text/html; charset=UTF-8", response.Fields.Single(field => field.Name == "content-type").Value);
        Assert.AreEqual("nghttpx", response.Fields.Single(field => field.Name == "server").Value);
        Assert.AreEqual("1.1 nghttpx", response.Fields.Single(field => field.Name == "via").Value);
        Assert.Contains("502 Bad Gateway", response.BodyText);
        Assert.IsTrue(response.EndedOnData);
    }

    [TestMethod]
    public async Task BackendThatCannotBeReached_EndsTheStreamWith502BadGateway()
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Get(1, "/x"));

        var served = await ServeAsync(request, new InMemoryOriginConnector(), 1);

        Assert.AreEqual("502", Responses(served.WrittenBytes)[1].Status);
    }

    [TestMethod]
    public async Task BackendThatClosesInTheMiddleOfABody_ResetsTheStreamInternalError()
    {
        var client = new Http2TestClient();
        var request = Concat(Start(), client.Get(1, "/x"));

        var served = await ServeAsync(request, Backend("HTTP/1.1 200 OK\r\nContent-Length: 10\r\n\r\nabc"));

        var response = Responses(served.WrittenBytes)[1];
        Assert.AreEqual(Http2ErrorCode.InternalError, response.ResetCode);
        Assert.IsEmpty(response.HeaderBlocks);
    }

    [TestMethod]
    public void Constructor_NoConnector_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http2GatewayUpstreamTestCaseServer(null!, BackendPort));

    [TestMethod]
    public void Schemes_AreHttp() =>
        CollectionAssert.AreEqual(new[] { "http" }, new Http2GatewayUpstreamTestCaseServer(new InMemoryOriginConnector(), BackendPort).Schemes.ToArray());

    [TestMethod]
    public async Task ServeAsync_NoConnectionOrNoContext_Throws()
    {
        var server = new Http2GatewayUpstreamTestCaseServer(new InMemoryOriginConnector(), BackendPort);
        var context = Context(new RecordingExchangeLog(), TimeProvider.System, TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, context));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(new InMemoryConnection([]), null!));
    }

    private static InMemoryConnection Backend(string reply, bool peerHalfCloses = true) =>
        new([Ascii(reply)], peerHalfClosesWhenExhausted: peerHalfCloses);

    private static InMemoryOriginConnector Connect(InMemoryConnection backend) =>
        new InMemoryOriginConnector().ScriptConnection("127.0.0.1", BackendPort, backend);

    private Task<InMemoryConnection> ServeAsync(byte[] request, InMemoryConnection backend, params int[] streamIds) =>
        ServeAsync(request, Connect(backend), streamIds.Length == 0 ? [1] : streamIds);

    // Serves until every stream in streamIds has ended, then ends the exchange as a client closing would.
    private async Task<InMemoryConnection> ServeAsync(byte[] request, InMemoryOriginConnector connector, params int[] streamIds)
    {
        var client = new InMemoryConnection([request], peerHalfClosesWhenExhausted: false);
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var server = new Http2GatewayUpstreamTestCaseServer(connector, BackendPort);
        var serving = server.ServeAsync(client, Context(new RecordingExchangeLog(), TimeProvider.System, exchange.Token));

        await WaitUntilAsync(() => streamIds.All(id => HasEnded(client, id)));
        await exchange.CancelAsync();
        await serving;

        return client;
    }
}
