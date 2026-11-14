using System.Security.Cryptography;
using Surl.Http3;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// The <c>http/3</c> gateway half of upstream test cases (ADR-0154), driven with QPACK-encoded HTTP/3
/// request streams over the in-memory QUIC test connection, with an <see cref="InMemoryOriginConnector"/>
/// standing in for the case's <c>http</c> half. The requests and replies are tests 2500's and 2503's.
/// </summary>
[TestClass]
public sealed class Http3GatewayUpstreamTestCaseServerTests
{
    private const int BackendPort = 4242;
    private const string GatewayAuthority = "localhost:18060";

    private const string Reply2500 =
        "HTTP/1.1 200 OK\r\nDate: Tue, 09 Nov 2010 14:49:00 GMT\r\nLast-Modified: Tue, 13 Jun 2000 12:10:00 GMT\r\nETag: \"21025-dc7-39462498\"\r\nAccept-Ranges: bytes\r\nContent-Length: 6\r\nConnection: close\r\nContent-Type: text/html\r\nFunny-head: yesyes\r\n\r\n-foo-\n";

    private const string Reply2503 =
        "HTTP/1.1 200 OK\r\nDate: Tue, 09 Nov 2010 14:49:00 GMT\r\nContent-Length: 6\r\nConnection: close\r\nContent-Type: text/html\r\nFunny-head: yesyes\r\n\r\n-foo-\n";

    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public async Task Get2500_ForwardsTheAbsoluteFormHttp11RequestAndAnswersWithHeadersAndDataFrames()
    {
        using var client = new Http3TestClient();
        var backend = Backend(Reply2500);
        await StartAsync(client, Connect(backend));

        client.Send(0, Get(client, "/2500", ("user-agent", "curl/8.21.0"), ("accept", "*/*")), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        Assert.AreEqual(
            "GET https://localhost:18060/2500 HTTP/1.1\r\nHost: localhost:18060\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nX-Forwarded-Proto: https\r\nVia: 1.1 nghttpx\r\n\r\n",
            Latin1(backend.WrittenBytes));
        var response = client.Response(0);
        Assert.AreEqual("200", response.Status);
        CollectionAssert.AreEqual(
            new[]
            {
                "date: Tue, 09 Nov 2010 14:49:00 GMT",
                "last-modified: Tue, 13 Jun 2000 12:10:00 GMT",
                "etag: \"21025-dc7-39462498\"",
                "accept-ranges: bytes",
                "content-length: 6",
                "content-type: text/html",
                "funny-head: yesyes",
                "via: 1.1 nghttpx",
            },
            response.RegularFields);
        Assert.AreEqual("-foo-\n", response.BodyText);
        Assert.IsTrue(response.Fin);
        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    public async Task Get2503_AnswersTheFieldsItsStdoutListsWithNoServerField()
    {
        using var client = new Http3TestClient();
        await StartAsync(client, Connect(Backend(Reply2503)));

        client.Send(0, Get(client, "/2503"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        var response = client.Response(0);
        Assert.AreEqual("200", response.Status);
        CollectionAssert.AreEqual(
            new[]
            {
                "date: Tue, 09 Nov 2010 14:49:00 GMT",
                "content-length: 6",
                "content-type: text/html",
                "funny-head: yesyes",
                "via: 1.1 nghttpx",
            },
            response.RegularFields);
        Assert.AreEqual("-foo-\n", response.BodyText);
    }

    [TestMethod]
    public async Task PostWithABody_ForwardsTheBody()
    {
        using var client = new Http3TestClient();
        var backend = Backend("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");
        await StartAsync(client, Connect(backend));

        client.Send(0, [.. Get(client, "/p", ("content-length", "4")).Take(0), .. client.Request("POST", "/p", ("content-length", "4")), .. Data("data")], fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        Assert.EndsWith("\r\n\r\ndata", Latin1(backend.WrittenBytes));
        Assert.IsTrue(client.Response(0).Fin);
    }

    [TestMethod]
    public async Task HeadRequest_AnswersHeadersOnly()
    {
        using var client = new Http3TestClient();
        await StartAsync(client, Connect(Backend("HTTP/1.1 200 OK\r\nContent-Length: 6\r\n\r\n")));

        client.Send(0, client.Request("HEAD", "/h"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        var response = client.Response(0);
        Assert.IsEmpty(response.Body);
        Assert.IsTrue(response.Fin);
        Assert.Contains("content-length: 6", response.RegularFields);
    }

    [TestMethod]
    public async Task BackendThatClosesWithoutAReply_EndsTheStreamWith502BadGatewayAndNoServerField()
    {
        using var client = new Http3TestClient();
        await StartAsync(client, Connect(Backend(string.Empty)));

        client.Send(0, Get(client, "/x"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        var response = client.Response(0);
        Assert.AreEqual("502", response.Status);
        Assert.Contains("content-type: text/html; charset=UTF-8", response.RegularFields);
        Assert.Contains("via: 1.1 nghttpx", response.RegularFields);
        Assert.DoesNotContain(field => field.StartsWith("server:", StringComparison.Ordinal), response.RegularFields);
        Assert.Contains("502 Bad Gateway", response.BodyText);
        Assert.IsTrue(response.Fin);
    }

    [TestMethod]
    public async Task BackendThatCannotBeReached_EndsTheStreamWith502BadGateway()
    {
        using var client = new Http3TestClient();
        await StartAsync(client, new InMemoryOriginConnector());

        client.Send(0, Get(client, "/x"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        Assert.AreEqual("502", client.Response(0).Status);
    }

    [TestMethod]
    public async Task BackendThatClosesInTheMiddleOfABody_ResetsTheStreamH3InternalError()
    {
        using var client = new Http3TestClient();
        await StartAsync(client, Connect(Backend("HTTP/1.1 200 OK\r\nContent-Length: 10\r\n\r\nabc")));

        client.Send(0, Get(client, "/x"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        Assert.AreEqual((ulong)Http3ErrorCode.InternalError, client.ResetCode(0));
        Assert.IsNull(client.CloseCode, "The connection carries on.");
    }

    [TestMethod]
    public async Task ServeAsync_ThroughTheServer_AnswersARequestOnAFlow()
    {
        using var client = new Http3TestClient();
        using var key = RSA.Create(2048);
        var server = new Http3GatewayUpstreamTestCaseServer(Connect(Backend(Reply2503)), BackendPort, [client.SelfSignedCertificate()], client.Key);
        _ = server.ServeAsync(client.Flow, Context(client.Log, client.Time, client.Exchange.Token));
        await client.OpenAsync(Token);

        client.Send(0, Get(client, "/2503"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        Assert.AreEqual("-foo-\n", client.Response(0).BodyText);
        await client.Exchange.CancelAsync();
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledBeforeTheHandshake_StopsTheConnection()
    {
        using var client = new Http3TestClient();
        var server = new Http3GatewayUpstreamTestCaseServer(new InMemoryOriginConnector(), BackendPort, [client.SelfSignedCertificate()], client.Key);
        using var exchange = new CancellationTokenSource();
        var serving = server.ServeAsync(client.Flow, Context(client.Log, client.Time, exchange.Token));

        await exchange.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => serving.WaitAsync(TimeSpan.FromSeconds(30), Token));
    }

    [TestMethod]
    public async Task ServeAsync_NoHeadTimeout_StillAnswersARequest()
    {
        using var client = new Http3TestClient();
        var server = new Http3GatewayUpstreamTestCaseServer(Connect(Backend(Reply2503)), BackendPort, [client.SelfSignedCertificate()], client.Key);
        var limits = Surl.Protocol.Abstractions.ExchangeLimits.Default with { HeadTimeout = Timeout.InfiniteTimeSpan };
        _ = server.ServeAsync(client.Flow, Context(client.Log, client.Time, client.Exchange.Token, limits));
        await client.OpenAsync(Token);

        client.Send(0, Get(client, "/2503"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        Assert.AreEqual("200", client.Response(0).Status);
        await client.Exchange.CancelAsync();
    }

    [TestMethod]
    public void Schemes_AreHttps() =>
        CollectionAssert.AreEqual(
            new[] { "https" },
            new Http3GatewayUpstreamTestCaseServer(new InMemoryOriginConnector(), BackendPort, [], RSA.Create(2048)).Schemes.ToArray());

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        using var key = RSA.Create(2048);
        var connector = new InMemoryOriginConnector();

        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3GatewayUpstreamTestCaseServer(null!, BackendPort, [], key));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3GatewayUpstreamTestCaseServer(connector, BackendPort, null!, key));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3GatewayUpstreamTestCaseServer(connector, BackendPort, [], null!));
    }

    [TestMethod]
    public async Task ServeAsync_NullArgument_Throws()
    {
        using var key = RSA.Create(2048);
        var server = new Http3GatewayUpstreamTestCaseServer(new InMemoryOriginConnector(), BackendPort, [], key);
        var context = Context(new RecordingExchangeLog(), TimeProvider.System, Token);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, context));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(new Surl.Protocol.Abstractions.InMemoryDatagramFlow(new byte[1], []), null!));
    }

    [TestMethod]
    public async Task ApplicationProtocol_NoConnection_Throws()
    {
        var application = new Http3GatewayApplicationProtocol(
            new NghttpxGatewayBackend(new InMemoryOriginConnector(), BackendPort), Context(new RecordingExchangeLog(), TimeProvider.System, Token));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => application.ServeAsync(null!, Token));
        Assert.IsFalse(application.HasStarted);
    }

    private static byte[] Get(Http3TestClient client, string path, params (string Name, string Value)[] extra) =>
        client.Headers([(":method", "GET"), (":scheme", "https"), (":authority", GatewayAuthority), (":path", path), .. extra]);

    private static byte[] Data(string text) => Http3TestClient.Frame(Http3FrameType.Data, Ascii(text));

    private static InMemoryConnection Backend(string reply) =>
        new([Ascii(reply)], peerHalfClosesWhenExhausted: true);

    private static InMemoryOriginConnector Connect(InMemoryConnection backend) =>
        new InMemoryOriginConnector().ScriptConnection("127.0.0.1", BackendPort, backend);

    private async Task StartAsync(Http3TestClient client, InMemoryOriginConnector connector)
    {
        var context = Context(client.Log, client.Time, client.Exchange.Token);
        var application = new Http3GatewayApplicationProtocol(new NghttpxGatewayBackend(connector, BackendPort), context);
        _ = client.Serve(application, client.Time, Token);
        await client.OpenAsync(Token);
    }
}
