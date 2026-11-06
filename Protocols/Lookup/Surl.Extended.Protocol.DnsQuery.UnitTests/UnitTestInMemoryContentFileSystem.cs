using Surl.Content;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// A hand-written in-memory <see cref="IContentFileSystem"/> for the zone folder's tests: files
/// and directories keyed by full path, read only; a file added as unreadable throws
/// <see cref="IOException"/> when opened.
/// </summary>
internal sealed class UnitTestInMemoryContentFileSystem : IContentFileSystem
{
    private readonly Dictionary<string, ContentEntryKind> entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]?> fileContents = new(StringComparer.Ordinal);

    public UnitTestInMemoryContentFileSystem AddFile(string path, byte[] contents)
    {
        entries[path] = ContentEntryKind.File;
        fileContents[path] = contents;
        return this;
    }

    public UnitTestInMemoryContentFileSystem AddUnreadableFile(string path)
    {
        entries[path] = ContentEntryKind.File;
        fileContents[path] = null;
        return this;
    }

    public UnitTestInMemoryContentFileSystem AddDirectory(string path)
    {
        entries[path] = ContentEntryKind.Directory;
        return this;
    }

    public ContentEntryKind GetEntryKind(string path) => entries.GetValueOrDefault(path);

    public string ResolveFinalPath(string path) => path;

    public long GetFileLength(string path) => fileContents[path]!.LongLength;

    public DateTimeOffset GetLastWriteTimeUtc(string path) => DateTimeOffset.UnixEpoch;

    public Stream OpenFileForAsyncRead(string path) =>
        fileContents[path] is { } contents
            ? new MemoryStream(contents, writable: false)
            : throw new IOException("The process cannot access the file because it is being used by another process.");

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path)
    {
        var prefix = path + Path.DirectorySeparatorChar;
        return entries.Keys
            .Where(entry => entry.StartsWith(prefix, StringComparison.Ordinal)
                && entry.IndexOf(Path.DirectorySeparatorChar, prefix.Length) < 0)
            .Select(entry => entry[prefix.Length..])
            .ToList();
    }
}
