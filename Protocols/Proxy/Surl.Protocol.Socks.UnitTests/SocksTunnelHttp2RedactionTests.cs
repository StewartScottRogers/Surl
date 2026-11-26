using System.Buffers;
using System.Text;
using Surl.Hpack;
using Surl.Http2;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

[TestClass]
public sealed class SocksTunnelHttp2RedactionTests
{
    private const string Token = "YXBwOnMzY3JldC1wdw==";
    private const string Credential = "Basic " + Token;
    private const string UpgradeRequestStart =
        "GET / HTTP/1.1\r\nHost: localhost\r\nConnection: Upgrade, HTTP2-Settings\r\nUpgrade: h2c\r\n"
        + "HTTP2-Settings: AAMAAABk\r\nAuthorization: ";
    private const string UpgradeRequest = UpgradeRequestStart + Credential + "\r\n\r\n";
    private const string MaskedUpgradeRequest = UpgradeRequestStart + "Basic ****\r\n\r\n";
    private const string SwitchingProtocols = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: h2c\r\n\r\n";

    private static readonly byte[] Preface = Http2ConnectionPreface.Preface.ToArray();
    private static readonly byte[] Settings = Frame(0x4, 0, 0, []);

    [TestMethod]
    [DataRow("authorization")]
    [DataRow("proxy-authorization")]
    public void Tunnel_Http2WithPriorKnowledge_WithholdsTheCredentialHeaderBlock(string name)
    {
        var headers = Headers(name, Credential);
        StringAssert.Contains(Encoding.Latin1.GetString(headers), Credential);

        var logged = Logged(string.Empty, [], [[.. Preface, .. Settings, .. headers]]);

        AssertHoldsNoCredential(logged);
        CollectionAssert.AreEqual((byte[])[.. Preface, .. Settings, .. WithheldHeaders(headers)], logged);
    }

    [TestMethod]
    public void PriorKnowledgeTunnel_ApiKeyFields_WithholdTheHeaderBlock()
    {
        foreach (var name in HttpWithheldHeaderNames.ApiKeysAndSessionTokens)
        {
            var headers = Headers(name.ToLowerInvariant(), Credential);
            StringAssert.Contains(Encoding.Latin1.GetString(headers), Credential);

            var logged = Logged(string.Empty, [], [[.. Preface, .. Settings, .. headers]]);

            AssertHoldsNoCredential(logged);
            CollectionAssert.AreEqual((byte[])[.. Preface, .. Settings, .. WithheldHeaders(headers)], logged, name);
        }
    }

    [TestMethod]
    public void Tunnel_Http2AfterAnUpgradeAndThe101_WithholdsTheCredentialHeaderBlock()
    {
        var headers = Headers("authorization", Credential);

        var logged = Logged(SwitchingProtocols, [Latin1(UpgradeRequest)], [[.. Preface, .. Settings], headers]);

        AssertHoldsNoCredential(logged);
        CollectionAssert.AreEqual(
            (byte[])[.. Latin1(MaskedUpgradeRequest), .. Preface, .. Settings, .. WithheldHeaders(headers)],
            logged);
    }

    [TestMethod]
    public void Tunnel_Http2AfterAnUpgradeWithARequestBody_FindsThePrefaceAfterTheBody()
    {
        var headers = Headers("authorization", Credential);

        var logged = Logged(SwitchingProtocols, [Latin1(UpgradeRequest + "x=1")], [[.. Preface, .. headers]]);

        AssertHoldsNoCredential(logged);
        CollectionAssert.AreEqual(
            (byte[])[.. Latin1(MaskedUpgradeRequest + "x=1"), .. Preface, .. WithheldHeaders(headers)],
            logged);
    }

    [TestMethod]
    public void Tunnel_Http2SplitAtEveryByte_IsLoggedTheSame()
    {
        var headers = Headers("authorization", Credential);
        byte[] sent = [.. Preface, .. Settings, .. headers];
        for (var split = 1; split < sent.Length; split++)
        {
            var logged = Logged(string.Empty, [], [sent[..split], [], sent[split..]]);

            CollectionAssert.AreEqual(
                (byte[])[.. Preface, .. Settings, .. WithheldHeaders(headers)],
                logged,
                $"split {split}");
        }
    }

    [TestMethod]
    public void Tunnel_Http2AfterAnUpgradeWithABodySplitAtEveryByte_IsLoggedTheSame()
    {
        var headers = Headers("authorization", Credential);
        byte[] sent = [.. Preface, .. headers];
        for (var split = 1; split < sent.Length; split++)
        {
            var logged = Logged(SwitchingProtocols, [Latin1(UpgradeRequest + "x=1")], [sent[..split], sent[split..]]);

            CollectionAssert.AreEqual(
                (byte[])[.. Latin1(MaskedUpgradeRequest + "x=1"), .. Preface, .. WithheldHeaders(headers)],
                logged,
                $"split {split}");
        }
    }

    [TestMethod]
    public void Tunnel_ALaterPrefaceStartingWhereTheEarlierChunksWereDropped_IsNotTakenForTheTunnelsStart()
    {
        var request = Latin1("GET / HTTP/1.1\r\nHost: localhost\r\n\r\n");
        byte[] first = [.. request, .. Preface[..^1]];

        var logged = Logged(string.Empty, [first], [Preface[^1..], Headers("authorization", Credential)]);

        CollectionAssert.AreEqual((byte[])[.. request, .. Preface, .. "****"u8], logged);
    }

    [TestMethod]
    public void Tunnel_Http2OneByteAtATime_IsLoggedTheSame()
    {
        var headers = Headers("authorization", Credential);
        byte[] sent = [.. Preface, .. headers];

        var logged = Logged(string.Empty, [], [.. sent.Select(value => new[] { value })]);

        CollectionAssert.AreEqual((byte[])[.. Preface, .. WithheldHeaders(headers)], logged);
    }

    [TestMethod]
    public void Tunnel_Http2HeaderBlockWithNoCredential_IsLoggedAsReceived()
    {
        byte[] sent = [.. Preface, .. Settings, .. Headers("accept", "*/*")];

        CollectionAssert.AreEqual(sent, Logged(string.Empty, [], [sent]));
    }

    [TestMethod]
    public void Tunnel_Http2CutOffInsideAHeaderBlock_WithholdsItAtTheEnd()
    {
        var headers = Headers("authorization", Credential);

        var logged = Logged(string.Empty, [], [[.. Preface, .. headers[..^1]]]);

        AssertHoldsNoCredential(logged);
        CollectionAssert.AreEqual((byte[])[.. Preface, .. WithheldHeaders(headers)], logged);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("HTTP/1.1 200 Connection established\r\n\r\n")]
    public void Tunnel_APrefaceLaterWithNo101_WithholdsEveryByteAfterIt(string originSends)
    {
        var connect = Latin1("CONNECT localhost:80 HTTP/1.1\r\n\r\n");
        var headers = Headers("authorization", Credential);

        var logged = Logged(originSends, [connect], [Preface, headers, Latin1("PASS s3cret-pw\r\n")]);

        AssertHoldsNoCredential(logged);
        CollectionAssert.AreEqual((byte[])[.. connect, .. Preface, .. "****"u8], logged);
    }

    [TestMethod]
    public void Tunnel_APrefaceLaterWithNothingAfterIt_IsLoggedAlone()
    {
        var request = Latin1("GET / HTTP/1.1\r\n\r\n");

        var logged = Logged(string.Empty, [request], [Preface]);

        CollectionAssert.AreEqual((byte[])[.. request, .. Preface], logged);
    }

    [TestMethod]
    public void Tunnel_A101SentAfterTheBytesWereWithheld_DoesNotLogTheLaterBytes()
    {
        var request = Latin1("GET / HTTP/1.1\r\n\r\n");
        var output = new ArrayBufferWriter<byte>();
        var redactor = GrantedTunnel(output);

        redactor.Redact(request, output);
        redactor.Redact(Preface, output);
        redactor.ObserveSent(Latin1(SwitchingProtocols));
        redactor.Redact(Headers("authorization", Credential), output);
        redactor.Flush(output);

        CollectionAssert.AreEqual((byte[])[.. request, .. Preface, .. "****"u8], output.WrittenSpan.ToArray());
    }

    [TestMethod]
    [DataRow("PRI * HTTP/2.0\r\n\r\nPASS s3cret-pw\r\n", "PRI * HTTP/2.0\r\n\r\nPASS ****\r\n")]
    [DataRow("PRI * HTTPRI * HTTP/1.1\r\nPASS s3cret-pw\r\n", "PRI * HTTPRI * HTTP/1.1\r\nPASS ****\r\n")]
    public void Tunnel_BytesThatOnlyResembleThePreface_StayRedactedByLine(string sent, string expected)
    {
        var logged = Logged(string.Empty, [], [Latin1(sent)]);

        Assert.AreEqual(expected, Encoding.Latin1.GetString(logged));
    }

    [TestMethod]
    public void Tunnel_WhoseFirstByteIsNotALetter_IsNotWatchedForAPreface()
    {
        byte[] sent = [0x16, .. Preface, .. Headers("authorization", Credential)];

        CollectionAssert.AreEqual(sent, Logged(SwitchingProtocols, [], [sent]));
    }

    private static byte[] Latin1(string text) => SocksTestKit.Latin1Bytes(text);

    private static byte[] Frame(byte type, byte flags, byte stream, byte[] payload) =>
        [0, (byte)(payload.Length >> 8), (byte)payload.Length, type, flags, 0, 0, 0, stream, .. payload];

    // A HEADERS frame with END_STREAM and END_HEADERS whose strings are raw bytes, so a leak is visible.
    private static byte[] Headers(string name, string value) => Frame(
        0x1,
        0x5,
        1,
        new HpackEncoder(stringCoding: HpackStringCoding.Raw).Encode(
        [
            new HpackHeaderField(":method", "GET"),
            new HpackHeaderField(name, value, HpackFieldIndexing.WithoutIndexing),
        ]));

    private static byte[] WithheldHeaders(byte[] frame) => [.. frame[..9], .. "****"u8];

    // A SOCKS5 CONNECT without a login, granted, as the engine shows it to the redactor.
    private static IReceivedBytesRedactor GrantedTunnel(ArrayBufferWriter<byte> output)
    {
        var redactor = new SocksProtocolServer(new InMemoryOriginConnector())
            .CreateRedactor(new ExchangeRedactionContext(1, "socks5"));
        redactor.Redact([5, 1, 0], output);
        redactor.ObserveSent([5, 0]);
        redactor.Redact(SocksTestKit.Socks5DomainRequest("localhost", 18080), output);
        redactor.ObserveSent(SocksTestKit.Socks5Succeeded);
        output.Clear();

        return redactor;
    }

    // What is logged of the tunnel when the origin's bytes arrive between the two runs of client chunks.
    private static byte[] Logged(string originSends, byte[][] before, byte[][] after)
    {
        var output = new ArrayBufferWriter<byte>();
        var redactor = GrantedTunnel(output);
        foreach (var chunk in before)
        {
            redactor.Redact(chunk, output);
        }

        redactor.ObserveSent(Latin1(originSends));
        foreach (var chunk in after)
        {
            redactor.Redact(chunk, output);
        }

        redactor.Flush(output);

        return output.WrittenSpan.ToArray();
    }

    private static void AssertHoldsNoCredential(byte[] logged)
    {
        var text = Encoding.Latin1.GetString(logged);
        Assert.IsFalse(text.Contains(Token, StringComparison.Ordinal), text);
    }
}
