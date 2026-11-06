using System.Globalization;
using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>restore_server</c> tool (ADR-0124 decisions 3 and 8): replaces a running server's stores with a snapshot
/// <c>snapshot_server</c> took of it, through <c>Surl.Api</c>'s <c>SurlServer.RestoreAsync</c>. Result
/// <c>{"server_id", "snapshot_id"}</c>; a snapshot ID the host does not keep for that server - never taken, or
/// dropped as the oldest - or a store that refuses is an <c>isError</c> result.
/// </summary>
public static class RestoreServerTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "restore_server";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Replace a running Surl server's files, mail, messages and accounts with a snapshot snapshot_server took of "
        + "that server. Open connections stay and see the restored contents on their next command.";

    /// <summary>The tool's input schema.</summary>
    internal const string InputSchema = """
        {
          "type": "object",
          "properties": {
            "server_id": { "type": "string", "description": "The ID start_server returned, e.g. s1" },
            "snapshot_id": { "type": "string", "description": "The ID snapshot_server returned for this server, e.g. p1" }
          },
          "required": ["server_id", "snapshot_id"],
          "additionalProperties": false
        }
        """;

    /// <summary>Creates the tool, restoring servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "Restore a Surl server", Description, InputSchema, RestoreAsync);

    /// <summary>The failure a snapshot ID the host does not keep is answered with.</summary>
    /// <param name="serverId">The server's ID.</param>
    /// <param name="snapshotId">The snapshot ID given.</param>
    /// <returns>The failure's text.</returns>
    internal static string NoSnapshot(string serverId, string snapshotId) => string.Create(
        CultureInfo.InvariantCulture,
        $"server {serverId} has no snapshot {snapshotId}; only its newest {ServerSnapshots.MaxSnapshots} snapshots are kept");

    private static async Task<McpToolResult> RestoreAsync(RunningSurlServer running, JsonElement arguments, CancellationToken cancellationToken)
    {
        var snapshotId = arguments.GetProperty("snapshot_id").GetString()!;
        if (running.Snapshots.Find(snapshotId) is not { } snapshot)
        {
            return McpToolResult.FromFailure(NoSnapshot(running.ServerId, snapshotId));
        }

        await running.Server.RestoreAsync(snapshot, cancellationToken);
        return ToolResults.FromObject(writer =>
        {
            writer.WriteString("server_id", running.ServerId);
            writer.WriteString("snapshot_id", snapshotId);
        });
    }
}
