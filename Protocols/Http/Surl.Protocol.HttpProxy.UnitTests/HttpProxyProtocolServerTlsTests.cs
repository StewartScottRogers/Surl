using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.HttpProxy.ProxyTestKit;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// The HTTPS proxy (ADR-0088 decision 6 and Amendment 2): curl's connection reaches the proxy
/// already secured by the serving engine's handshake, so these tests hand the proxy an
/// <see cref="InMemoryConnection"/> with an initial <see cref="TlsSession"/>. The bytes inside
/// the TLS are the ones pinned upstream curl 8.21.0 sent to the recorder playing an HTTPS proxy
/// (Fixtures/README.md).
/// </summary>
[TestClass]
public sealed class HttpProxyProtocolServerTlsTests
{
    private const string OriginResponse = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";

    private static readonly TlsSession Http11Session =
        new(SslProtocols.Tls12, TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384, "http/1.1", null, null);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("https-proxy-forward")]
    [DataRow("https-proxy-http2-offered")]
    public async Task ServeAsync_CurlsForwardedGetOverTlsToTheProxy_NotesTheTlsAndForwardsAsOverPlainTcp(string fixture)
    {
        var origin = Connection(OriginResponse);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin);
        var client = SecuredConnection(Http11Session, Fixture(fixture, "request.bin"));
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(client, origins, log);

        Assert.AreEqual(
            "GET /a?b HTTP/1.1\r\nHost: origin.test\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nVia: 1.1 surl\r\n\r\n",
            Latin1(origin.WrittenBytes));
        Assert.AreEqual(FixtureText(fixture, "response.bin"), written);
        Assert.AreEqual(
            "HTTPS proxy: curl's connection to the proxy is TLS (Tls12, ALPN http/1.1); requests are read inside it.",
            log.Notes.First());
    }

    [TestMethod]
    public async Task ServeAsync_TlsWithNoAlpnAgreed_NotesAlpnNoneAndServesHttp11()
    {
        var origin = Connection(OriginResponse);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin);
        var session = Http11Session with { ApplicationProtocol = null };
        var client = SecuredConnection(session, Fixture("https-proxy-forward", "request.bin"));
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(client, origins, log);

        Assert.AreEqual(FixtureText("https-proxy-forward", "response.bin"), written);
        Assert.AreEqual(
            "HTTPS proxy: curl's connection to the proxy is TLS (Tls12, ALPN none); requests are read inside it.",
            log.Notes.First());
    }

    [TestMethod]
    public async Task ServeAsync_TlsAgreedAnAlpnNeitherH2NorHttp11_ClosesWithoutWritingOrConnectingAndNotesWhy()
    {
        var origins = new InMemoryOriginConnector();
        var client = SecuredConnection(Http11Session with { ApplicationProtocol = "spdy/3.1" }, "GET / HTTP/1.1\r\n\r\n"u8.ToArray());
        var log = new RecordingExchangeLog();

        var written = await ServeAsync(client, origins, log);

        Assert.AreEqual(string.Empty, written);
        Assert.IsEmpty(origins.Requests);
        CollectionAssert.AreEqual(
            new[] { "HTTPS proxy: the TLS handshake agreed ALPN spdy/3.1, which the proxy does not serve; the connection closes." },
            log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_PlainConnection_NotesNoTls()
    {
        var origin = Connection(OriginResponse);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin);
        var client = ConnectionOf(Fixture("forward-get", "request.bin"));
        var log = new RecordingExchangeLog();

        await ServeAsync(client, origins, log);

        Assert.IsFalse(log.Notes.Any(note => note.Contains("TLS", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_ConnectTunnelThroughTheHttpsProxy_RelaysANestedTlsRecordStreamUnchanged()
    {
        var clientHello = await CaptureClientHelloAsync("origin.test", TestContext.CancellationToken);
        byte[] clientApplicationData = [0x17, 0x03, 0x03, 0x00, 0x08, .. "\r\n\r\n\0\r\n\x7f"u8];
        byte[] originRecords =
        [
            0x16, 0x03, 0x03, 0x00, 0x06, 0x02, 0x00, 0x00, 0x02, 0x03, 0x03,
            0x17, 0x03, 0x03, 0x00, 0x13, .. "HTTP/1.1 200 OK\r\n\r\n"u8,
        ];
        var origin = new InMemoryConnection([originRecords]);
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 443, origin);
        var client = SecuredConnection(
            Http11Session,
            "CONNECT origin.test:443 HTTP/1.1\r\nHost: origin.test:443\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n"u8.ToArray(),
            clientHello,
            clientApplicationData);
        var log = new RecordingExchangeLog();

        await ServeAsync(client, origins, log);

        Assert.AreEqual((byte)0x16, clientHello[0], "the captured bytes are a TLS handshake record");
        CollectionAssert.AreEqual(clientHello.Concat(clientApplicationData).ToArray(), origin.WrittenBytes);
        CollectionAssert.AreEqual(
            "HTTP/1.1 200 Connection established\r\n\r\n"u8.ToArray().Concat(originRecords).ToArray(),
            client.WrittenBytes);
        CollectionAssert.Contains(log.Notes.ToList(), "Tunnelling to origin.test:443; answered 200.");
    }

    private static InMemoryConnection SecuredConnection(TlsSession session, params byte[][] chunks) =>
        new(
            chunks.Select(chunk => (ReadOnlyMemory<byte>)chunk),
            new IPEndPoint(IPAddress.Loopback, 3129),
            initialTlsSession: session);

    // The first TLS record a real client sends to origin.test: SslStream's ClientHello, caught by
    // a stream that keeps what is written and ends every read, so the handshake stops there.
    private static async Task<byte[]> CaptureClientHelloAsync(string host, CancellationToken cancellationToken)
    {
        await using var capture = new ClientHelloCaptureStream();
        await using var tls = new SslStream(capture, leaveInnerStreamOpen: true);
        try
        {
            await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = host }, cancellationToken);
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException)
        {
            // Expected: the stream ends before a ServerHello.
        }

        return capture.Written;
    }

    private sealed class ClientHelloCaptureStream : Stream
    {
        private readonly MemoryStream written = new();

        public byte[] Written => written.ToArray();

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => 0;

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromResult(0);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => written.Write(buffer, offset, count);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            written.Write(buffer.Span);

            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                written.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
