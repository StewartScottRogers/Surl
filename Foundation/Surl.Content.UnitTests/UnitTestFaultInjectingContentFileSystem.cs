namespace Surl.Content;

/// <summary>
/// An <see cref="IContentFileSystem"/> that passes every call to another one, and throws the
/// exception a test asks for when a file is opened for reading, created or deleted.
/// </summary>
internal sealed class UnitTestFaultInjectingContentFileSystem(IContentFileSystem inner) : IContentFileSystem
{
    public Exception? OpenForReadFailure { get; set; }

    public bool FailCreateForWrite { get; set; }

    public bool FailDelete { get; set; }

    public List<string> CreatedFiles { get; } = [];

    public List<string> DeletedFiles { get; } = [];

    public ContentEntryKind GetEntryKind(string path) => inner.GetEntryKind(path);

    public string ResolveFinalPath(string path) => inner.ResolveFinalPath(path);

    public long GetFileLength(string path) => inner.GetFileLength(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => inner.GetLastWriteTimeUtc(path);

    public Stream OpenFileForAsyncRead(string path) =>
        OpenForReadFailure is null ? inner.OpenFileForAsyncRead(path) : throw OpenForReadFailure;

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => inner.EnumerateDirectoryEntryNames(path);

    public Stream CreateFileForAsyncWrite(string path)
    {
        CreatedFiles.Add(path);
        return FailCreateForWrite ? throw new IOException("The write failed.") : inner.CreateFileForAsyncWrite(path);
    }

    public Stream OpenFileForAsyncReadWrite(string path) => inner.OpenFileForAsyncReadWrite(path);

    public void DeleteFile(string path)
    {
        DeletedFiles.Add(path);
        if (FailDelete)
        {
            throw new UnauthorizedAccessException("The delete was refused.");
        }

        inner.DeleteFile(path);
    }

    public void MoveFileReplacing(string source, string destination) => inner.MoveFileReplacing(source, destination);

    public void MoveFileWithoutReplacing(string source, string destination) => inner.MoveFileWithoutReplacing(source, destination);

    public void SetLastWriteTimeUtc(string path, DateTimeOffset lastWriteTimeUtc) => inner.SetLastWriteTimeUtc(path, lastWriteTimeUtc);

    public void MoveDirectory(string source, string destination) => inner.MoveDirectory(source, destination);

    public void CreateDirectory(string path) => inner.CreateDirectory(path);

    public void RemoveEmptyDirectory(string path) => inner.RemoveEmptyDirectory(path);
}
