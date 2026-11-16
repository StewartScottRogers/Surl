using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>read_zone</c> tool (ADR-0164 decision 9): reads a running server's DNS zones back through <c>Surl.Api</c>'s
/// <c>SurlServer.Zones</c>. With an <c>apex</c> the result is <c>{"apex", "zone"}</c>, the zone as master-file text; without
/// one it is <c>{"zones": [apex, ...]}</c>, every apex held with its final dot. It publishes no event; an apex no zone
/// has is an <c>isError</c> result.
/// </summary>
public static class ReadZoneTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "read_zone";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Read a running Surl server's DNS zones back. With apex, that zone as master-file text (a record a line, "
        + "absolute names, a TTL and class IN), at most 16 MiB; without apex, the list of every apex held. Reading "
        + "changes nothing.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "apex": { "type": "string", "description": "The zone's apex, e.g. example.com; omit to list every apex" }
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
        RunningServerTool.Create(servers, Name, "Read the DNS zones of a Surl server", Description, InputSchema, ReadAsync);

    private static async Task<McpToolResult> ReadAsync(RunningSurlServer running, JsonElement arguments, CancellationToken cancellationToken)
    {
        var apex = RunningServerTool.ReadOptionalString(arguments, "apex");
        if (apex is null)
        {
            var apexes = await running.Server.Zones.ListAsync(cancellationToken);
            return ToolResults.FromObject(writer =>
            {
                writer.WriteStartArray("zones");
                foreach (var held in apexes)
                {
                    writer.WriteStringValue(held);
                }

                writer.WriteEndArray();
            });
        }

        var zone = await running.Server.Zones.ReadAsync(apex, cancellationToken);
        return ToolResults.FromObject(writer =>
        {
            writer.WriteString("apex", apex);
            writer.WriteString("zone", zone);
        });
    }
}
