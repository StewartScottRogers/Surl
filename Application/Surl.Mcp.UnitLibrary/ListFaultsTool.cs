using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>list_faults</c> tool (ADR-0124 decisions 4 and 8): lists the rules of a running server's fault plan, in
/// the order they are tried, through <c>Surl.Api</c>'s <c>SurlServer.Faults.List</c>. Result
/// <c>{"faults": [{"fault_id", "item", "remaining"}]}</c>; <c>remaining</c> is <c>null</c> for a rule without
/// <c>times</c>. An unknown server ID is an <c>isError</c> result.
/// </summary>
public static class ListFaultsTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "list_faults";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "List the fault rules of a running Surl server in the order they are tried: each rule's ID (for remove_fault), "
        + "its --fault item without times, and how many more times it applies (null when it always does, 0 once spent).";

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

    /// <summary>Creates the tool, listing rules of servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "List the fault rules of a Surl server", Description, InputSchema,
            (running, _, _) => Task.FromResult(List(running)));

    private static McpToolResult List(RunningSurlServer running) =>
        ToolResults.FromObject(writer =>
        {
            writer.WriteStartArray("faults");
            foreach (var rule in running.Server.Faults.List())
            {
                writer.WriteStartObject();
                writer.WriteString("fault_id", rule.Id);
                writer.WriteString("item", rule.Item);
                if (rule.Remaining is { } remaining)
                {
                    writer.WriteNumber("remaining", remaining);
                }
                else
                {
                    writer.WriteNull("remaining");
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        });
}
