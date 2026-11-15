using System.Text;
using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>add_dns_zone</c> tool (ADR-0158 decision 9): adds one zone, read from zone-file text, to a running server's
/// DNS zones through <c>Surl.Api</c>'s <c>SurlServer.Zones</c>. Result <c>{"apex", "byte_count"}</c>; a second zone for
/// the apex is an <c>isError</c> result.
/// </summary>
public static class AddDnsZoneTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "add_dns_zone";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Add a DNS zone, from zone-file text, to a running Surl server so its dns server answers from it. At most 16 MiB; "
        + "a second zone for an apex already held is refused until a reset.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "apex": { "type": "string", "minLength": 1, "description": "The zone's apex, e.g. example.com" },
            "zone_text": { "type": "string", "minLength": 1, "description": "The zone file's text" }
          },
          "required": ["server_id", "apex", "zone_text"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, seeding the zones of servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "Add a DNS zone to a Surl server", Description, InputSchema, AddAsync);

    private static async Task<McpToolResult> AddAsync(RunningSurlServer running, JsonElement arguments, CancellationToken cancellationToken)
    {
        var apex = arguments.GetProperty("apex").GetString()!;
        var zoneText = arguments.GetProperty("zone_text").GetString()!;
        var byteCount = Encoding.UTF8.GetByteCount(zoneText);
        if (byteCount > SeededContent.MaxBytes)
        {
            return McpToolResult.FromFailure($"zone_text: more than the {SeededContent.MaxBytes} (16 MiB) one seed may carry");
        }

        await running.Server.Zones.AddZoneAsync(apex, zoneText, cancellationToken);
        return ToolResults.FromObject(writer =>
        {
            writer.WriteString("apex", apex);
            writer.WriteNumber("byte_count", byteCount);
        });
    }
}
