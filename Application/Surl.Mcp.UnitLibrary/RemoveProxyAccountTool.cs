using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>remove_proxy_account</c> tool (ADR-0170 decision 4): removes a proxy account, one given at start included, through
/// <c>Surl.Api</c>'s <c>SurlServer.ProxyAccounts.Remove</c>. Result <c>{"user"}</c>, plus a <c>warning</c> member and a second
/// text block when no proxy account is left: that does not open the proxy, which without <c>--allow-anonymous</c> then lets no
/// new client through (ADR-0132 amendment 4). An account the server does not have, or a server that holds no such accounts, is
/// an <c>isError</c> result.
/// </summary>
public static class RemoveProxyAccountTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "remove_proxy_account";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Remove a proxy account from a running Surl server, one given at start included, from its next login on. "
        + "Undoes add_proxy_account. Removing the last proxy account does not open the proxy: without --allow-anonymous it then "
        + "lets no new client through; the result then carries a warning, and add_proxy_account lets that account in.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "user": { "type": "string", "minLength": 1, "description": "The user name" }
          },
          "required": ["server_id", "user"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, removing accounts from servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "Remove a proxy account from a Surl server", Description, InputSchema,
            (running, arguments, _) => Task.FromResult(Remove(running, arguments)));

    private static McpToolResult Remove(RunningSurlServer running, JsonElement arguments)
    {
        var user = arguments.GetProperty("user").GetString()!;
        try
        {
            var accounts = running.Server.ProxyAccounts;
            if (!accounts.Remove(user))
            {
                return McpToolResult.FromFailure($"server {running.ServerId} has no proxy account {user}");
            }

            return accounts.HasAccounts ? ToolResults.FromObject(writer => writer.WriteString("user", user)) : RemovedLastAccount(running, user);
        }
        catch (InvalidOperationException)
        {
            return McpToolResult.FromFailure($"server {running.ServerId} holds no proxy accounts, so its proxies are anonymous");
        }
    }

    private static McpToolResult RemovedLastAccount(RunningSurlServer running, string user)
    {
        var warning = $"server {running.ServerId} has no proxy account left; without --allow-anonymous its proxy now lets no new client through";
        var removed = ToolResults.FromObject(writer =>
        {
            writer.WriteString("user", user);
            writer.WriteString("warning", warning);
        });
        return new McpToolResult([.. removed.TextContent, warning], removed.StructuredContent);
    }
}
