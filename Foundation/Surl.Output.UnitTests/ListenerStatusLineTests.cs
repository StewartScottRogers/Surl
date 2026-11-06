using Surl.Protocol.Abstractions;

namespace Surl.Output;

[TestClass]
public sealed class ListenerStatusLineTests
{
    [TestMethod]
    [DataRow("http", "127.0.0.1", 8080, 8080, "Listening on http://127.0.0.1:8080/", DisplayName = "IPv4")]
    [DataRow("http", "127.0.0.1", 0, 49731, "Listening on http://127.0.0.1:49731/", DisplayName = "IPv4, port 0 shows the bound port")]
    [DataRow("http", "::1", 0, 49731, "Listening on http://[::1]:49731/", DisplayName = "IPv6 bracketed, port 0 shows the bound port")]
    [DataRow("http", "::", 8080, 8080, "Listening on http://[::]:8080/", DisplayName = "IPv6 any address bracketed")]
    [DataRow("http", "fe80::1%eth0", 8080, 8080, "Listening on http://[fe80::1%25eth0]:8080/", DisplayName = "IPv6 zone as %25")]
    [DataRow("tftp", "0.0.0.0", 69, 69, "Listening on tftp://0.0.0.0:69/", DisplayName = "ADR example: tftp")]
    [DataRow("http", "localhost", 80, 80, "Listening on http://localhost:80/", DisplayName = "ADR example: host name")]
    public void Write_BoundListenUrl_WritesTheAdrLine(string scheme, string host, int port, int boundPort, string expected)
    {
        using var writer = new StringWriter();
        var statusLine = new ListenerStatusLine(writer);

        statusLine.Write(new ListenUrl(scheme, host, port).WithBoundPort(boundPort));

        Assert.AreEqual(expected + Environment.NewLine, writer.ToString());
    }

    [TestMethod]
    public void Write_TwoListenUrls_WritesOneLineEachInOrder()
    {
        using var writer = new StringWriter();
        var statusLine = new ListenerStatusLine(writer);

        statusLine.Write(new ListenUrl("http", "127.0.0.1", 8080).WithBoundPort(8080));
        statusLine.Write(new ListenUrl("http", "::1", 0).WithBoundPort(50000));

        Assert.AreEqual(
            "Listening on http://127.0.0.1:8080/" + Environment.NewLine
            + "Listening on http://[::1]:50000/" + Environment.NewLine,
            writer.ToString());
    }

    [TestMethod]
    public void FormatBoundListenUrl_IPv6_ReturnsTheUrlWithoutPrefix() =>
        Assert.AreEqual("http://[::1]:49731/", ListenerStatusLine.FormatBoundListenUrl(new ListenUrl("http", "::1", 0).WithBoundPort(49731)));

    [TestMethod]
    public void Format_UnboundListenUrl_Throws() =>
        Assert.ThrowsExactly<ArgumentException>(() => ListenerStatusLine.Format(new ListenUrl("http", "127.0.0.1", 0)));

    [TestMethod]
    public void FormatBoundListenUrl_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ListenerStatusLine.FormatBoundListenUrl(null!));

    [TestMethod]
    public void Constructor_NullWriter_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new ListenerStatusLine(null!));
}
