using Surl.Content;

namespace Surl.Protocol.Ftp;

/// <summary>
/// The standard in-memory content file system, except that no directory lists any entry: as a
/// directory reached by a name its parent spells another way, on a case-insensitive disk.
/// </summary>
internal sealed class UnitTestUnlistingContentFileSystem : IContentFileSystem
{
    private readonly InMemoryContentFileSystem inner = FtpTestExchange.StandardFileSystem();

    public ContentEntryKind GetEntryKind(string path) => inner.GetEntryKind(path);

    public string ResolveFinalPath(string path) => inner.ResolveFinalPath(path);

    public long GetFileLength(string path) => inner.GetFileLength(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => inner.GetLastWriteTimeUtc(path);

    public Stream OpenFileForAsyncRead(string path) => inner.OpenFileForAsyncRead(path);

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => [];
}
