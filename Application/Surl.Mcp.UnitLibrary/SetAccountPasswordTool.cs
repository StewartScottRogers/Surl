using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>set_account_password</c> tool (ADR-0132 amendment 1): changes the password of an existing account on a running server, through
/// <c>Surl.Api</c>'s <c>SurlServer.Accounts.SetPassword</c>. Result <c>{"user"}</c>: the password is never echoed. A user
/// with no such account is an <c>isError</c> result, <c>user &lt;name&gt; has no account</c>.
/// </summary>
public static class SetAccountPasswordTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "set_account_password";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Change the password of an existing account on a running Surl server, from its next login on. "
        + "The password is never shown back.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "user": { "type": "string", "minLength": 1, "description": "The user name of the existing account" },
            "password": { "type": "string", "minLength": 1, "description": "The new password" }
          },
          "required": ["server_id", "user", "password"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, changing passwords on servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "Change an account's password on a Surl server", Description, InputSchema,
            (running, arguments, _) => Task.FromResult(SetPassword(running, arguments)));

    private static McpToolResult SetPassword(RunningSurlServer running, JsonElement arguments)
    {
        var user = arguments.GetProperty("user").GetString()!;
        try
        {
            running.Server.Accounts.SetPassword(user, arguments.GetProperty("password").GetString()!);
        }
        catch (ArgumentException)
        {
            // The only refusal left is a user name with no account; its text is written here so no exception text is echoed.
            return McpToolResult.FromFailure($"user {user} has no account");
        }

        return ToolResults.FromObject(writer => writer.WriteString("user", user));
    }
}
