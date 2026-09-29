using System.Text;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class HttpRequestHeadLineReaderTests
{
    [TestMethod]
    public void AcceptLine_EmptyLinesThenRequestLineThenFieldsThenEmptyLine_ReturnsTheHead()
    {
        var reader = new HttpRequestHeadLineReader();

        Assert.IsNull(reader.AcceptLine("\r"u8));
        Assert.IsNull(reader.AcceptLine(""u8));
        Assert.IsFalse(reader.HasRequestLine);
        Assert.IsNull(reader.AcceptLine("GET /x HTTP/1.1\r"u8));
        Assert.IsTrue(reader.HasRequestLine);
        Assert.IsNull(reader.AcceptLine("Host: h"u8));
        var result = reader.AcceptLine("\r"u8);

        Assert.AreEqual(HttpRequestHeadReadOutcome.HeadRead, result!.Outcome);
        Assert.AreEqual("/x", result.Head!.RequestTarget);
        Assert.AreEqual("h", result.Head.Fields[0].Value);
    }

    [TestMethod]
    public void AcceptLine_MalformedRequestLine_ReturnsItsFailure()
    {
        var reader = new HttpRequestHeadLineReader();

        var result = reader.AcceptLine(Encoding.ASCII.GetBytes("GET / HTTP/2.0\r"));

        Assert.AreEqual(HttpRequestHeadReadOutcome.UnsupportedVersion, result!.Outcome);
        Assert.IsFalse(reader.HasRequestLine);
    }

    [TestMethod]
    public void AcceptLine_MalformedFieldLine_ReturnsItsFailure()
    {
        var reader = new HttpRequestHeadLineReader();
        reader.AcceptLine("GET / HTTP/1.1"u8);

        var result = reader.AcceptLine("Host : h\r"u8);

        Assert.AreEqual(HttpRequestHeadReadOutcome.WhitespaceBeforeColon, result!.Outcome);
    }
}
