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

    // Where the one DATA frame of https-proxy-h2-connect/request.bin lies, on its CONNECT stream.
    private const int CurlsConnectDataStart = 114;
    private const int CurlsConnectDataLength = 81;

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
    public void H2ProxyConnection_CurlsConnectStreamData_IsWithheldFromTheLog()
    {
        var recorded = Fixture("https-proxy-h2-connect", "request.bin");
        var dataEnd = CurlsConnectDataStart + FrameHeaderLength + CurlsConnectDataLength;
        CollectionAssert.AreEqual(
            new byte[] { 0, 0, CurlsConnectDataLength, 0x0, 0x0, 0, 0, 0, 1 },
            recorded[CurlsConnectDataStart..(CurlsConnectDataStart + FrameHeaderLength)]);
        StringAssert.StartsWith(Latin1(recorded[(CurlsConnectDataStart + FrameHeaderLength)..dataEnd]), "GET /a HTTP/1.1\r\n");

        var logged = HttpProxyRedactionTestBytes.Logged(ProxyRedactor(), [recorded], string.Empty, []);

        Assert.DoesNotContain("GET /a HTTP/1.1", Latin1(logged));
        CollectionAssert.AreEqual(
            (byte[])
            [
                .. recorded[..(CurlsConnectDataStart + FrameHeaderLength)],
                .. "****"u8,
                .. recorded[dataEnd..],
            ],
            logged,
            Latin1(logged));
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
    public void Http1RequestThroughTheProxy_ApiKeyFields_AreLoggedAsMasks()
    {
        foreach (var name in HttpWithheldHeaderNames.ApiKeysAndSessionTokens)
        {
            const string Forwarded = "GET http://origin.test/a HTTP/1.1\r\nHost: origin.test\r\n";
            const string Tunnelled = "GET /a HTTP/1.1\r\nHost: origin.test\r\n";
            var sent = $"{name}: s3cret key\r\n\r\n";
            var masked = $"{name}: ****\r\n\r\n";
            var lower = name.ToLowerInvariant();

            const string ConnectLine = "CONNECT origin.test:80 HTTP/1.1\r\n";

            Assert.AreEqual(Forwarded + masked, Logged(Forwarded + sent), name);
            Assert.AreEqual(ConnectLine + masked, Logged(ConnectLine + sent), name);
            Assert.AreEqual(
                HttpProxyTunnelRedactionTestKit.Connect + Tunnelled + masked,
                HttpProxyTunnelRedactionTestKit.LoggedTunnel(Tunnelled + sent),
                name);
            Assert.AreEqual(
                $"{HttpProxyTunnelRedactionTestKit.Connect}{Tunnelled}{lower}: ****\r\n\r\n",
                HttpProxyTunnelRedactionTestKit.LoggedTunnelByteByByte($"{Tunnelled}{lower}: s3cret\r\n\r\n"),
                name);
        }
    }

    [TestMethod]
    public void ConnectTunnelReadAsTextLines_ApiKeyFields_AreLoggedAsMasks()
    {
        // The PROXY line of curl's --haproxy-protocol makes the proxy judge the tunnel line by line.
        const string Start = "PROXY TCP4 127.0.0.1 127.0.0.1 50000 80\r\nGET /a HTTP/1.1\r\n";

        foreach (var name in HttpWithheldHeaderNames.ApiKeysAndSessionTokens)
        {
            var sent = $"{Start}{name}: s3cret key\r\n \tfolded s3cret\r\n{name.ToUpperInvariant()}:s3cret\r\n{name}:\r\nAccept: */*\r\n\r\n";
            var expected = $"{HttpProxyTunnelRedactionTestKit.Connect}{Start}{name}: ****\r\n \t****\r\n{name.ToUpperInvariant()}:****\r\n{name}:\r\nAccept: */*\r\n\r\n";

            Assert.AreEqual(expected, HttpProxyTunnelRedactionTestKit.LoggedTunnel(sent), name);
            Assert.AreEqual(expected, HttpProxyTunnelRedactionTestKit.LoggedTunnelByteByByte(sent), name);
        }
    }

    [TestMethod]
    public void Http2RequestThroughTheProxy_ApiKeyFields_WithholdTheHeaderBlock()
    {
        foreach (var name in HttpWithheldHeaderNames.ApiKeysAndSessionTokens)
        {
            var headers = RawHeaders(name.ToLowerInvariant(), Token);
            var start = Http2TestClient.Start();

            var logged = HttpProxyRedactionTestBytes.Logged(ProxyRedactor(), [Bytes(Connect).ToArray()], Established, [start, headers]);

            Assert.DoesNotContain(Token, Latin1(logged), name);
            CollectionAssert.AreEqual((byte[])[.. Bytes(Connect).Span, .. start, .. Withheld(headers)], logged, name);
        }
    }

    [TestMethod]
    public void ConnectTunnel_FtpPassAndAcct_AreLoggedAsMasks()
    {
        string[] turns =
        [
            "S:220 ready\r\n",
            "USER alice\r\n",
            "S:331 password\r\n",
            "PASS s3cret\r\n",
            "S:332 account\r\n",
            "ACCT billing-s3cret\r\n",
        ];
        const string Expected = HttpProxyTunnelRedactionTestKit.Connect + "USER alice\r\nPASS ****\r\nACCT ****\r\n";

        Assert.AreEqual(Expected, HttpProxyTunnelRedactionTestKit.LoggedTunnel(turns));
        Assert.AreEqual(Expected, HttpProxyTunnelRedactionTestKit.LoggedTunnelByteByByte(turns));
    }

    [TestMethod]
    [DataRow("AUTH PLAIN AGFsaWNlAHMzY3JldA==", "AUTH PLAIN ****", "235 ok")]
    [DataRow("A1 AUTHENTICATE PLAIN AGFsaWNlAHMzY3JldA==", "A1 AUTHENTICATE PLAIN ****", "A1 OK")]
    [DataRow("AUTH LOGIN\r\n|334 VXNlcm5hbWU6|AGFsaWNlAHMzY3JldA==", "AUTH LOGIN\r\n****", "235 ok")]
    [DataRow("A1 AUTHENTICATE PLAIN\r\n|+ |AGFsaWNlAHMzY3JldA==", "A1 AUTHENTICATE PLAIN\r\n****", "A1 OK")]
    public void ConnectTunnel_MailSaslCredentials_AreAbsentFromTheLog(string exchange, string expected, string answer)
    {
        var parts = exchange.Split('|');
        string[] turns = parts.Length == 1
            ? [parts[0] + "\r\n", $"S:{answer}\r\n"]
            : [parts[0], $"S:{parts[1]}\r\n", parts[2] + "\r\n", $"S:{answer}\r\n"];
        var logged = HttpProxyTunnelRedactionTestKit.Connect + expected + "\r\n";

        Assert.AreEqual(logged, HttpProxyTunnelRedactionTestKit.LoggedTunnel(turns));
        Assert.AreEqual(logged, HttpProxyTunnelRedactionTestKit.LoggedTunnelByteByByte(turns));
    }

    [TestMethod]
    public void ConnectTunnel_ImapLogin_PasswordIsAbsentFromTheLog()
    {
        string[] turns = ["S:* OK ready\r\n", "A1 CAPABILITY\r\n", "S:A1 OK\r\n", "A2 LOGIN alice s3cret\r\n"];
        const string Expected = HttpProxyTunnelRedactionTestKit.Connect + "A1 CAPABILITY\r\nA2 LOGIN alice ****\r\n";

        Assert.AreEqual(Expected, HttpProxyTunnelRedactionTestKit.LoggedTunnel(turns));
        Assert.AreEqual(Expected, HttpProxyTunnelRedactionTestKit.LoggedTunnelByteByByte(turns));
    }

    [TestMethod]
    public void ConnectTunnel_MqttConnectPassword_IsAbsentFromTheLog()
    {
        // Pinned upstream curl 8.21.0 with -p: its CONNECT, then MQTT CONNECT and SUBSCRIBE in the tunnel.
        var recorded = Fixture("proxytunnel-mqtt-user-and-password", "request.bin");
        var headLength = recorded.AsSpan().IndexOf("\r\n\r\n"u8) + 4;
        var head = recorded[..headLength];
        var tunnelled = recorded[headLength..];
        var password = tunnelled.AsSpan().IndexOf("\0\u0006secret"u8);
        Assert.AreEqual(0x28, tunnelled[1]);
        byte[] expected =
        [
            .. head,
            0x10, 0x26, // the Remaining Length counts what is written
            .. tunnelled[2..password],
            .. "\0\u0004****"u8,
            .. tunnelled[(password + 8)..],
        ];

        AssertTunnelLoggedAtOnceAndByteByByte(expected, head, tunnelled);
    }

    [TestMethod]
    public void ConnectTunnel_MqttConnectPasswordAfterCurlsProxyProtocolLine_IsAbsentFromTheLog()
    {
        // The same with --haproxy-protocol: curl sends a PROXY line in the tunnel before the CONNECT.
        var recorded = Fixture("proxytunnel-haproxy-mqtt-user-and-password", "request.bin");
        var headLength = recorded.AsSpan().IndexOf("\r\n\r\n"u8) + 4;
        var head = recorded[..headLength];
        var tunnelled = recorded[headLength..];
        var connect = tunnelled.AsSpan().IndexOf((byte)'\n') + 1;
        var password = tunnelled.AsSpan().IndexOf("\0\u0006secret"u8);
        StringAssert.StartsWith(Latin1(tunnelled), "PROXY TCP4 ");
        Assert.AreEqual(0x10, tunnelled[connect]);
        byte[] expected =
        [
            .. head,
            .. tunnelled[..(connect + 1)],
            0x26,
            .. tunnelled[(connect + 2)..password],
            .. "\0\u0004****"u8,
            .. tunnelled[(password + 8)..],
        ];

        AssertTunnelLoggedAtOnceAndByteByByte(expected, head, tunnelled);
    }

    [TestMethod]
    public void ConnectTunnel_LdapBindAfterALongBodyRunsIntoTheConnectLine_IsAbsentFromTheLog()
    {
        var upload = $"POST http://o/a HTTP/1.1\r\nHost: o\r\nContent-Length: 70000\r\n\r\n{new string('a', 70000)}";
        byte[] bind = [0x30, 0x0F, 0x02, 0x01, 0x01, 0x60, 0x0A, 0x02, 0x01, 0x03, 0x04, 0x00, 0x80, 0x03, .. "pw!"u8, 0x42, 0x00];

        var logged = Logged(upload, Connect, Latin1(bind));

        Assert.DoesNotContain("pw!", logged);
        StringAssert.EndsWith(logged, "\u0080\u0004****B\0");
    }

    [TestMethod]
    public void ConnectTunnel_LdapSimpleBindPassword_IsAbsentFromTheLog()
    {
        // The bind and unbind pinned upstream curl 8.21.0 sent for `-u alice:secret ldap://...`
        // (Surl.Protocol.Ldap.UnitTests/Fixtures/simple-bind-base-search).
        byte[] bind =
        [
            0x30, 0x84, 0, 0, 0, 0x1B, 0x02, 0x01, 0x01, 0x60, 0x84, 0, 0, 0, 0x12, 0x02, 0x01, 0x03,
            0x04, 0x05, .. "alice"u8, 0x80, 0x06, .. "secret"u8,
        ];
        byte[] unbind = [0x30, 0x84, 0, 0, 0, 0x05, 0x02, 0x01, 0x03, 0x42, 0x00];
        var head = Bytes(Connect).ToArray();
        byte[] expected = [.. head, .. bind[..^8], 0x80, 0x04, .. "****"u8, .. unbind];

        AssertTunnelLoggedAtOnceAndByteByByte(expected, head, [.. bind, .. unbind]);
    }

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

    // What is logged of a CONNECT head, the proxy's 200 and the tunnelled bytes, whether the client's
    // bytes arrive in one chunk each or one at a time.
    private static void AssertTunnelLoggedAtOnceAndByteByByte(byte[] expected, byte[] head, byte[] tunnelled)
    {
        var atOnce = HttpProxyRedactionTestBytes.Logged(ProxyRedactor(), [head], Established, [tunnelled]);
        var byteByByte = HttpProxyRedactionTestBytes.Logged(
            ProxyRedactor(), OneByteEach(head), Established, OneByteEach(tunnelled));

        Assert.DoesNotContain("secret", Latin1(atOnce));
        CollectionAssert.AreEqual(expected, atOnce, Latin1(atOnce));
        CollectionAssert.AreEqual(expected, byteByByte, Latin1(byteByByte));
    }

    private static byte[][] OneByteEach(byte[] bytes) => [.. bytes.Select(value => new[] { value })];

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
