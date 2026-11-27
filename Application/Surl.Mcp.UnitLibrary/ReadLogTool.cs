using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>read_log</c> tool (ADR-0164 decision 6): reads a topic log's records from an offset, through
/// <c>Surl.Api</c>'s <c>SurlServer.Messages.ReadLogAsync</c>. Result <c>{"records": [{"offset", "timestamp",
/// "key" or "key_base64" when it has one, "text" or "base64"}], "next_offset", "has_more"}</c>. It moves no
/// subscription's or group's offset and publishes no event; an absent log is an <c>isError</c> result with the
/// API's message.
/// </summary>
public static class ReadLogTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "read_log";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Read the records of a topic's log on a running Surl server's message bus from an offset: offset, timestamp, "
        + "key and payload (text when UTF-8 with no control character other than tab, CR and LF, else base64), and "
        + "the next offset. Moves no subscription's or group's offset.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "topic": { "type": "string", "minLength": 1, "description": "The topic, e.g. orders" },
            "from_offset": { "type": "integer", "minimum": 0, "default": 0, "description": "The first offset to read" },
            "limit": { "type": "integer", "minimum": 1, "maximum": 1000, "default": 100, "description": "The most records to return" }
          },
          "required": ["server_id", "topic"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, reading from servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "Read a topic log", Description, InputSchema, ReadAsync);

    private static async Task<McpToolResult> ReadAsync(RunningSurlServer running, JsonElement arguments, CancellationToken cancellationToken)
    {
        var fromOffset = arguments.TryGetProperty("from_offset", out var from) ? from.GetInt64() : 0;
        var limit = arguments.TryGetProperty("limit", out var max) ? max.GetInt32() : 100;
        var page = await running.Server.Messages.ReadLogAsync(arguments.GetProperty("topic").GetString()!, fromOffset, limit, cancellationToken);
        return ToolResults.FromObject(writer =>
        {
            writer.WriteStartArray("records");
            foreach (var record in page.Records)
            {
                WriteRecord(writer, record);
            }

            writer.WriteEndArray();
            writer.WriteNumber("next_offset", page.NextOffset);
            writer.WriteBoolean("has_more", page.HasMore);
        });
    }

    /// <summary>Writes one log record as an object: <c>offset</c>, <c>timestamp</c>, a key when it has one, and the payload.</summary>
    /// <param name="writer">The writer, inside an array.</param>
    /// <param name="record">The record.</param>
    internal static void WriteRecord(Utf8JsonWriter writer, Surl.Api.SurlLogRecord record)
    {
        writer.WriteStartObject();
        writer.WriteNumber("offset", record.Offset);
        ReadBackJson.WriteTime(writer, "timestamp", record.Timestamp);
        if (record.Key is { } key)
        {
            ReadBackJson.WriteBytes(writer, key, "key", "key_base64");
        }

        ReadBackJson.WriteBytes(writer, record.Payload);
        writer.WriteEndObject();
    }
}
