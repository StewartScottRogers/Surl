using static Surl.Protocol.HttpProxy.HttpProxyRedactionTestBytes;
using static Surl.Protocol.HttpProxy.ProxyTestKit;

namespace Surl.Protocol.HttpProxy;

[TestClass]
public sealed class HttpProxyConnectionBytesRedactorTests
{
    private const string ConnectStart = "CONNECT origin.test:80 HTTP/1.1\r\nHost: origin.test:80\r\nProxy-Authorization: ";
    private const string Connect = ConnectStart + Credential + "\r\n\r\n";
    private const string MaskedConnect = ConnectStart + "Basic ****\r\n\r\n";
    private const string ProxyLine = "PROXY TCP4 127.0.0.1 127.0.0.1 50000 3128\r\n";
    private const string UpgradeStart =
        "POST http://origin.test/a HTTP/1.1\r\nHost: origin.test\r\nConnection: Upgrade, HTTP2-Settings\r\nUpgrade: h2c\r\n"
        + "HTTP2-Settings: AAMAAABk\r\nContent-Length: 3\r\nAuthorization: ";
    private const string Upgrade = UpgradeStart + Credential + "\r\n\r\nx=1";
    private const string MaskedUpgrade = UpgradeStart + "Basic ****\r\n\r\nx=1";
    private const string SwitchingProtocols = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: h2c\r\n\r\n";
    private const string Preface = "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n";

    private static byte[] AuthorizationHeaders => RawHeaders("authorization", Credential);

    [TestMethod]
    public void Redact_PrefaceSplitAtEveryByte_LogsTheSame()
    {
        var headers = AuthorizationHeaders;
        byte[] atRequestStart = [.. Raw(Connect + Preface), .. headers];
        byte[] expectedAtRequestStart = [.. Raw(MaskedConnect + Preface), .. Withheld(headers)];
        byte[] afterAProxyLine = [.. Raw(Connect + ProxyLine + Preface), .. headers];
        var expectedAfterAProxyLine = Raw(MaskedConnect + ProxyLine + Preface + "****");

        foreach (var (sent, expected) in new[] { (atRequestStart, expectedAtRequestStart), (afterAProxyLine, expectedAfterAProxyLine) })
        {
            for (var split = 1; split < sent.Length; split++)
            {
                var logged = Logged([sent[..split], [], sent[split..]]);

                CollectionAssert.AreEqual(expected, logged, $"split {split}: {Latin1(logged)}");
            }
        }
    }

    [TestMethod]
    public void Redact_OneByteAtATime_LogsTheSame()
    {
        var headers = AuthorizationHeaders;

        var atRequestStart = Logged(OneByteEach([.. Raw(Connect + Preface), .. headers]));
        var afterAProxyLine = Logged(OneByteEach([.. Raw(Connect + ProxyLine + Preface), .. headers]));

        CollectionAssert.AreEqual((byte[])[.. Raw(MaskedConnect + Preface), .. Withheld(headers)], atRequestStart, Latin1(atRequestStart));
        CollectionAssert.AreEqual(Raw(MaskedConnect + ProxyLine + Preface + "****"), afterAProxyLine, Latin1(afterAProxyLine));
    }

    [TestMethod]
    public void Redact_Http2AfterAnUpgradeBodyAnd101_WithholdsTheHeaderBlock()
    {
        var headers = AuthorizationHeaders;

        var logged = HttpProxyRedactionTestBytes.Logged(
            new HttpProxyConnectionBytesRedactor(), [Raw(Upgrade)], SwitchingProtocols, [Raw(Preface), headers]);

        CollectionAssert.AreEqual((byte[])[.. Raw(MaskedUpgrade + Preface), .. Withheld(headers)], logged, Latin1(logged));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("body-before-")]
    public void Redact_PrefaceInARequestBody_WithholdsEveryLaterByte(string bodyBeforeThePreface)
    {
        const string BodyAfterThePreface = "rest";
        var length = bodyBeforeThePreface.Length + Preface.Length + BodyAfterThePreface.Length;
        var head = $"POST http://origin.test/a HTTP/1.1\r\nContent-Length: {length}\r\n\r\n";

        var logged = Logged(
        [
            Raw(head + bodyBeforeThePreface + Preface + BodyAfterThePreface),
            AuthorizationHeaders,
            Raw("GET / HTTP/1.1\r\nAuthorization: " + Credential + "\r\n\r\n"),
        ]);

        Assert.AreEqual(head + bodyBeforeThePreface + Preface + "****", Latin1(logged));
    }

    [TestMethod]
    [DataRow("GET / HTTP/1.1\r\nX-Note: a")]
    [DataRow("PRI * HTTPRI * HTTP/1.1\r\nX-Note: ")]
    [DataRow("PRI * HTT")]
    public void Redact_PrefaceStartingMidLine_WithholdsEveryLaterByte(string beforeThePreface)
    {
        byte[][] sent = [Raw(beforeThePreface + Preface), AuthorizationHeaders, Raw("PRI more\r\n")];

        var inChunks = Logged(sent);
        var byteByByte = Logged(OneByteEach([.. sent.SelectMany(chunk => chunk)]));

        Assert.AreEqual(beforeThePreface + Preface + "****", Latin1(inChunks));
        Assert.AreEqual(beforeThePreface + Preface + "****", Latin1(byteByByte));
    }

    [TestMethod]
    [DataRow(
        "PRI * HTTP/2.0\r\n\r\nSM\r\nAuthorization: Basic YWxpY2U6\r\n\r\n",
        "PRI * HTTP/2.0\r\n\r\nSM\r\nAuthorization: Basic ****\r\n\r\n")]
    [DataRow(
        "PRI * HTTP/2\r\nAuthorization: Basic YWxpY2U6\r\n\r\n",
        "PRI * HTTP/2\r\nAuthorization: Basic ****\r\n\r\n")]
    [DataRow(
        "PRI * HTTPRI * HTTP/1.1\r\nAuthorization: Basic YWxpY2U6\r\n\r\n",
        "PRI * HTTPRI * HTTP/1.1\r\nAuthorization: Basic ****\r\n\r\n")]
    [DataRow(
        "PUT http://origin.test/a HTTP/1.1\r\nAuthorization: Basic YWxpY2U6\r\n\r\n",
        "PUT http://origin.test/a HTTP/1.1\r\nAuthorization: Basic ****\r\n\r\n")]
    public void Redact_BytesThatOnlyResembleThePreface_StayRedactedByLine(string sent, string expected)
    {
        var whole = Logged([Raw(sent)]);
        var byteByByte = Logged(OneByteEach(Raw(sent)));

        Assert.AreEqual(expected, Latin1(whole));
        Assert.AreEqual(expected, Latin1(byteByByte));
    }

    [TestMethod]
    public void Redact_LongHeaderLineWithoutPOrLineFeed_IsMaskedAsBefore()
    {
        const string Start = "GET http://origin.test/a HTTP/1.1\r\nAuthorization: Bearer ";
        var sent = Raw(Start + new string('a', 100) + "\r\n\r\n");

        var whole = Logged([sent]);
        var inThree = Logged([sent[..(Start.Length + 10)], sent[(Start.Length + 10)..(Start.Length + 60)], sent[(Start.Length + 60)..]]);

        Assert.AreEqual(Start + "****\r\n\r\n", Latin1(whole));
        Assert.AreEqual(Start + "****\r\n\r\n", Latin1(inThree));
    }

    [TestMethod]
    [DataRow("", 10)]
    [DataRow("", 23)]
    [DataRow("CONNECT origin.test:80 HTTP/1.1\r\n\r\n", 1)]
    [DataRow("CONNECT origin.test:80 HTTP/1.1\r\n\r\n", 20)]
    public void Flush_APartialPrefaceReceivedLast_WritesThoseBytes(string before, int prefaceBytes)
    {
        var sent = before + Preface[..prefaceBytes];

        var logged = Logged([Raw(sent)]);

        Assert.AreEqual(sent, Latin1(logged));
    }

    [TestMethod]
    public void Flush_Http2CutOffInsideAHeaderBlock_WithholdsIt()
    {
        var headers = AuthorizationHeaders;

        var logged = Logged([[.. Raw(Preface), .. headers[..^1]]]);

        CollectionAssert.AreEqual((byte[])[.. Raw(Preface), .. Withheld(headers)], logged, Latin1(logged));
    }

    [TestMethod]
    public void RedactAndFlush_NullOutput_Throw()
    {
        var redactor = new HttpProxyConnectionBytesRedactor();

        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact("GET"u8, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact([], null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Flush(null!));
    }

    private static byte[] Raw(string text) => Bytes(text).ToArray();

    private static byte[][] OneByteEach(byte[] sent) => [.. sent.Select(value => new[] { value })];

    // What a new connection's redactor logs of the client's chunks, the proxy having sent nothing.
    private static byte[] Logged(byte[][] chunks) =>
        HttpProxyRedactionTestBytes.Logged(new HttpProxyConnectionBytesRedactor(), chunks, string.Empty, []);
}
