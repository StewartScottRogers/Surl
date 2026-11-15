using Surl.Content;
using Surl.Extended.Protocol.S3;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Joins the open multipart uploads of the composed S3 servers to whole-instance reset, snapshot and
/// restore (ADR-0167 decisions 2, 3 and 5): reset and restore abort every open upload, snapshot holds none.
/// </summary>
internal static class S3UploadStoreRegistration
{
    private const string OnDiskMessage =
        "The content store is on disk (--directory): the S3 server's open uploads are not aborted, so a test never deletes a file in a real directory.";

    /// <summary>
    /// Builds the entry that controls every S3 server's open uploads, publishing one
    /// <see cref="StoreChange.Reset"/> or <see cref="StoreChange.Restored"/> per run named
    /// <see cref="S3ProtocolServer.Scheme"/>.
    /// </summary>
    /// <param name="servers">The composed S3 servers.</param>
    /// <param name="content">The content store the uploads' temporary files live in.</param>
    /// <param name="events">The instance's event stream.</param>
    /// <returns>The entry, or <see langword="null"/> when no S3 server is composed.</returns>
    public static ComposedStoreRegistration? For(IReadOnlyList<S3ProtocolServer> servers, ContentStore content, ISurlEventPublisher events) =>
        servers.Count == 0
            ? null
            : new ComposedStoreRegistration(
                StoreKind.Uploads,
                cancellationToken => AbortAsync(servers, content, events, StoreChange.Reset, cancellationToken),
                (_, _) =>
                {
                    RequireInMemory(content);
                    return Task.FromResult(new TakenSnapshot(
                        StoreKind.Uploads,
                        0,
                        cancellationToken => AbortAsync(servers, content, events, StoreChange.Restored, cancellationToken)));
                });

    private static async Task AbortAsync(IReadOnlyList<S3ProtocolServer> servers, ContentStore content, ISurlEventPublisher events, StoreChange change, CancellationToken cancellationToken)
    {
        RequireInMemory(content);
        foreach (var server in servers)
        {
            await server.AbortOpenUploadsAsync(cancellationToken);
        }

        events.Publish(new StoreChangedEvent(StoreKind.Uploads, change, S3ProtocolServer.Scheme, null, null));
    }

    private static void RequireInMemory(ContentStore content)
    {
        if (!content.IsControllable)
        {
            throw new InvalidOperationException(OnDiskMessage);
        }
    }
}
