using System.Text;
using Surl.HttpMessage;

namespace Surl.Protocol.HttpProxy;

[TestClass]
public sealed class OriginResponseHeadTests
{
    private static byte[] Line(string text) => Encoding.Latin1.GetBytes(text);

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r", 1, 200, "OK")]
    [DataRow("HTTP/1.0 404 Not Found", 0, 404, "Not Found")]
    [DataRow("HTTP/1.1 204", 1, 204, "")]
    [DataRow("HTTP/1.1 204 ", 1, 204, "")]
    public void ParseStatusLine_StatusLine_IsRead(string text, int minor, int code, string reason)
    {
        Assert.AreEqual((minor, code, reason), OriginResponseHead.ParseStatusLine(Line(text)));
    }

    [TestMethod]
    [DataRow("HTTP/1.1 20 OK")]
    [DataRow("HTTP/1.1 2000 OK")]
    [DataRow("HTTP/2.0 200 OK")]
    [DataRow("HTTP/1.x 200 OK")]
    [DataRow("HTTP/1.1_200 OK")]
    [DataRow("HTTP/1.1 0a0 OK")]
    [DataRow("HTTP/1.1 099 OK")]
    public void ParseStatusLine_OtherLine_IsNone(string text)
    {
        Assert.IsNull(OriginResponseHead.ParseStatusLine(Line(text)));
    }

    [TestMethod]
    [DataRow("Content-Length: 2\r", "Content-Length", "2")]
    [DataRow("X-Tab:\ta\tb ", "X-Tab", "a\tb")]
    [DataRow("Empty:", "Empty", "")]
    public void ParseFieldLine_FieldLine_IsRead(string text, string name, string value)
    {
        var field = OriginResponseHead.ParseFieldLine(Line(text));

        Assert.AreEqual(name, field!.Name);
        Assert.AreEqual(value, field.Value);
    }

    [TestMethod]
    [DataRow("No colon")]
    [DataRow(": no name")]
    [DataRow("Bad Name: x")]
    [DataRow("X: a\u0001b")]
    [DataRow("X: a\u007Fb")]
    public void ParseFieldLine_OtherLine_IsNone(string text)
    {
        Assert.IsNull(OriginResponseHead.ParseFieldLine(Line(text)));
    }

    [TestMethod]
    [DataRow(1, "keep-alive", true)]
    [DataRow(1, "close", false)]
    [DataRow(0, "keep-alive", false)]
    public void KeepsConnection_VersionAndConnection_SayWhetherTheOriginKeepsIt(int minor, string connection, bool expected)
    {
        var head = new OriginResponseHead(minor, 200, "OK", [new HttpRequestField("Connection", connection)]);

        Assert.AreEqual(expected, head.KeepsConnection);
    }

    [TestMethod]
    [DataRow(100, true)]
    [DataRow(200, false)]
    [DataRow(204, true)]
    [DataRow(304, true)]
    [DataRow(404, false)]
    public void HasNoBody_Status_IsSetFor1xx204And304(int code, bool expected)
    {
        Assert.AreEqual(expected, new OriginResponseHead(1, code, string.Empty, []).HasNoBody);
    }
}
