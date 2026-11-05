using Surl.Content;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Walks a bucket's folder tree and yields its files as object names in UTF-8 byte order (ADR-0110,
/// decision 6): each folder's entries sorted by their UTF-8 bytes, a folder <c>x</c> visited as
/// the prefix <c>x/</c>, and a folder no key under <c>prefix</c> can lie in not entered. Empty
/// folders are not keys. What the store hides from a listing - dot-files, links not followed - is
/// left out.
/// </summary>
/// <param name="store">The content store.</param>
/// <param name="caller">Who lists, and on which scheme.</param>
internal sealed class GcsBucketWalk(ContentStore store, ContentCaller caller)
{
    /// <summary>
    /// The files in a bucket whose keys start with <paramref name="prefix"/>.
    /// </summary>
    /// <param name="rawBucket">The bucket as sent, still percent-encoded.</param>
    /// <param name="prefix">The prefix every key starts with; empty for every key.</param>
    /// <param name="cancellationToken">Checked as each folder is listed.</param>
    /// <returns>The files, in key order.</returns>
    public IEnumerable<GcsListedFile> Files(string rawBucket, string prefix, CancellationToken cancellationToken) =>
        Walk($"/{rawBucket}/", string.Empty, prefix, cancellationToken);

    private IEnumerable<GcsListedFile> Walk(string folderPath, string keyPrefix, string prefix, CancellationToken cancellationToken)
    {
        var listing = store.ListDirectory(store.MapBucketRequestPathForCaller(folderPath, caller), cancellationToken);
        foreach (var entry in listing.Entries.OrderBy(SortName, GcsNameOrder.Instance))
        {
            var key = keyPrefix + SortName(entry);
            // The store lists only entries whose names it maps, so each entry maps.
            var mapping = store.MapBucketRequestPathForCaller(folderPath + Uri.EscapeDataString(entry.Name), caller);
            foreach (var file in Found(folderPath, entry, key, mapping, prefix, cancellationToken))
            {
                yield return file;
            }
        }
    }

    private IEnumerable<GcsListedFile> Found(string folderPath, ContentDirectoryEntry entry, string key, ContentPathMapping mapping, string prefix, CancellationToken cancellationToken)
    {
        if (entry.Kind != ContentEntryKind.Directory)
        {
            return key.StartsWith(prefix, StringComparison.Ordinal) ? [new GcsListedFile(key, mapping)] : [];
        }

        // A folder holds keys under the prefix when one starts with the other.
        return key.StartsWith(prefix, StringComparison.Ordinal) || prefix.StartsWith(key, StringComparison.Ordinal)
            ? Walk($"{folderPath}{Uri.EscapeDataString(entry.Name)}/", key, prefix, cancellationToken)
            : [];
    }

    private static string SortName(ContentDirectoryEntry entry) =>
        entry.Kind == ContentEntryKind.Directory ? $"{entry.Name}/" : entry.Name;
}
