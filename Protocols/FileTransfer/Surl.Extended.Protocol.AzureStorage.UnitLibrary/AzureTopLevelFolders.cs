using Surl.Content;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The containers and filesystems a caller may read, as List Containers and Filesystem - List name
/// them (ADR-0109, decisions 1 and 2; ADR-0100, decision 2.1; ADR-0102, decision 2.3): the served
/// root's top-level folders - shares included, as the root's listing shows them - and every
/// <c>--bucket</c>, in ascending UTF-8 byte order of their names.
/// </summary>
/// <remarks>
/// A top-level file is not a container, and a share or bucket the caller may not read is left
/// out, as it is missing to every other request. Like List Blobs it is a listing in ADR-0006's
/// sense, so the callers check <c>--list-directories</c> first: off, the root lists nothing.
/// </remarks>
/// <param name="store">The content store whose top-level folders are the containers.</param>
/// <param name="caller">Who lists, and on which scheme.</param>
internal sealed class AzureTopLevelFolders(ContentStore store, ContentCaller caller)
{
    /// <summary>
    /// The folders whose names start with <paramref name="prefix"/> and come after
    /// <paramref name="after"/>, in name order.
    /// </summary>
    /// <param name="prefix">The prefix every name starts with; empty for every name.</param>
    /// <param name="after">The name a page resumes after, or <see langword="null"/> for the first page.</param>
    /// <param name="cancellationToken">Cancels reading the root folder.</param>
    /// <returns>Each folder's name and last write time.</returns>
    public IEnumerable<AzureTopLevelFolder> Folders(string prefix, string? after, CancellationToken cancellationToken)
    {
        var listing = store.ListDirectory(store.MapBucketRequestPathForCaller("/", caller), cancellationToken);
        var names = listing.Entries.Select(entry => entry.Name).Concat(store.Buckets.Select(bucket => bucket.Name));
        foreach (var name in names.Distinct(StringComparer.Ordinal).Order(AzureNameOrder.Instance))
        {
            if (!name.StartsWith(prefix, StringComparison.Ordinal) || (after is not null && AzureNameOrder.Instance.Compare(name, after) <= 0))
            {
                continue;
            }

            // The store lists only names it maps and checks every bucket name at start, so each
            // name maps; with the trailing '/', a status is a folder's, and a file, a hidden share
            // or nothing has none.
            if (store.GetEntryStatus(store.MapBucketRequestPathForCaller($"/{Uri.EscapeDataString(name)}/", caller)) is { } folder)
            {
                yield return new AzureTopLevelFolder(name, folder.LastModifiedUtc);
            }
        }
    }
}
