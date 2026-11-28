using Surl.McpServing;

namespace Surl.Mcp;

/// <summary>
/// The <c>reset_server</c> tool (ADR-0124 decisions 3 and 8): returns a running server's stores, the content store,
/// mail store, message bus, accounts, Redis keyspace, LDAP directory and DNS zones among them, to their state at start,
/// <c>start_server</c>'s seeds included (ADR-0158 decision 4), through <c>Surl.Api</c>'s <c>SurlServer.ResetAsync</c>.
/// Result <c>{"server_id"}</c>; a store that refuses, such as one on disk, is an <c>isError</c> result naming it.
/// </summary>
public static class ResetServerTool
{
    /// <summary>The tool's name.</summary>
    public const string Name = "reset_server";

    /// <summary>The tool's description.</summary>
    internal const string Description =
        "Return a running Surl server's files, mail, messages, accounts, Redis keys, LDAP directory and DNS zones to "
        + "their state at start, what `start_server` seeded included. Open connections "
        + "stay and see the new contents on their next command. A store on disk refuses.";

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
