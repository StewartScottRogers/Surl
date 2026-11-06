using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>seed_mail</c> tool (ADR-0124 decision 8): delivers a message into a running server's mail store, as SMTP
/// would after <c>RCPT TO:</c>, through <c>Surl.Api</c>'s <c>SurlServer.Mail.DeliverAsync</c>. Result
/// <c>{"delivered": true}</c>; a recipient or a delivery the store refuses is an <c>isError</c> result.
/// </summary>
public static class SeedMailTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "seed_mail";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Deliver a message into a running Surl server's mail store, as SMTP would, so its POP3 and IMAP servers "
        + "serve it. Give the message as UTF-8 text or base64, headers first, at most 16 MiB.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "to": { "type": "string", "minLength": 1, "description": "The recipient, e.g. alice@example.com" },
            "message": { "type": "string", "description": "The message as UTF-8 text, headers first" },
            "message_base64": { "type": "string", "description": "The message as base64 bytes, headers first" }
          },
          "required": ["server_id", "to"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, seeding servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "Seed mail into a Surl server", Description, InputSchema, SeedAsync);

    private static async Task<McpToolResult> SeedAsync(RunningSurlServer running, JsonElement arguments, CancellationToken cancellationToken)
    {
        if (SeededContent.TryRead(arguments, "message", "message_base64", out var message) is { } refusal)
        {
            return McpToolResult.FromFailure(refusal);
        }

        await running.Server.Mail.DeliverAsync(arguments.GetProperty("to").GetString()!, message, cancellationToken);
        return ToolResults.FromObject(writer => writer.WriteBoolean("delivered", true));
    }
}
