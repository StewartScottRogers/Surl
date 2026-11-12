using System.Security.Cryptography;
using System.Text;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The Server-Sent Events and Long Polling transports with HTTP Post, as TransportProtocols.md's
/// "HTTP Post", "Server-Sent Events" and "Long Polling" describe their requests and answers
/// (ADR-0119, decision 2, amendment 1).
/// </summary>
[TestClass]
public sealed class SignalRHttpTransportsTests
{
    private const string Echo = "{\"type\":1,\"invocationId\":\"1\",\"target\":\"Echo\",\"arguments\":[\"hi\"]}\u001e";

    private const string EchoAnswer = "{\"type\":3,\"invocationId\":\"1\",\"result\":\"hi\"}\u001e";

    private const string EventStreamField = "Accept: text/event-stream";

    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task AnswerAsync_ServerSentEventsGet_AnswersAnEventStreamWithEachRecordAsData()
    {
        var harness = new SignalRServerHarness();
        var id = Negotiate(harness);

        var stream = await OpenEventStreamAsync(harness, id);

        var head = await stream.WaitForTextAsync(":\r\n\r\n");
        StringAssert.StartsWith(head, "HTTP/1.1 200 OK\r\n");
        StringAssert.Contains(head, "\r\nContent-Type: text/event-stream\r\n");
        StringAssert.Contains(head, "\r\nCache-Control: no-cache\r\n");
        Assert.StartsWith("HTTP/1.1 200 OK", await harness.SendRawAsync(Post(id, SignalRHubClient.HandshakeRequest)));
        await stream.WaitForTextAsync("data: {}\u001e\r\n\r\n");
        Assert.StartsWith("HTTP/1.1 200 OK", await harness.SendRawAsync(Post(id, Echo)));
        await stream.WaitForTextAsync("data: " + EchoAnswer + "\r\n\r\n");
        await Delete(harness, id);
        await stream.EndAsync();
    }

    [TestMethod]
    public async Task AnswerAsync_ServerSentEventsGetWithoutAnId_IsBadRequest()
    {
        var harness = new SignalRServerHarness();

        var response = await harness.SendAsync("GET", "/chat", SignalRServerHarness.BearerField, EventStreamField);

        Assert.StartsWith("HTTP/1.1 400 Bad Request\r\n", response);
    }

    [TestMethod]
    public async Task AnswerAsync_ServerSentEventsGetOfAnUnknownId_IsNotFound()
    {
        var harness = new SignalRServerHarness();

        var response = await harness.SendAsync("GET", "/chat?id=unknown", SignalRServerHarness.BearerField, EventStreamField);

        Assert.StartsWith("HTTP/1.1 404 Not Found\r\n", response);
    }

    [TestMethod]
    public async Task AnswerAsync_ServerSentEventsGetOfAConnectionAlreadyTakenUp_IsConflict()
    {
        var harness = new SignalRServerHarness();
        var id = Negotiate(harness);
        var stream = await OpenEventStreamAsync(harness, id);
        await stream.WaitForTextAsync(":\r\n\r\n");

        var response = await harness.SendAsync("GET", "/chat?id=" + id, SignalRServerHarness.BearerField, EventStreamField);

        Assert.StartsWith("HTTP/1.1 409 Conflict\r\n", response);
        await Delete(harness, id);
        await stream.EndAsync();
    }

    [TestMethod]
    public async Task AnswerAsync_ServerSentEventsHandshakeForMessagePack_IsRefusedAndTheStreamEnds()
    {
        var harness = new SignalRServerHarness();
        var id = Negotiate(harness);
        var stream = await OpenEventStreamAsync(harness, id);

        await harness.SendRawAsync(Post(id, "{\"protocol\":\"messagepack\",\"version\":1}\u001e"));

        await stream.WaitForTextAsync("data: {\"error\":\"" + HubHandshake.ProtocolNotAvailableText + "\"}\u001e\r\n\r\n");
        await stream.EndAsync();
        Assert.AreEqual(0, harness.Connections.Count);
    }

    [TestMethod]
    public async Task AnswerAsync_ServerSentEventsRecordTheHubRefuses_SendsTheHubCloseAndEndsTheStream()
    {
        var harness = new SignalRServerHarness();
        var id = Negotiate(harness);
        var stream = await OpenEventStreamAsync(harness, id);
        await harness.SendRawAsync(Post(id, SignalRHubClient.HandshakeRequest));

        await harness.SendRawAsync(Post(id, "{\"type\":99}\u001e"));

        await stream.WaitForTextAsync("data: {\"type\":7,\"error\":\"" + SignalRSessionEnd.ProtocolErrorText + "\"}\u001e\r\n\r\n");
        await stream.EndAsync();
    }

    [TestMethod]
    public void Event_RecordOfSeveralLines_IsOneDataFieldPerLine()
    {
        var bytes = SignalRServerSentEventsTransport.Event(Encoding.UTF8.GetBytes("{\r\n\"a\":1\n}"));

        Assert.AreEqual("data: {\r\ndata: \"a\":1\r\ndata: }\r\n\r\n", Encoding.UTF8.GetString(bytes));
    }

    [TestMethod]
    public async Task AnswerAsync_LongPollingFirstGet_IsAnsweredAtOnceWithNoContent()
    {
        var harness = new SignalRServerHarness();
        var id = Negotiate(harness);

        var response = await harness.SendAsync("GET", "/chat?id=" + id, SignalRServerHarness.BearerField);

        Assert.AreEqual(
            "HTTP/1.1 200 OK\r\nDate: " + SignalRServerHarness.NowHttpDate + "\r\nContent-Length: 0\r\nContent-Type: application/octet-stream\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    public async Task AnswerAsync_LongPollingPollAfterAPost_AnswersTheWaitingRecords()
    {
        var harness = new SignalRServerHarness();
        var id = Negotiate(harness);
        await harness.SendAsync("GET", "/chat?id=" + id, SignalRServerHarness.BearerField);

        var handshake = await harness.SendRawAsync(Post(id, SignalRHubClient.HandshakeRequest));
        var answer = await harness.SendRawAsync(Post(id, Echo));
        var poll = await harness.SendAsync("GET", "/chat?id=" + id, SignalRServerHarness.BearerField);

        Assert.StartsWith("HTTP/1.1 200 OK\r\n", handshake);
        Assert.StartsWith("HTTP/1.1 200 OK\r\n", answer);
        Assert.StartsWith("HTTP/1.1 200 OK\r\n", poll);
        StringAssert.Contains(poll, "\r\nContent-Type: application/octet-stream\r\n");
        Assert.AreEqual(SignalRHubClient.HandshakeResponse + EchoAnswer, SignalRServerHarness.BodyOf(poll));
    }

    [TestMethod]
    public async Task AnswerAsync_LongPollingPollWhileTheHubSends_AnswersTheRecord()
    {
        var harness = new SignalRServerHarness();
        var id = Negotiate(harness);
        await harness.SendAsync("GET", "/chat?id=" + id, SignalRServerHarness.BearerField);
        var waiting = harness.SendAsync("GET", "/chat?id=" + id, SignalRServerHarness.BearerField);

        await harness.SendRawAsync(Post(id, SignalRHubClient.HandshakeRequest));

        Assert.AreEqual(SignalRHubClient.HandshakeResponse, SignalRServerHarness.BodyOf(await waiting));
    }

    [TestMethod]
    public async Task AnswerAsync_LongPollingDelete_EndsTheConnectionAndCompletesThePollWithNoContent()
    {
        var harness = new SignalRServerHarness();
        var id = Negotiate(harness);
        await harness.SendAsync("GET", "/chat?id=" + id, SignalRServerHarness.BearerField);
        var waiting = harness.SendAsync("GET", "/chat?id=" + id, SignalRServerHarness.BearerField);
        await harness.SendRawAsync(Post(id, SignalRHubClient.HandshakeRequest));
        await waiting;
        var secondWaiting = harness.SendAsync("GET", "/chat?id=" + id, SignalRServerHarness.BearerField);

        var deleted = await Delete(harness, id);

        Assert.AreEqual(
            "HTTP/1.1 202 Accepted\r\nDate: " + SignalRServerHarness.NowHttpDate + "\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            deleted);
        Assert.AreEqual(
            "HTTP/1.1 204 No Content\r\nDate: " + SignalRServerHarness.NowHttpDate + "\r\nConnection: close\r\n\r\n",
            await secondWaiting);
        await WaitForCountAsync(harness, 0);
        Assert.StartsWith("HTTP/1.1 404 Not Found\r\n", await harness.SendAsync("GET", "/chat?id=" + id, SignalRServerHarness.BearerField));
    }

    [TestMethod]
    public async Task AnswerAsync_LongPollingOfAnotherCallersConnection_IsForbidden()
    {
        var harness = new SignalRServerHarness(allowsAnonymous: true);
        var negotiated = harness.Connections.Negotiate(null, true, TimeSpan.FromSeconds(30))!;
        harness.Connections.TakeUp(negotiated.UpgradeId, null, out _);
        negotiated.HttpTransport = new SignalRHttpTransportState(new SignalRPostedPayloads(), null);

        var response = await harness.SendAsync("GET", "/chat?id=" + negotiated.UpgradeId, SignalRServerHarness.BearerField);

        Assert.StartsWith("HTTP/1.1 403 Forbidden\r\n", response);
    }

    [TestMethod]
    public async Task AnswerAsync_LongPollingFirstGetByAnotherCaller_IsForbidden()
    {
        var harness = new SignalRServerHarness(allowsAnonymous: true);
        var negotiated = harness.Connections.Negotiate(null, true, TimeSpan.FromSeconds(30))!;

        var response = await harness.SendAsync("GET", "/chat?id=" + negotiated.UpgradeId, SignalRServerHarness.BearerField);

        Assert.StartsWith("HTTP/1.1 403 Forbidden\r\n", response);
    }

    [TestMethod]
    public async Task AnswerAsync_PollOfAServerSentEventsConnection_IsBadRequest()
    {
        var harness = new SignalRServerHarness();
        var id = Negotiate(harness);
        var stream = await OpenEventStreamAsync(harness, id);
        await stream.WaitForTextAsync(":\r\n\r\n");

        var response = await harness.SendAsync("GET", "/chat?id=" + id, SignalRServerHarness.BearerField);

        Assert.StartsWith("HTTP/1.1 400 Bad Request\r\n", response);
        await Delete(harness, id);
        await stream.EndAsync();
    }

    [TestMethod]
    public async Task AnswerAsync_PollOfAWebSocketConnection_IsBadRequest()
    {
        var harness = new SignalRServerHarness();
        var negotiated = harness.Connections.Negotiate(string.Empty, true, TimeSpan.FromSeconds(30))!;
        harness.Connections.TakeUp(negotiated.UpgradeId, string.Empty, out _);

        var response = await harness.SendAsync("GET", "/chat?id=" + negotiated.UpgradeId, SignalRServerHarness.BearerField);

        Assert.StartsWith("HTTP/1.1 400 Bad Request\r\n", response);
    }

    [TestMethod]
    [DataRow("GET")]
    [DataRow("POST")]
    [DataRow("DELETE")]
    public async Task AnswerAsync_RequestWithoutAnId_IsBadRequest(string method)
    {
        var harness = new SignalRServerHarness();

        var response = await harness.SendAsync(method, "/chat", SignalRServerHarness.BearerField);

        Assert.StartsWith("HTTP/1.1 400 Bad Request\r\n", response);
    }

    [TestMethod]
    [DataRow("POST")]
    [DataRow("DELETE")]
    public async Task AnswerAsync_PostOrDeleteOfAnUnknownId_IsNotFound(string method)
    {
        var harness = new SignalRServerHarness();

        var response = await harness.SendAsync(method, "/chat?id=unknown", SignalRServerHarness.BearerField);

        Assert.StartsWith("HTTP/1.1 404 Not Found\r\n", response);
    }

    [TestMethod]
    public async Task AnswerAsync_OtherMethodWithoutAnUpgrade_IsMethodNotAllowed()
    {
        var harness = new SignalRServerHarness();

        var response = await harness.SendAsync("PUT", "/chat?id=x", SignalRServerHarness.BearerField);

        Assert.AreEqual(
            "HTTP/1.1 405 Method Not Allowed\r\nDate: " + SignalRServerHarness.NowHttpDate + "\r\nContent-Length: 0\r\nAllow: GET, POST, DELETE\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    public async Task AnswerAsync_PostWhileAnEarlierPostIsUnprocessed_IsConflict()
    {
        var harness = new SignalRServerHarness();
        var posts = TakeUpWithoutASession(harness, out var id);
        _ = posts.Post([1]);

        var response = await harness.SendRawAsync(Post(id, "x"));

        Assert.StartsWith("HTTP/1.1 409 Conflict\r\n", response);
    }

    [TestMethod]
    public async Task AnswerAsync_PostToAnEndedConnection_IsNotFound()
    {
        var harness = new SignalRServerHarness();
        var posts = TakeUpWithoutASession(harness, out var id);
        posts.Close(SignalRSessionEnd.Silent("a test"));

        var response = await harness.SendRawAsync(Post(id, "x"));

        Assert.StartsWith("HTTP/1.1 404 Not Found\r\n", response);
    }

    [TestMethod]
    public async Task AnswerAsync_PostToAWebSocketConnection_IsBadRequest()
    {
        var harness = new SignalRServerHarness();
        var negotiated = harness.Connections.Negotiate(string.Empty, true, TimeSpan.FromSeconds(30))!;
        harness.Connections.TakeUp(negotiated.UpgradeId, string.Empty, out _);

        var response = await harness.SendRawAsync(Post(negotiated.UpgradeId, "x"));

        Assert.StartsWith("HTTP/1.1 400 Bad Request\r\n", response);
    }

    [TestMethod]
    public async Task AnswerAsync_DeleteOfAWebSocketConnection_IsBadRequest()
    {
        var harness = new SignalRServerHarness();
        var negotiated = harness.Connections.Negotiate(string.Empty, true, TimeSpan.FromSeconds(30))!;
        harness.Connections.TakeUp(negotiated.UpgradeId, string.Empty, out _);

        var response = await Delete(harness, negotiated.UpgradeId);

        Assert.StartsWith("HTTP/1.1 400 Bad Request\r\n", response);
    }

    [TestMethod]
    public async Task AnswerAsync_PostAnnouncedPastMaxMessage_IsContentTooLarge()
    {
        var harness = new SignalRServerHarness
        {
            DefaultLimits = ExchangeLimits.Default with { MaxMessageBytes = 2 },
        };
        TakeUpWithoutASession(harness, out var id);

        var response = await harness.SendRawAsync(Post(id, "abc"));

        Assert.StartsWith("HTTP/1.1 413 Content Too Large\r\n", response);
    }

    [TestMethod]
    public async Task AnswerAsync_PostWithoutABody_ProcessesAnEmptyPayload()
    {
        var harness = new SignalRServerHarness();
        var id = Negotiate(harness);
        await harness.SendAsync("GET", "/chat?id=" + id, SignalRServerHarness.BearerField);

        var response = await harness.SendAsync("POST", "/chat?id=" + id, SignalRServerHarness.BearerField);

        Assert.StartsWith("HTTP/1.1 200 OK\r\n", response);
    }

    [TestMethod]
    [DataRow(HttpRequestBodyDiscardOutcome.TooLarge, 413, true, 0L)]
    [DataRow(HttpRequestBodyDiscardOutcome.Incomplete, 400, true, 0L)]
    [DataRow(HttpRequestBodyDiscardOutcome.Discarded, 413, true, 2L)]
    [DataRow(HttpRequestBodyDiscardOutcome.Discarded, 400, false, 0L)]
    public async Task AnswerAsync_PostBodyNotReadWithinTheLimit_IsRefused(HttpRequestBodyDiscardOutcome outcome, int status, bool isReadable, long limit)
    {
        var harness = new SignalRServerHarness();
        TakeUpWithoutASession(harness, out var id);
        var answerer = new SignalRRequestAnswerer(
            SignalRHubPath.FromListenPath("/chat"),
            harness.Connections,
            harness.Policy.StartHttpConnection(null),
            harness.Context(ExchangeLimits.Default with { MaxMessageBytes = limit }));
        var body = new UnitTestPostBody(isReadable, outcome, [1, 2, 3]);
        var request = new HttpRequest("POST", "/chat?id=" + id, "h", [new("Authorization", "Bearer " + UnitTestBearerAuthenticationPolicy.Token)], body);

        var answer = await answerer.AnswerAsync(request, CancellationToken.None);

        Assert.AreEqual(status, answer.Status.Code);
        Assert.IsTrue(answer.IsRefusal);
        Assert.IsTrue(answer.LeavesRequestBodyUnread);
    }

    [TestMethod]
    public async Task AnswerAsync_PostWithNoMaxMessage_ReadsTheBodyWhateverItsSize()
    {
        var harness = new SignalRServerHarness();
        var posts = TakeUpWithoutASession(harness, out var id);
        posts.Close(SignalRSessionEnd.Silent("a test"));
        var answerer = new SignalRRequestAnswerer(
            SignalRHubPath.FromListenPath("/chat"),
            harness.Connections,
            harness.Policy.StartHttpConnection(null),
            harness.Context(ExchangeLimits.Default with { MaxMessageBytes = 0 }));
        var body = new UnitTestPostBody(true, HttpRequestBodyDiscardOutcome.Discarded, [1, 2, 3]);
        var request = new HttpRequest("POST", "/chat?id=" + id, "h", [new("Authorization", "Bearer " + UnitTestBearerAuthenticationPolicy.Token)], body);

        var answer = await answerer.AnswerAsync(request, CancellationToken.None);

        Assert.AreEqual(404, answer.Status.Code);
        Assert.IsFalse(answer.IsRefusal);
    }

    private static string Negotiate(SignalRServerHarness harness) =>
        harness.Connections.Negotiate(string.Empty, true, TimeSpan.FromSeconds(30))!.UpgradeId;

    private static SignalRPostedPayloads TakeUpWithoutASession(SignalRServerHarness harness, out string id)
    {
        var negotiated = harness.Connections.Negotiate(string.Empty, true, TimeSpan.FromSeconds(30))!;
        harness.Connections.TakeUp(negotiated.UpgradeId, string.Empty, out _);
        var posts = new SignalRPostedPayloads();
        negotiated.HttpTransport = new SignalRHttpTransportState(posts, null);
        id = negotiated.UpgradeId;

        return posts;
    }

    private static string Post(string id, string body) =>
        $"POST /chat?id={id} HTTP/1.1\r\nHost: 127.0.0.1:5000\r\n{SignalRServerHarness.BearerField}\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}";

    private static Task<string> Delete(SignalRServerHarness harness, string id) =>
        harness.SendAsync("DELETE", "/chat?id=" + id, SignalRServerHarness.BearerField);

    private static async Task WaitForCountAsync(SignalRServerHarness harness, int count)
    {
        var giveUpAt = DateTime.UtcNow + WaitLimit;
        while (harness.Connections.Count != count)
        {
            Assert.IsLessThan(giveUpAt, DateTime.UtcNow, "The hub did not forget the connection.");
            await Task.Delay(1);
        }
    }

    private static async Task<EventStream> OpenEventStreamAsync(SignalRServerHarness harness, string id)
    {
        var connection = new UnitTestDuplexConnection();
        connection.Send(Encoding.ASCII.GetBytes(
            $"GET /chat?id={id} HTTP/1.1\r\nHost: 127.0.0.1:5000\r\n{SignalRServerHarness.BearerField}\r\n{EventStreamField}\r\nConnection: close\r\n\r\n"));
        var serving = harness.Server.ServeAsync(connection, harness.Context());
        await Task.Yield();

        return new EventStream(connection, serving);
    }

    private sealed class EventStream(UnitTestDuplexConnection connection, Task serving)
    {
        // The server closes the response, then reads what the client still sends until the client ends it.
        public async Task EndAsync()
        {
            connection.HalfClose();
            await serving.WaitAsync(WaitLimit);
        }

        public async Task<string> WaitForTextAsync(string text)
        {
            try
            {
                var bytes = await connection.WaitForWrittenAsync(written => Encoding.UTF8.GetString(written).Contains(text, StringComparison.Ordinal));

                return Encoding.UTF8.GetString(bytes);
            }
            catch (TimeoutException)
            {
                Assert.Fail($"The server never wrote {text.ReplaceLineEndings("\\n")}; it wrote {Encoding.UTF8.GetString(connection.WrittenBytes)}");
                throw;
            }
        }
    }

    private sealed class UnitTestPostBody(bool isReadable, HttpRequestBodyDiscardOutcome outcome, byte[] content) : IHttpRequestBody
    {
        public bool IsAnnounced => true;

        public long? AnnouncedLength => null;

        public bool IsReadable { get; } = isReadable;

        public Task<HttpRequestBodyDiscardOutcome> DiscardAsync(IncrementalHash? bodyHash, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public async Task<HttpRequestBodyDiscardOutcome> CopyToAsync(Stream destination, CancellationToken cancellationToken)
        {
            await destination.WriteAsync(content, cancellationToken);

            return outcome;
        }
    }
}
