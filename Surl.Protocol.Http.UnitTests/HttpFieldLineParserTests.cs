using System.Text;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class HttpFieldLineParserTests
{
    [TestMethod]
    [DataRow("Host: 127.0.0.1:18017", "Host", "127.0.0.1:18017")]
    [DataRow("X-Custom: a b", "X-Custom", "a b")]
    [DataRow("X:\t a \t", "X", "a")]
    [DataRow("Empty:", "Empty", "")]
    [DataRow("Accept:*/*", "Accept", "*/*")]
    public void Parse_WellFormedLine_ReturnsNullAndTheTrimmedField(string line, string name, string value)
    {
        var failure = HttpFieldLineParser.Parse(Encoding.ASCII.GetBytes(line), out var field);

        Assert.IsNull(failure);
        Assert.AreEqual(name, field!.Name);
        Assert.AreEqual(value, field.Value);
    }

    [TestMethod]
    [DataRow("Host : x")]
    [DataRow("Host\t: x")]
    [DataRow("Host  :x")]
    public void Parse_WhitespaceBeforeColon_ReturnsWhitespaceBeforeColon(string line)
    {
        var failure = HttpFieldLineParser.Parse(Encoding.ASCII.GetBytes(line), out var field);

        Assert.AreEqual(HttpRequestHeadReadOutcome.WhitespaceBeforeColon, failure);
        Assert.IsNull(field);
    }

    [TestMethod]
    [DataRow(" Host: x")]
    [DataRow("\tHost: x")]
    [DataRow(" Host : x")]
    [DataRow(" folded")]
    [DataRow(": x")]
    [DataRow("NoColon")]
    [DataRow("Bad Name: x")]
    [DataRow("X: a\u0001b")]
    [DataRow("X: a\u007Fb")]
    [DataRow("X: a\u0000b")]
    [DataRow("X: a\r")]
    public void Parse_MalformedLine_ReturnsMalformedHeaderField(string line)
    {
        var failure = HttpFieldLineParser.Parse(Encoding.Latin1.GetBytes(line), out var field);

        Assert.AreEqual(HttpRequestHeadReadOutcome.MalformedHeaderField, failure);
        Assert.IsNull(field);
    }
}
