using Surl.Content;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// Walks a bucket's folder tree and yields its objects as keys in UTF-8 binary order (ADR-0108,
/// decision 6): each folder's entries sorted by their UTF-8 bytes, a folder <c>x</c> visited as
/// the prefix <c>x/</c>, and a folder no key under <c>prefix</c> can lie in not entered. A folder
/// is a key, <c>x/</c>, only when a client put it as an object, which the object metadata index
/// records; any other folder, empty or not, is not. What the store hides from a listing -
/// dot-files, links not followed - is left out.
/// </summary>
/// <param name="store">The content store.</param>
/// <param name="caller">Who lists, and on which scheme.</param>
/// <param name="folderObjects">Every folder object the index holds, by its index key
/// (<see cref="S3IndexKey.OfFolderObject"/>).</param>
internal sealed class S3BucketWalk(ContentStore store, ContentCaller caller, IReadOnlyDictionary<string, ObjectProperties> folderObjects)
{
    /// <summary>
    /// The objects in a bucket whose keys start with <paramref name="prefix"/>.
    /// </summary>
    /// <param name="rawBucket">The bucket as sent, still percent-encoded.</param>
    /// <param name="prefix">The prefix every key starts with; empty for every key.</param>
    /// <param name="cancellationToken">Checked as each folder is listed.</param>
    /// <returns>The objects, in key order.</returns>
    public IEnumerable<S3ListedFile> Files(string rawBucket, string prefix, CancellationToken cancellationToken) =>
        Walk(new WalkedBucket(Uri.UnescapeDataString(rawBucket), prefix), $"/{rawBucket}/", string.Empty, cancellationToken);

    private IEnumerable<S3ListedFile> Walk(WalkedBucket bucket, string folderPath, string keyPrefix, CancellationToken cancellationToken)
    {
        var listing = store.ListDirectory(store.MapBucketRequestPathForCaller(folderPath, caller), cancellationToken);
        foreach (var entry in listing.Entries.OrderBy(SortName, S3KeyOrder.Instance))
        {
            var key = keyPrefix + SortName(entry);
            // The store lists only entries whose names it maps, so each entry maps.
            var mapping = store.MapBucketRequestPathForCaller(folderPath + Uri.EscapeDataString(entry.Name), caller);
            foreach (var file in Found(bucket, folderPath, entry, key, mapping, cancellationToken))
            {
                yield return file;
            }
        }
    }

    private IEnumerable<S3ListedFile> Found(WalkedBucket bucket, string folderPath, ContentDirectoryEntry entry, string key, ContentPathMapping mapping, CancellationToken cancellationToken)
    {
        var prefix = bucket.Prefix;
        if (entry.Kind != ContentEntryKind.Directory)
        {
            return key.StartsWith(prefix, StringComparison.Ordinal) ? [new S3ListedFile(key, mapping)] : [];
        }

        // A folder holds keys under the prefix when one starts with the other.
        var inside = key.StartsWith(prefix, StringComparison.Ordinal) || prefix.StartsWith(key, StringComparison.Ordinal)
            ? Walk(bucket, $"{folderPath}{Uri.EscapeDataString(entry.Name)}/", key, cancellationToken)
            : [];

        // The folder's own key, x/, sorts before every key inside it.
        return FolderObjectAt(bucket, key, mapping) is { } folderObject ? inside.Prepend(folderObject) : inside;
    }

    private S3ListedFile? FolderObjectAt(WalkedBucket bucket, string key, ContentPathMapping mapping) =>
        key.StartsWith(bucket.Prefix, StringComparison.Ordinal)
        && S3IndexKey.OfFolderObject(store, bucket.Name, key) is { } indexKey
        && folderObjects.TryGetValue(indexKey, out var properties)
            ? new S3ListedFile(key, mapping, properties)
            : null;

    private static string SortName(ContentDirectoryEntry entry) =>
        entry.Kind == ContentEntryKind.Directory ? $"{entry.Name}/" : entry.Name;

    // The bucket being walked, decoded, and the prefix every key starts with.
    private sealed record WalkedBucket(string Name, string Prefix);
}
