using Surl.Content;
using Surl.Extended.Protocol.OciRegistry;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Joins the open blob upload sessions of the composed OCI registries to whole-instance reset, snapshot and
/// restore (ADR-0167 decisions 2, 3 and 5): reset and restore abort every open session, snapshot holds none.
/// </summary>
internal static class OciUploadStoreRegistration
{
    private const string SchemeName = "oci";

    private const string OnDiskMessage =
        "The content store is on disk (--directory): the OCI registry's open uploads are not aborted, so a test never deletes a file in a real directory.";

    /// <summary>
    /// Builds the entry that controls every OCI registry's open uploads, publishing one
    /// <see cref="StoreChange.Reset"/> or <see cref="StoreChange.Restored"/> per run named <c>oci</c>.
    /// </summary>
    /// <param name="servers">The composed OCI registries.</param>
    /// <param name="content">The content store the sessions' staged files live in.</param>
    /// <param name="events">The instance's event stream.</param>
    /// <returns>The entry, or <see langword="null"/> when no OCI registry is composed.</returns>
    public static ComposedStoreRegistration? For(IReadOnlyList<OciRegistryProtocolServer> servers, ContentStore content, ISurlEventPublisher events) =>
        servers.Count == 0
            ? null
            : new ComposedStoreRegistration(
                StoreKind.Uploads,
                _ => AbortAsync(servers, content, events, StoreChange.Reset),
                (_, _) =>
                {
                    RequireInMemory(content);
                    return Task.FromResult(new TakenSnapshot(
                        StoreKind.Uploads,
                        0,
                        _ => AbortAsync(servers, content, events, StoreChange.Restored)));
                });

    private static async Task AbortAsync(IReadOnlyList<OciRegistryProtocolServer> servers, ContentStore content, ISurlEventPublisher events, StoreChange change)
    {
        RequireInMemory(content);
        foreach (var server in servers)
        {
            await server.AbortOpenUploadsAsync();
        }

        events.Publish(new StoreChangedEvent(StoreKind.Uploads, change, SchemeName, null, null));
    }

    private static void RequireInMemory(ContentStore content)
    {
        if (!content.IsControllable)
        {
            throw new InvalidOperationException(OnDiskMessage);
        }
    }
}
