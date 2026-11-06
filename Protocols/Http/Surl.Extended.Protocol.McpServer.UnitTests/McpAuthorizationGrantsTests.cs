namespace Surl.Extended.Protocol.McpServer;

[TestClass]
public sealed class McpAuthorizationGrantsTests
{
    private static readonly McpIssuedToken Grant = new("client", "http://127.0.0.1:8080/mcp", "alice");

    [TestMethod]
    public void IssueTokens_RefreshTokensFullWhileAccessTokensAreNot_IsNull()
    {
        FixedTimeProvider clock = new(McpHttpHarness.Now);
        McpAuthorizationGrants grants = new(new CountingRandomNumberGenerator(), clock);
        for (int i = 0; i < McpExpiringTable<McpIssuedToken>.MaximumEntries; i++)
        {
            grants.IssueTokens(Grant, withRefreshToken: true);
        }

        clock.UtcNow += McpAuthorizationGrants.AccessTokenLifetime;
        McpTokenPair? withRefresh = grants.IssueTokens(Grant, withRefreshToken: true);
        McpTokenPair? withoutRefresh = grants.IssueTokens(Grant, withRefreshToken: false);

        Assert.IsNull(withRefresh);
        Assert.IsNotNull(withoutRefresh);
        Assert.IsNull(withoutRefresh.RefreshToken);
    }

    [TestMethod]
    public void AccountOf_TokenForAnotherResource_IsNull()
    {
        McpAuthorizationGrants grants = new(new CountingRandomNumberGenerator(), new FixedTimeProvider(McpHttpHarness.Now));
        McpTokenPair tokens = grants.IssueTokens(Grant, withRefreshToken: false)!;

        Assert.AreEqual("alice", grants.AccountOf(tokens.AccessToken, Grant.Resource));
        Assert.IsNull(grants.AccountOf(tokens.AccessToken, "http://localhost:8080/mcp"));
    }
}
