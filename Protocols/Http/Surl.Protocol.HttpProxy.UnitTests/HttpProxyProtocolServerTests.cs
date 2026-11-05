using Surl.Protocol.Abstractions;
using static Surl.Protocol.HttpProxy.ProxyTestKit;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// The proxy driven with the bytes pinned upstream curl 8.21.0 sent (Fixtures/README.md):
/// what reaches the origin, and what curl receives, which is what curl was measured accepting.
/// </summary>
[TestClass]
public sealed class HttpProxyProtocolServerTests
{
    private const string OriginResponse = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";

    [TestMethod]
    public async Task ServeAsync_CurlsForwardedGet_SendsOriginFormToTheOriginAndRelaysTheResponseCurlAccepted()
    {
        var origin = Connection(OriginResponse);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin);
        var client = ConnectionOf(Fixture("forward-get", "request.bin"));

        var written = await ServeAsync(client, origins);

        Assert.AreEqual(
            "GET /a?b HTTP/1.1\r\nHost: origin.test\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nVia: 1.1 surl\r\n\r\n",
            Latin1(origin.WrittenBytes));
        Assert.AreEqual(FixtureText("forward-get", "response.bin"), written);
        Assert.AreEqual(new OriginConnectionRequest("origin.test", 80, ExchangeLimits.Default.HeadTimeout), origins.Requests.Single());
        Assert.IsTrue(origin.Disposed);
    }

    [TestMethod]
    public async Task ServeAsync_CurlsConnect_AnswersTheExact200AndRelaysBothWaysUntilBothClose()
    {
        var origin = Connection(OriginResponse);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 8080, origin);
        var client = ConnectionOf(Fixture("connect-tunnel", "request-1.bin"), Fixture("connect-tunnel", "request-2.bin"));
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(client, origins, log);

        Assert.AreEqual(FixtureText("connect-tunnel", "response.bin"), written);
        Assert.AreEqual(FixtureText("connect-tunnel", "request-2.bin"), Latin1(origin.WrittenBytes));
        Assert.IsTrue(origin.WritesCompleted);
        Assert.IsTrue(client.WritesCompleted);
        Assert.IsTrue(origin.Disposed);
        CollectionAssert.Contains(log.Notes.ToList(), "Tunnelling to origin.test:8080; answered 200.");
        CollectionAssert.Contains(log.Notes.ToList(), "The tunnel to origin.test:8080 closed.");
    }

    [TestMethod]
    public async Task ServeAsync_ConnectTunnel_RelaysBytesTheOriginSendsFirst()
    {
        var origin = Connection("220 banner\r\n");
        var origins = new InMemoryOriginConnector().ScriptConnection("127.0.0.1", 25, origin);
        var client = Connection("CONNECT 127.0.0.1:25 HTTP/1.1\r\nHost: 127.0.0.1:25\r\n\r\n", "QUIT\r\n");

        var written = await ServeAsync(client, origins);

        Assert.AreEqual("HTTP/1.1 200 Connection established\r\n\r\n220 banner\r\n", written);
        Assert.AreEqual("QUIT\r\n", Latin1(origin.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_ForwardedGetToAnUnreachableOrigin_Answers502CurlPrintsAndKeepsTheConnection()
    {
        var origins = new InMemoryOriginConnector().ScriptFailure("origin.test", 80, OriginConnectionFailure.Unreachable);
        var client = ConnectionOf(Fixture("forward-unreachable-502", "request.bin"));
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(client, origins, log);

        Assert.AreEqual(FixtureText("forward-unreachable-502", "response.bin"), written);
        CollectionAssert.Contains(log.Notes.ToList(), "Origin origin.test:80 could not be reached (Unreachable); answered 502.");
    }

    [TestMethod]
    public async Task ServeAsync_ConnectToARefusedOrigin_Answers502ThatCurlReportsAsATunnelFailure()
    {
        var origins = new InMemoryOriginConnector().ScriptFailure("origin.test", 8080, OriginConnectionFailure.Refused);
        var client = ConnectionOf(Fixture("connect-refused-502", "request.bin"));
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(client, origins, log);

        Assert.AreEqual(FixtureText("connect-refused-502", "response.bin"), written);
        CollectionAssert.Contains(log.Notes.ToList(), "Origin origin.test:8080 could not be reached (Refused); answered 502.");
    }

    [TestMethod]
    public async Task ServeAsync_ConnectToAPortTheOriginRuleRefuses_Answers403ThatCurlReportsAsATunnelFailure()
    {
        var origins = new InMemoryOriginConnector().ScriptFailure("origin.test", 25, OriginConnectionFailure.NotAllowed);
        var client = ConnectionOf(Fixture("connect-not-allowed-403", "request.bin"));
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(client, origins, log);

        Assert.AreEqual(FixtureText("connect-not-allowed-403", "response.bin"), written);
        CollectionAssert.Contains(log.Notes.ToList(), "Origin origin.test:25 is not an allowed origin; answered 403.");
    }

    [TestMethod]
    public async Task ServeAsync_ConnectToAnOriginThatDidNotResolve_Answers502()
    {
        var origins = new InMemoryOriginConnector().ScriptFailure("nowhere.invalid", 443, OriginConnectionFailure.HostNotFound);
        var client = Connection("CONNECT nowhere.invalid:443 HTTP/1.1\r\nHost: nowhere.invalid:443\r\n\r\n");
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(client, origins, log);

        Assert.AreEqual(Answer(502, "Bad Gateway"), written);
        CollectionAssert.Contains(log.Notes.ToList(), "Origin nowhere.invalid did not resolve; answered 502.");
    }

    [TestMethod]
    public async Task ServeAsync_ConnectToAnOriginThatDidNotAcceptInTime_Answers504()
    {
        var origins = new InMemoryOriginConnector().ScriptFailure("127.0.0.1", 9, OriginConnectionFailure.TimedOut);
        var client = Connection("CONNECT 127.0.0.1:9 HTTP/1.1\r\nHost: 127.0.0.1:9\r\nProxy-Connection: close\r\n\r\n");
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(client, origins, log);

        Assert.AreEqual(Answer(504, "Gateway Timeout", closes: true), written);
        CollectionAssert.Contains(log.Notes.ToList(), "Origin 127.0.0.1:9 did not accept in time; answered 504.");
        Assert.IsTrue(client.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_RefusedConnectThenAnother_TriesTheSecondOnTheSameConnection()
    {
        var origin = Connection();
        var origins = new InMemoryOriginConnector()
            .ScriptFailure("127.0.0.1", 8080, OriginConnectionFailure.Refused)
            .ScriptConnection("127.0.0.1", 8080, origin);
        var request = "CONNECT 127.0.0.1:8080 HTTP/1.1\r\nHost: 127.0.0.1:8080\r\n\r\n";
        var client = Connection(request + request);

        var written = await ServeAsync(client, origins);

        Assert.AreEqual(Answer(502, "Bad Gateway") + "HTTP/1.1 200 Connection established\r\n\r\n", written);
        Assert.HasCount(2, origins.Requests);
    }

    [TestMethod]
    [DataRow("CONNECT 127.0.0.1 HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n")]
    [DataRow("CONNECT 127.0.0.1:0 HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n")]
    [DataRow("CONNECT http://127.0.0.1:80/ HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n")]
    public async Task ServeAsync_ConnectNotInAuthorityForm_Answers400AndCloses(string request)
    {
        var origins = new InMemoryOriginConnector();
        var client = Connection(request);

        var written = await ServeAsync(client, origins);

        Assert.AreEqual(Answer(400, "Bad Request", closes: true), written);
        Assert.IsEmpty(origins.Requests);
    }

    [TestMethod]
    public async Task ServeAsync_TunnelWhoseOriginFails_AbortsBothConnections()
    {
        var origin = Connection("x");
        origin.Abort();
        var origins = new InMemoryOriginConnector().ScriptConnection("127.0.0.1", 8080, origin);
        var client = HeldOpenConnection("CONNECT 127.0.0.1:8080 HTTP/1.1\r\nHost: 127.0.0.1:8080\r\n\r\n");
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(client, origins, log);

        Assert.AreEqual("HTTP/1.1 200 Connection established\r\n\r\n", written);
        Assert.IsTrue(client.Aborted);
        CollectionAssert.Contains(log.Notes.ToList(), "The tunnel to 127.0.0.1:8080 failed; both connections were aborted.");
    }

    [TestMethod]
    public async Task ServeAsync_ClientConnectionThatFails_EndsTheExchangeWithANote()
    {
        var client = Connection("GET http://origin.test/ HTTP/1.1\r\n");
        client.Abort();
        var log = new RecordingExchangeLog();

        await ServeAsync(client, new InMemoryOriginConnector(), log);

        CollectionAssert.Contains(log.Notes.ToList(), "A connection failed mid-exchange; the exchange ended.");
    }

    [TestMethod]
    public void Constructor_NullOriginConnector_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpProxyProtocolServer(null!));
    }

    [TestMethod]
    public async Task ServeAsync_NullArguments_Throw()
    {
        var server = new HttpProxyProtocolServer(new InMemoryOriginConnector());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, Context(new RecordingExchangeLog())));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(Connection(), null!));
    }

    [TestMethod]
    public void Schemes_AreHttpAndHttps()
    {
        var server = new HttpProxyProtocolServer(new InMemoryOriginConnector());

        CollectionAssert.AreEqual(new[] { "http", "https" }, server.Schemes.ToArray());
    }

    [TestMethod]
    public async Task WriteRefusalAsync_AnyRefusal_Writes503AndHalfCloses()
    {
        var server = new HttpProxyProtocolServer(new InMemoryOriginConnector());
        var connection = Connection();

        await server.WriteRefusalAsync(connection, ConnectionRefusal.TooManyConnections, CancellationToken.None);

        Assert.AreEqual("HTTP/1.1 503 Service Unavailable\r\nServer: surl\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", Latin1(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await server.WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnections, CancellationToken.None));
    }
}
