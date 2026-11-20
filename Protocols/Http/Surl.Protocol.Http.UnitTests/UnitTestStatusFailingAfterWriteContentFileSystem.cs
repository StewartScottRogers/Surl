using Surl.Content;

namespace Surl.Protocol.Http;

// Reads the file system it wraps, except that once a file has been moved into place its
// modification time can no longer be read.
internal sealed class UnitTestStatusFailingAfterWriteContentFileSystem(IContentFileSystem inner) : IContentFileSystem
{
    private bool moved;

    public ContentEntryKind GetEntryKind(string path) => inner.GetEntryKind(path);

    public string ResolveFinalPath(string path) => inner.ResolveFinalPath(path);

    public long GetFileLength(string path) => inner.GetFileLength(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) =>
        moved ? throw new IOException("The disk failed.") : inner.GetLastWriteTimeUtc(path);

    public Stream OpenFileForAsyncRead(string path) => inner.OpenFileForAsyncRead(path);

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => inner.EnumerateDirectoryEntryNames(path);

    public Stream CreateFileForAsyncWrite(string path) => inner.CreateFileForAsyncWrite(path);

    public Stream OpenFileForAsyncReadWrite(string path) => inner.OpenFileForAsyncReadWrite(path);

    public void DeleteFile(string path) => inner.DeleteFile(path);

    public void MoveFileReplacing(string source, string destination)
    {
        inner.MoveFileReplacing(source, destination);
        moved = true;
    }

    public void MoveFileWithoutReplacing(string source, string destination)
    {
        inner.MoveFileWithoutReplacing(source, destination);
        moved = true;
    }

    public void SetLastWriteTimeUtc(string path, DateTimeOffset lastWriteTimeUtc) => inner.SetLastWriteTimeUtc(path, lastWriteTimeUtc);

    public void MoveDirectory(string source, string destination) => inner.MoveDirectory(source, destination);

    public void CreateDirectory(string path) => inner.CreateDirectory(path);

    public void RemoveEmptyDirectory(string path) => inner.RemoveEmptyDirectory(path);
}
