using System.Buffers;
using System.Text;
using Surl.Hpack;
using Surl.Http2;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.HttpProxy.HttpProxyRedactionTestBytes;
using static Surl.Protocol.HttpProxy.ProxyTestKit;

namespace Surl.Protocol.HttpProxy;

[TestClass]
public sealed class HttpProxyExchangeLogRedactionTests
{
    private const string Established = "HTTP/1.1 200 Connection established\r\n\r\n";
    private const string Connect = "CONNECT origin.test:80 HTTP/1.1\r\nHost: origin.test:80\r\n\r\n";

    // Where the one HEADERS frame of proxy-http2-prior-knowledge-connect/request-2.bin lies: after
    // the preface (24 bytes), SETTINGS (27) and WINDOW_UPDATE (13); its payload is 49 bytes.
    private const int PriorKnowledgeHeadersStart = 64;

    // The same frame in proxy-haproxy-http2-connect/request-2.bin, behind curl's 44-byte PROXY line.
    private const int HaProxyHeadersStart = 108;

    private const int FrameHeaderLength = 9;
    private const int CurlsHeaderBlockLength = 49;
    private const string CurlsAuthorization = "Basic YTpzM2NyZXQ=";

    private static IReceivedBytesRedactor ProxyRedactor() =>
        new HttpProxyProtocolServer(new InMemoryOriginConnector())
            .CreateRedactor(new ExchangeRedactionContext(1, "http"));

    private static string Logged(params string[] chunks)
    {
        var redactor = ProxyRedactor();
        var output = new ArrayBufferWriter<byte>();
        foreach (var chunk in chunks)
        {
            redactor.Redact(Encoding.Latin1.GetBytes(chunk), output);
        }

        redactor.Flush(output);

        return Encoding.Latin1.GetString(output.WrittenSpan);
    }

    [TestMethod]
    public void H2ProxyConnection_HpackProxyAuthorization_IsAbsentFromTheLog()
    {
        var headers = RawHeaders("proxy-authorization", Credential);
        var start = Http2TestClient.Start();

        var logged = HttpProxyRedactionTestBytes.Logged(ProxyRedactor(), [start, headers], string.Empty, []);

        Assert.DoesNotContain(Token, Latin1(logged));
        CollectionAssert.AreEqual((byte[])[.. start, .. Withheld(headers)], logged);
    }

    [TestMethod]
    public void ConnectTunnel_HpackAuthorizationAfterThePreface_IsAbsentFromTheLog()
    {
        var headers = RawHeaders("authorization", Credential);
        var start = Http2TestClient.Start();

        var logged = HttpProxyRedactionTestBytes.Logged(ProxyRedactor(), [Bytes(Connect).ToArray()], Established, [start, headers]);

        Assert.DoesNotContain(Token, Latin1(logged));
        CollectionAssert.AreEqual((byte[])[.. Bytes(Connect).Span, .. start, .. Withheld(headers)], logged);
    }

    [TestMethod]
    public void ConnectTunnel_CurlsPriorKnowledgeRequest_WithholdsTheHeaderBlock()
    {
        var connect = Fixture("proxy-http2-prior-knowledge-connect", "request-1.bin");
        var tunnelled = Fixture("proxy-http2-prior-knowledge-connect", "request-2.bin");
        var blockEnd = AssertCarriesCurlsAuthorization(tunnelled, PriorKnowledgeHeadersStart);

        var logged = HttpProxyRedactionTestBytes.Logged(ProxyRedactor(), [connect], Established, [tunnelled]);

        var maskedConnect = Latin1(connect).Replace("Basic cDpwdw==", "Basic ****", StringComparison.Ordinal);
        Assert.Contains("Proxy-Authorization: Basic ****\r\n", maskedConnect);
        CollectionAssert.AreEqual(
            (byte[])
            [
                .. Bytes(maskedConnect).Span,
                .. tunnelled[..(PriorKnowledgeHeadersStart + FrameHeaderLength)],
                .. "****"u8,
                .. tunnelled[blockEnd..],
            ],
            logged,
            Latin1(logged));
    }

    [TestMethod]
    public void ConnectTunnel_CurlsProxyProtocolLineBeforeThePreface_WithholdsEveryLaterByte()
    {
        var connect = Fixture("proxy-haproxy-http2-connect", "request-1.bin");
        var tunnelled = Fixture("proxy-haproxy-http2-connect", "request-2.bin");
        _ = AssertCarriesCurlsAuthorization(tunnelled, HaProxyHeadersStart);
        var prefaceEnd = tunnelled.AsSpan().IndexOf(Http2ConnectionPreface.Preface) + Http2ConnectionPreface.Preface.Length;
        StringAssert.StartsWith(Latin1(tunnelled), "PROXY TCP4 ");

        var logged = HttpProxyRedactionTestBytes.Logged(ProxyRedactor(), [connect], Established, [tunnelled]);

        CollectionAssert.AreEqual((byte[])[.. connect, .. tunnelled[..prefaceEnd], .. "****"u8], logged, Latin1(logged));
    }

    [TestMethod]
    public void ForwardedGet_ProxyAuthorizationAndAuthorization_AreLoggedWithTheirCredentialsMasked() =>
        Assert.AreEqual(
            "GET http://origin.test/a HTTP/1.1\r\nHost: origin.test\r\nProxy-Authorization: Basic ****\r\nAuthorization: Bearer ****\r\n\r\n",
            Logged("GET http://origin.test/a HTTP/1.1\r\nHost: origin.test\r\nProxy-Authorization: Basic YWxpY2U6czNjcmV0\r\nAuthorization: Bearer tok3n\r\n\r\n"));

    [TestMethod]
    public void Connect_ProxyAuthorization_IsMaskedAndTheTunnelledBytesAreLoggedAsReceived() =>
        Assert.AreEqual(
            "CONNECT origin.test:443 HTTP/1.1\r\nHost: origin.test:443\r\nProxy-Authorization: Basic ****\r\n\r\n\x16\x03\x01tls-bytes",
            Logged("CONNECT origin.test:443 HTTP/1.1\r\nHost: origin.test:443\r\nProxy-Authorization: Basic YWxpY2U6\r\n\r\n", "\x16\x03\x01tls-bytes"));

    [TestMethod]
    public void TunnelledPlaintextRequest_AuthorizationIsMasked() =>
        Assert.AreEqual(
            "CONNECT o:80 HTTP/1.1\r\n\r\nGET / HTTP/1.1\r\nAuthorization: Basic ****\r\n\r\n",
            Logged("CONNECT o:80 HTTP/1.1\r\n\r\n", "GET / HTTP/1.1\r\nAuthorization: Basic YWxpY2U6\r\n\r\n"));

    [TestMethod]
    public async Task ServeAsync_ForwardedAuthorization_ReachesTheOriginUnchangedWhileTheLogHoldsOnlyMaskedBytes()
    {
        var origin = Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok");
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin);
        var client = Connection("GET http://origin.test/a HTTP/1.1\r\nHost: origin.test\r\nAuthorization: Bearer tok3n\r\n\r\n");

        await ServeAsync(client, origins);

        StringAssert.Contains(Latin1(origin.WrittenBytes), "Authorization: Bearer tok3n");
        Assert.AreEqual(new OriginConnectionRequest("origin.test", 80, ExchangeLimits.Default.HeadTimeout), origins.Requests.Single());
    }

    // Proves a recording holds the credential, Huffman-coded, in the HEADERS frame at headersStart,
    // and returns where that frame's header block ends.
    private static int AssertCarriesCurlsAuthorization(byte[] recorded, int headersStart)
    {
        var blockStart = headersStart + FrameHeaderLength;
        var blockEnd = blockStart + CurlsHeaderBlockLength;
        CollectionAssert.AreEqual(
            new byte[] { 0, 0, CurlsHeaderBlockLength, 0x1, 0x5, 0, 0, 0, 1 },
            recorded[headersStart..blockStart]);
        var decoded = new HpackDecoder(HpackDecoder.DefaultMaximumTableSize, int.MaxValue).Decode(recorded.AsSpan(blockStart..blockEnd));
        Assert.AreEqual(CurlsAuthorization, decoded.Fields.Single(field => field.Name == "authorization").Value);

        return blockEnd;
    }
}
