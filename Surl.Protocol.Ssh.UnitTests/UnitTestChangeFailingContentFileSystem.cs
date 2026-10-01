using Surl.Content;

namespace Surl.Protocol.Ssh;

/// <summary>
/// An in-memory content file system that reads, writes and deletes files as <paramref name="inner"/>
/// does, except that moving a file, setting a time and creating or removing a directory throw
/// <paramref name="failure"/>, as a disk fault or a read-only directory does; so does deleting a
/// file when <paramref name="failsDeletes"/> is set.
/// </summary>
internal sealed class UnitTestChangeFailingContentFileSystem(InMemoryContentFileSystem inner, Exception failure, bool failsDeletes = false) : IContentFileSystem
{
    public ContentEntryKind GetEntryKind(string path) => inner.GetEntryKind(path);

    public string ResolveFinalPath(string path) => inner.ResolveFinalPath(path);

    public long GetFileLength(string path) => inner.GetFileLength(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => inner.GetLastWriteTimeUtc(path);

    public Stream OpenFileForAsyncRead(string path) => inner.OpenFileForAsyncRead(path);

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => inner.EnumerateDirectoryEntryNames(path);

    public Stream OpenFileForAsyncReadWrite(string path) => inner.OpenFileForAsyncReadWrite(path);

    public void DeleteFile(string path)
    {
        if (failsDeletes)
        {
            throw failure;
        }

        inner.DeleteFile(path);
    }

    public void MoveFileReplacing(string source, string destination) => throw failure;

    public void MoveFileWithoutReplacing(string source, string destination) => throw failure;

    public void SetLastWriteTimeUtc(string path, DateTimeOffset lastWriteTimeUtc) => throw failure;

    public void CreateDirectory(string path) => throw failure;

    public void RemoveEmptyDirectory(string path) => throw failure;
}
