namespace Surl.Extended.Protocol.McpServer;

/// <summary>An authorization code: who it was issued to, for what, and the PKCE challenge it must be redeemed with.</summary>
/// <param name="ClientId">The client it was issued to.</param>
/// <param name="RedirectUri">The redirect URI it was sent to.</param>
/// <param name="CodeChallenge">The PKCE <c>S256</c> challenge (RFC 7636).</param>
/// <param name="Resource">The resource (RFC 8707) its tokens are bound to.</param>
/// <param name="Account">The account that authorized it.</param>
internal sealed record McpAuthorizationCode(string ClientId, string RedirectUri, string CodeChallenge, string Resource, string Account);
