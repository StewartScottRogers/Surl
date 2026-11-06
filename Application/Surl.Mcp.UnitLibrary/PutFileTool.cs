using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>put_file</c> tool (ADR-0124 decision 8): writes a file into a running server's content store, as an
/// upload would, through <c>Surl.Api</c>'s <c>SurlServer.Files.PutAsync</c>. Result <c>{"path", "byte_count"}</c>;
/// a path no client may write or a file past the size limit is an <c>isError</c> result.
/// </summary>
public static class PutFileTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "put_file";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Write a file into a running Surl server's content store, as an upload would, so its HTTP, FTP, SFTP and "
        + "other file servers serve it. Give the contents as UTF-8 text or base64, at most 16 MiB.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "path": { "type": "string", "minLength": 1, "description": "Where the file goes, as a request path, e.g. /reports/a.txt" },
            "text": { "type": "string", "description": "The contents as UTF-8 text" },
            "base64": { "type": "string", "description": "The contents as base64 bytes" }
          },
          "required": ["server_id", "path"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, writing into servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "Put a file on a Surl server", Description, InputSchema, PutAsync);

    private static async Task<McpToolResult> PutAsync(RunningSurlServer running, JsonElement arguments, CancellationToken cancellationToken)
    {
        if (SeededContent.TryRead(arguments, "text", "base64", out var contents) is { } refusal)
        {
            return McpToolResult.FromFailure(refusal);
        }

        var path = arguments.GetProperty("path").GetString()!;
        await running.Server.Files.PutAsync(path, contents, cancellationToken);
        return ToolResults.FromObject(writer =>
        {
            writer.WriteString("path", path);
            writer.WriteNumber("byte_count", contents!.Length);
        });
    }
}
