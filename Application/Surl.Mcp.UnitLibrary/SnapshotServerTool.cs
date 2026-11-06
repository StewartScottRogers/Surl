using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>snapshot_server</c> tool (ADR-0124 decisions 3 and 8): takes an in-memory copy of a running server's
/// stores through <c>Surl.Api</c>'s <c>SurlServer.SnapshotAsync</c> and keeps it in the host under an ID, never
/// returning it whole. Result <c>{"snapshot_id"}</c>; the host keeps the newest
/// <see cref="ServerSnapshots.MaxSnapshots"/> per server. A store that refuses, such as one on disk, or stores past
/// 256 MiB, is an <c>isError</c> result.
/// </summary>
public static class SnapshotServerTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "snapshot_server";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Take a copy of a running Surl server's files, mail, messages and accounts, kept in memory by this host, and "
        + "return its ID for restore_server. The newest 4 snapshots per server are kept; the oldest is dropped.";

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

    /// <summary>Creates the tool, snapshotting servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "Snapshot a Surl server", Description, InputSchema,
            async (running, _, cancellationToken) =>
            {
                var snapshotId = running.Snapshots.Keep(await running.Server.SnapshotAsync(cancellationToken));
                return ToolResults.FromObject(writer => writer.WriteString("snapshot_id", snapshotId));
            });
}
