using Surl.Content;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Walks a Data Lake directory and yields its files and directories in ascending UTF-8 byte order of
/// their <see cref="AzureListedPath.SortKey"/> (ADR-0109, decision 5.4): each directory just before
/// what it holds, which a recursive walk yields next. What the store hides from a listing is left
/// out.
/// </summary>
/// <param name="store">The content store.</param>
/// <param name="caller">Who lists, and on which scheme.</param>
internal sealed class AzureFilesystemWalk(ContentStore store, ContentCaller caller)
{
    /// <summary>
    /// The paths under a directory whose sort keys come after <paramref name="after"/>.
    /// </summary>
    /// <param name="folderPath">The directory's request path, percent-encoded, ending in <c>/</c>.</param>
    /// <param name="namePrefix">What each name under it starts with: empty at the filesystem root,
    /// else the directory's path and <c>/</c>.</param>
    /// <param name="isRecursive">Whether directories are walked into.</param>
    /// <param name="after">The sort key a continuation resumes after, or null for the first page.</param>
    /// <param name="cancellationToken">Checked as each directory is listed.</param>
    /// <returns>The paths, in order.</returns>
    public IEnumerable<AzureListedPath> Paths(string folderPath, string namePrefix, bool isRecursive, string? after, CancellationToken cancellationToken) =>
        store.ListDirectory(store.MapBucketRequestPathForCaller(folderPath, caller), cancellationToken).Entries
            .OrderBy(SortName, AzureNameOrder.Instance)
            .SelectMany(entry => PathsAt(folderPath, namePrefix, entry, isRecursive, after, cancellationToken));

    // The entry itself when it comes after the continuation, then what it holds when it is a
    // directory the walk enters: one after the continuation, or one the continuation lies inside.
    private IEnumerable<AzureListedPath> PathsAt(string folderPath, string namePrefix, ContentDirectoryEntry entry, bool isRecursive, string? after, CancellationToken cancellationToken)
    {
        var entryPath = folderPath + Uri.EscapeDataString(entry.Name);
        // The store lists only entries whose names it maps, so each entry maps.
        var path = new AzureListedPath(namePrefix + entry.Name, entry.Kind == ContentEntryKind.Directory, store.MapBucketRequestPathForCaller(entryPath, caller), entry.LastModifiedUtc);
        var isAfter = after is null || AzureNameOrder.Instance.Compare(path.SortKey, after) > 0;
        IEnumerable<AzureListedPath> own = isAfter ? [path] : [];
        var entersDirectory = path.IsDirectory && isRecursive && (isAfter || after!.StartsWith(path.SortKey, StringComparison.Ordinal));

        return entersDirectory ? own.Concat(Paths($"{entryPath}/", path.SortKey, isRecursive, after, cancellationToken)) : own;
    }

    private static string SortName(ContentDirectoryEntry entry) =>
        entry.Kind == ContentEntryKind.Directory ? $"{entry.Name}/" : entry.Name;
}
