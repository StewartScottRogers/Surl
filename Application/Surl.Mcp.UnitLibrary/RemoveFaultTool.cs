using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>remove_fault</c> tool (ADR-0124 decisions 4 and 8): removes one rule from a running server's fault plan, a
/// rule given at start included, through <c>Surl.Api</c>'s <c>SurlServer.Faults.Remove</c>. Result
/// <c>{"fault_id"}</c>; an ID the plan does not hold is an <c>isError</c> result.
/// </summary>
public static class RemoveFaultTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "remove_fault";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Remove a fault rule from a running Surl server, so it behaves again from its next connection, login or "
        + "command on: one add_fault returned, or f<n> for the n-th --fault rule given at start.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "fault_id": { "type": "string", "description": "The rule's ID, e.g. f1" }
          },
          "required": ["server_id", "fault_id"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, removing rules from servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "Remove a fault rule from a Surl server", Description, InputSchema,
            (running, arguments, _) => Task.FromResult(Remove(running, arguments)));

    /// <summary>The failure an ID the plan does not hold is answered with.</summary>
    /// <param name="serverId">The server's ID.</param>
    /// <param name="faultId">The rule ID given.</param>
    /// <returns>The failure's text.</returns>
    internal static string NoFault(string serverId, string faultId) => $"server {serverId} has no fault rule {faultId}";

    private static McpToolResult Remove(RunningSurlServer running, JsonElement arguments)
    {
        var faultId = arguments.GetProperty("fault_id").GetString()!;
        return running.Server.Faults.Remove(faultId)
            ? ToolResults.FromObject(writer => writer.WriteString("fault_id", faultId))
            : McpToolResult.FromFailure(NoFault(running.ServerId, faultId));
    }
}
