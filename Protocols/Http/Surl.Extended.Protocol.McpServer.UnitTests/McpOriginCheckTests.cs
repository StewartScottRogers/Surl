namespace Surl.Extended.Protocol.McpServer;

[TestClass]
public sealed class McpOriginCheckTests
{
    [TestMethod]
    [DataRow(null, "http", "example.com")]
    [DataRow("http://localhost", "http", "example.com")]
    [DataRow("http://127.9.9.9:1234", "https", "example.com")]
    [DataRow("https://[::1]:443", "http", "example.com")]
    [DataRow("http://example.com", "http", "example.com")]
    [DataRow("http://Example.com:80", "http", "example.com")]
    [DataRow("https://example.com", "https", "example.com:443")]
    public void IsAllowed_NoOriginLoopbackOrSameOrigin_IsTrue(string? origin, string scheme, string host)
    {
        bool allowed = McpOriginCheck.IsAllowed(origin, scheme, host);

        Assert.IsTrue(allowed);
    }

    [TestMethod]
    [DataRow("null", "http", "example.com")]
    [DataRow("http://other.example", "http", "example.com")]
    [DataRow("https://example.com", "http", "example.com")]
    [DataRow("http://example.com:8080", "http", "example.com")]
    [DataRow("http://example.com/", "http", "example.com")]
    [DataRow("http://example.com/path", "http", "example.com")]
    [DataRow("http://example.com?q", "http", "example.com")]
    [DataRow("http://example.com#f", "http", "example.com")]
    [DataRow("http://user@example.com", "http", "example.com")]
    [DataRow("ftp://example.com", "ftp", "example.com")]
    [DataRow("http://example.com", "http", null)]
    [DataRow("http://example.com", "http", "bad host^")]
    public void IsAllowed_AnotherOriginOrOneThatDoesNotParse_IsFalse(string origin, string scheme, string? host)
    {
        bool allowed = McpOriginCheck.IsAllowed(origin, scheme, host);

        Assert.IsFalse(allowed);
    }
}
