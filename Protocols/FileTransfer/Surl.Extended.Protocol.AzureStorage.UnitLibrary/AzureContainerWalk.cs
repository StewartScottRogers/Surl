using Surl.Content;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Walks a container's folder tree and yields its files as blob names in ascending UTF-8 byte
/// order (ADR-0109, decision 5.3; ADR-0108, decision 6): each folder's entries sorted by their
/// UTF-8 bytes, a folder <c>x</c> visited as the prefix <c>x/</c>, and a folder no name under
/// <c>prefix</c> can lie in not entered. Empty folders are not blobs. What the store hides from a
/// listing - dot-files, links not followed - is left out.
/// </summary>
/// <param name="store">The content store.</param>
/// <param name="caller">Who lists, and on which scheme.</param>
internal sealed class AzureContainerWalk(ContentStore store, ContentCaller caller)
{
    /// <summary>
    /// The files in a container whose blob names start with <paramref name="prefix"/>.
    /// </summary>
    /// <param name="rawContainer">The container as sent, still percent-encoded.</param>
    /// <param name="prefix">The prefix every name starts with; empty for every name.</param>
    /// <param name="cancellationToken">Checked as each folder is listed.</param>
    /// <returns>The files, in name order.</returns>
    public IEnumerable<AzureListedBlob> Blobs(string rawContainer, string prefix, CancellationToken cancellationToken) =>
        Walk($"/{rawContainer}/", string.Empty, prefix, cancellationToken);

    private IEnumerable<AzureListedBlob> Walk(string folderPath, string namePrefix, string prefix, CancellationToken cancellationToken)
    {
        var listing = store.ListDirectory(store.MapBucketRequestPathForCaller(folderPath, caller), cancellationToken);
        foreach (var entry in listing.Entries.OrderBy(SortName, AzureNameOrder.Instance))
        {
            var name = namePrefix + SortName(entry);
            // The store lists only entries whose names it maps, so each entry maps.
            var mapping = store.MapBucketRequestPathForCaller(folderPath + Uri.EscapeDataString(entry.Name), caller);
            foreach (var blob in Found(folderPath, entry, name, mapping, prefix, cancellationToken))
            {
                yield return blob;
            }
        }
    }

    private IEnumerable<AzureListedBlob> Found(string folderPath, ContentDirectoryEntry entry, string name, ContentPathMapping mapping, string prefix, CancellationToken cancellationToken)
    {
        if (entry.Kind != ContentEntryKind.Directory)
        {
            return name.StartsWith(prefix, StringComparison.Ordinal) ? [new AzureListedBlob(name, mapping)] : [];
        }

        // A folder holds names under the prefix when one starts with the other.
        return name.StartsWith(prefix, StringComparison.Ordinal) || prefix.StartsWith(name, StringComparison.Ordinal)
            ? Walk($"{folderPath}{Uri.EscapeDataString(entry.Name)}/", name, prefix, cancellationToken)
            : [];
    }

    private static string SortName(ContentDirectoryEntry entry) =>
        entry.Kind == ContentEntryKind.Directory ? $"{entry.Name}/" : entry.Name;
}
