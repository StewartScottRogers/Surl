using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>read_message</c> tool (ADR-0164 decision 6): reads one mail message by UID, through <c>Surl.Api</c>'s
/// <c>SurlServer.Mail.ReadMessageAsync</c>. Result <c>{"uid", "size", "flags", "internal_date", "text" or
/// "base64"}</c>. It sets no flag and publishes no event; an absent account, mailbox or UID is an <c>isError</c>
/// result with the API's message.
/// </summary>
public static class ReadMessageTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "read_message";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Read one mail message of an account's mailbox on a running Surl server by UID: its metadata and exact bytes "
        + "(message/rfc822), as text when UTF-8 or else base64. Sets no flag and expunges nothing.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "owner": { "type": "string", "description": "The account's name; the empty string names the --allow-anonymous mailbox owner" },
            "mailbox": { "type": "string", "minLength": 1, "description": "The mailbox, e.g. INBOX" },
            "uid": { "type": "integer", "minimum": 0, "description": "The message's UID" }
          },
          "required": ["server_id", "owner", "mailbox", "uid"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, reading from servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "Read one message of a Surl mailbox", Description, InputSchema, ReadAsync);

    private static async Task<McpToolResult> ReadAsync(RunningSurlServer running, JsonElement arguments, CancellationToken cancellationToken)
    {
        var message = await running.Server.Mail.ReadMessageAsync(
            arguments.GetProperty("owner").GetString()!,
            arguments.GetProperty("mailbox").GetString()!,
            arguments.GetProperty("uid").GetInt64(),
            cancellationToken);
        return ToolResults.FromObject(writer =>
        {
            ListMessagesTool.WriteSummary(writer, message.Summary);
            ReadBackJson.WriteBytes(writer, message.Bytes);
        });
    }
}
