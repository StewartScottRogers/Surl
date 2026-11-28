using System.Text.Json;
using Surl.Api;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>read_key</c> tool (ADR-0164 decision 9): reads one key from a running server's Redis keyspace, through
/// <c>Surl.Api</c>'s <c>SurlServer.Keys.GetAsync</c>. Result <c>{"key" or "key_base64", "type", "byte_count",
/// "expires_at", "text" or "base64"}</c>: <c>expires_at</c> is absent for a key that never expires, and a key of a type
/// other than <c>string</c> has no value. It publishes no event; an absent or expired key is an <c>isError</c> result.
/// </summary>
public static class ReadKeyTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "read_key";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Read one key from a running Surl server's Redis keyspace, the one the redis server serves: its type, size, "
        + "expiry and, for a string, its exact value (text when UTF-8 with no control character other than tab, CR "
        + "and LF, else base64), at most 16 MiB. Give the key as text or base64 and optionally a database (0 to 15). "
        + "Reading changes nothing.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "key": { "type": "string", "minLength": 1, "description": "The key as UTF-8 text" },
            "key_base64": { "type": "string", "description": "The key as base64 bytes" },
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
        RunningServerTool.Create(servers, Name, "Read a Redis key from a Surl server", Description, InputSchema, ReadAsync);

    /// <summary>Writes one key's name, type, size and expiry into the open object.</summary>
    /// <param name="writer">The writer, inside an object.</param>
    /// <param name="info">The key.</param>
    internal static void WriteInfo(Utf8JsonWriter writer, SurlKeyInfo info)
    {
        ReadBackJson.WriteBytes(writer, info.KeyBytes, "key", "key_base64");
        writer.WriteString("type", info.Type);
        writer.WriteNumber("byte_count", info.ByteCount);
        if (info.ExpiresAt is { } expiresAt)
        {
            ReadBackJson.WriteTime(writer, "expires_at", expiresAt);
        }
    }

    private static async Task<McpToolResult> ReadAsync(RunningSurlServer running, JsonElement arguments, CancellationToken cancellationToken)
    {
        if (SeededContent.TryRead(arguments, "key", "key_base64", out var key) is { } refusal)
        {
            return McpToolResult.FromFailure(refusal);
        }

        var database = arguments.TryGetProperty("database", out var databaseValue) ? databaseValue.GetWholeInt32() : 0;
        var read = await running.Server.Keys.GetAsync(key!, database, cancellationToken);
        return ToolResults.FromObject(writer =>
        {
            WriteInfo(writer, read.Info);
            if (read.Bytes is { } bytes)
            {
                ReadBackJson.WriteBytes(writer, bytes);
            }
        });
    }
}
