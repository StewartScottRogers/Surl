using Surl.Extended.Protocol.OciRegistry;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Joins the tokens of the composed OCI registries to whole-instance reset, snapshot and restore
/// (ADR-0167 decisions 2 and 3): reset and restore revoke every token, snapshot holds none.
/// </summary>
internal static class OciTokenStoreRegistration
{
    private const string SchemeName = "oci";

    /// <summary>
    /// Builds the entry that revokes every OCI registry's tokens, publishing one
    /// <see cref="StoreChange.Reset"/> or <see cref="StoreChange.Restored"/> per run named <c>oci</c>.
    /// </summary>
    /// <param name="servers">The composed OCI registries.</param>
    /// <param name="events">The instance's event stream.</param>
    /// <returns>The entry, or <see langword="null"/> when no OCI registry is composed.</returns>
    public static ComposedStoreRegistration? For(IReadOnlyList<OciRegistryProtocolServer> servers, ISurlEventPublisher events) =>
        servers.Count == 0
            ? null
            : new ComposedStoreRegistration(
                StoreKind.Sessions,
                _ => Revoke(servers, events, StoreChange.Reset),
                (_, _) => Task.FromResult(new TakenSnapshot(StoreKind.Sessions, 0, _ => Revoke(servers, events, StoreChange.Restored))));

    private static Task Revoke(IReadOnlyList<OciRegistryProtocolServer> servers, ISurlEventPublisher events, StoreChange change)
    {
        foreach (var server in servers)
        {
            server.RevokeTokens();
        }

        events.Publish(new StoreChangedEvent(StoreKind.Sessions, change, SchemeName, null, null));
        return Task.CompletedTask;
    }
}
