using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using Surl.Http2;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.HttpProxy.ProxyTestKit;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// The HTTPS proxy over HTTP/2 (ADR-0088 decision 6 and Amendment 5): a connection whose TLS
/// handshake agreed ALPN <c>h2</c> is served by <c>Surl.Http2</c>'s connection, each stream by
/// <see cref="Http2ProxyStreamHandler"/>. The bytes are the ones pinned upstream curl 8.21.0
/// (the static build with HTTP/2) sent with <c>--proxy-http2</c> to the recorder playing an
/// HTTPS proxy that offered <c>h2,http/1.1</c> (Fixtures/README.md).
/// </summary>
[TestClass]
public sealed class HttpProxyProtocolServerHttp2Tests
{
    private const string OriginResponse = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";

    // Where curl's bursts end in each recording: what it sent before the proxy's first frames,
    // then (CONNECT) its SETTINGS ACK and the tunnelled GET, then its SETTINGS ACK and GOAWAY.
    private const int ConnectFirstBurstEnd = 105;
    private const int ConnectTunnelledGetEnd = 204;
    private const int ForwardFirstBurstEnd = 132;

    private static readonly TlsSession H2Session =
        new(SslProtocols.Tls12, TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384, "h2", null, null);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("https-proxy-h2-connect", "h2")]
    [DataRow("https-proxy-h2-forward", "h2")]
    [DataRow("https-proxy-h2-offered-http11-chosen", "http/1.1")]
    public void Fixtures_ProxyOfferingH2ThenHttp11_CurlAgreesH2OnlyWithProxyHttp2(string fixture, string agreed)
    {
        Assert.AreEqual(agreed, FixtureText(fixture, "alpn.txt").Trim());
        Assert.AreEqual("0", FixtureText(fixture, "exitcode.txt").Trim());
        Assert.AreEqual("ok", FixtureText(fixture, "stdout.bin"));
    }

    [TestMethod]
    public async Task ServeAsync_CurlsConnectStream_Answers200AndRelaysTheTunnelInDataFrames()
    {
        var request = Fixture("https-proxy-h2-connect", "request.bin");
        var origin = Connection(OriginResponse);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 8080, origin);
        var client = new InteractiveClientConnection(H2Session);
        var log = new RecordingExchangeLog();

        var serving = new HttpProxyProtocolServer(origins).ServeAsync(client, Context(log));
        client.Send(request[..ConnectFirstBurstEnd]);
        await Http2TestClient.WaitUntilAsync(() => Response(client) is { HeaderBlocks.Count: > 0 });
        client.Send(request[ConnectFirstBurstEnd..ConnectTunnelledGetEnd]);
        await Http2TestClient.WaitUntilAsync(() => Response(client) is { EndedOnData: true } && origin.WrittenBytes.Length > 0);
        client.Send(request[ConnectTunnelledGetEnd..]);
        client.HalfClose();
        await serving;

        var response = Response(client)!;
        Assert.AreEqual(Http2FrameType.Settings, Http2TestClient.Frames(client.WrittenBytes)[0].Type);
        CollectionAssert.AreEqual(new[] { ":status" }, response.HeaderBlocks.Single().Select(field => field.Name).ToArray());
        Assert.AreEqual("200", response.Status);
        Assert.AreEqual(OriginResponse, response.BodyText);
        Assert.AreEqual("GET /a HTTP/1.1\r\nHost: origin.test:8080\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n", Latin1(origin.WrittenBytes));
        Assert.AreEqual("origin.test", origins.Requests.Single().Host);
        CollectionAssert.Contains(log.Notes.ToList(), "HTTPS proxy: curl's connection to the proxy is TLS (Tls12, ALPN h2); requests are read as HTTP/2 streams.");
        CollectionAssert.Contains(log.Notes.ToList(), "Stream 1: tunnelling to origin.test:8080; answered 200.");
    }

    [TestMethod]
    public async Task ServeAsync_CurlsForwardedGetStream_ForwardsItOverHttp11AndRelaysTheResponse()
    {
        var request = Fixture("https-proxy-h2-forward", "request.bin");
        var origin = Connection(OriginResponse);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin);
        var client = Secured(request[..ForwardFirstBurstEnd]);
        var log = new RecordingExchangeLog();

        await new HttpProxyProtocolServer(origins).ServeAsync(client, Context(log));

        var response = Http2TestClient.Responses(client.WrittenBytes)[1];
        Assert.AreEqual("GET /a?b HTTP/1.1\r\nHost: origin.test\r\nuser-agent: curl/8.21.0\r\naccept: */*\r\nVia: 1.1 surl\r\n\r\n", Latin1(origin.WrittenBytes));
        Assert.AreEqual("200", response.Status);
        CollectionAssert.AreEqual(
            new[] { ":status 200", "content-length 2", "via 1.1 surl" },
            response.Fields.Select(field => $"{field.Name} {field.Value}").ToArray());
        Assert.AreEqual("ok", response.BodyText);
        Assert.IsTrue(response.EndedOnData);
        CollectionAssert.Contains(log.Notes.ToList(), "Stream 1: forwarded GET to origin.test:80; the origin answered 200.");
    }

    [TestMethod]
    public async Task ServeAsync_H2AgreedButNotThePreface_ClosesWithoutWritingOrConnecting()
    {
        var origins = new InMemoryOriginConnector();
        var client = Secured("GET http://origin.test/ HTTP/1.1\r\n\r\n"u8.ToArray());
        var log = new RecordingExchangeLog();

        await new HttpProxyProtocolServer(origins).ServeAsync(client, Context(log));

        Assert.IsEmpty(client.WrittenBytes);
        Assert.IsEmpty(origins.Requests);
        Assert.AreEqual("The 36 bytes read are not the HTTP/2 connection preface ALPN h2 promised; the connection closes.", log.Notes.Last());
    }

    [TestMethod]
    public async Task ServeAsync_H2AgreedAndThePrefaceNeverCompletes_ClosesAtTheHeadTimeout()
    {
        var clock = new ManualTimeProvider(Now);
        var client = new InMemoryConnection([(ReadOnlyMemory<byte>)"PRI * "u8.ToArray()], peerHalfClosesWhenExhausted: false, initialTlsSession: H2Session);
        var log = new RecordingExchangeLog();

        var serving = new HttpProxyProtocolServer(new InMemoryOriginConnector()).ServeAsync(client, Context(log, clock: clock));
        await Http2TestClient.WaitUntilAsync(() => clock.ActiveTimerCount > 0);
        clock.Advance(ExchangeLimits.Default.HeadTimeout);
        await serving;

        Assert.IsEmpty(client.WrittenBytes);
        Assert.AreEqual("The HTTP/2 connection preface was not complete within the head timeout (6 bytes read); the connection closes.", log.Notes.Last());
    }

    private static Http2TestResponse? Response(InteractiveClientConnection client) =>
        Http2TestClient.Responses(client.WrittenBytes).GetValueOrDefault(1);

    private static InMemoryConnection Secured(byte[] bytes) =>
        new([bytes], new IPEndPoint(IPAddress.Loopback, 3129), initialTlsSession: H2Session);
}
