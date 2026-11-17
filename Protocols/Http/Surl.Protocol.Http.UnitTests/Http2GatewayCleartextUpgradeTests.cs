using Surl.Protocol.Abstractions;
using static Surl.Http2.Http2TestClient;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// The <c>http/2</c> gateway half's plaintext port when a client opens with HTTP/1.1 (ADR-0154 decision 2),
/// replayed from the requests of upstream test cases 1700, 1701 and 1702 and the replies of their <c>http</c> half.
/// </summary>
[TestClass]
public sealed class Http2GatewayCleartextUpgradeTests
{
    private const int BackendPort = 4242;

    private const string SwitchingProtocolsHead = "HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: h2c\r\n\r\n";

    private const string UpgradeFields = "Connection: Upgrade, HTTP2-Settings\r\nUpgrade: h2c\r\nHTTP2-Settings: AAMAAABkAAQAAQAAAAIAAAAA\r\n";

    private const string Reply1700 =
        "HTTP/1.1 200 OK\r\nDate: Tue, 09 Nov 2010 14:49:00 GMT\r\nServer: test-server/fake\r\nLast-Modified: Tue, 13 Jun 2000 12:10:00 GMT\r\nContent-Length: 6\r\nConnection: close\r\nContent-Type: text/html\r\nFunny-head: yesyes\r\n\r\n-foo-\n";

    private const string Reply17000001 =
        "HTTP/1.1 200 OK\r\nDate: Tue, 09 Nov 2010 14:49:00 GMT\r\nContent-Length: 6\r\nConnection: close\r\nContent-Type: text/html\r\n\r\n-maa-\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Test1700_UpgradeIsAnswered101ThenStream1AndTheSecondUrlIsStream3OfTheSameConnection()
    {
        var client = new Http2TestClient();
        var request = Concat(
            Ascii($"GET /1700 HTTP/1.1\r\nHost: 127.0.0.1:1234\r\nUser-Agent: curl/8\r\nAccept: */*\r\n{UpgradeFields}\r\n"),
            Start(),
            client.Headers(3, RequestFields("GET", "/17000001", ("user-agent", "curl/8"), ("accept", "*/*")), endStream: true));
        var first = Backend(Reply1700);
        var second = Backend(Reply17000001);
        var connector = new InMemoryOriginConnector()
            .ScriptConnection("127.0.0.1", BackendPort, first)
            .ScriptConnection("127.0.0.1", BackendPort, second);

        var served = await ServeUpgradedAsync(request, connector, 1, 3);

        Assert.AreEqual(SwitchingProtocolsHead, Latin1(served.WrittenBytes[..SwitchingProtocolsHead.Length]));
        Assert.AreEqual(
            "GET /1700 HTTP/1.1\r\nHost: 127.0.0.1:1234\r\nUser-Agent: curl/8\r\nAccept: */*\r\nX-Forwarded-Proto: http\r\nVia: 2 nghttpx\r\n\r\n",
            Latin1(first.WrittenBytes));
        StringAssert.StartsWith(Latin1(second.WrittenBytes), "GET /17000001 HTTP/1.1\r\n");
        var responses = Responses(served.WrittenBytes[SwitchingProtocolsHead.Length..]);
        Assert.AreEqual("200", responses[1].Status);
        CollectionAssert.AreEqual(
            new[]
            {
                "date=Tue, 09 Nov 2010 14:49:00 GMT",
                "last-modified=Tue, 13 Jun 2000 12:10:00 GMT",
                "content-length=6",
                "content-type=text/html",
                "funny-head=yesyes",
                "server=nghttpx",
                "via=1.1 nghttpx",
            },
            responses[1].Fields.Skip(1).Select(field => $"{field.Name}={field.Value}").ToArray());
        Assert.AreEqual("-foo-\n", responses[1].BodyText);
        Assert.AreEqual("-maa-\n", responses[3].BodyText);
    }

    [TestMethod]
    public async Task Test2407_TwoUpgradedConnectionsServedAtOnce_EachGetsHeadersAndDataOnStream1()
    {
        var request = Concat(
            Ascii($"GET /2407 HTTP/1.1\r\nHost: 127.0.0.1:1234\r\nUser-Agent: curl/8\r\nAccept: */*\r\n{UpgradeFields}\r\n"),
            Start());
        var connector = new InMemoryOriginConnector()
            .ScriptConnection("127.0.0.1", BackendPort, Backend(Reply1700))
            .ScriptConnection("127.0.0.1", BackendPort, Backend(Reply1700));
        var clients = new[] { new InMemoryConnection([request], peerHalfClosesWhenExhausted: false), new InMemoryConnection([request], peerHalfClosesWhenExhausted: false) };
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = clients.Select(client => new Http2GatewayUpstreamTestCaseServer(connector, BackendPort)
            .ServeAsync(client, Context(new RecordingExchangeLog(), TimeProvider.System, exchange.Token))).ToArray();

        await WaitUntilAsync(() => clients.All(client => AfterTheHead(client) is { } frames && Responses(frames).TryGetValue(1, out var response) && response.EndedOnData));
        await exchange.CancelAsync();
        await Task.WhenAll(serving);

        foreach (var client in clients)
        {
            Assert.AreEqual("-foo-\n", Responses(AfterTheHead(client)!)[1].BodyText);
        }
    }

    [TestMethod]
    public async Task Test1701_UpgradedPostForwardsItsBodyAndContentLength()
    {
        var request = Concat(
            Ascii($"POST /1701 HTTP/1.1\r\nHost: 127.0.0.1:1234\r\nUser-Agent: curl/8\r\nAccept: */*\r\n{UpgradeFields}Content-Length: 10\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\ndatatosend"),
            Start());
        var backend = Backend(Reply1700);

        var served = await ServeUpgradedAsync(request, Connect(backend), 1);

        Assert.AreEqual(
            "POST /1701 HTTP/1.1\r\nHost: 127.0.0.1:1234\r\nUser-Agent: curl/8\r\nAccept: */*\r\nContent-Length: 10\r\nContent-Type: application/x-www-form-urlencoded\r\nX-Forwarded-Proto: http\r\nVia: 2 nghttpx\r\n\r\ndatatosend",
            Latin1(backend.WrittenBytes));
        Assert.AreEqual("-foo-\n", Responses(served.WrittenBytes[SwitchingProtocolsHead.Length..])[1].BodyText);
    }

    [TestMethod]
    public async Task Test1702_UpgradedHeadAnswersStream1WithNoData()
    {
        var request = Concat(
            Ascii($"HEAD /1702 HTTP/1.1\r\nHost: 127.0.0.1:1234\r\nUser-Agent: curl/8\r\nAccept: */*\r\n{UpgradeFields}\r\n"),
            Start());
        var backend = Backend("HTTP/1.1 200 OK\r\nContent-Length: 6\r\nConnection: close\r\n\r\n", peerHalfCloses: false);

        var served = await ServeUpgradedAsync(request, Connect(backend), 1);

        StringAssert.StartsWith(Latin1(backend.WrittenBytes), "HEAD /1702 HTTP/1.1\r\n");
        var response = Responses(served.WrittenBytes[SwitchingProtocolsHead.Length..])[1];
        Assert.IsTrue(response.EndedOnHeaders);
        Assert.IsEmpty(response.Body);
        Assert.AreEqual("6", response.Fields.Single(field => field.Name == "content-length").Value);
    }

    [TestMethod]
    public async Task PlaintextRequestWithNoUpgrade_IsForwardedAsHttp11AndAnsweredHttp11()
    {
        var backend = Backend(Reply1700);
        var client = new InMemoryConnection([Ascii("GET /x HTTP/1.1\r\nHost: 127.0.0.1:1234\r\nUser-Agent: curl/8\r\n\r\n")]);

        await ServeAsync(client, Connect(backend));

        Assert.AreEqual(
            "GET /x HTTP/1.1\r\nHost: 127.0.0.1:1234\r\nUser-Agent: curl/8\r\nX-Forwarded-Proto: http\r\nVia: 2 nghttpx\r\n\r\n",
            Latin1(backend.WrittenBytes));
        var written = Latin1(client.WrittenBytes);
        StringAssert.StartsWith(written, "HTTP/1.1 200 OK\r\n");
        StringAssert.Contains(written, "via: 1.1 nghttpx\r\n");
        StringAssert.Contains(written, "server: nghttpx\r\n");
        Assert.DoesNotContain("test-server/fake", written);
        StringAssert.EndsWith(written, "\r\n\r\n-foo-\n");
    }

    [TestMethod]
    public async Task PlaintextRequestThatTheBackendCannotAnswer_IsAnswered502()
    {
        var client = new InMemoryConnection([Ascii("GET /x HTTP/1.1\r\nHost: a\r\n\r\n")]);

        await ServeAsync(client, new InMemoryOriginConnector());

        StringAssert.StartsWith(Latin1(client.WrittenBytes), "HTTP/1.1 502 ");
        StringAssert.Contains(Latin1(client.WrittenBytes), "502 Bad Gateway");
    }

    [TestMethod]
    public async Task PlaintextRequestWhoseReplyBodyIsCutShort_AbortsTheConnectionWithNoBytes()
    {
        var client = new InMemoryConnection([Ascii("GET /x HTTP/1.1\r\nHost: a\r\n\r\n")]);

        await ServeAsync(client, Connect(Backend("HTTP/1.1 200 OK\r\nContent-Length: 10\r\n\r\nabc")));

        Assert.IsEmpty(client.WrittenBytes);
    }

    [TestMethod]
    public async Task RequestWhoseBodyEndsEarly_IsAnswered400()
    {
        var client = new InMemoryConnection([Ascii("POST /x HTTP/1.1\r\nHost: a\r\nContent-Length: 10\r\n\r\nabc")]);
        var connector = new InMemoryOriginConnector();

        await ServeAsync(client, connector);

        StringAssert.StartsWith(Latin1(client.WrittenBytes), "HTTP/1.1 400 Bad Request\r\n");
        Assert.IsEmpty(connector.Requests);
    }

    [TestMethod]
    public async Task RequestWhoseBodyCodingCannotBeRead_IsAnswered400()
    {
        var client = new InMemoryConnection([Ascii("POST /x HTTP/1.1\r\nHost: a\r\nTransfer-Encoding: gzip\r\n\r\nabc")]);

        await ServeAsync(client, new InMemoryOriginConnector());

        StringAssert.StartsWith(Latin1(client.WrittenBytes), "HTTP/1.1 400 Bad Request\r\n");
    }

    [TestMethod]
    public async Task ClientThatDoesNotSendThePrefaceOverTls_IsClosedWithNoBytesAndNoBackendConnection()
    {
        var connector = new InMemoryOriginConnector();
        var client = new InMemoryConnection([Ascii("GET / HTTP/1.1\r\n\r\n")], initialTlsSession: InMemoryConnection.DefaultUpgradeTlsSession);
        var log = new RecordingExchangeLog();

        await new Http2GatewayUpstreamTestCaseServer(connector, BackendPort).ServeAsync(client, Context(log, TimeProvider.System, TestContext.CancellationToken));

        Assert.IsEmpty(client.WrittenBytes);
        Assert.IsEmpty(connector.Requests);
        CollectionAssert.Contains(log.Notes.ToArray(), "The client did not send the HTTP/2 connection preface; closed with no bytes.");
    }

    private static InMemoryConnection Backend(string reply, bool peerHalfCloses = true) =>
        new([Ascii(reply)], peerHalfClosesWhenExhausted: peerHalfCloses);

    private static InMemoryOriginConnector Connect(InMemoryConnection backend) =>
        new InMemoryOriginConnector().ScriptConnection("127.0.0.1", BackendPort, backend);

    private Task ServeAsync(InMemoryConnection client, InMemoryOriginConnector connector) =>
        new Http2GatewayUpstreamTestCaseServer(connector, BackendPort).ServeAsync(client, Context(new RecordingExchangeLog(), TimeProvider.System, TestContext.CancellationToken));

    // Serves until every stream in streamIds has ended after the 101, then ends the exchange as a client closing would.
    private async Task<InMemoryConnection> ServeUpgradedAsync(byte[] request, InMemoryOriginConnector connector, params int[] streamIds)
    {
        var client = new InMemoryConnection([request], peerHalfClosesWhenExhausted: false);
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = new Http2GatewayUpstreamTestCaseServer(connector, BackendPort).ServeAsync(client, Context(new RecordingExchangeLog(), TimeProvider.System, exchange.Token));

        await WaitUntilAsync(() => AfterTheHead(client) is { } frames && streamIds.All(id => Responses(frames).TryGetValue(id, out var response) && (response.EndedOnHeaders || response.EndedOnData)));
        await exchange.CancelAsync();
        await serving;

        return client;
    }

    private static byte[]? AfterTheHead(InMemoryConnection client) =>
        client.WrittenBytes.Length >= SwitchingProtocolsHead.Length ? client.WrittenBytes[SwitchingProtocolsHead.Length..] : null;
}
