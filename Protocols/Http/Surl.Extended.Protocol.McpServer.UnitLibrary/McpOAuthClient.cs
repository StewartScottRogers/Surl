namespace Surl.Extended.Protocol.McpServer;

/// <summary>A client registered through RFC 7591 dynamic client registration: public, with no secret.</summary>
/// <param name="RedirectUris">The redirect URIs it registered, each <c>https</c> or loopback <c>http</c>.</param>
/// <param name="ClientName">The <c>client_name</c> it gave, or <see langword="null" />.</param>
/// <param name="IssuedAt">When it was registered.</param>
internal sealed record McpOAuthClient(IReadOnlyList<string> RedirectUris, string? ClientName, DateTimeOffset IssuedAt);
