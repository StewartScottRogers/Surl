namespace Surl.Content;

/// <summary>
/// A copy of every entry an <see cref="InMemoryContentFileSystem"/> held at one moment: the
/// content store's configured state, and its part of a <see cref="ContentStoreSnapshot"/>
/// (ADR-0124 decision 3).
/// </summary>
/// <param name="Entries">Every directory and file, by full path.</param>
internal sealed record InMemoryContentFileSystemContents(IReadOnlyList<InMemoryContentFileSystemContents.Entry> Entries)
{
    /// <summary>The bytes of every file in the copy.</summary>
    internal long ByteCount => Entries.Sum(entry => (long)entry.Contents.Length);

    /// <summary>One directory or file.</summary>
    /// <param name="Path">Its full path, as the file system keys it.</param>
    /// <param name="Kind">A directory or a file.</param>
    /// <param name="Contents">A file's bytes; empty for a directory. Shared, never changed.</param>
    /// <param name="LastWriteTimeUtc">Its last write time.</param>
    internal sealed record Entry(string Path, ContentEntryKind Kind, byte[] Contents, DateTimeOffset LastWriteTimeUtc);
}
