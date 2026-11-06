namespace Surl.Extended.Protocol.McpServer;

// OAuth 2.1 section 2.3 as ADR-0111 decision 6.3 reads it.
[TestClass]
public sealed class McpRedirectUriTests
{
    [TestMethod]
    [DataRow("https://example.com/cb", true)]
    [DataRow("http://localhost:3000/cb", true)]
    [DataRow("http://[::1]/cb", true)]
    [DataRow("http://example.com/cb", false)]
    [DataRow("ftp://example.com/cb", false)]
    [DataRow("https://example.com/cb#x", false)]
    [DataRow("cb", false)]
    public void IsAllowed_Uri_IsHttpsOrLoopbackHttpWithoutAFragment(string uri, bool allowed)
    {
        Assert.AreEqual(allowed, McpRedirectUri.IsAllowed(uri));
    }

    [TestMethod]
    public void WithParameters_NullValue_IsLeftOutAndOthersEscaped()
    {
        Assert.AreEqual("https://e.com/cb?code=a%20b", McpRedirectUri.WithParameters("https://e.com/cb", ("code", "a b"), ("state", null)));
    }
}
