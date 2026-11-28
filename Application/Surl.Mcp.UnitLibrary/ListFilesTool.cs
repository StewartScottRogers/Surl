using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>list_files</c> tool (ADR-0164 decision 6): lists a folder of a running server's content store in ordinal
/// order of name, through <c>Surl.Api</c>'s <c>SurlServer.Files.ListAsync</c>. Result <c>{"entries": [{"name",
/// "kind", "byte_count"}], "has_more"}</c>; a folder has no <c>byte_count</c>. It publishes no event; an absent
/// folder or a bad path is an <c>isError</c> result with the API's message.
/// </summary>
public static class ListFilesTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "list_files";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "List the children of a folder in a running Surl server's content store, in ordinal order of name: name, "
        + "kind (file or folder) and size. Page with after_name and limit.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "path": { "type": "string", "minLength": 1, "description": "The folder's request path, e.g. / or /reports" },
            "after_name": { "type": "string", "description": "The last name seen; only later names are returned" },
            "limit": { "type": "integer", "minimum": 1, "maximum": 1000, "default": 100, "description": "The most children to return" }
          },
          "required": ["server_id", "path"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, reading from servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "List a folder of a Surl server", Description, InputSchema, ReadAsync);

    private static async Task<McpToolResult> ReadAsync(RunningSurlServer running, JsonElement arguments, CancellationToken cancellationToken)
    {
        var limit = arguments.TryGetProperty("limit", out var max) ? max.GetWholeInt32() : 100;
        var page = await running.Server.Files.ListAsync(
            arguments.GetProperty("path").GetString()!, RunningServerTool.ReadOptionalString(arguments, "after_name"), limit, cancellationToken);
        return ToolResults.FromObject(writer =>
        {
            writer.WriteStartArray("entries");
            foreach (var entry in page.Entries)
            {
                writer.WriteStartObject();
                writer.WriteString("name", entry.Name);
                writer.WriteString("kind", entry.IsFolder ? "folder" : "file");
                if (entry.ByteCount is { } byteCount)
                {
                    writer.WriteNumber("byte_count", byteCount);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteBoolean("has_more", page.HasMore);
        });
    }
}
