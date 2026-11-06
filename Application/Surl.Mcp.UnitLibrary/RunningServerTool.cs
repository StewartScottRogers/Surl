using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// What every tool that acts on one running server shares (ADR-0124 decision 8): it reads <c>server_id</c>, answers
/// an ID that names no running server with <c>stop_server</c>'s <c>isError</c> text, and answers a store's or the
/// fault plan's refusal - an <see cref="InvalidOperationException"/>, such as a seed refused, a store on disk, a
/// snapshot past 256 MiB or a 257th fault rule - as an <c>isError</c> result with its message.
/// </summary>
internal static class RunningServerTool
{
    /// <summary>Creates a tool whose <paramref name="act"/> runs on the server <c>server_id</c> names.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <param name="name">The tool's name.</param>
    /// <param name="title">The tool's title.</param>
    /// <param name="description">The tool's description.</param>
    /// <param name="inputSchema">The tool's input schema.</param>
    /// <param name="act">Acts on the server with the call's arguments.</param>
    /// <returns>The tool.</returns>
    public static McpTool Create(
        RunningSurlServers servers,
        string name,
        string title,
        string description,
        string inputSchema,
        Func<RunningSurlServer, JsonElement, CancellationToken, Task<McpToolResult>> act)
    {
        ArgumentNullException.ThrowIfNull(servers);
        return new(name, title, description, inputSchema,
            (arguments, cancellationToken) => ActAsync(servers, act, arguments, cancellationToken));
    }

    /// <summary>Reads the optional string named <paramref name="name"/>; the schema has checked its type.</summary>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="name">The property's name.</param>
    /// <returns>The string, or <see langword="null"/> when absent.</returns>
    public static string? ReadOptionalString(JsonElement arguments, string name) =>
        arguments.TryGetProperty(name, out var value) ? value.GetString() : null;

    private static async Task<McpToolResult> ActAsync(
        RunningSurlServers servers,
        Func<RunningSurlServer, JsonElement, CancellationToken, Task<McpToolResult>> act,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var serverId = arguments.GetProperty("server_id").GetString()!;
        if (servers.Find(serverId) is not { } running)
        {
            return McpToolResult.FromFailure(StopServerTool.NoRunningServer(serverId));
        }

        try
        {
            return await act(running, arguments, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return McpToolResult.FromFailure(exception.Message);
        }
    }
}
