using System.Net;
using Surl.HttpServing;
using static Surl.Control.ControlHarness;

namespace Surl.Control;

[TestClass]
public sealed class ControlRequestHandlerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AnswerAsync_NonLoopbackPeerWithTheToken_IsRefused403AndCloses()
    {
        var target = new FakeControlTarget();
        var peer = new IPEndPoint(IPAddress.Parse("192.0.2.7"), 50000);

        var response = await ServeAsync(Endpoint(target), Request("POST", "/v1/reset"), new ManualTimeProvider(), TestContext.CancellationToken, peer);

        Assert.AreEqual(
            "HTTP/1.1 403 Forbidden\r\n"
            + $"Date: {ManualTimeProvider.StartHttpDate}\r\nServer: surl\r\nContent-Type: application/json\r\nCache-Control: no-store\r\n"
            + "Content-Length: 83\r\nConnection: close\r\n\r\n"
            + "{\"error\":\"forbidden\",\"message\":\"the control endpoint answers only a loopback peer\"}",
            response);
        Assert.IsEmpty(target.Calls);
    }

    [TestMethod]
    public async Task AnswerAsync_PeerThatIsNoIpEndPoint_IsRefused403()
    {
        var response = await ServeAsync(Endpoint(new FakeControlTarget()), Request("POST", "/v1/reset"), new ManualTimeProvider(), TestContext.CancellationToken, new DnsEndPoint("localhost", 50000));

        Assert.AreEqual("HTTP/1.1 403 Forbidden", StatusLine(response));
    }

    [TestMethod]
    [DataRow("::1")]
    [DataRow("::ffff:127.0.0.1")]
    [DataRow("127.8.9.10")]
    public async Task AnswerAsync_LoopbackPeer_IsAnswered(string address)
    {
        var target = new FakeControlTarget();

        var response = await ServeAsync(Endpoint(target), Request("POST", "/v1/reset"), new ManualTimeProvider(), TestContext.CancellationToken, new IPEndPoint(IPAddress.Parse(address), 50000));

        Assert.AreEqual("HTTP/1.1 204 No Content", StatusLine(response));
        CollectionAssert.AreEqual(new[] { "Reset" }, target.Calls);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("Authorization: Bearer 0123456789abcdefghijklmnopqrstuw\r\n")]
    [DataRow("Authorization: Basic MDEyMzQ1Njc4OWFiY2RlZmdoaWprbG1ub3BxcnN0dXY=\r\n")]
    public async Task AnswerAsync_MissingOrWrongToken_IsRefused401AfterTheFixedDelay(string authorization)
    {
        var target = new FakeControlTarget();
        var time = new ManualTimeProvider();
        var serving = ServeAsync(Endpoint(target), RequestWith("POST", "/v1/reset", authorization), time, TestContext.CancellationToken);

        await time.WaitForTimerAsync(ControlEndpoint.RefusalDelay, TestContext.CancellationToken);
        Assert.IsFalse(serving.IsCompleted);
        time.Advance(ControlEndpoint.RefusalDelay);
        var response = await serving;

        Assert.AreEqual(
            "HTTP/1.1 401 Unauthorized\r\n"
            + $"Date: Thu, 05 Nov 2026 10:00:01 GMT\r\nServer: surl\r\nWWW-Authenticate: Bearer\r\nContent-Type: application/json\r\nCache-Control: no-store\r\n"
            + "Content-Length: 115\r\nConnection: close\r\n\r\n"
            + "{\"error\":\"unauthorized\",\"message\":\"the request needs the control token as Authorization: Bearer \\u003Ctoken\\u003E\"}",
            response);
        Assert.IsEmpty(target.Calls);
        Assert.DoesNotContain(Token, response);
    }

    [TestMethod]
    [DataRow("/", "/")]
    [DataRow("/v2/events", "/v2/events")]
    [DataRow("/v1/nothing?after=1", "/v1/nothing")]
    [DataRow("/v1/events/extra", "/v1/events/extra")]
    [DataRow("/v1/files/", "/v1/files/")]
    [DataRow("/v1/faults//", "/v1/faults//")]
    [DataRow("/v1/snapshots/p1", "/v1/snapshots/p1")]
    public async Task AnswerAsync_UnknownPath_Is404(string target, string path)
    {
        var response = await ServeAsync(Endpoint(new FakeControlTarget()), Request("GET", target), new ManualTimeProvider(), TestContext.CancellationToken);

        Assert.AreEqual("HTTP/1.1 404 Not Found", StatusLine(response));
        Assert.AreEqual($"{{\"error\":\"not_found\",\"message\":\"no control request has path {path}\"}}", Body(response));
    }

    [TestMethod]
    public async Task AnswerAsync_KnownPathWithAnotherMethod_Is405NamingTheMethodsItTakes()
    {
        var response = await ServeAsync(Endpoint(new FakeControlTarget()), Request("PUT", "/v1/faults"), new ManualTimeProvider(), TestContext.CancellationToken);

        Assert.AreEqual("HTTP/1.1 405 Method Not Allowed", StatusLine(response));
        Assert.AreEqual("{\"error\":\"method_not_allowed\",\"message\":\"/v1/faults takes POST, GET\"}", Body(response));
    }

    [TestMethod]
    [DataRow("/v1/faults?x=1", "query key x is not one this request takes")]
    [DataRow("/v1/events?after=1&after=2", "query key after is given more than once")]
    [DataRow("/v1/events?limit=0", "query key limit must be a whole number from 1 to 1000")]
    [DataRow("/v1/events?limit=1001", "query key limit must be a whole number from 1 to 1000")]
    [DataRow("/v1/events?after=-1", "query key after must be a whole number from 0 to 9223372036854775807")]
    [DataRow("/v1/events?after", "query key after must be a whole number from 0 to 9223372036854775807")]
    public async Task AnswerAsync_MalformedQuery_Is400NamingTheKey(string target, string message)
    {
        var response = await ServeAsync(Endpoint(new FakeControlTarget()), Request("GET", target), new ManualTimeProvider(), TestContext.CancellationToken);

        Assert.AreEqual("HTTP/1.1 400 Bad Request", StatusLine(response));
        Assert.AreEqual($"{{\"error\":\"bad_request\",\"message\":\"{message}\"}}", Body(response));
    }

    [TestMethod]
    [DataRow(ControlRefusalKind.BadRequest, "HTTP/1.1 400 Bad Request", "bad_request")]
    [DataRow(ControlRefusalKind.NotFound, "HTTP/1.1 404 Not Found", "not_found")]
    [DataRow(ControlRefusalKind.Conflict, "HTTP/1.1 409 Conflict", "conflict")]
    public async Task AnswerAsync_TargetRefuses_AnswersTheRefusalsStatusAndMessage(ControlRefusalKind kind, string statusLine, string code)
    {
        var target = new FakeControlTarget { Failure = new ControlRefusalException(kind, "the content store is on disk") };

        var response = await ServeAsync(Endpoint(target), Request("POST", "/v1/reset"), new ManualTimeProvider(), TestContext.CancellationToken);

        Assert.AreEqual(statusLine, StatusLine(response));
        Assert.AreEqual($"{{\"error\":\"{code}\",\"message\":\"the content store is on disk\"}}", Body(response));
    }

    [TestMethod]
    public async Task AnswerAsync_TwoRequestsOnOneConnection_AnswersBothAndKeepsTheConnectionOpenBetween()
    {
        var target = new FakeControlTarget();
        var keepAlive = $"POST /v1/reset HTTP/1.1\r\nHost: 127.0.0.1\r\nAuthorization: Bearer {Token}\r\n\r\n";

        var response = await ServeAsync(Endpoint(target), keepAlive + Request("POST", "/v1/reset"), new ManualTimeProvider(), TestContext.CancellationToken);

        Assert.AreEqual(2, response.Split("HTTP/1.1 204 No Content").Length - 1);
        CollectionAssert.AreEqual(new[] { "Reset", "Reset" }, target.Calls);
    }

    [TestMethod]
    public async Task AnswerAsync_NullRequest_Throws()
    {
        var handler = new ControlRequestHandler([], new ControlToken(Token), LoopbackPeer, new ControlAnswers(TimeProvider.System), TimeProvider.System);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => handler.AnswerAsync(null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task AnswerAsync_RouteThrowsAnythingElse_LetsItThrough()
    {
        var handler = new ControlRequestHandler(
            [new ControlRoute("POST", ["reset"], [], _ => throw new InvalidOperationException("broken"))],
            new ControlToken(Token),
            LoopbackPeer,
            new ControlAnswers(TimeProvider.System),
            TimeProvider.System);
        var request = new HttpRequest("POST", "/v1/reset", "127.0.0.1", [new("Authorization", $"Bearer {Token}")], new UnannouncedBody());

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => handler.AnswerAsync(request, TestContext.CancellationToken));
    }

    private sealed class UnannouncedBody : IHttpRequestBody
    {
        public bool IsAnnounced => false;

        public long? AnnouncedLength => null;

        public bool IsReadable => true;

        public Task<HttpRequestBodyDiscardOutcome> DiscardAsync(System.Security.Cryptography.IncrementalHash? bodyHash, CancellationToken cancellationToken) =>
            Task.FromResult(HttpRequestBodyDiscardOutcome.Discarded);

        public Task<HttpRequestBodyDiscardOutcome> CopyToAsync(Stream destination, CancellationToken cancellationToken) =>
            Task.FromResult(HttpRequestBodyDiscardOutcome.Discarded);
    }
}
