using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>add_proxy_account</c> tool (ADR-0170 decision 4): adds a proxy account to a running server from its next login on, through
/// <c>Surl.Api</c>'s <c>SurlServer.ProxyAccounts.Add</c>. Result <c>{"user"}</c>: the password is never echoed. A server that holds
/// no such accounts is an <c>isError</c> result.
/// </summary>
public static class AddProxyAccountTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "add_proxy_account";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Add a proxy account to a running Surl server, from its next login on. Needs a server started with a proxy account. "
        + "The password is never shown back.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "user": { "type": "string", "minLength": 1, "description": "The user name" },
            "password": { "type": "string", "minLength": 1, "description": "The password" }
          },
          "required": ["server_id", "user", "password"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, adding accounts to servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "Add a proxy account to a Surl server", Description, InputSchema,
            (running, arguments, _) => Task.FromResult(Add(running, arguments)));

    private static McpToolResult Add(RunningSurlServer running, JsonElement arguments)
    {
        var user = arguments.GetProperty("user").GetString()!;
        try
        {
            running.Server.ProxyAccounts.Add(user, arguments.GetProperty("password").GetString()!);
        }
        catch (ArgumentException)
        {
            // A ':' in the user name or a user name taken; the text is written here so no exception text is echoed.
            return McpToolResult.FromFailure($"user {user} cannot be added: it holds ':' or already has a proxy account");
        }
        catch (InvalidOperationException)
        {
            return McpToolResult.FromFailure($"server {running.ServerId} holds no proxy accounts, so its proxies are anonymous");
        }

        return ToolResults.FromObject(writer => writer.WriteString("user", user));
    }
}
