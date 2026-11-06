using System.Security.Cryptography;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// Everything the MCP server's authorization server has issued (ADR-0111 decision 6.3), in memory only: clients,
/// which never expire; codes, valid <see cref="CodeLifetime" /> and used once; access tokens, valid
/// <see cref="AccessTokenLifetime" />; refresh tokens, valid <see cref="RefreshTokenLifetime" /> and rotated on use.
/// Each kind holds at most <see cref="McpExpiringTable{T}.MaximumEntries" /> live entries.
/// </summary>
/// <param name="randomSource">Where every identifier comes from: <see cref="RandomNumberGenerator" /> in production.</param>
/// <param name="timeProvider">The clock expiry is measured on.</param>
internal sealed class McpAuthorizationGrants(RandomNumberGenerator randomSource, TimeProvider timeProvider)
{
    /// <summary>How long an authorization code may wait to be redeemed.</summary>
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    /// <summary>How long an access token is accepted.</summary>
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(1);

    /// <summary>How long a refresh token may wait to be used.</summary>
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromHours(24);

    private readonly McpExpiringTable<McpOAuthClient> _clients = new(randomSource, timeProvider);

    private readonly McpExpiringTable<McpAuthorizationCode> _codes = new(randomSource, timeProvider);

    private readonly McpExpiringTable<McpIssuedToken> _accessTokens = new(randomSource, timeProvider);

    private readonly McpExpiringTable<McpIssuedToken> _refreshTokens = new(randomSource, timeProvider);

    /// <summary>Registers a client.</summary>
    /// <param name="redirectUris">Its redirect URIs, already checked.</param>
    /// <param name="clientName">Its <c>client_name</c>, or <see langword="null" />.</param>
    /// <returns>Its <c>client_id</c> and the client, or <see langword="null" /> when the server holds as many as it may.</returns>
    public (string ClientId, McpOAuthClient Client)? RegisterClient(IReadOnlyList<string> redirectUris, string? clientName)
    {
        McpOAuthClient client = new(redirectUris, clientName, _clients.Now);
        return _clients.Add(client, DateTimeOffset.MaxValue) is { } clientId ? (clientId, client) : null;
    }

    /// <summary>Finds a registered client.</summary>
    /// <param name="clientId">Its <c>client_id</c>.</param>
    /// <returns>The client, or <see langword="null" /> when none has the ID.</returns>
    public McpOAuthClient? FindClient(string clientId) => _clients.Find(clientId);

    /// <summary>Issues an authorization code.</summary>
    /// <param name="code">What the code grants.</param>
    /// <returns>The code, or <see langword="null" /> when the server holds as many as it may.</returns>
    public string? IssueCode(McpAuthorizationCode code) => _codes.Add(code, _codes.Now + CodeLifetime);

    /// <summary>Redeems an authorization code, which is then gone whatever the request's outcome.</summary>
    /// <param name="code">The code.</param>
    /// <returns>What it grants, or <see langword="null" /> when it is unknown, used or expired.</returns>
    public McpAuthorizationCode? RedeemCode(string code) => _codes.Take(code);

    /// <summary>Issues an access token, and a refresh token with it when asked.</summary>
    /// <param name="grant">The client, resource and account the tokens carry.</param>
    /// <param name="withRefreshToken">Whether a refresh token is issued too.</param>
    /// <returns>The tokens, or <see langword="null" /> when the server holds as many as it may.</returns>
    public McpTokenPair? IssueTokens(McpIssuedToken grant, bool withRefreshToken)
    {
        if (_accessTokens.Add(grant, _accessTokens.Now + AccessTokenLifetime) is not { } accessToken)
        {
            return null;
        }

        if (!withRefreshToken)
        {
            return new McpTokenPair(accessToken, null);
        }

        return _refreshTokens.Add(grant, _refreshTokens.Now + RefreshTokenLifetime) is { } refreshToken
            ? new McpTokenPair(accessToken, refreshToken)
            : null;
    }

    /// <summary>Uses a refresh token, which is then gone: a new one is issued in its place.</summary>
    /// <param name="refreshToken">The refresh token.</param>
    /// <returns>What it grants, or <see langword="null" /> when it is unknown, used or expired.</returns>
    public McpIssuedToken? RedeemRefreshToken(string refreshToken) => _refreshTokens.Take(refreshToken);

    /// <summary>The account a live access token acts as, when it is bound to the resource it is used at.</summary>
    /// <param name="accessToken">The token the request carried.</param>
    /// <param name="resource">The resource the request reached, <c>&lt;scheme&gt;://&lt;authority&gt;&lt;endpoint&gt;</c>.</param>
    /// <returns>The account, or <see langword="null" /> when the token is not live or is bound to another resource.</returns>
    public string? AccountOf(string accessToken, string resource) =>
        _accessTokens.Find(accessToken) is { } token && token.Resource == resource ? token.Account : null;
}
