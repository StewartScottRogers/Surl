using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>add_account</c> tool (ADR-0124 decision 8): adds an account every scheme of a running server accepts from
/// the next login on, through <c>Surl.Api</c>'s <c>SurlServer.Accounts.Add</c>. Result <c>{"user"}</c>: the password
/// is never echoed in a result, a refusal or a log line (ADR-0103 decision 4).
/// </summary>
public static class AddAccountTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "add_account";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Add an account a client may log in as on every scheme of a running Surl server, from its next login on. "
        + "The password is never shown back.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "user": { "type": "string", "minLength": 1, "description": "The user name, without ':'" },
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
        RunningServerTool.Create(servers, Name, "Add an account to a Surl server", Description, InputSchema,
            (running, arguments, _) => Task.FromResult(Add(running, arguments)));

    private static McpToolResult Add(RunningSurlServer running, JsonElement arguments)
    {
        var user = arguments.GetProperty("user").GetString()!;
        if (user.Contains(':', StringComparison.Ordinal))
        {
            return McpToolResult.FromFailure("user: a user name may not hold ':'");
        }

        try
        {
            running.Server.Accounts.Add(user, arguments.GetProperty("password").GetString()!);
        }
        catch (ArgumentException)
        {
            // The only refusal left is a user name taken; its text is written here so no exception text is echoed.
            return McpToolResult.FromFailure($"user {user} already has an account");
        }

        return ToolResults.FromObject(writer => writer.WriteString("user", user));
    }
}
