using Surl.Content;

namespace Surl.Protocol.Ftp;

/// <summary>
/// The standard in-memory content file system, except that it throws <paramref name="failure"/>
/// when a file's status is read (<paramref name="failsStatus"/>) or when a file is opened, as
/// a file without read permission or a disk fault does.
/// </summary>
internal sealed class UnitTestThrowingContentFileSystem(Exception failure, bool failsStatus) : IContentFileSystem
{
    private readonly InMemoryContentFileSystem inner = FtpTestExchange.StandardFileSystem();

    public ContentStore ContentStore() => new(InMemoryContentFileSystem.RootPath, this, new ContentExposureOptions());

    public ContentEntryKind GetEntryKind(string path) => inner.GetEntryKind(path);

    public string ResolveFinalPath(string path) => inner.ResolveFinalPath(path);

    public long GetFileLength(string path) => failsStatus ? throw failure : inner.GetFileLength(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => inner.GetLastWriteTimeUtc(path);

    public Stream OpenFileForAsyncRead(string path) => throw failure;

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => inner.EnumerateDirectoryEntryNames(path);
}
