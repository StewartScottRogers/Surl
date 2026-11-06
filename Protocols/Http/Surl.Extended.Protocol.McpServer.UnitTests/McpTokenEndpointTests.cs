using static Surl.Extended.Protocol.McpServer.McpHttpHarness;
using static Surl.Extended.Protocol.McpServer.McpOAuthSteps;

namespace Surl.Extended.Protocol.McpServer;

// Every request and every expected answer is written from OAuth 2.1 (draft-ietf-oauth-v2-1-13) section 3.2, RFC 7636 (its appendix B gives the verifier) and RFC 8707, as ADR-0111 decision 6.3 reads
// it. The harness's random source makes the n-th identifier issued SessionIdNumber(n): a client is the first, its
// code the second, then the access and refresh tokens.
[TestClass]
public sealed class McpTokenEndpointTests
{
    [TestMethod]
    public async Task ServeAsync_WrongCodeVerifier_IsInvalidGrant()
    {
        McpHttpHarness harness = new();
        string clientId = await RegisterAsync(harness);
        string code = await AuthorizeAsync(harness, clientId);

        string response = await harness.SendAsync(
            "POST", "/token", $"grant_type=authorization_code&code={code}&client_id={clientId}&code_verifier={new string('a', 43)}", FormField);

        Assert.AreEqual(BodyAnswer("400 Bad Request", "application/json", """{"error":"invalid_grant"}""", NoStore), response);
    }

    [TestMethod]
    [DataRow("code={code}&client_id={client}")]
    [DataRow("code={code}&client_id={client}&code_verifier=short")]
    [DataRow("code={code}&client_id={client}&code_verifier=" + Verifier + "!")]
    [DataRow("code={code}&client_id=other&code_verifier=" + Verifier)]
    [DataRow("code={code}&code_verifier=" + Verifier)]
    [DataRow("code={code}&client_id={client}&redirect_uri=https%3A%2F%2Fexample.com%2Fcb&code_verifier=" + Verifier)]
    [DataRow("code=unknown&client_id={client}&code_verifier=" + Verifier)]
    [DataRow("client_id={client}&code_verifier=" + Verifier)]
    public async Task ServeAsync_CodeRedeemedWrongly_IsInvalidGrant(string parameters)
    {
        McpHttpHarness harness = new();
        string clientId = await RegisterAsync(harness);
        string code = await AuthorizeAsync(harness, clientId);

        string response = await harness.SendAsync(
            "POST", "/token", "grant_type=authorization_code&" + parameters.Replace("{code}", code, StringComparison.Ordinal).Replace("{client}", clientId, StringComparison.Ordinal), FormField);

        Assert.AreEqual(BodyAnswer("400 Bad Request", "application/json", """{"error":"invalid_grant"}""", NoStore), response);
    }

    [TestMethod]
    public async Task ServeAsync_CodeNamingItsRedirectUri_IsRedeemed()
    {
        McpHttpHarness harness = new();
        string clientId = await RegisterAsync(harness);
        string code = await AuthorizeAsync(harness, clientId);

        string response = await harness.SendAsync(
            "POST", "/token",
            $"grant_type=authorization_code&code={code}&client_id={clientId}&redirect_uri={Uri.EscapeDataString(RedirectUri)}&code_verifier={Verifier}&resource={Uri.EscapeDataString(Resource)}",
            FormField);

        Assert.AreEqual("HTTP/1.1 200 OK", StatusLineOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_CodeUsedTwice_IsInvalidGrantTheSecondTime()
    {
        McpHttpHarness harness = new();
        string clientId = await RegisterAsync(harness);
        string code = await AuthorizeAsync(harness, clientId);
        await RedeemAsync(harness, clientId, code);

        string response = await RedeemAsync(harness, clientId, code);

        Assert.AreEqual("HTTP/1.1 400 Bad Request", StatusLineOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_CodeOlderThanTenMinutes_IsInvalidGrant()
    {
        McpHttpHarness harness = new();
        string clientId = await RegisterAsync(harness);
        string code = await AuthorizeAsync(harness, clientId);
        harness.TimeProvider.UtcNow += TimeSpan.FromMinutes(10);

        string response = await RedeemAsync(harness, clientId, code);

        Assert.AreEqual("HTTP/1.1 400 Bad Request", StatusLineOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_ClientCredentialsByBasic_IssuesAnAccessTokenOnly()
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync("POST", "/token", "grant_type=client_credentials", FormField, AliceBasic);

        string tokens = $$"""{"access_token":"{{SessionIdNumber(1)}}","token_type":"Bearer","expires_in":3600}""";
        Assert.AreEqual(BodyAnswer("200 OK", "application/json", tokens, NoStore), response);
    }

    [TestMethod]
    public async Task ServeAsync_ClientCredentialsInTheBody_IssuesAnAccessToken()
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync("POST", "/token", "grant_type=client_credentials&client_id=alice&client_secret=alice-password", FormField);

        Assert.AreEqual("HTTP/1.1 200 OK", StatusLineOf(response));
        Assert.AreEqual("alice", harness.Policy.PasswordLogins.Single().UserName);
    }

    [TestMethod]
    [DataRow("grant_type=client_credentials")]
    [DataRow("grant_type=client_credentials&client_id=alice")]
    [DataRow("grant_type=client_credentials&client_id=alice&client_secret=wrong")]
    public async Task ServeAsync_ClientCredentialsRefused_Is401InvalidClient(string form)
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync("POST", "/token", form, FormField);

        Assert.AreEqual(
            BodyAnswer("401 Unauthorized", "application/json", """{"error":"invalid_client"}""", $"{NoStore}WWW-Authenticate: Basic realm=\"surl\", charset=\"UTF-8\"\r\n"),
            response);
    }

    [TestMethod]
    public async Task ServeAsync_TokensPastTheBound_Is503()
    {
        McpHttpHarness harness = new();
        for (int i = 0; i < McpExpiringTable<McpIssuedToken>.MaximumEntries; i++)
        {
            await harness.SendAsync("POST", "/token", "grant_type=client_credentials", FormField, AliceBasic);
        }

        string response = await harness.SendAsync("POST", "/token", "grant_type=client_credentials", FormField, AliceBasic);

        Assert.AreEqual(EmptyAnswer("503 Service Unavailable", "Retry-After: 60\r\n"), response);
    }

    [TestMethod]
    public async Task ServeAsync_RefreshToken_IsRotatedOnUse()
    {
        McpHttpHarness harness = new();
        string clientId = await RegisterAsync(harness);
        await RedeemAsync(harness, clientId, await AuthorizeAsync(harness, clientId));

        string refreshed = await harness.SendAsync("POST", "/token", $"grant_type=refresh_token&refresh_token={SessionIdNumber(4)}&client_id={clientId}", FormField);
        string reused = await harness.SendAsync("POST", "/token", $"grant_type=refresh_token&refresh_token={SessionIdNumber(4)}&client_id={clientId}", FormField);

        string tokens = $$"""{"access_token":"{{SessionIdNumber(5)}}","token_type":"Bearer","expires_in":3600,"refresh_token":"{{SessionIdNumber(6)}}"}""";
        Assert.AreEqual(BodyAnswer("200 OK", "application/json", tokens, NoStore), refreshed);
        Assert.AreEqual(BodyAnswer("400 Bad Request", "application/json", """{"error":"invalid_grant"}""", NoStore), reused);
        string initialize = await harness.SendAsync("POST", "/mcp", InitializeMessage, $"Authorization: Bearer {SessionIdNumber(5)}", AcceptField, JsonField);
        Assert.AreEqual("HTTP/1.1 200 OK", StatusLineOf(initialize));
    }

    [TestMethod]
    [DataRow("grant_type=refresh_token&refresh_token={refresh}&client_id=other")]
    [DataRow("grant_type=refresh_token&client_id={client}")]
    public async Task ServeAsync_RefreshTokenUsedWrongly_IsInvalidGrant(string form)
    {
        McpHttpHarness harness = new();
        string clientId = await RegisterAsync(harness);
        await RedeemAsync(harness, clientId, await AuthorizeAsync(harness, clientId));

        string response = await harness.SendAsync(
            "POST", "/token", form.Replace("{refresh}", SessionIdNumber(4), StringComparison.Ordinal).Replace("{client}", clientId, StringComparison.Ordinal), FormField);

        Assert.AreEqual(BodyAnswer("400 Bad Request", "application/json", """{"error":"invalid_grant"}""", NoStore), response);
    }

    [TestMethod]
    public async Task ServeAsync_RefreshTokenOlderThanADay_IsInvalidGrant()
    {
        McpHttpHarness harness = new();
        string clientId = await RegisterAsync(harness);
        await RedeemAsync(harness, clientId, await AuthorizeAsync(harness, clientId));
        harness.TimeProvider.UtcNow += TimeSpan.FromHours(24);

        string response = await harness.SendAsync("POST", "/token", $"grant_type=refresh_token&refresh_token={SessionIdNumber(4)}&client_id={clientId}", FormField);

        Assert.AreEqual("HTTP/1.1 400 Bad Request", StatusLineOf(response));
    }

    [TestMethod]
    [DataRow("grant_type=client_credentials", "Content-Type: application/json", "invalid_request")]
    [DataRow("grant_type=client_credentials&grant_type=client_credentials", FormField, "invalid_request")]
    [DataRow("client_id=alice", FormField, "invalid_request")]
    [DataRow("grant_type=password", FormField, "unsupported_grant_type")]
    [DataRow("grant_type=client_credentials&resource=http%3A%2F%2Fexample.com%2Fmcp", FormField, "invalid_target")]
    public async Task ServeAsync_BadTokenRequest_Is400WithItsError(string form, string contentType, string error)
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync("POST", "/token", form, contentType, AliceBasic);

        Assert.AreEqual(BodyAnswer("400 Bad Request", "application/json", $$"""{"error":"{{error}}"}""", NoStore), response);
    }

    [TestMethod]
    public async Task ServeAsync_TokenRequestWithoutABody_IsInvalidRequest()
    {
        McpHttpHarness harness = new();

        string response = await harness.SendAsync("POST", "/token", null, FormField);

        Assert.AreEqual("HTTP/1.1 400 Bad Request", StatusLineOf(response));
    }

    [TestMethod]
    public async Task ServeAsync_TokenRequestPastTheMessageLimit_Is413()
    {
        McpHttpHarness harness = new() { Limits = Surl.Protocol.Abstractions.ExchangeLimits.Default with { MaxMessageBytes = 8 } };

        string response = await harness.SendAsync("POST", "/token", "grant_type=client_credentials", FormField);

        Assert.AreEqual("HTTP/1.1 413 Content Too Large", StatusLineOf(response));
    }
}
