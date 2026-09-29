using Surl.Content;

namespace Surl.Protocol.Dict;

/// <summary>
/// A hand-written in-memory <see cref="IContentFileSystem"/> for the DICT server's tests:
/// files and directories keyed by full path, with no symbolic links.
/// </summary>
internal sealed class InMemoryContentFileSystem : IContentFileSystem
{
    private readonly Dictionary<string, ContentEntryKind> entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> fileContents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> reportedLengths = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> lastWriteTimes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Exception> openFailures = new(StringComparer.Ordinal);

    /// <summary>
    /// Adds a file whose reported length is <paramref name="reportedLength"/>, or the length
    /// of <paramref name="contents"/> when it is <see langword="null"/>, so a test can make a
    /// file shrink between its status and its read.
    /// </summary>
    public InMemoryContentFileSystem AddFile(string path, byte[] contents, DateTimeOffset lastWriteTime, long? reportedLength = null)
    {
        entries[path] = ContentEntryKind.File;
        fileContents[path] = contents;
        reportedLengths[path] = reportedLength ?? contents.LongLength;
        lastWriteTimes[path] = lastWriteTime;
        return this;
    }

    /// <summary>
    /// Adds a file whose status can be read but whose opening throws <paramref name="failure"/>,
    /// as when it is deleted or its access is denied between the two.
    /// </summary>
    public InMemoryContentFileSystem AddUnreadableFile(string path, Exception failure)
    {
        openFailures[path] = failure;
        return AddFile(path, [], DateTimeOffset.UnixEpoch, reportedLength: 1);
    }

    public InMemoryContentFileSystem AddDirectory(string path)
    {
        entries[path] = ContentEntryKind.Directory;
        lastWriteTimes[path] = DateTimeOffset.UnixEpoch;
        return this;
    }

    public ContentEntryKind GetEntryKind(string path) => entries.GetValueOrDefault(path);

    public string ResolveFinalPath(string path) => path;

    public long GetFileLength(string path) => reportedLengths[path];

    public DateTimeOffset GetLastWriteTimeUtc(string path) => lastWriteTimes[path];

    public Stream OpenFileForAsyncRead(string path) =>
        openFailures.TryGetValue(path, out var failure) ? throw failure : new MemoryStream(fileContents[path], writable: false);

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path)
    {
        string prefix = path + Path.DirectorySeparatorChar;
        return entries.Keys
            .Where(entry => entry.StartsWith(prefix, StringComparison.Ordinal)
                && entry.IndexOf(Path.DirectorySeparatorChar, prefix.Length) < 0)
            .Select(entry => entry[prefix.Length..])
            .ToList();
    }
}
