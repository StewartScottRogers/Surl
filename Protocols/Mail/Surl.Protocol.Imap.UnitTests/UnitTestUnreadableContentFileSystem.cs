using Surl.Content;

namespace Surl.Protocol.Imap;

/// <summary>
/// A file system that keeps every file in memory and, once <see cref="FailReads"/> is set,
/// throws <see cref="IOException"/> on every read - as a disk whose message files something
/// other than the store removed or locked does.
/// </summary>
internal sealed class UnitTestUnreadableContentFileSystem : IContentFileSystem
{
    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);

    public bool FailReads { get; set; }

    public ContentEntryKind GetEntryKind(string path) => files.ContainsKey(path) ? ContentEntryKind.File : ContentEntryKind.None;

    public string ResolveFinalPath(string path) => path;

    public long GetFileLength(string path) => files[path].LongLength;

    public DateTimeOffset GetLastWriteTimeUtc(string path) => throw new FileNotFoundException(path);

    public Stream OpenFileForAsyncRead(string path) =>
        FailReads ? throw new IOException("message file unreadable") : new MemoryStream(files[path], writable: false);

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => [];

    public Stream CreateFileForAsyncWrite(string path) => new StoringStream(bytes => files[path] = bytes);

    public void MoveFileReplacing(string source, string destination)
    {
        files[destination] = files[source];
        files.Remove(source);
    }

    public void DeleteFile(string path) => files.Remove(path);

    public void CreateDirectory(string path)
    {
    }

    // Keeps its bytes in the file system when it is disposed.
    private sealed class StoringStream(Action<byte[]> store) : MemoryStream
    {
        protected override void Dispose(bool disposing)
        {
            store(ToArray());
            base.Dispose(disposing);
        }
    }
}
