using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>reset_server</c> tool (ADR-0124 decisions 3 and 8): returns a running server's stores to their state at
/// start, <c>start_server</c>'s seeds included (ADR-0158 decision 4), through <c>Surl.Api</c>'s
/// <c>SurlServer.ResetAsync</c>, which also discards open uploads and ends OCI tokens, MCP OAuth state and MCP HTTP
/// sessions (ADR-0167 decision 2). Result <c>{"server_id"}</c>; a store that refuses, such as one on disk, is an
/// <c>isError</c> result naming it.
/// </summary>
public static class ResetServerTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "reset_server";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Return a running Surl server's files, mail, messages, accounts, proxy accounts, TLS-SRP accounts, Redis keys, "
        + "LDAP directory, AMQP 0-9-1 exchanges and bindings, and DNS zones to their state at start, what "
        + "`start_server` seeded included. It also clears Kafka producer state and consumer groups, and MQTT 5 sessions "
        + "kept with no connection, pending Wills, queued deliveries and unacknowledged publishes. It discards every open "
        + "or staged object upload (S3, Azure Storage, Google Cloud Storage, OCI) and Azure Storage's remembered "
        + "committed block lists, and ends every OCI registry token, all MCP server OAuth state (clients, codes and "
        + "tokens) and every MCP HTTP session. Open connections stay open and see the new contents on their next "
        + "command. A store on disk refuses.";

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

    /// <summary>Creates the tool, resetting servers in <paramref name="servers"/>.</summary>
    /// <param name="servers">The host's running servers.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="servers"/> is <see langword="null"/>.</exception>
    public static McpTool Create(RunningSurlServers servers) =>
        RunningServerTool.Create(servers, Name, "Reset a Surl server", Description, InputSchema,
            async (running, _, cancellationToken) =>
            {
                await running.Server.ResetAsync(cancellationToken);
                return ToolResults.FromObject(writer => writer.WriteString("server_id", running.ServerId));
            });
}
