using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>stop_server</c> tool (ADR-0103 decision 3): stops the server an ID names, releases its ports and
/// forgets it and its exchanges, answering the exit code <c>surl</c> would have exited with. An ID that names no
/// running server is an <c>isError</c> result.
/// </summary>
public static class StopServerTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "stop_server";

    /// <summary>The tool's description.</summary>
    internal const string Description = "Stop a Surl server this host started and release its ports.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" }
          },
          "required": ["server_id"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, stopping servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers)
    {
        ArgumentNullException.ThrowIfNull(servers);
        return new(Name, "Stop a Surl server", Description, InputSchema,
            (arguments, cancellationToken) => StopAsync(servers, arguments, cancellationToken));
    }

    /// <summary>The failure an ID that names no running server is answered with.</summary>
    /// <param name="serverId">The ID given.</param>
    /// <returns>The failure's text.</returns>
    internal static string NoRunningServer(string serverId) => $"no running server has ID {serverId}";

    private static async Task<McpToolResult> StopAsync(RunningSurlServers servers, JsonElement arguments, CancellationToken cancellationToken)
    {
        var serverId = arguments.GetProperty("server_id").GetString()!;
        var exitCode = await servers.StopAsync(serverId, cancellationToken);
        return exitCode is not { } stopped
            ? McpToolResult.FromFailure(NoRunningServer(serverId))
            : ToolResults.FromObject(writer =>
            {
                writer.WriteString("server_id", serverId);
                ToolResults.WriteExitCode(writer, stopped);
            });
    }
}
