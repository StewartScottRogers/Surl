namespace Surl.Content;

/// <summary>
/// A hand-written in-memory <see cref="IContentFileSystem"/>: files, directories and symbolic
/// links keyed by full path, and a record of every question the content store asked.
/// </summary>
internal sealed class InMemoryContentFileSystem : IContentFileSystem
{
    private const int MaximumLinksFollowed = 40;

    private readonly Dictionary<string, ContentEntryKind> entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> symbolicLinks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> fileContents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> lastWriteTimes = new(StringComparer.Ordinal);

    public List<string> Calls { get; } = [];

    public InMemoryContentFileSystem AddFile(string path) => AddFile(path, [], DateTimeOffset.UnixEpoch);

    public InMemoryContentFileSystem AddFile(string path, byte[] contents, DateTimeOffset lastWriteTime)
    {
        entries[path] = ContentEntryKind.File;
        fileContents[path] = contents;
        lastWriteTimes[path] = lastWriteTime;
        return this;
    }

    public long GetFileLength(string path)
    {
        Calls.Add($"{nameof(GetFileLength)}({path})");
        return fileContents[path].LongLength;
    }

    public DateTimeOffset GetLastWriteTimeUtc(string path)
    {
        Calls.Add($"{nameof(GetLastWriteTimeUtc)}({path})");
        return lastWriteTimes[path];
    }

    public Stream OpenFileForAsyncRead(string path)
    {
        Calls.Add($"{nameof(OpenFileForAsyncRead)}({path})");
        return new MemoryStream(fileContents[path], writable: false);
    }

    public InMemoryContentFileSystem AddDirectory(string path) => AddDirectory(path, DateTimeOffset.UnixEpoch);

    public InMemoryContentFileSystem AddDirectory(string path, DateTimeOffset lastWriteTime)
    {
        entries[path] = ContentEntryKind.Directory;
        lastWriteTimes[path] = lastWriteTime;
        return this;
    }

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path)
    {
        Calls.Add($"{nameof(EnumerateDirectoryEntryNames)}({path})");
        string prefix = path + Path.DirectorySeparatorChar;
        List<string> names = entries.Keys.Concat(symbolicLinks.Keys)
            .Where(entry => entry.StartsWith(prefix, StringComparison.Ordinal)
                && entry.IndexOf(Path.DirectorySeparatorChar, prefix.Length) < 0)
            .Select(entry => entry[prefix.Length..])
            .ToList();
        return YieldNames(names);
    }

    public Action? AfterEachEnumeratedName { get; set; }

    private IEnumerable<string> YieldNames(List<string> names)
    {
        foreach (string name in names)
        {
            yield return name;
            AfterEachEnumeratedName?.Invoke();
        }
    }

    public InMemoryContentFileSystem AddSymbolicLink(string path, string target)
    {
        symbolicLinks[path] = target;
        return this;
    }

    public ContentEntryKind GetEntryKind(string path)
    {
        Calls.Add($"{nameof(GetEntryKind)}({path})");
        return entries.GetValueOrDefault(path);
    }

    public string ResolveFinalPath(string path)
    {
        Calls.Add($"{nameof(ResolveFinalPath)}({path})");
        for (int followed = 0; followed < MaximumLinksFollowed; followed++)
        {
            string? link = symbolicLinks.Keys.FirstOrDefault(
                key => path == key || path.StartsWith(key + Path.DirectorySeparatorChar, StringComparison.Ordinal));
            if (link is null)
            {
                return path;
            }

            path = symbolicLinks[link] + path[link.Length..];
        }

        throw new IOException("Too many levels of symbolic links.");
    }
}
