using Surl.Extended.Protocol.McpServer;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Joins the OAuth state and HTTP sessions of the composed MCP servers to whole-instance reset, snapshot and
/// restore (ADR-0167 decisions 2 and 3): reset and restore delete every client, code and token and end every
/// session, snapshot holds none.
/// </summary>
internal static class McpSessionStoreRegistration
{
    private const string SchemeName = "mcp";

    /// <summary>
    /// Builds the entry that clears every MCP server's OAuth state and sessions, publishing one
    /// <see cref="StoreChange.Reset"/> or <see cref="StoreChange.Restored"/> per run named <c>mcp</c>.
    /// </summary>
    /// <param name="servers">The composed MCP servers.</param>
    /// <param name="events">The instance's event stream.</param>
    /// <returns>The entry, or <see langword="null"/> when no MCP server is composed.</returns>
    public static ComposedStoreRegistration? For(IReadOnlyList<McpProtocolServer> servers, ISurlEventPublisher events) =>
        servers.Count == 0
            ? null
            : new ComposedStoreRegistration(
                StoreKind.Sessions,
                _ => Clear(servers, events, StoreChange.Reset),
                (_, _) => Task.FromResult(new TakenSnapshot(StoreKind.Sessions, 0, _ => Clear(servers, events, StoreChange.Restored))));

    private static Task Clear(IReadOnlyList<McpProtocolServer> servers, ISurlEventPublisher events, StoreChange change)
    {
        foreach (var server in servers)
        {
            server.ResetOAuthAndSessions();
        }

        events.Publish(new StoreChangedEvent(StoreKind.Sessions, change, SchemeName, null, null));
        return Task.CompletedTask;
    }
}
