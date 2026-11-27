using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>add_tls_srp_account</c> tool (ADR-0170 decision 4): adds a TLS-SRP account to a running server from its next login on, through
/// <c>Surl.Api</c>'s <c>SurlServer.TlsSrpAccounts.Add</c>. Result <c>{"user"}</c>: the password is never echoed. A server that holds
/// no such accounts is an <c>isError</c> result.
/// </summary>
public static class AddTlsSrpAccountTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "add_tls_srp_account";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Add a TLS-SRP account to a running Surl server, from its next login on. Needs a server started with TLS-SRP. "
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
        RunningServerTool.Create(servers, Name, "Add a TLS-SRP account to a Surl server", Description, InputSchema,
            (running, arguments, _) => Task.FromResult(Add(running, arguments)));

    private static McpToolResult Add(RunningSurlServer running, JsonElement arguments)
    {
        var user = arguments.GetProperty("user").GetString()!;
        try
        {
            running.Server.TlsSrpAccounts.Add(user, arguments.GetProperty("password").GetString()!);
        }
        catch (ArgumentException)
        {
            // A user name taken; the text is written here so no exception text is echoed.
            return McpToolResult.FromFailure($"user {user} already has a TLS-SRP account");
        }
        catch (InvalidOperationException)
        {
            return McpToolResult.FromFailure($"server {running.ServerId} was not started with TLS-SRP");
        }

        return ToolResults.FromObject(writer => writer.WriteString("user", user));
    }
}
