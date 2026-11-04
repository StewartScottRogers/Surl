using Surl.Content;

namespace Surl.Protocol.Http;

/// <summary>
/// A hand-written in-memory <see cref="IContentFileSystem"/> for the HTTP server's tests:
/// files and directories keyed by full path, with no symbolic links.
/// </summary>
internal sealed class UnitTestInMemoryContentFileSystem : IContentFileSystem
{
    private readonly Dictionary<string, ContentEntryKind> entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> fileContents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> reportedLengths = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> lastWriteTimes = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Path, string Member), Exception> failures = [];

    /// <summary>
    /// Adds a file whose reported length is <paramref name="reportedLength"/>, or the length
    /// of <paramref name="contents"/> when it is <see langword="null"/>, so a test can make a
    /// file shrink between its status and its read.
    /// </summary>
    public UnitTestInMemoryContentFileSystem AddFile(string path, byte[] contents, DateTimeOffset lastWriteTime, long? reportedLength = null)
    {
        entries[path] = ContentEntryKind.File;
        fileContents[path] = contents;
        reportedLengths[path] = reportedLength ?? contents.LongLength;
        lastWriteTimes[path] = lastWriteTime;
        return this;
    }

    public UnitTestInMemoryContentFileSystem AddDirectory(string path)
    {
        entries[path] = ContentEntryKind.Directory;
        return this;
    }

    /// <summary>
    /// Makes the member named <paramref name="member"/> (<c>nameof</c> one of
    /// <see cref="GetFileLength"/>, <see cref="GetLastWriteTimeUtc"/> or
    /// <see cref="OpenFileForAsyncRead"/>) throw <paramref name="failure"/> for
    /// <paramref name="path"/>, as an unreadable or vanished file on a real disk would.
    /// </summary>
    public UnitTestInMemoryContentFileSystem FailOn(string path, string member, Exception failure)
    {
        failures[(path, member)] = failure;
        return this;
    }

    public ContentEntryKind GetEntryKind(string path) => entries.GetValueOrDefault(path);

    public string ResolveFinalPath(string path) => path;

    public long GetFileLength(string path)
    {
        ThrowIfFailing(path, nameof(GetFileLength));
        return reportedLengths[path];
    }

    public DateTimeOffset GetLastWriteTimeUtc(string path)
    {
        ThrowIfFailing(path, nameof(GetLastWriteTimeUtc));
        return lastWriteTimes[path];
    }

    public Stream OpenFileForAsyncRead(string path)
    {
        ThrowIfFailing(path, nameof(OpenFileForAsyncRead));
        return new MemoryStream(fileContents[path], writable: false);
    }

    private void ThrowIfFailing(string path, string member)
    {
        if (failures.TryGetValue((path, member), out var failure))
        {
            throw failure;
        }
    }

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
