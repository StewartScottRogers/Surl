using static Surl.Extended.Protocol.McpServer.McpHttpHarness;
using static Surl.Extended.Protocol.McpServer.McpOAuthSteps;

namespace Surl.Extended.Protocol.McpServer;

// Every request and every expected answer is written from RFC 8414, RFC 8707, OAuth 2.1 and MCP 2025-06-18 "Base Protocol > Authorization", as ADR-0111 decision 6.3 reads
// it. The harness's random source makes the n-th identifier issued SessionIdNumber(n): a client is the first, its
// code the second, then the access and refresh tokens.
[TestClass]
public sealed class McpHttpRequestAnswererTests
{
    [TestMethod]
    public async Task ServeAsync_AuthorizationServerMetadata_IsTheRfc8414Document()
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync("GET", "/.well-known/oauth-authorization-server", null);

        const string document =
            """{"issuer":"http://127.0.0.1:8080","authorization_endpoint":"http://127.0.0.1:8080/authorize","token_endpoint":"http://127.0.0.1:8080/token","registration_endpoint":"http://127.0.0.1:8080/register","response_types_supported":["code"],"grant_types_supported":["authorization_code","refresh_token","client_credentials"],"token_endpoint_auth_methods_supported":["none","client_secret_basic","client_secret_post"],"code_challenge_methods_supported":["S256"]}""";
        Assert.AreEqual(BodyAnswer("200 OK", "application/json", document), response);
    }

    [TestMethod]
    public async Task ServeAsync_AuthorizationCodeWithPkce_IssuesATokenTheEndpointAccepts()
    {
        McpHttpHarness harness = new();
        string clientId = await RegisterAsync(harness);

        string authorize = await harness.SendAsync("GET", AuthorizeTarget(clientId) + "&state=xyz", null, AliceBasic);
        Assert.AreEqual(EmptyAnswer("302 Found", $"Location: {RedirectUri}?code={SessionIdNumber(2)}&state=xyz\r\n{NoStore}"), authorize);

        string token = await RedeemAsync(harness, clientId, SessionIdNumber(2));
        string tokens = $$"""{"access_token":"{{SessionIdNumber(3)}}","token_type":"Bearer","expires_in":3600,"refresh_token":"{{SessionIdNumber(4)}}"}""";
        Assert.AreEqual(BodyAnswer("200 OK", "application/json", tokens, NoStore), token);

        string initialize = await harness.SendAsync("POST", "/mcp", InitializeMessage, $"Authorization: Bearer {SessionIdNumber(3)}", AcceptField, JsonField);
        Assert.AreEqual("HTTP/1.1 200 OK", StatusLineOf(initialize));
        Assert.AreEqual(SessionIdNumber(5), FieldOf(initialize, "Mcp-Session-Id"));
        Assert.AreEqual("alice", harness.Policy.PasswordLogins.Single().UserName);
    }

    [TestMethod]
    public async Task ServeAsync_IssuedTokenSession_BelongsToTheTokensAccount()
    {
        McpHttpHarness harness = new();
        string accessToken = await AccessTokenByClientCredentialsAsync(harness);
        string sessionId = await harness.InitializeAsync($"Authorization: Bearer {accessToken}");

        string response = await harness.SendAsync(
            "POST", "/mcp", """{"jsonrpc":"2.0","id":2,"method":"ping"}""",
            $"Authorization: Bearer {UnitTestBearerAuthenticationPolicy.AliceToken}", AcceptField, JsonField, $"Mcp-Session-Id: {sessionId}");

        Assert.AreEqual("HTTP/1.1 200 OK", StatusLineOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_TokenIssuedForAnotherResource_Is401InvalidTokenAtTheEndpoint()
    {
        McpHttpHarness harness = new();
        string accessToken = await AccessTokenByClientCredentialsAsync(harness);

        string response = await harness.SendRawAsync(
            $"POST /mcp HTTP/1.1\r\nHost: localhost:8080\r\nAuthorization: Bearer {accessToken}\r\n{AcceptField}\r\n{JsonField}\r\n"
            + $"Content-Length: {InitializeMessage.Length}\r\nConnection: close\r\n\r\n{InitializeMessage}");

        string challenge = "Bearer resource_metadata=\"http://localhost:8080/.well-known/oauth-protected-resource/mcp\", error=\"invalid_token\"";
        Assert.AreEqual(EmptyAnswer("401 Unauthorized", $"WWW-Authenticate: {challenge}\r\n"), response);
    }

    [TestMethod]
    public async Task ServeAsync_ExpiredAccessToken_Is401InvalidToken()
    {
        McpHttpHarness harness = new();
        string accessToken = await AccessTokenByClientCredentialsAsync(harness);
        harness.TimeProvider.UtcNow += TimeSpan.FromHours(1);

        string response = await harness.SendAsync("POST", "/mcp", InitializeMessage, $"Authorization: Bearer {accessToken}", AcceptField, JsonField);

        Assert.AreEqual("HTTP/1.1 401 Unauthorized", StatusLineOf(response));
        StringAssert.Contains(FieldOf(response, "WWW-Authenticate"), "error=\"invalid_token\"");
    }

    [TestMethod]
    [DataRow("POST", "/authorize", "GET")]
    [DataRow("POST", "/.well-known/oauth-authorization-server", "GET")]
    [DataRow("GET", "/token", "POST")]
    [DataRow("GET", "/register", "POST")]
    public async Task ServeAsync_AuthorizationServerPathWithAnotherMethod_Is405(string method, string target, string allowed)
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync(method, target, method == "POST" ? string.Empty : null);

        Assert.AreEqual(EmptyAnswer("405 Method Not Allowed", $"Allow: {allowed}\r\n"), response);
    }

    [TestMethod]
    [DataRow("GET", "/.well-known/oauth-authorization-server")]
    [DataRow("POST", "/token")]
    public async Task ServeAsync_AuthorizationServerPathFromAForeignOrigin_Is403(string method, string target)
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync(method, target, null, "Origin: https://evil.example");

        Assert.AreEqual(EmptyAnswer("403 Forbidden"), response);
    }
}
