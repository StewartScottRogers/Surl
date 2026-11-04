using Surl.Content;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The SFTP tests' standard files in memory, except that every file is empty by the time it is
/// opened, as a file truncated between its length being read and its bytes being copied is.
/// </summary>
internal sealed class UnitTestShrinkingContentFileSystem(TimeProvider clock) : IContentFileSystem
{
    private readonly InMemoryContentFileSystem inner = StandardFiles(clock);

    public ContentEntryKind GetEntryKind(string path) => inner.GetEntryKind(path);

    public string ResolveFinalPath(string path) => inner.ResolveFinalPath(path);

    public long GetFileLength(string path) => inner.GetFileLength(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => inner.GetLastWriteTimeUtc(path);

    public Stream OpenFileForAsyncRead(string path) => new MemoryStream();

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => inner.EnumerateDirectoryEntryNames(path);

    private static InMemoryContentFileSystem StandardFiles(TimeProvider clock)
    {
        var fileSystem = new InMemoryContentFileSystem(clock);
        SftpTestPackets.WriteFile(fileSystem, "hello world\n", "a.txt");

        return fileSystem;
    }
}
