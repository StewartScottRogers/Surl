using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>read_file</c> tool (ADR-0164 decision 6): reads one file from a running server's content store, through
/// <c>Surl.Api</c>'s <c>SurlServer.Files.ReadAsync</c>. Result <c>{"path", "byte_count", "modified_at",
/// "content_type", "text" or "base64"}</c>. It publishes no event; an absent file, a hidden or bad path or a file
/// past 16 MiB is an <c>isError</c> result with the API's message.
/// </summary>
public static class ReadFileTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "read_file";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Read a file from a running Surl server's content store, the one its HTTP, FTP, SFTP and other file servers "
        + "serve: its size, modification time, content type and exact bytes (text when UTF-8 with no control "
        + "character other than tab, CR and LF, else base64), at most 16 MiB.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "path": { "type": "string", "minLength": 1, "description": "The file's request path, e.g. /reports/a.txt" }
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
        RunningServerTool.Create(servers, Name, "Read a file from a Surl server", Description, InputSchema, ReadAsync);

    private static async Task<McpToolResult> ReadAsync(RunningSurlServer running, JsonElement arguments, CancellationToken cancellationToken)
    {
        var path = arguments.GetProperty("path").GetString()!;
        var file = await running.Server.Files.ReadAsync(path, cancellationToken);
        return ToolResults.FromObject(writer =>
        {
            writer.WriteString("path", path);
            writer.WriteNumber("byte_count", file.ByteCount);
            ReadBackJson.WriteTime(writer, "modified_at", file.ModifiedAt);
            writer.WriteString("content_type", file.ContentType);
            ReadBackJson.WriteBytes(writer, file.Bytes);
        });
    }
}
