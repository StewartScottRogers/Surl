namespace Surl.Content;

/// <summary>
/// The outcome of listing a mapped location: the entries of a directory, or what was found
/// there instead when it is not one.
/// </summary>
public sealed class ContentDirectoryListing
{
    private ContentDirectoryListing(ContentEntryKind locationKind, IReadOnlyList<ContentDirectoryEntry> entries)
    {
        LocationKind = locationKind;
        Entries = entries;
    }

    /// <summary>
    /// Whether the location is a directory and <see cref="Entries"/> lists it.
    /// </summary>
    public bool IsListed => LocationKind == ContentEntryKind.Directory;

    /// <summary>
    /// What the file-system seam found at the location when it was listed: a directory,
    /// a file, or nothing.
    /// </summary>
    public ContentEntryKind LocationKind { get; }

    /// <summary>
    /// The directory's entries, in ordinal order of <see cref="ContentDirectoryEntry.Name"/>;
    /// empty when the directory is empty or the location is not a directory.
    /// </summary>
    public IReadOnlyList<ContentDirectoryEntry> Entries { get; }

    internal static ContentDirectoryListing Listed(IReadOnlyList<ContentDirectoryEntry> entries) =>
        new(ContentEntryKind.Directory, entries);

    internal static ContentDirectoryListing NotADirectory(ContentEntryKind locationKind) =>
        new(locationKind, []);
}
