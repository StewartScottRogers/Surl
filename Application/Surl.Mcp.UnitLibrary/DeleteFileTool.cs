using System.Text.Json;
using Surl.Api;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>delete_file</c> tool (ADR-0161 decision 1): deletes a file from a running server's content store as the
/// test's own delete, through <c>Surl.Api</c>'s <c>SurlServer.Files.DeleteAsync</c>. Result <c>{"path"}</c>; nothing,
/// a folder or a hidden path at the path is an <c>isError</c> result <c>no file at &lt;path&gt;</c>, and a path no request may name (relative, or holding
/// <c>..</c>) is an <c>isError</c> result <c>path &lt;path&gt; may not be named: &lt;reason&gt;</c>.
/// </summary>
public static class DeleteFileTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "delete_file";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Delete a file from a running Surl server's content store, as the test's own delete: no client permission is "
        + "needed. Undoes put_file. Nothing, a folder or a hidden path at the path is an error.";

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

    /// <summary>Creates the tool, deleting from servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "Delete a file from a Surl server", Description, InputSchema, DeleteAsync);

    private static async Task<McpToolResult> DeleteAsync(RunningSurlServer running, JsonElement arguments, CancellationToken cancellationToken)
    {
        var path = arguments.GetProperty("path").GetString()!;
        try
        {
            await running.Server.Files.DeleteAsync(path);
        }
        catch (ArgumentException exception)
        {
            return McpToolResult.FromFailure($"path {path} may not be named: {exception.Message}");
        }
        catch (SurlSeedRefusedException exception) when (exception.Refusal == "Absent")
        {
            return McpToolResult.FromFailure($"no file at {path}");
        }

        return ToolResults.FromObject(writer => writer.WriteString("path", path));
    }
}
