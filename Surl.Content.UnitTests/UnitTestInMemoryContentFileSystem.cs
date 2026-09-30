namespace Surl.Content;

/// <summary>
/// A hand-written in-memory <see cref="IContentFileSystem"/>: files, directories and symbolic
/// links keyed by full path, and a record of every question the content store asked.
/// </summary>
internal sealed class UnitTestInMemoryContentFileSystem : IContentFileSystem
{
    private const int MaximumLinksFollowed = 40;

    private readonly Dictionary<string, ContentEntryKind> entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> symbolicLinks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> fileContents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> lastWriteTimes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MemoryStream> writtenFiles = new(StringComparer.Ordinal);

    public Stream CreateFileForAsyncWrite(string path)
    {
        Calls.Add($"{nameof(CreateFileForAsyncWrite)}({path})");
        var written = new MemoryStream();
        entries[path] = ContentEntryKind.File;
        fileContents.Remove(path);
        writtenFiles[path] = written;
        return written;
    }

    public Stream OpenFileForAsyncReadWrite(string path)
    {
        Calls.Add($"{nameof(OpenFileForAsyncReadWrite)}({path})");
        var opened = new MemoryStream();
        if (fileContents.Remove(path, out byte[]? existing))
        {
            opened.Write(existing);
            opened.Position = 0;
        }

        entries[path] = ContentEntryKind.File;
        writtenFiles[path] = opened;
        return opened;
    }

    public void DeleteFile(string path)
    {
        Calls.Add($"{nameof(DeleteFile)}({path})");
        entries.Remove(path);
        fileContents.Remove(path);
        writtenFiles.Remove(path);
    }

    public void MoveFileReplacing(string source, string destination)
    {
        Calls.Add($"{nameof(MoveFileReplacing)}({source}, {destination})");
        if (FailMoves)
        {
            throw new IOException("The rename failed.");
        }

        MoveKey(entries, source, destination);
        fileContents.Remove(destination);
        writtenFiles.Remove(destination);
        MoveKey(fileContents, source, destination);
        MoveKey(writtenFiles, source, destination);
        MoveKey(lastWriteTimes, source, destination);
    }

    public void MoveFileWithoutReplacing(string source, string destination)
    {
        Calls.Add($"{nameof(MoveFileWithoutReplacing)}({source}, {destination})");
        if (entries.ContainsKey(destination))
        {
            throw new IOException("An entry is already at the destination.");
        }

        MoveKey(entries, source, destination);
        MoveKey(fileContents, source, destination);
        MoveKey(writtenFiles, source, destination);
        MoveKey(lastWriteTimes, source, destination);
    }

    public void SetLastWriteTimeUtc(string path, DateTimeOffset lastWriteTimeUtc)
    {
        Calls.Add($"{nameof(SetLastWriteTimeUtc)}({path}, {lastWriteTimeUtc:O})");
        lastWriteTimes[path] = lastWriteTimeUtc;
    }

    public void MoveDirectory(string source, string destination)
    {
        Calls.Add($"{nameof(MoveDirectory)}({source}, {destination})");
        string prefix = source + Path.DirectorySeparatorChar;
        foreach (string key in entries.Keys.Where(key => key == source || key.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            string moved = destination + key[source.Length..];
            MoveKey(entries, key, moved);
            MoveKey(fileContents, key, moved);
            MoveKey(writtenFiles, key, moved);
            MoveKey(lastWriteTimes, key, moved);
        }
    }

    public void CreateDirectory(string path)
    {
        Calls.Add($"{nameof(CreateDirectory)}({path})");
        entries[path] = ContentEntryKind.Directory;
    }

    public void RemoveEmptyDirectory(string path)
    {
        Calls.Add($"{nameof(RemoveEmptyDirectory)}({path})");
        entries.Remove(path);
    }

    private static void MoveKey<TValue>(Dictionary<string, TValue> dictionary, string source, string destination)
    {
        if (dictionary.Remove(source, out TValue? value))
        {
            dictionary[destination] = value;
        }
    }

    /// <summary>
    /// When set, <see cref="MoveFileReplacing(string, string)"/> throws <see cref="IOException"/>.
    /// </summary>
    public bool FailMoves { get; set; }

    /// <summary>
    /// When set, <see cref="OpenFileForAsyncRead(string)"/> throws <see cref="IOException"/>.
    /// </summary>
    public bool FailReads { get; set; }

    public byte[] ReadWrittenFile(string path) => writtenFiles[path].ToArray();

    /// <summary>
    /// The bytes of the file at <paramref name="path"/>, whether added or written.
    /// </summary>
    public byte[] ReadFile(string path) =>
        writtenFiles.TryGetValue(path, out MemoryStream? written) ? written.ToArray() : fileContents[path];

    /// <summary>
    /// The full paths of every file, directory and symbolic link directly inside
    /// <paramref name="directory"/>, in ordinal order, without recording a call.
    /// </summary>
    public List<string> EntriesDirectlyInside(string directory)
    {
        string prefix = directory + Path.DirectorySeparatorChar;
        return entries.Keys.Concat(symbolicLinks.Keys)
            .Where(entry => entry.StartsWith(prefix, StringComparison.Ordinal)
                && entry.IndexOf(Path.DirectorySeparatorChar, prefix.Length) < 0)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    public List<string> Calls { get; } = [];

    public UnitTestInMemoryContentFileSystem AddFile(string path) => AddFile(path, [], DateTimeOffset.UnixEpoch);

    public UnitTestInMemoryContentFileSystem AddFile(string path, byte[] contents, DateTimeOffset lastWriteTime)
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
        if (FailReads)
        {
            throw new IOException("The read failed.");
        }

        return fileContents.TryGetValue(path, out byte[]? contents)
            ? new MemoryStream(contents, writable: false)
            : throw new FileNotFoundException("No file in the fake.", path);
    }

    public UnitTestInMemoryContentFileSystem AddDirectory(string path) => AddDirectory(path, DateTimeOffset.UnixEpoch);

    public UnitTestInMemoryContentFileSystem AddDirectory(string path, DateTimeOffset lastWriteTime)
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

    public UnitTestInMemoryContentFileSystem AddSymbolicLink(string path, string target)
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
