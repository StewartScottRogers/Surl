using Surl.Content;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// An <see cref="IContentFileSystem"/> that passes every call to an in-memory one, and throws
/// <see cref="IOException"/> when a test asks a read, a write or a delete to fail.
/// </summary>
internal sealed class UnitTestFailingContentFileSystem : IContentFileSystem
{
    private readonly InMemoryContentFileSystem inner = new(new ManualTimeProvider());

    public bool FailRead { get; set; }

    public bool FailWrite { get; set; }

    public bool FailDelete { get; set; }

    public List<string> DeletedFiles { get; } = [];

    public ContentEntryKind GetEntryKind(string path) => inner.GetEntryKind(path);

    public string ResolveFinalPath(string path) => inner.ResolveFinalPath(path);

    public long GetFileLength(string path) => inner.GetFileLength(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => inner.GetLastWriteTimeUtc(path);

    public Stream OpenFileForAsyncRead(string path) => FailRead ? throw new IOException("The read failed.") : inner.OpenFileForAsyncRead(path);

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => inner.EnumerateDirectoryEntryNames(path);

    public Stream CreateFileForAsyncWrite(string path) => FailWrite ? throw new IOException("The write failed.") : inner.CreateFileForAsyncWrite(path);

    public void CreateDirectory(string path) => inner.CreateDirectory(path);

    public void MoveFileReplacing(string source, string destination) => inner.MoveFileReplacing(source, destination);

    public void DeleteFile(string path)
    {
        DeletedFiles.Add(path);
        if (FailDelete)
        {
            throw new IOException("The delete failed.");
        }

        inner.DeleteFile(path);
    }
}
