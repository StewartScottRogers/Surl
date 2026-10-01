using Surl.Content;

namespace Surl.Protocol.Ssh;

/// <summary>
/// An in-memory content file system that reads as <paramref name="inner"/> does, except that
/// opening a file to write throws <paramref name="failure"/>, as a full disk or a read-only
/// directory does.
/// </summary>
internal sealed class UnitTestWriteFailingContentFileSystem(InMemoryContentFileSystem inner, Exception failure) : IContentFileSystem
{
    public ContentEntryKind GetEntryKind(string path) => inner.GetEntryKind(path);

    public string ResolveFinalPath(string path) => inner.ResolveFinalPath(path);

    public long GetFileLength(string path) => inner.GetFileLength(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => inner.GetLastWriteTimeUtc(path);

    public Stream OpenFileForAsyncRead(string path) => inner.OpenFileForAsyncRead(path);

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => inner.EnumerateDirectoryEntryNames(path);

    public Stream OpenFileForAsyncReadWrite(string path) => throw failure;
}
