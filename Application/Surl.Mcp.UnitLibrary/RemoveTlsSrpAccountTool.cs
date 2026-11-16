using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>remove_tls_srp_account</c> tool (ADR-0170 decision 4): removes a TLS-SRP account, one given at start included, through
/// <c>Surl.Api</c>'s <c>SurlServer.TlsSrpAccounts.Remove</c>. Result <c>{"user"}</c>. An account the server does not have, or a
/// server that holds no such accounts, is an <c>isError</c> result.
/// </summary>
public static class RemoveTlsSrpAccountTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "remove_tls_srp_account";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Remove a TLS-SRP account from a running Surl server, one given at start included, from its next login on. "
        + "Undoes add_tls_srp_account.";

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
        RunningServerTool.Create(servers, Name, "Remove a TLS-SRP account from a Surl server", Description, InputSchema,
            (running, arguments, _) => Task.FromResult(Remove(running, arguments)));

    private static McpToolResult Remove(RunningSurlServer running, JsonElement arguments)
    {
        var user = arguments.GetProperty("user").GetString()!;
        try
        {
            return running.Server.TlsSrpAccounts.Remove(user)
                ? ToolResults.FromObject(writer => writer.WriteString("user", user))
                : McpToolResult.FromFailure($"server {running.ServerId} has no TLS-SRP account {user}");
        }
        catch (InvalidOperationException)
        {
            return McpToolResult.FromFailure($"server {running.ServerId} was not started with TLS-SRP");
        }
    }
}
