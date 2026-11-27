using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>remove_account</c> tool (ADR-0161 decision 1): removes an account, one given at start included, through
/// <c>Surl.Api</c>'s <c>SurlServer.Accounts.Remove</c>. Result <c>{"user"}</c>, never anything about the password, plus a
/// <c>warning</c> member and a second text block when no local account is left: that does not open the server, which still
/// asks for a login wherever it did (ADR-0132 amendment 5). An account the server does not have is an <c>isError</c> result.
/// </summary>
public static class RemoveAccountTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "remove_account";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Remove an account from a running Surl server, one given at start included, so a login with it fails from "
        + "its next login on. Undoes add_account. Removing the last account does not open the server: it still asks "
        + "for a login wherever it did; the result then carries a warning, and add_account lets that account in.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "user": { "type": "string", "description": "The user name; an empty string names the Bearer-token account" }
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
        RunningServerTool.Create(servers, Name, "Remove an account from a Surl server", Description, InputSchema,
            (running, arguments, _) => Task.FromResult(Remove(running, arguments)));

    private static McpToolResult Remove(RunningSurlServer running, JsonElement arguments)
    {
        var user = arguments.GetProperty("user").GetString()!;
        var accounts = running.Server.Accounts;
        if (!accounts.Remove(user))
        {
            return McpToolResult.FromFailure(user.Length == 0 ? $"server {running.ServerId} has no Bearer-token account" : $"server {running.ServerId} has no account {user}");
        }

        return accounts.HasAccounts ? ToolResults.FromObject(writer => writer.WriteString("user", user)) : RemovedLastAccount(running, user);
    }

    private static McpToolResult RemovedLastAccount(RunningSurlServer running, string user)
    {
        var warning = $"server {running.ServerId} has no local account left; it still asks for a login wherever it did";
        var removed = ToolResults.FromObject(writer =>
        {
            writer.WriteString("user", user);
            writer.WriteString("warning", warning);
        });
        return new McpToolResult([.. removed.TextContent, warning], removed.StructuredContent);
    }
}
