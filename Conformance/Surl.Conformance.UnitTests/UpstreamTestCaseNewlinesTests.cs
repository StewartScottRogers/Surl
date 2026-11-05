namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseNewlinesTests
{
    [TestMethod]
    public void ForceCrlf_CarriageReturnsBeforeTheFirstLineFeed_BecomeOneCrlf()
    {
        var line = UpstreamTestCaseNewlines.ForceCrlf("a\r\r\nb\n");

        Assert.AreEqual("a\r\nb\n", line);
    }

    [TestMethod]
    public void ForceCrlf_LineWithoutNewline_IsUnchanged()
    {
        var line = UpstreamTestCaseNewlines.ForceCrlf("a");

        Assert.AreEqual("a", line);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\n")]
    [DataRow("HTTP/2 404 \n")]
    [DataRow("GET /x HTTP/1.1\n")]
    [DataRow("CONNECT host:80 HTTP/1.0\n")]
    [DataRow("DESCRIBE rtsp://h/ RTSP/1.0\n")]
    [DataRow("Content-Type: text/html\n")]
    public void GuessHeaderCrlf_HeaderLines_BecomeCrlf(string line)
    {
        var rewritten = new UpstreamTestCaseNewlines().GuessHeaderCrlf(line);

        Assert.AreEqual(line[..^1] + "\r\n", rewritten);
    }

    [TestMethod]
    [DataRow("curl: (7) Failed to connect\n")]
    [DataRow("HTTP/1.1 200 OK\r\n")]
    [DataRow("body text\n")]
    [DataRow("\n")]
    public void GuessHeaderCrlf_OtherLines_AreUnchanged(string line)
    {
        var rewritten = new UpstreamTestCaseNewlines().GuessHeaderCrlf(line);

        Assert.AreEqual(line, rewritten);
    }

    [TestMethod]
    public void GuessHeaderCrlf_EmptyLineAfterAHeader_BecomesCrlfOnce()
    {
        var newlines = new UpstreamTestCaseNewlines();

        var lines = new[] { "A: b\n", "\n", "\n" }.Select(newlines.GuessHeaderCrlf).ToArray();

        CollectionAssert.AreEqual(new[] { "A: b\r\n", "\r\n", "\n" }, lines);
    }

    [TestMethod]
    public void GuessHeaderCrlf_RequestTargetWithByteA0_IsAHeaderLineAsPerlSeesIt()
    {
        var line = "GET /a" + (char)0xA0 + "b HTTP/1.1\n";

        var rewritten = new UpstreamTestCaseNewlines().GuessHeaderCrlf(line);

        Assert.AreEqual(line[..^1] + "\r\n", rewritten);
    }
}
