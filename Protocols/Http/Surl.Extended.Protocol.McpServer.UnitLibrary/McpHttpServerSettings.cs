using Surl.Content;
using Surl.McpServing;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// What every Streamable HTTP connection of one server shares: the content store its tools serve, surl's
/// version for <c>serverInfo</c>, the endpoint's path, the session table and what its authorization server has
/// issued (ADR-0111 decision 6.3).
/// </summary>
/// <param name="Store">The shared content store.</param>
/// <param name="SurlVersion">surl's informational version.</param>
/// <param name="EndpointPath">The MCP endpoint's path (ADR-0111 decision 4.1).</param>
/// <param name="Sessions">The server's live sessions.</param>
/// <param name="Grants">The clients, codes and tokens the authorization server has issued.</param>
internal sealed record McpHttpServerSettings(ContentStore Store, string SurlVersion, string EndpointPath, McpHttpSessionTable Sessions, McpAuthorizationGrants Grants)
{
    /// <summary>Creates the core session a new <c>initialize</c> is answered by, its tools running as the login.</summary>
    /// <param name="login">The account; <see langword="null" /> or empty (a Bearer token's account) runs as the anonymous caller.</param>
    /// <param name="scheme">The listen scheme the content store names as the caller's.</param>
    /// <param name="maximumMessageBytes">The message limit, <c>--max-message</c>.</param>
    /// <returns>The core session.</returns>
    public McpServerSession CreateSession(string? login, string scheme, int maximumMessageBytes)
    {
        ContentCaller caller = string.IsNullOrEmpty(login) ? ContentCaller.Anonymous(scheme) : ContentCaller.ForAccount(login, scheme);
        McpServerRegistry registry = McpServerTools.CreateRegistry(Store, caller, maximumMessageBytes);
        return new McpServerSession(registry, McpServerSession.CreateOptions(SurlVersion, maximumMessageBytes));
    }
}
