using System.Text;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class HttpRequestLineParserTests
{
    [TestMethod]
    [DataRow("GET / HTTP/1.1", "GET", "/", 1)]
    [DataRow("HEAD /file.txt HTTP/1.0", "HEAD", "/file.txt", 0)]
    [DataRow("GET /a?b=%20 HTTP/1.2", "GET", "/a?b=%20", 1)]
    [DataRow("GET /a HTTP/1.9", "GET", "/a", 1)]
    [DataRow("M-SEARCH * HTTP/1.1", "M-SEARCH", "*", 1)]
    public void Parse_WellFormedHttp1Line_ReturnsNullAndItsParts(string line, string method, string requestTarget, int minorVersion)
    {
        var failure = HttpRequestLineParser.Parse(Encoding.ASCII.GetBytes(line), out var parsedMethod, out var parsedTarget, out var version);

        Assert.IsNull(failure);
        Assert.AreEqual(method, parsedMethod);
        Assert.AreEqual(requestTarget, parsedTarget);
        Assert.AreEqual(new Version(1, minorVersion), version);
    }

    [TestMethod]
    public void Parse_TargetWithObsText_KeepsItAsLatin1()
    {
        var failure = HttpRequestLineParser.Parse(Encoding.Latin1.GetBytes("GET /café HTTP/1.1"), out _, out var requestTarget, out _);

        Assert.IsNull(failure);
        Assert.AreEqual("/café", requestTarget);
    }

    [TestMethod]
    [DataRow("GET/ HTTP/1.1")]
    [DataRow("GET  / HTTP/1.1")]
    [DataRow(" GET / HTTP/1.1")]
    [DataRow("G@T / HTTP/1.1")]
    [DataRow("GET /\u0001 HTTP/1.1")]
    [DataRow("GET /\u007F HTTP/1.1")]
    [DataRow("GET /\r HTTP/1.1")]
    [DataRow("GET /")]
    [DataRow("GET")]
    [DataRow("GET / HTTP/1.1 extra")]
    [DataRow("GET / http/1.1")]
    [DataRow("GET / HTTP/11")]
    [DataRow("GET / HTTP/a.1")]
    [DataRow("GET / HTTP/1x1")]
    [DataRow("GET / HTTP/1.b")]
    [DataRow("GET / HTTP/1.1\r")]
    public void Parse_MalformedLine_ReturnsMalformedRequestLine(string line)
    {
        var failure = HttpRequestLineParser.Parse(Encoding.Latin1.GetBytes(line), out _, out _, out _);

        Assert.AreEqual(HttpRequestHeadReadOutcome.MalformedRequestLine, failure);
    }

    [TestMethod]
    [DataRow("HTTP/2.0")]
    [DataRow("HTTP/0.9")]
    [DataRow("HTTP/3.0")]
    public void Parse_WellFormedVersionWithMajorOtherThanOne_ReturnsUnsupportedVersion(string version)
    {
        var failure = HttpRequestLineParser.Parse(Encoding.ASCII.GetBytes($"GET / {version}"), out _, out _, out _);

        Assert.AreEqual(HttpRequestHeadReadOutcome.UnsupportedVersion, failure);
    }
}
