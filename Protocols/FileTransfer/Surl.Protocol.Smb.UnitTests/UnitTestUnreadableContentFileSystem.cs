using Surl.Content;

namespace Surl.Protocol.Smb;

/// <summary>
/// The standard in-memory content file system, except that opening a file to read it throws an
/// <see cref="IOException"/>, as a disk fault does.
/// </summary>
internal sealed class UnitTestUnreadableContentFileSystem : IContentFileSystem
{
    private readonly InMemoryContentFileSystem inner = SmbTestExchange.StandardFileSystem();

    public ContentStore ContentStore() => new(InMemoryContentFileSystem.RootPath, this, new ContentExposureOptions());

    public ContentEntryKind GetEntryKind(string path) => inner.GetEntryKind(path);

    public string ResolveFinalPath(string path) => inner.ResolveFinalPath(path);

    public long GetFileLength(string path) => inner.GetFileLength(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => inner.GetLastWriteTimeUtc(path);

    public Stream OpenFileForAsyncRead(string path) => throw new IOException("The disk is unreadable.");

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => inner.EnumerateDirectoryEntryNames(path);
}
