using System.Globalization;
using System.Text.Json;
using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>restore_server</c> tool (ADR-0124 decisions 3 and 8): replaces a running server's stores with a snapshot
/// <c>snapshot_server</c> took of it, through <c>Surl.Api</c>'s <c>SurlServer.RestoreAsync</c>, which clears what a
/// snapshot does not copy, the messages of queues exclusive to a connection (the queues are kept), open uploads, OCI
/// tokens, MCP OAuth state and MCP HTTP sessions among it (ADR-0167 decision 2). Result <c>{"server_id", "snapshot_id"}</c>; a snapshot ID the host does not keep for that server is an <c>isError</c>
/// result that names the other running server keeping an ID like it (a snapshot restores only into the server
/// that took it), or else says it was never taken or was dropped as the oldest; a store that refuses is one too.
/// </summary>
public static class RestoreServerTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "restore_server";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Replace a running Surl server's files, mail, messages, accounts, proxy accounts, TLS-SRP accounts, Redis keys, "
        + "LDAP directory, AMQP 0-9-1 exchanges and bindings, Kafka producer state, MQTT 5 sessions kept with no "
        + "connection and DNS zones with a snapshot snapshot_server took of that server. What a snapshot does not copy "
        + "is cleared, even if it began before the snapshot: the messages of queues exclusive to a connection (the queues "
        + "are kept); Kafka consumer groups; MQTT 5 pending Wills, queued deliveries and unacknowledged publishes; every "
        + "open or staged object upload (S3, Azure Storage, Google Cloud Storage, OCI) and Azure Storage's remembered "
        + "committed block lists; every OCI registry token; all MCP server OAuth state (clients, codes and tokens); and "
        + "every MCP HTTP session. A snapshot restores only into the server that took it. Open connections stay open and "
        + "see the restored contents on their next command.";

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
        RunningServerTool.Create(
            servers,
            Name,
            "Restore a Surl server",
            Description,
            InputSchema,
            (running, arguments, cancellationToken) => RestoreAsync(servers, running, arguments, cancellationToken));

    /// <summary>The failure a snapshot ID the host does not keep for any server is answered with.</summary>
    /// <param name="serverId">The server's ID.</param>
    /// <param name="snapshotId">The snapshot ID given.</param>
    /// <returns>The failure's text.</returns>
    internal static string NoSnapshot(string serverId, string snapshotId) => string.Create(
        CultureInfo.InvariantCulture,
        $"server {serverId} has no snapshot {snapshotId}; it was never taken of this server, or was dropped as one past its newest {ServerSnapshots.MaxSnapshots} snapshots");

    /// <summary>The failure a snapshot ID another running server keeps is answered with.</summary>
    /// <param name="serverId">The server's ID.</param>
    /// <param name="snapshotId">The snapshot ID given.</param>
    /// <param name="keeperId">The ID of the server that keeps it.</param>
    /// <returns>The failure's text.</returns>
    internal static string SnapshotOfAnotherServer(string serverId, string snapshotId, string keeperId) => string.Create(
        CultureInfo.InvariantCulture,
        $"server {serverId} has no snapshot {snapshotId}; server {keeperId} keeps a snapshot {snapshotId}, and a snapshot restores only into the server that took it");

    private static async Task<McpToolResult> RestoreAsync(
        RunningSurlServers servers, RunningSurlServer running, JsonElement arguments, CancellationToken cancellationToken)
    {
        var snapshotId = arguments.GetProperty("snapshot_id").GetString()!;
        if (running.Snapshots.Find(snapshotId) is not { } snapshot)
        {
            var keeper = servers.List().FirstOrDefault(other => other != running && other.Snapshots.Find(snapshotId) is not null);
            return McpToolResult.FromFailure(keeper is null
                ? NoSnapshot(running.ServerId, snapshotId)
                : SnapshotOfAnotherServer(running.ServerId, snapshotId, keeper.ServerId));
        }

        await running.Server.RestoreAsync(snapshot, cancellationToken);
        return ToolResults.FromObject(writer =>
        {
            writer.WriteString("server_id", running.ServerId);
            writer.WriteString("snapshot_id", snapshotId);
        });
    }
}
