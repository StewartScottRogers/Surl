using static Surl.Extended.Protocol.McpServer.McpHttpHarness;
using static Surl.Extended.Protocol.McpServer.McpOAuthSteps;

namespace Surl.Extended.Protocol.McpServer;

// Every request and every expected answer is written from OAuth 2.1 (draft-ietf-oauth-v2-1-13) section 4.1, RFC 7636 and RFC 8707, as ADR-0111 decision 6.3 reads
// it. The harness's random source makes the n-th identifier issued SessionIdNumber(n): a client is the first, its
// code the second, then the access and refresh tokens.
[TestClass]
public sealed class McpAuthorizeEndpointTests
{
    [TestMethod]
    public async Task ServeAsync_RedirectUriWithAQuery_GetsTheCodeAppended()
    {
        McpHttpHarness harness = new();
        string clientId = await RegisterAsync(harness, "https://example.com/cb?app=1");

        string response = await harness.SendAsync("GET", AuthorizeTarget(clientId), null, AliceBasic);

        Assert.AreEqual($"https://example.com/cb?app=1&code={SessionIdNumber(2)}", FieldOf(response, "Location"));
    }

    [TestMethod]
    public async Task ServeAsync_AuthorizeNamingTheRegisteredRedirectUri_IsAccepted()
    {
        McpHttpHarness harness = new();
        string clientId = await RegisterAsync(harness);

        string response = await harness.SendAsync(
            "GET", AuthorizeTarget(clientId) + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}&resource={Uri.EscapeDataString(Resource)}", null, AliceBasic);

        Assert.AreEqual($"{RedirectUri}?code={SessionIdNumber(2)}", FieldOf(response, "Location"));
    }

    [TestMethod]
    [DataRow("client_id=unknown&response_type=code")]
    [DataRow("response_type=code")]
    [DataRow("client_id={0}&client_id={0}&response_type=code")]
    [DataRow("client_id={0}&redirect_uri=https%3A%2F%2Fevil.example%2Fcb&response_type=code")]
    public async Task ServeAsync_AuthorizeForAnUnknownClientOrRedirectUri_Is400AndNotRedirected(string query)
    {
        McpHttpHarness harness = new();
        string clientId = await RegisterAsync(harness);

        string response = await harness.SendAsync("GET", "/authorize?" + string.Format(null, query, clientId), null, AliceBasic);

        Assert.AreEqual(BodyAnswer("400 Bad Request", "application/json", """{"error":"invalid_request"}""", NoStore), response);
    }

    [TestMethod]
    public async Task ServeAsync_AuthorizeWithoutARedirectUriForAClientWithSeveral_Is400()
    {
        McpHttpHarness harness = new();
        string registration = await harness.SendAsync(
            "POST", "/register", $$"""{"redirect_uris":["{{RedirectUri}}","https://example.com/cb"]}""", JsonField);
        Assert.AreEqual("HTTP/1.1 201 Created", StatusLineOf(registration));

        string response = await harness.SendAsync("GET", AuthorizeTarget(SessionIdNumber(1)), null, AliceBasic);

        Assert.AreEqual("HTTP/1.1 400 Bad Request", StatusLineOf(response));
    }

    [TestMethod]
    [DataRow("response_type=token&code_challenge={1}&code_challenge_method=S256", "unsupported_response_type")]
    [DataRow("code_challenge={1}&code_challenge_method=S256", "unsupported_response_type")]
    [DataRow("response_type=code&code_challenge_method=S256", "invalid_request")]
    [DataRow("response_type=code&code_challenge={1}", "invalid_request")]
    [DataRow("response_type=code&code_challenge={1}&code_challenge_method=plain", "invalid_request")]
    [DataRow("response_type=code&code_challenge={1}&code_challenge_method=S256&resource=http%3A%2F%2F127.0.0.1%3A8080%2Fother", "invalid_target")]
    public async Task ServeAsync_AuthorizeWithABadParameter_RedirectsTheError(string query, string error)
    {
        McpHttpHarness harness = new();
        string clientId = await RegisterAsync(harness);

        string response = await harness.SendAsync("GET", $"/authorize?client_id={clientId}&state=s%201&" + string.Format(null, query, clientId, Challenge), null);

        Assert.AreEqual(EmptyAnswer("302 Found", $"Location: {RedirectUri}?error={error}&state=s%201\r\n{NoStore}"), response);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("Authorization: Basic not-base64!")]
    [DataRow("Authorization: Basic YWxpY2U=")]
    [DataRow("Authorization: Bearer secret-token")]
    [DataRow("Authorization: Basic YWxpY2U6d3Jvbmc=")]
    public async Task ServeAsync_AuthorizeWithoutTheOwnersLogin_Is401WithABasicChallenge(string? login)
    {
        McpHttpHarness harness = new();
        string clientId = await RegisterAsync(harness);

        string response = await harness.SendAsync("GET", AuthorizeTarget(clientId), null, login is null ? [] : [login]);

        Assert.AreEqual(EmptyAnswer("401 Unauthorized", "WWW-Authenticate: Basic realm=\"surl\", charset=\"UTF-8\"\r\n"), response);
    }

    [TestMethod]
    public async Task ServeAsync_AuthorizePastTheCodeBound_RedirectsTemporarilyUnavailable()
    {
        McpHttpHarness harness = new();
        string clientId = await RegisterAsync(harness);
        for (int i = 0; i < McpExpiringTable<McpAuthorizationCode>.MaximumEntries; i++)
        {
            await harness.SendAsync("GET", AuthorizeTarget(clientId), null, AliceBasic);
        }

        string response = await harness.SendAsync("GET", AuthorizeTarget(clientId), null, AliceBasic);

        Assert.AreEqual($"{RedirectUri}?error=temporarily_unavailable", FieldOf(response, "Location"));
    }
}
