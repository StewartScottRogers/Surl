namespace Surl.Extended.Protocol.McpServer;

/// <summary>The tokens a successful token request is answered with.</summary>
/// <param name="AccessToken">The access token.</param>
/// <param name="RefreshToken">The refresh token, or <see langword="null" /> when none is issued.</param>
internal sealed record McpTokenPair(string AccessToken, string? RefreshToken);
