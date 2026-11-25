using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>list_keys</c> tool (ADR-0164 decision 9): lists the keys of a running server's Redis keyspace that match a
/// <c>KEYS</c> glob, in ordinal order of their bytes, through <c>Surl.Api</c>'s <c>SurlServer.Keys.ListAsync</c>.
/// Result <c>{"keys": [{"key" or "key_base64", "type", "byte_count", "expires_at"}], "has_more"}</c>; no value is
/// listed, and a key of any type is. It publishes no event; a bad limit or database is an <c>isError</c> result.
/// </summary>
public static class ListKeysTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "list_keys";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "List the keys of a running Surl server's Redis keyspace that match a KEYS glob (default *), in ordinal order: "
        + "name, type, size and expiry, never the value. Page with after_key (UTF-8 text) and limit. Listing "
        + "changes nothing.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "pattern": { "type": "string", "default": "*", "description": "A Redis KEYS glob: * ? [abc] [^a] [a-z] and a backslash to escape" },
            "after_key": { "type": "string", "description": "The last key seen, as UTF-8 text; only later keys are returned" },
            "limit": { "type": "integer", "minimum": 1, "maximum": 1000, "default": 100, "description": "The most keys to return" },
            "database": { "type": "integer", "minimum": 0, "maximum": 15, "description": "The database (default 0)" }
          },
          "required": ["server_id"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, reading from servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "List Redis keys on a Surl server", Description, InputSchema, ReadAsync);

    private static async Task<McpToolResult> ReadAsync(RunningSurlServer running, JsonElement arguments, CancellationToken cancellationToken)
    {
        var pattern = RunningServerTool.ReadOptionalString(arguments, "pattern") ?? "*";
        var limit = arguments.TryGetProperty("limit", out var max) ? max.GetInt32() : 100;
        var database = arguments.TryGetProperty("database", out var databaseValue) ? databaseValue.GetInt32() : 0;
        var page = await running.Server.Keys.ListAsync(pattern, RunningServerTool.ReadOptionalString(arguments, "after_key"), limit, database, cancellationToken);
        return ToolResults.FromObject(writer =>
        {
            writer.WriteStartArray("keys");
            foreach (var key in page.Keys)
            {
                writer.WriteStartObject();
                ReadKeyTool.WriteInfo(writer, key);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteBoolean("has_more", page.HasMore);
        });
    }
}
