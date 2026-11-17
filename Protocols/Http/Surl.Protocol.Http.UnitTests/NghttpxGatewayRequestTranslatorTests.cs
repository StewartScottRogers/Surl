using System.Text;

namespace Surl.Protocol.Http;

/// <summary>
/// ADR-0154 decision 2: the HTTP/1.1 request nghttpx forwards. Cases 1700 and 2500 are upstream's
/// <c>&lt;protocol&gt;</c> after the case's <c>&lt;strip&gt;</c> of <c>X-Forwarded-Proto</c> and <c>Via</c>.
/// </summary>
[TestClass]
public sealed class NghttpxGatewayRequestTranslatorTests
{
    private static readonly KeyValuePair<string, string>[] CurlGetFields =
    [
        new("user-agent", "curl/8.21.0"),
        new("accept", "*/*"),
    ];

    [TestMethod]
    public void Translate_Case1700_Http2Get_WritesOriginFormRequestWithForwardingFieldsLast()
    {
        KeyValuePair<string, string>[] fields =
        [
            new("host", "ignored:1"),
            new("user-agent", "curl/8.21.0"),
            new("accept", "*/*"),
            new("connection", "Upgrade, HTTP2-Settings"),
            new("upgrade", "h2c"),
            new("http2-settings", "AAMAAABkAAQCAAAAAAIAAAAA"),
        ];

        string request = Text(NghttpxGatewayRequestTranslator.Translate(
            NghttpxGatewayVersion.Http2, "GET", null, "127.0.0.1:8080", "/1700", fields, default));

        Assert.AreEqual(
            "GET /1700 HTTP/1.1\r\nHost: 127.0.0.1:8080\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "X-Forwarded-Proto: http\r\nVia: 2 nghttpx\r\n\r\n",
            request);
        Assert.AreEqual(
            "GET /1700 HTTP/1.1\r\nHost: 127.0.0.1:8080\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n",
            StripForwardingLines(request));
    }

    [TestMethod]
    public void Translate_Case2500_Http3Get_WritesAbsoluteFormRequest()
    {
        string request = Text(NghttpxGatewayRequestTranslator.Translate(
            NghttpxGatewayVersion.Http3, "GET", "https", "localhost:4433", "/2500", CurlGetFields, default));

        Assert.AreEqual(
            "GET https://localhost:4433/2500 HTTP/1.1\r\nHost: localhost:4433\r\nUser-Agent: curl/8.21.0\r\n"
            + "Accept: */*\r\n\r\n",
            StripForwardingLines(request));
        StringAssert.Contains(request, "Via: 3 nghttpx\r\n");
        Assert.IsFalse(request.Contains("X-Forwarded-Proto", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Translate_Case1701_PostWithContentLength_ForwardsBodyAsSentWithoutChunking()
    {
        KeyValuePair<string, string>[] fields = [new("content-length", "10"), new("content-type", "application/x-www-form-urlencoded")];

        string request = Text(NghttpxGatewayRequestTranslator.Translate(
            NghttpxGatewayVersion.Http2, "POST", "http", "h:1", "/1701", fields, "datatosend"u8));

        Assert.AreEqual(
            "POST /1701 HTTP/1.1\r\nHost: h:1\r\nContent-Length: 10\r\nContent-Type: application/x-www-form-urlencoded\r\n"
            + "X-Forwarded-Proto: http\r\nVia: 2 nghttpx\r\n\r\ndatatosend",
            request);
    }

    [TestMethod]
    public void Translate_BodyWithoutContentLength_WritesChunkedUnpinned()
    {
        string request = Text(NghttpxGatewayRequestTranslator.Translate(
            NghttpxGatewayVersion.Http2, "POST", "http", "h:1", "/", [], "abc"u8));

        Assert.AreEqual(
            "POST / HTTP/1.1\r\nHost: h:1\r\nTransfer-Encoding: chunked\r\nX-Forwarded-Proto: http\r\n"
            + "Via: 2 nghttpx\r\n\r\n3\r\nabc\r\n0\r\n\r\n",
            request);
    }

    [TestMethod]
    public void Translate_NoAuthority_UsesTheHostField()
    {
        KeyValuePair<string, string>[] fields = [new("host", "from-host:9")];

        string request = Text(NghttpxGatewayRequestTranslator.Translate(
            NghttpxGatewayVersion.Http2, "GET", null, null, "/", fields, default));

        StringAssert.StartsWith(request, "GET / HTTP/1.1\r\nHost: from-host:9\r\n");
    }

    [TestMethod]
    public void Translate_NoAuthorityAndNoHostField_WritesAnEmptyHost()
    {
        string request = Text(NghttpxGatewayRequestTranslator.Translate(
            NghttpxGatewayVersion.Http2, "GET", null, null, "/", [new("accept", "*/*")], default));

        StringAssert.StartsWith(request, "GET / HTTP/1.1\r\nHost: \r\n");
    }

    [TestMethod]
    public void Translate_ConnectionSpecificPseudoAndForwardingFields_AreRemoved()
    {
        KeyValuePair<string, string>[] fields =
        [
            new(":method", "GET"),
            new("keep-alive", "5"),
            new("proxy-connection", "keep-alive"),
            new("transfer-encoding", "chunked"),
            new("te", "gzip"),
            new("x-forwarded-proto", "spoofed"),
            new("x-custom-thing", "kept"),
        ];

        string request = Text(NghttpxGatewayRequestTranslator.Translate(
            NghttpxGatewayVersion.Http2, "GET", "http", "h:1", "/", fields, default));

        Assert.AreEqual(
            "GET / HTTP/1.1\r\nHost: h:1\r\nX-Custom-Thing: kept\r\nX-Forwarded-Proto: http\r\nVia: 2 nghttpx\r\n\r\n",
            request);
    }

    [TestMethod]
    public void Translate_TeTrailers_IsKept()
    {
        string request = Text(NghttpxGatewayRequestTranslator.Translate(
            NghttpxGatewayVersion.Http2, "GET", "http", "h:1", "/", [new("te", "trailers")], default));

        StringAssert.Contains(request, "Te: trailers\r\n");
    }

    [TestMethod]
    public void Translate_SplitCookies_AreJoinedIntoOneCookieFieldUnpinned()
    {
        KeyValuePair<string, string>[] fields = [new("cookie", "a=1"), new("accept", "*/*"), new("cookie", "b=2")];

        string request = Text(NghttpxGatewayRequestTranslator.Translate(
            NghttpxGatewayVersion.Http2, "GET", "http", "h:1", "/", fields, default));

        StringAssert.Contains(request, "Accept: */*\r\nCookie: a=1; b=2\r\n");
    }

    [TestMethod]
    public void Translate_ExistingVia_GetsNghttpxAppended()
    {
        string request = Text(NghttpxGatewayRequestTranslator.Translate(
            NghttpxGatewayVersion.Http2, "GET", "http", "h:1", "/", [new("via", "1.1 proxy")], default));

        StringAssert.Contains(request, "Via: 1.1 proxy, 2 nghttpx\r\n");
        Assert.IsFalse(request.StartsWith("GET / HTTP/1.1\r\nHost: h:1\r\nVia", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("user-agent", "User-Agent")]
    [DataRow("ACCEPT", "Accept")]
    [DataRow("x-a-b", "X-A-B")]
    public void CanonicalName_CapitalisesEachHyphenatedPart(string name, string expected)
    {
        Assert.AreEqual(expected, NghttpxGatewayRequestTranslator.CanonicalName(name));
    }

    private static string Text(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    private static string StripForwardingLines(string request) =>
        string.Concat(request.Split("\r\n").Where(line => !line.StartsWith("X-Forwarded-Proto:", StringComparison.Ordinal)
            && !line.StartsWith("Via:", StringComparison.Ordinal)).Select(line => line + "\r\n"))[..^2];
}
