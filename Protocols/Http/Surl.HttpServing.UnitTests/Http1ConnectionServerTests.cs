using Surl.Protocol.Abstractions;
using static Surl.HttpServing.HttpServingHarness;

namespace Surl.HttpServing;

/// <summary>
/// <see cref="Http1ConnectionServer"/> serves a handler that knows nothing of HTTP/1.1 framing.
/// Every request and response byte below is written from RFC 9112: the message framing of
/// section 6 (<c>Content-Length</c>), the chunked coding of section 7.1, and the persistence of
/// section 9.3; <c>100 Continue</c> from RFC 9110 section 10.1.1.
/// </summary>
[TestClass]
public sealed class Http1ConnectionServerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        var context = Context(new RecordingExchangeLog(), new FixedTimeProvider(Now), CancellationToken.None);

        Assert.ThrowsExactly<ArgumentNullException>(() => new Http1ConnectionServer(null!, context));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http1ConnectionServer(new UnitTestEchoRequestHandler(), null!));
    }

    [TestMethod]
    public async Task ServeAsync_NullConnection_Throws()
    {
        var server = new Http1ConnectionServer(new UnitTestEchoRequestHandler(), Context(new RecordingExchangeLog(), new FixedTimeProvider(Now), TestContext.CancellationToken));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!));
    }

    [TestMethod]
    public async Task ServeAsync_PutWithAContentLengthBody_HandsTheHandlerTheBody()
    {
        var (connection, handler) = await ServeAsync("PUT /object HTTP/1.1\r\nHost: example.test\r\nContent-Length: 5\r\n\r\nhello");

        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello", Latin1(connection.WrittenBytes));
        var request = handler.Requests.Single();
        Assert.AreEqual("PUT", request.Method);
        Assert.AreEqual("/object", request.Target);
        Assert.AreEqual("example.test", request.Authority);
        Assert.IsFalse(request.Body.IsAnnounced, "The body was read to its end.");
    }

    [TestMethod]
    public async Task ServeAsync_ChunkedPost_HandsTheHandlerTheDecodedContent()
    {
        var (connection, _) = await ServeAsync(
            "POST /object HTTP/1.1\r\nHost: example.test\r\nTransfer-Encoding: chunked\r\n\r\n"
            + "5\r\nhello\r\n6;name=value\r\n world\r\n0\r\nTrailer-Field: x\r\n\r\n");

        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 11\r\n\r\nhello world", Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_ExpectContinue_Writes100ContinueBeforeTheBodyIsRead()
    {
        var connection = new InMemoryConnection(
            [Ascii("PUT /object HTTP/1.1\r\nHost: example.test\r\nContent-Length: 2\r\nExpect: 100-continue\r\n\r\n"), Ascii("ok")]);

        await Server(new UnitTestEchoRequestHandler()).ServeAsync(connection);

        Assert.AreEqual("HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_TwoRequestsOnAKeptAliveConnection_AnswersBoth()
    {
        var (connection, handler) = await ServeAsync(
            "PUT /a HTTP/1.1\r\nHost: example.test\r\nContent-Length: 1\r\n\r\nA"
            + "PUT /b HTTP/1.1\r\nHost: example.test\r\nContent-Length: 2\r\n\r\nBB");

        Assert.AreEqual(
            "HTTP/1.1 200 OK\r\nContent-Length: 1\r\n\r\nA" + "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nBB",
            Latin1(connection.WrittenBytes));
        CollectionAssert.AreEqual(new[] { "/a", "/b" }, handler.Requests.Select(request => request.Target).ToArray());
        Assert.IsFalse(connection.WritesCompleted, "The client closed the connection, not the server.");
    }

    [TestMethod]
    public async Task ServeAsync_ConnectionClose_AnswersWithConnectionCloseAndReadsNoMore()
    {
        var (connection, handler) = await ServeAsync(
            "GET /a HTTP/1.1\r\nHost: example.test\r\nConnection: close\r\n\r\n"
            + "GET /b HTTP/1.1\r\nHost: example.test\r\n\r\n");

        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", Latin1(connection.WrittenBytes));
        Assert.HasCount(1, handler.Requests);
        Assert.IsTrue(connection.WritesCompleted, "The server half-closed after the answer.");
    }

    [TestMethod]
    public async Task ServeAsync_Http10KeepAlive_AnswersKeepAlive()
    {
        var (connection, _) = await ServeAsync("GET /a HTTP/1.0\r\nConnection: keep-alive\r\n\r\n");

        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: keep-alive\r\n\r\n", Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_Http11RequestWithoutHost_IsRefused400BeforeTheHandler()
    {
        var log = new RecordingExchangeLog();
        var (connection, handler) = await ServeAsync("GET /a HTTP/1.1\r\n\r\n", log);

        Assert.AreEqual(
            $"HTTP/1.1 400 Bad Request\r\nDate: {NowHttpDate}\r\nServer: surl\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            Latin1(connection.WrittenBytes));
        Assert.IsEmpty(handler.Requests);
        Assert.AreEqual("GET /a: a request may carry at most one Host field, and an HTTP/1.1 request needs one; answered 400 and closed.", log.Notes[0]);
    }

    [TestMethod]
    public async Task ServeAsync_ChunkedBodyThatEndsEarly_IsTheHandlersToRefuse()
    {
        var (connection, _) = await ServeAsync("POST /a HTTP/1.1\r\nHost: example.test\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhel");

        Assert.AreEqual("HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_AnswerWithATakeover_WritesTheHeadThenHandsTheConnectionOver()
    {
        var takeover = new UnitTestRecordingTakeover();
        var handler = new UnitTestEchoRequestHandler { Takeover = takeover };
        var connection = new InMemoryConnection([Ascii("GET /chat HTTP/1.1\r\nHost: example.test\r\nUpgrade: unit-test\r\nConnection: Upgrade\r\n\r\nafter")]);

        await Server(handler).ServeAsync(connection);

        Assert.AreEqual("HTTP/1.1 101 Switching Protocols\r\nUpgrade: unit-test\r\nConnection: Upgrade\r\n\r\n", Latin1(connection.WrittenBytes));
        Assert.AreEqual("after", takeover.BytesRead);
        Assert.IsFalse(connection.WritesCompleted, "Nothing is closed after a takeover.");
    }

    [TestMethod]
    public async Task CopyToAsync_NullDestination_Throws()
    {
        var handler = new UnitTestNullCopyRequestHandler();

        await Server(handler).ServeAsync(new InMemoryConnection([Ascii("PUT /a HTTP/1.1\r\nHost: example.test\r\nContent-Length: 1\r\n\r\nA")]));

        Assert.IsInstanceOfType<ArgumentNullException>(handler.Failure);
    }

    private async Task<(InMemoryConnection Connection, UnitTestEchoRequestHandler Handler)> ServeAsync(string request, RecordingExchangeLog? log = null)
    {
        var connection = new InMemoryConnection([Ascii(request)]);
        var handler = new UnitTestEchoRequestHandler();

        await Server(handler, log).ServeAsync(connection);

        return (connection, handler);
    }

    private Http1ConnectionServer Server(IHttpRequestHandler handler, RecordingExchangeLog? log = null) =>
        new(handler, Context(log ?? new RecordingExchangeLog(), new FixedTimeProvider(Now), TestContext.CancellationToken));

    // Reads what the client sent after the upgrade head, through the reader it is handed.
    private sealed class UnitTestRecordingTakeover : IHttpConnectionTakeover
    {
        public string BytesRead { get; private set; } = "";

        public async Task TakeOverAsync(IConnection connection, HttpConnectionReader reader, CancellationToken cancellationToken)
        {
            var buffer = new byte[64];
            var read = await reader.ReadAsync(buffer, cancellationToken);
            BytesRead = Latin1(buffer[..read]);
        }
    }

    // Copies the body to nowhere, and records what that throws.
    private sealed class UnitTestNullCopyRequestHandler : IHttpRequestHandler
    {
        public Exception? Failure { get; private set; }

        public async Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken)
        {
            try
            {
                await request.Body.CopyToAsync(null!, cancellationToken);
            }
            catch (ArgumentNullException failure)
            {
                Failure = failure;
            }

            return new HttpAnswer(HttpStatus.BadRequest, [], HttpAnswerBody.None, IsRefusal: true, LeavesRequestBodyUnread: true);
        }
    }
}
