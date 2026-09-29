using Surl.Content;

namespace Surl.Protocol.Tftp;

/// <summary>
/// A hand-written in-memory <see cref="IContentFileSystem"/> for the TFTP server's tests:
/// files and directories keyed by full path, with no symbolic links.
/// </summary>
internal sealed class InMemoryContentFileSystem : IContentFileSystem
{
    private readonly Dictionary<string, ContentEntryKind> entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> fileContents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MemoryStream> writtenFiles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> reportedLengths = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> lastWriteTimes = new(StringComparer.Ordinal);

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
    /// When set, every write to a file created for writing throws <see cref="IOException"/>,
    /// as a full disk does.
    /// </summary>
    public bool FailWrites { get; init; }

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

    public Stream OpenFileForAsyncRead(string path) => new MemoryStream(fileContents[path], writable: false);

    /// <summary>
    /// Creates an empty file whose contents are whatever is written to the returned stream,
    /// read back with <see cref="ReadFile"/>.
    /// </summary>
    public Stream CreateFileForAsyncWrite(string path)
    {
        var contents = FailWrites ? new FailingWriteStream() : new MemoryStream();
        entries[path] = ContentEntryKind.File;
        writtenFiles[path] = contents;
        return contents;
    }

    public void DeleteFile(string path)
    {
        entries.Remove(path);
        writtenFiles.Remove(path);
        fileContents.Remove(path);
    }

    /// <summary>
    /// The bytes of the file at <paramref name="path"/>, or <see langword="null"/> when there
    /// is none.
    /// </summary>
    public byte[]? ReadFile(string path) =>
        writtenFiles.TryGetValue(path, out var written) ? written.ToArray() : fileContents.GetValueOrDefault(path);

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path)
    {
        string prefix = path + Path.DirectorySeparatorChar;
        return entries.Keys
            .Where(entry => entry.StartsWith(prefix, StringComparison.Ordinal)
                && entry.IndexOf(Path.DirectorySeparatorChar, prefix.Length) < 0)
            .Select(entry => entry[prefix.Length..])
            .ToList();
    }

    private sealed class FailingWriteStream : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new IOException("There is not enough space on the disk.");
    }
}
