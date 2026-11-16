using Surl.Content;
using Surl.Extended.Protocol.AzureStorage;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Joins the staged blocks of the composed Azure Storage servers to whole-instance reset, snapshot and
/// restore (ADR-0167 decisions 2, 3 and 5): reset and restore discard every staged upload, snapshot holds none.
/// </summary>
internal static class AzureStagedUploadStoreRegistration
{
    private const string SchemeName = "azure";

    private const string OnDiskMessage =
        "The content store is on disk (--directory): the Azure Storage server's staged blocks are not discarded, so a test never deletes a file in a real directory.";

    /// <summary>
    /// Builds the entry that controls every Azure Storage server's staged blocks, publishing one
    /// <see cref="StoreChange.Reset"/> or <see cref="StoreChange.Restored"/> per run named
    /// <c>azure</c>.
    /// </summary>
    /// <param name="servers">The composed Azure Storage servers.</param>
    /// <param name="content">The content store the uploads' temporary files live in.</param>
    /// <param name="events">The instance's event stream.</param>
    /// <returns>The entry, or <see langword="null"/> when no Azure Storage server is composed.</returns>
    public static ComposedStoreRegistration? For(IReadOnlyList<AzureStorageProtocolServer> servers, ContentStore content, ISurlEventPublisher events) =>
        servers.Count == 0
            ? null
            : new ComposedStoreRegistration(
                StoreKind.Uploads,
                cancellationToken => DiscardAsync(servers, content, events, StoreChange.Reset, cancellationToken),
                (_, _) =>
                {
                    RequireInMemory(content);
                    return Task.FromResult(new TakenSnapshot(
                        StoreKind.Uploads,
                        0,
                        cancellationToken => DiscardAsync(servers, content, events, StoreChange.Restored, cancellationToken)));
                });

    private static async Task DiscardAsync(IReadOnlyList<AzureStorageProtocolServer> servers, ContentStore content, ISurlEventPublisher events, StoreChange change, CancellationToken cancellationToken)
    {
        RequireInMemory(content);
        foreach (var server in servers)
        {
            await server.DiscardStagedUploadsAsync(cancellationToken);
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
