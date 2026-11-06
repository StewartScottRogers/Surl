using static Surl.Extended.Protocol.McpServer.McpHttpHarness;
using static Surl.Extended.Protocol.McpServer.McpOAuthSteps;

namespace Surl.Extended.Protocol.McpServer;

// Every request and every expected answer is written from RFC 7591 and MCP 2025-06-18 "Base Protocol > Authorization", as ADR-0111 decision 6.3 reads
// it. The harness's random source makes the n-th identifier issued SessionIdNumber(n): a client is the first, its
// code the second, then the access and refresh tokens.
[TestClass]
public sealed class McpClientRegistrationEndpointTests
{
    [TestMethod]
    public async Task ServeAsync_RegisterAClient_Answers201WithAPublicClient()
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync(
            "POST", "/register", $$"""{"redirect_uris":["{{RedirectUri}}"],"client_name":"Example","token_endpoint_auth_method":"client_secret_basic"}""", JsonField);

        string registration =
            $$"""{"client_id":"{{SessionIdNumber(1)}}","client_id_issued_at":{{Now.ToUnixTimeSeconds()}},"redirect_uris":["{{RedirectUri}}"],"token_endpoint_auth_method":"none","grant_types":["authorization_code","refresh_token"],"response_types":["code"],"client_name":"Example"}""";
        Assert.AreEqual(BodyAnswer("201 Created", "application/json", registration, NoStore), response);
    }

    [TestMethod]
    public async Task ServeAsync_RegisterWithoutAName_LeavesClientNameOut()
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync("POST", "/register", """{"redirect_uris":["https://example.com/cb"],"client_name":7}""", JsonField);

        Assert.AreEqual("HTTP/1.1 201 Created", StatusLineOf(response));
        Assert.IsFalse(BodyOf(response).Contains("client_name", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("""{}""")]
    [DataRow("""{"redirect_uris":"https://example.com/cb"}""")]
    [DataRow("""{"redirect_uris":[]}""")]
    [DataRow("""{"redirect_uris":[7]}""")]
    [DataRow("""{"redirect_uris":["http://example.com/cb"]}""")]
    [DataRow("""{"redirect_uris":["https://example.com/cb#here"]}""")]
    [DataRow("""{"redirect_uris":["https://example.com/cb#"]}""")]
    [DataRow("""{"redirect_uris":["/cb"]}""")]
    public async Task ServeAsync_RegisterWithABadRedirectUri_Is400InvalidRedirectUri(string metadata)
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync("POST", "/register", metadata, JsonField);

        Assert.AreEqual(BodyAnswer("400 Bad Request", "application/json", """{"error":"invalid_redirect_uri"}""", NoStore), response);
    }

    [TestMethod]
    [DataRow("""{"redirect_uris":["https://example.com/cb"]}""", "Content-Type: text/plain")]
    [DataRow("""not json""", JsonField)]
    [DataRow("""["https://example.com/cb"]""", JsonField)]
    public async Task ServeAsync_RegisterWithBadMetadata_Is400InvalidClientMetadata(string metadata, string contentType)
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync("POST", "/register", metadata, contentType);

        Assert.AreEqual(BodyAnswer("400 Bad Request", "application/json", """{"error":"invalid_client_metadata"}""", NoStore), response);
    }

    [TestMethod]
    public async Task ServeAsync_RegisterPastTheClientBound_Is503()
    {
        McpHttpHarness harness = new();
        for (int i = 0; i < McpExpiringTable<McpOAuthClient>.MaximumEntries; i++)
        {
            await RegisterAsync(harness);
        }

        string response = await harness.SendAsync("POST", "/register", $$"""{"redirect_uris":["{{RedirectUri}}"]}""", JsonField);

        Assert.AreEqual(EmptyAnswer("503 Service Unavailable", "Retry-After: 60\r\n"), response);
    }
}
