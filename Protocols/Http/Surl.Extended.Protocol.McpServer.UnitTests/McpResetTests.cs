using static Surl.Extended.Protocol.McpServer.McpHttpHarness;
using static Surl.Extended.Protocol.McpServer.McpOAuthSteps;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// Clearing the OAuth state, ending every session, and judging tokens against the account book,
/// as a whole-instance reset or restore and a removed account need (ADR-0167, decisions 2 and 4; BL-1233).
/// </summary>
[TestClass]
public sealed class McpResetTests
{
    [TestMethod]
    public async Task ResetOAuthAndSessions_ARegisteredClient_IsAsUnknownAsAnyOtherClientID()
    {
        McpHttpHarness harness = new();
        string clientId = await RegisterAsync(harness);

        harness.Server.ResetOAuthAndSessions();
        string response = await harness.SendAsync("GET", AuthorizeTarget(clientId), null, AliceBasic);

        StringAssert.StartsWith(response, "HTTP/1.1 400 Bad Request\r\n");
        StringAssert.Contains(response, "invalid_request");
    }

    [TestMethod]
    public async Task ResetOAuthAndSessions_AnAuthorizationCodeAndARefreshToken_AreRefused()
    {
        McpHttpHarness harness = new();
        string clientId = await RegisterAsync(harness);
        string code = await AuthorizeAsync(harness, clientId);
        string refreshToken = await RefreshTokenAsync(harness, clientId, await AuthorizeAsync(harness, clientId));

        harness.Server.ResetOAuthAndSessions();
        string byCode = await RedeemAsync(harness, clientId, code);
        string byRefresh = await harness.SendAsync("POST", "/token", $"grant_type=refresh_token&refresh_token={refreshToken}&client_id={clientId}", FormField);

        StringAssert.Contains(byCode, "invalid_grant");
        StringAssert.Contains(byRefresh, "invalid_grant");
    }

    [TestMethod]
    public async Task ResetOAuthAndSessions_AnEarlierAccessToken_IsJudgedAsNoToken()
    {
        McpHttpHarness harness = new();
        string token = await AccessTokenByClientCredentialsAsync(harness);
        string before = await harness.SendAsync("POST", "/mcp", InitializeMessage, $"Authorization: Bearer {token}", AcceptField, JsonField);
        StringAssert.StartsWith(before, "HTTP/1.1 200 OK\r\n");

        harness.Server.ResetOAuthAndSessions();
        string response = await harness.SendAsync("POST", "/mcp", InitializeMessage, $"Authorization: Bearer {token}", AcceptField, JsonField);

        StringAssert.StartsWith(response, "HTTP/1.1 401 Unauthorized\r\n");
    }

    [TestMethod]
    public async Task ResetOAuthAndSessions_AnEarlierSessionId_IsAnswered404()
    {
        McpHttpHarness harness = new();
        string sessionId = await harness.InitializeAsync();

        harness.Server.ResetOAuthAndSessions();
        string response = await harness.SendAsync("DELETE", "/mcp", null, BearerField, $"Mcp-Session-Id: {sessionId}");

        StringAssert.StartsWith(response, "HTTP/1.1 404 Not Found\r\n");
    }

    [TestMethod]
    public void ResetOAuthAndSessions_NothingHeld_Completes() =>
        new McpHttpHarness().Server.ResetOAuthAndSessions();

    [TestMethod]
    public async Task LogInAsync_AnAccessTokenOfARemovedAccount_IsRefused()
    {
        McpHttpHarness harness = new();
        string token = await AccessTokenByClientCredentialsAsync(harness);
        string sessionId = FieldOf(await harness.SendAsync("POST", "/mcp", InitializeMessage, $"Authorization: Bearer {token}", AcceptField, JsonField), "Mcp-Session-Id")!;
        harness.Policy.RemovedAccounts.Add("alice");

        string response = await harness.SendAsync("DELETE", "/mcp", null, $"Authorization: Bearer {token}", $"Mcp-Session-Id: {sessionId}");

        StringAssert.StartsWith(response, "HTTP/1.1 401 Unauthorized\r\n");
    }

    [TestMethod]
    public async Task LogInAsync_AnAnonymousSessionAfterAnAccountIsRemoved_IsStillServed()
    {
        McpHttpHarness harness = new(allowsAnonymous: true);
        string sessionId = FieldOf(await harness.SendAsync("POST", "/mcp", InitializeMessage, AcceptField, JsonField), "Mcp-Session-Id")!;
        harness.Policy.RemovedAccounts.Add("alice");

        string response = await harness.SendAsync("DELETE", "/mcp", null, $"Mcp-Session-Id: {sessionId}");

        StringAssert.StartsWith(response, "HTTP/1.1 2");
    }

    [TestMethod]
    public async Task Refresh_ARefreshTokenOfARemovedAccount_IsInvalidGrant()
    {
        McpHttpHarness harness = new();
        string clientId = await RegisterAsync(harness);
        string refreshToken = await RefreshTokenAsync(harness, clientId, await AuthorizeAsync(harness, clientId));
        harness.Policy.RemovedAccounts.Add("alice");

        string response = await harness.SendAsync("POST", "/token", $"grant_type=refresh_token&refresh_token={refreshToken}&client_id={clientId}", FormField);

        StringAssert.Contains(response, "invalid_grant");
    }

    private static async Task<string> RefreshTokenAsync(McpHttpHarness harness, string clientId, string code)
    {
        string body = BodyOf(await RedeemAsync(harness, clientId, code));
        int start = body.IndexOf("\"refresh_token\":\"", StringComparison.Ordinal) + 17;
        return body[start..body.IndexOf('"', start)];
    }
}
