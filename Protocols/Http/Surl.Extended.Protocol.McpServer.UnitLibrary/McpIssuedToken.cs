namespace Surl.Extended.Protocol.McpServer;

/// <summary>An access or refresh token: the client, the resource it is bound to and the account it acts as.</summary>
/// <param name="ClientId">The registered client it was issued to, or <see langword="null" /> for a <c>client_credentials</c> grant.</param>
/// <param name="Resource">The resource (RFC 8707) it is bound to.</param>
/// <param name="Account">The account it acts as.</param>
internal sealed record McpIssuedToken(string? ClientId, string Resource, string Account);
