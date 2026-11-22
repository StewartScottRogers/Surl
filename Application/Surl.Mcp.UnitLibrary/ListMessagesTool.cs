using System.Text.Json;
using Surl.Api;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>list_messages</c> tool (ADR-0164 decision 6): lists one mailbox's messages in UID order, through
/// <c>Surl.Api</c>'s <c>SurlServer.Mail.ListMessagesAsync</c>. Result <c>{"messages": [{"uid", "size", "flags",
/// "internal_date"}], "has_more"}</c>. It sets no flag and publishes no event; an absent account or mailbox, or a
/// limit outside 1 to 1000, is an <c>isError</c> result with the API's message.
/// </summary>
public static class ListMessagesTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "list_messages";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "List the messages of one mailbox of an account on a running Surl server, in UID order: UID, size, flags and "
        + "internal date. Page with after_uid and limit. Sets no flag.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "owner": { "type": "string", "description": "The account's name; the empty string names the --allow-anonymous mailbox owner" },
            "mailbox": { "type": "string", "minLength": 1, "description": "The mailbox, e.g. INBOX" },
            "after_uid": { "type": "integer", "minimum": 0, "default": 0, "description": "The last UID seen; only larger UIDs are returned" },
            "limit": { "type": "integer", "minimum": 1, "maximum": 1000, "default": 100, "description": "The most messages to return" }
          },
          "required": ["server_id", "owner", "mailbox"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, reading from servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "List the messages of a Surl mailbox", Description, InputSchema, ReadAsync);

    /// <summary>Writes a message summary: <c>uid</c>, <c>size</c>, <c>flags</c> and <c>internal_date</c>.</summary>
    /// <param name="writer">The writer, inside an object.</param>
    /// <param name="message">The message.</param>
    internal static void WriteSummary(Utf8JsonWriter writer, SurlMessageSummary message)
    {
        writer.WriteNumber("uid", message.Uid);
        writer.WriteNumber("size", message.Size);
        ToolResults.WriteStrings(writer, "flags", message.Flags);
        ReadBackJson.WriteTime(writer, "internal_date", message.InternalDate);
    }

    private static async Task<McpToolResult> ReadAsync(RunningSurlServer running, JsonElement arguments, CancellationToken cancellationToken)
    {
        var afterUid = arguments.TryGetProperty("after_uid", out var after) ? after.GetInt64() : 0;
        var limit = arguments.TryGetProperty("limit", out var max) ? max.GetInt32() : 100;
        var page = await running.Server.Mail.ListMessagesAsync(
            arguments.GetProperty("owner").GetString()!, arguments.GetProperty("mailbox").GetString()!, afterUid, limit, cancellationToken);
        return ToolResults.FromObject(writer =>
        {
            writer.WriteStartArray("messages");
            foreach (var message in page.Messages)
            {
                writer.WriteStartObject();
                WriteSummary(writer, message);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteBoolean("has_more", page.HasMore);
        });
    }
}
