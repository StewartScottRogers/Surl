using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>browse_queue</c> tool (ADR-0164 decision 6): lists a queue's ready messages in delivery order, through
/// <c>Surl.Api</c>'s <c>SurlServer.Messages.BrowseQueueAsync</c>. Result <c>{"messages": [{"position", "text" or
/// "base64", "headers": [{"name", "value"}]}], "ready_count", "in_flight_count", "has_more"}</c>. It consumes,
/// acknowledges and redelivers nothing and publishes no event; an absent queue is an <c>isError</c> result with
/// the API's message.
/// </summary>
public static class BrowseQueueTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "browse_queue";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "List the ready messages of a queue on a running Surl server's message bus in delivery order, with their "
        + "payloads (text when UTF-8 with no control character other than tab, CR and LF, else base64) and headers, "
        + "and the queue's ready and in-flight counts. "
        + "Consumes, acknowledges and redelivers nothing. Page with after_position and limit.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "queue": { "type": "string", "minLength": 1, "description": "The queue's name" },
            "after_position": { "type": "integer", "minimum": -1, "maximum": 2147483647, "default": -1,"description": "The last position seen; -1 starts at the first" },
            "limit": { "type": "integer", "minimum": 1, "maximum": 1000, "default": 100, "description": "The most messages to return" }
          },
          "required": ["server_id", "queue"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, reading from servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "Browse a queue on a Surl server", Description, InputSchema, ReadAsync);

    private static async Task<McpToolResult> ReadAsync(RunningSurlServer running, JsonElement arguments, CancellationToken cancellationToken)
    {
        var afterPosition = arguments.TryGetProperty("after_position", out var after) ? after.GetWholeInt32() : -1;
        var limit = arguments.TryGetProperty("limit", out var max) ? max.GetWholeInt32() : 100;
        var page = await running.Server.Messages.BrowseQueueAsync(arguments.GetProperty("queue").GetString()!, afterPosition, limit, cancellationToken);
        return ToolResults.FromObject(writer =>
        {
            writer.WriteStartArray("messages");
            foreach (var message in page.Messages)
            {
                WriteMessage(writer, message);
            }

            writer.WriteEndArray();
            writer.WriteNumber("ready_count", page.ReadyCount);
            writer.WriteNumber("in_flight_count", page.InFlightCount);
            writer.WriteBoolean("has_more", page.HasMore);
        });
    }

    /// <summary>Writes one queued message as an object: <c>position</c>, the payload and <c>headers</c> as name and value pairs.</summary>
    /// <param name="writer">The writer, inside an array.</param>
    /// <param name="message">The message.</param>
    internal static void WriteMessage(Utf8JsonWriter writer, Surl.Api.SurlQueueMessage message)
    {
        writer.WriteStartObject();
        writer.WriteNumber("position", message.Position);
        ReadBackJson.WriteBytes(writer, message.Payload);
        writer.WriteStartArray("headers");
        foreach (var header in message.Headers)
        {
            writer.WriteStartObject();
            writer.WriteString("name", header.Key);
            writer.WriteString("value", header.Value);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
