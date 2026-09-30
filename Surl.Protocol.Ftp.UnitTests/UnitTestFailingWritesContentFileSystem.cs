using Surl.Content;

namespace Surl.Protocol.Ftp;

/// <summary>
/// The standard in-memory file system (<see cref="FtpTestExchange.StandardFileSystem"/>), with
/// uploads allowed, whose disk fails every file or directory it is asked to create with
/// <see cref="IOException"/>; everything else goes to the in-memory file system.
/// </summary>
internal sealed class UnitTestFailingWritesContentFileSystem : IContentFileSystem
{
    private readonly InMemoryContentFileSystem inner = FtpTestExchange.StandardFileSystem();

    public ContentStore ContentStore() =>
        new(InMemoryContentFileSystem.RootPath, this, new ContentExposureOptions { AllowUploads = true });

    public ContentEntryKind GetEntryKind(string path) => inner.GetEntryKind(path);

    public string ResolveFinalPath(string path) => inner.ResolveFinalPath(path);

    public long GetFileLength(string path) => inner.GetFileLength(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => inner.GetLastWriteTimeUtc(path);

    public Stream OpenFileForAsyncRead(string path) => inner.OpenFileForAsyncRead(path);

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => inner.EnumerateDirectoryEntryNames(path);

    public Stream CreateFileForAsyncWrite(string path) => throw new IOException("The disk is full.");

    public void DeleteFile(string path) => inner.DeleteFile(path);

    public void CreateDirectory(string path) => throw new IOException("The disk is full.");
}
