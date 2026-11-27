using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>set_key</c> tool (ADR-0158 decision 9): sets a string key in a running server's Redis keyspace, as
/// <c>SET</c> would, through <c>Surl.Api</c>'s <c>SurlServer.Keys</c>. Result <c>{"key" or "key_base64", "database",
/// "event_name", "byte_count"}</c> (ADR-0158 amendment 1): the key as <c>read_key</c> and <c>list_keys</c> spell it
/// (<c>key_base64</c> for a key that is not UTF-8 or that holds a control character other than tab, CR and LF),
/// <c>event_name</c> the keyspace event's name (<c>&lt;database&gt;/&lt;key&gt;</c>); the value is never echoed.
/// </summary>
public static class SetKeyTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "set_key";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Set a string key in a running Surl server's Redis keyspace, as a SET would, so the redis server answers GET "
        + "with it. Give the key and the value as UTF-8 text or base64, at most 16 MiB together; optionally a database (0 to 15) "
        + "and an expiry in milliseconds. The value is never shown back. "
        + "Answers the key as read_key and list_keys spell it (key, or key_base64 for a key that is not UTF-8 or that "
        + "holds a control character other than tab, CR and LF), the database, "
        + "event_name (<database>/<key>, the name a wait_for_event on the store watches) and byte_count. "
        + "A server with no redis listen URL refuses it.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "key": { "type": "string", "minLength": 1, "description": "The key as UTF-8 text" },
            "key_base64": { "type": "string", "description": "The key as base64 bytes" },
            "value": { "type": "string", "description": "The value as UTF-8 text" },
            "value_base64": { "type": "string", "description": "The value as base64 bytes" },
            "database": { "type": "integer", "minimum": 0, "maximum": 15, "description": "The database (default 0)" },
            "expire_ms": { "type": "integer", "minimum": 1, "maximum": 31536000000, "description": "Milliseconds until the key expires (default: never)" }
          },
          "required": ["server_id"],
          "additionalProperties": false
        }
        """;

    private const int MaxNameCharacters = 256;

    /// <summary>Creates the tool, seeding keys on servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "Set a key on a Surl server", Description, InputSchema, SetAsync);

    private static async Task<McpToolResult> SetAsync(RunningSurlServer running, JsonElement arguments, CancellationToken cancellationToken)
    {
        byte[]? value = null;
        if ((SeededContent.TryRead(arguments, "key", "key_base64", out var key) ?? SeededContent.TryRead(arguments, "value", "value_base64", out value)) is { } refusal)
        {
            return McpToolResult.FromFailure(refusal);
        }

        if (key!.Length == 0)
        {
            return McpToolResult.FromFailure("key_base64: a key needs at least one byte");
        }

        var database = arguments.TryGetProperty("database", out var databaseValue) ? databaseValue.GetInt32() : 0;
        TimeSpan? expiry = arguments.TryGetProperty("expire_ms", out var expireValue) ? TimeSpan.FromMilliseconds(expireValue.GetInt64()) : null;
        await running.Server.Keys.SetAsync(key, value!, expiry, database, cancellationToken);
        return ToolResults.FromObject(writer =>
        {
            ReadBackJson.WriteBytes(writer, key, "key", "key_base64");
            writer.WriteNumber("database", database);
            writer.WriteString("event_name", NameOf(database, key));
            writer.WriteNumber("byte_count", value!.Length);
        });
    }

    // The keyspace event's Name (ADR-0158 decision 2): "<database>/<key>", each byte that is not valid UTF-8 as \xHH,
    // at most 256 characters.
    private static string NameOf(int database, byte[] key)
    {
        var name = new StringBuilder(string.Create(CultureInfo.InvariantCulture, $"{database}/"));
        var remaining = key.AsSpan();
        while (remaining.Length > 0 && name.Length < MaxNameCharacters)
        {
            var consumed = 1;
            if (Rune.DecodeFromUtf8(remaining, out var rune, out var decoded) == OperationStatus.Done)
            {
                name.Append(rune.ToString());
                consumed = decoded;
            }
            else
            {
                name.Append(CultureInfo.InvariantCulture, $"\\x{remaining[0]:X2}");
            }

            remaining = remaining[consumed..];
        }

        return name.Length > MaxNameCharacters ? name.ToString(0, MaxNameCharacters) : name.ToString();
    }
}
