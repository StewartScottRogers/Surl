namespace Surl.Content;

/// <summary>
/// One entry of a directory the content store listed: its name, whether it is a file or a
/// directory, a file's length, and when it was last written.
/// </summary>
/// <param name="Name">The entry's name inside the listed directory, as the file system spells
/// it; for a symbolic link, the link's own name.</param>
/// <param name="Kind"><see cref="ContentEntryKind.File"/> or
/// <see cref="ContentEntryKind.Directory"/>; for a symbolic link, the kind of its final
/// target.</param>
/// <param name="Length">A file's length in bytes; <see langword="null"/> for a
/// directory.</param>
/// <param name="LastModifiedUtc">When the entry (a symbolic link's final target) was last
/// written, in UTC.</param>
public sealed record ContentDirectoryEntry(string Name, ContentEntryKind Kind, long? Length, DateTimeOffset LastModifiedUtc);
