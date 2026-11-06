using System.Globalization;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>list_servers</c> tool (ADR-0103 decision 3): each running server in start order, with its listen URLs,
/// bound URLs, start time and how many exchanges it has recorded and dropped. Never an error.
/// </summary>
public static class ListServersTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "list_servers";

    /// <summary>The tool's description.</summary>
    internal const string Description = "List the Surl servers this host started and that are still running.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """{"type": "object", "properties": {}, "additionalProperties": false}""";

    /// <summary>Creates the tool, listing <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers)
    {
        ArgumentNullException.ThrowIfNull(servers);
        return new(Name, "List Surl servers", Description, InputSchema, (_, _) => Task.FromResult(List(servers)));
    }

    private static McpToolResult List(RunningSurlServers servers) =>
        ToolResults.FromObject(writer =>
        {
            writer.WriteStartArray("servers");
            foreach (var running in servers.List())
            {
                writer.WriteStartObject();
                writer.WriteString("server_id", running.ServerId);
                ToolResults.WriteStrings(writer, "listen_urls", running.ListenUrls);
                ToolResults.WriteStrings(writer, "bound_urls", running.Server.BoundUrls.Select(boundUrl => boundUrl.ToString()));
                writer.WriteString("started_at", running.StartedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                writer.WriteNumber("exchange_count", running.Server.Exchanges.Count);
                writer.WriteNumber("dropped_exchange_count", running.Server.DroppedExchangeCount);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        });
}
