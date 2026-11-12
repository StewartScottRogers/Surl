using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.HttpProxy.ProxyTestKit;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// The HTTP proxy server half of upstream test cases (ADR-0080 decision 2), driven with inline
/// cases through <see cref="InMemoryConnection"/> and <see cref="InMemoryOriginConnector"/>.
/// </summary>
[TestClass]
public sealed class HttpProxyUpstreamTestCaseServerTests
{
    private const string Data = "HTTP/1.1 200 OK\r\nContent-Length: 6\r\n\r\n-foo-\n";

    private const string Established = "HTTP/1.1 200 Mighty fine indeed\r\n\r\n";

    private const string ConnectRequest = "CONNECT test.80:8990 HTTP/1.1\r\nHost: test.80:8990\r\n\r\n";

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        var replies = Replies();

        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpProxyUpstreamTestCaseServer(null!, new InMemoryOriginConnector()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpProxyUpstreamTestCaseServer(replies, null!));
    }

    [TestMethod]
    public async Task ServeAsync_NullArguments_Throw()
    {
        var server = new HttpProxyUpstreamTestCaseServer(Replies(), new InMemoryOriginConnector());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, Context(new RecordingExchangeLog())));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(Connection(), null!));
    }

    [TestMethod]
    public void Schemes_IsHttp()
    {
        var server = new HttpProxyUpstreamTestCaseServer(Replies(), new InMemoryOriginConnector());

        CollectionAssert.AreEqual(new[] { "http" }, server.Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_AnAbsoluteFormGet_SendsTheDataPartVerbatimAndRecordsTheRequest()
    {
        const string request = "GET http://test.1/1 HTTP/1.1\r\nHost: test.1\r\n\r\n";
        var replies = Replies(("data", Data));
        var client = Connection(request);

        await Serve(replies, client, new InMemoryOriginConnector());

        Assert.AreEqual(Data, Latin1(client.WrittenBytes));
        Assert.AreEqual(request, Latin1(replies.ReceivedBytes));
        Assert.IsFalse(client.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_SwscloseInThePart_ClosesAfterTheReply()
    {
        const string data = "HTTP/1.1 200 OK\r\nFunny-head: swsclose\r\n\r\n";
        var client = Connection("GET http://t/1 HTTP/1.1\r\nHost: t\r\n\r\nGET http://t/1 HTTP/1.1\r\nHost: t\r\n\r\n");

        await Serve(Replies(("data", data)), client, new InMemoryOriginConnector());

        Assert.AreEqual(data, Latin1(client.WrittenBytes));
        Assert.IsTrue(client.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_BodiesOfEveryFraming_AreReadSoTheNextRequestIsFound()
    {
        const string post = "POST http://t/1 HTTP/1.1\r\nHost: t\r\nContent-Length: 4\r\n\r\nabcd";
        const string chunked = "POST http://t/1 HTTP/1.1\r\nHost: t\r\nTransfer-Encoding: chunked\r\n\r\n3;x=y\r\nabc\r\n0\r\nTrailer: v\r\n\r\n";
        const string get = "GET http://t/1 HTTP/1.1\r\nHost: t\r\n\r\n";
        var replies = Replies(("data", Data));
        var client = Connection(post + chunked + get);

        await Serve(replies, client, new InMemoryOriginConnector());

        Assert.AreEqual(Data + Data + Data, Latin1(client.WrittenBytes));
        Assert.AreEqual(post + chunked + get, Latin1(replies.ReceivedBytes));
    }

    [TestMethod]
    public async Task ServeAsync_BodiesCutShortOrMalformed_EndTheReadWithoutHanging()
    {
        var replies = Replies(("data", Data));
        var shortBody = Connection("POST http://t/1 HTTP/1.1\r\nHost: t\r\nContent-Length: 40\r\n\r\nabcd");
        var badChunkSize = Connection("POST http://t/1 HTTP/1.1\r\nHost: t\r\nTransfer-Encoding: chunked\r\n\r\nzz\r\n");
        var cutChunk = Connection("POST http://t/1 HTTP/1.1\r\nHost: t\r\nTransfer-Encoding: chunked\r\n\r\n9\r\nabc");
        var noChunkLine = Connection("POST http://t/1 HTTP/1.1\r\nHost: t\r\nTransfer-Encoding: chunked\r\n\r\n");
        var cutTrailer = Connection("POST http://t/1 HTTP/1.1\r\nHost: t\r\nTransfer-Encoding: chunked\r\n\r\n0\r\n");
        var negativeChunkSize = Connection("POST http://t/1 HTTP/1.1\r\nHost: t\r\nTransfer-Encoding: chunked\r\n\r\nffffffffffffffff\r\n");

        await Serve(replies, shortBody, new InMemoryOriginConnector());
        await Serve(replies, badChunkSize, new InMemoryOriginConnector());
        await Serve(replies, cutChunk, new InMemoryOriginConnector());
        await Serve(replies, noChunkLine, new InMemoryOriginConnector());
        await Serve(replies, negativeChunkSize, new InMemoryOriginConnector());
        await Serve(replies, cutTrailer, new InMemoryOriginConnector());

        Assert.AreEqual(Data, Latin1(shortBody.WrittenBytes));
        Assert.AreEqual(Data, Latin1(badChunkSize.WrittenBytes));
        Assert.AreEqual(Data, Latin1(cutChunk.WrittenBytes));
        Assert.AreEqual(Data, Latin1(noChunkLine.WrittenBytes));
        Assert.AreEqual(Data, Latin1(negativeChunkSize.WrittenBytes));
        Assert.AreEqual(Data, Latin1(cutTrailer.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_ARequestWithNoHead_EndsWithNoBytesAndSaysWhy()
    {
        var log = new RecordingExchangeLog();
        var client = Connection("this is not a request\r\n\r\n");

        await Serve(Replies(("data", Data)), client, new InMemoryOriginConnector(), log);

        Assert.IsEmpty(client.WrittenBytes);
        Assert.IsTrue(log.Notes.Any(note => note.StartsWith("No request head was read:", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_AMissingPart_ClosesWithNoBytes()
    {
        var log = new RecordingExchangeLog();
        var client = Connection("GET http://t/1 HTTP/1.1\r\nHost: t\r\n\r\n");

        await Serve(Replies(), client, new InMemoryOriginConnector(), log);

        Assert.IsEmpty(client.WrittenBytes);
        Assert.IsTrue(client.WritesCompleted);
        CollectionAssert.Contains(log.Notes.ToList(), "GET http://t/1: the case has no <data> part; closed with no bytes.");
    }

    [TestMethod]
    public async Task ServeAsync_ConnectWithA2xxPart_TunnelsToLoopbackAtTheConnectPort()
    {
        var origin = Connection("pong");
        var origins = new InMemoryOriginConnector().ScriptConnection("127.0.0.1", 8990, origin);
        var replies = Replies(("connect", Established));
        var client = Connection(ConnectRequest, "GET /80 HTTP/1.1\r\nHost: test.80:8990\r\n\r\n");

        await Serve(replies, client, origins);

        Assert.AreEqual(Established + "pong", Latin1(client.WrittenBytes));
        Assert.AreEqual("GET /80 HTTP/1.1\r\nHost: test.80:8990\r\n\r\n", Latin1(origin.WrittenBytes));
        Assert.AreEqual(ConnectRequest, Latin1(replies.ReceivedBytes), "The proxy's record stops where the tunnel opens.");
        Assert.AreEqual("127.0.0.1", origins.Requests.Single().Host);
    }

    [TestMethod]
    public async Task ServeAsync_ConnectWithANon2xxPart_AnswersItAndOpensNoTunnel()
    {
        const string refused = "HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n";
        var origins = new InMemoryOriginConnector();
        var client = Connection(ConnectRequest);

        await Serve(Replies(("connect", refused)), client, origins);

        Assert.AreEqual(refused, Latin1(client.WrittenBytes));
        Assert.IsEmpty(origins.Requests);
    }

    [TestMethod]
    public async Task ServeAsync_ConnectWithAShortPart_IsNotATunnel()
    {
        var origins = new InMemoryOriginConnector();
        var client = Connection(ConnectRequest);

        await Serve(Replies(("connect", "HTTP/1.1")), client, origins);

        Assert.AreEqual("HTTP/1.1", Latin1(client.WrittenBytes));
        Assert.IsEmpty(origins.Requests);
    }

    [TestMethod]
    public async Task ServeAsync_ConnectToAnOriginThatRefuses_ClosesWithNoBytes()
    {
        var log = new RecordingExchangeLog();
        var origins = new InMemoryOriginConnector().ScriptFailure("127.0.0.1", 8990, OriginConnectionFailure.Refused);
        var client = Connection(ConnectRequest);

        await Serve(Replies(("connect", Established)), client, origins, log);

        Assert.IsEmpty(client.WrittenBytes);
        Assert.IsTrue(client.WritesCompleted);
        CollectionAssert.Contains(log.Notes.ToList(), "CONNECT test.80:8990: 127.0.0.1:8990 could not be reached (Refused); closed with no bytes.");
    }

    [TestMethod]
    public async Task ServeAsync_ConnectWithoutAPort_ClosesWithNoBytes()
    {
        var log = new RecordingExchangeLog();
        var origins = new InMemoryOriginConnector();
        var client = Connection("CONNECT test.80 HTTP/1.1\r\nHost: test.80\r\n\r\n");

        await Serve(Replies(("connect", Established)), client, origins, log);

        Assert.IsEmpty(client.WrittenBytes);
        Assert.IsEmpty(origins.Requests);
        CollectionAssert.Contains(log.Notes.ToList(), "CONNECT test.80: not host:port; closed with no bytes.");
    }

    private static HttpProxyUpstreamTestCaseReplies Replies(params (string Name, string Content)[] parts) =>
        new(parts.ToDictionary(part => part.Name, part => Encoding.Latin1.GetBytes(part.Content)), []);

    private static Task Serve(HttpProxyUpstreamTestCaseReplies replies, InMemoryConnection client, InMemoryOriginConnector origins, RecordingExchangeLog? log = null) =>
        new HttpProxyUpstreamTestCaseServer(replies, origins).ServeAsync(client, Context(log ?? new RecordingExchangeLog()));
}
