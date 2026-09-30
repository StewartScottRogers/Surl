using Surl.Content;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The SFTP tests' standard files (<c>/a.txt</c>, <c>/dir/b.txt</c>, <c>/.hidden.txt</c>) in
/// memory, except that opening a file throws <paramref name="failure"/>, and so does reading a
/// file's length when it is an <see cref="UnauthorizedAccessException"/>, as a disk fault or a file
/// without read permission does.
/// </summary>
internal sealed class UnitTestThrowingContentFileSystem(TimeProvider clock, Exception failure) : IContentFileSystem
{
    private readonly InMemoryContentFileSystem inner = StandardFiles(clock);

    public ContentEntryKind GetEntryKind(string path) => inner.GetEntryKind(path);

    public string ResolveFinalPath(string path) => inner.ResolveFinalPath(path);

    public long GetFileLength(string path) => failure is UnauthorizedAccessException ? throw failure : inner.GetFileLength(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => inner.GetLastWriteTimeUtc(path);

    public Stream OpenFileForAsyncRead(string path) => throw failure;

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => inner.EnumerateDirectoryEntryNames(path);

    private static InMemoryContentFileSystem StandardFiles(TimeProvider clock)
    {
        var fileSystem = new InMemoryContentFileSystem(clock);
        SftpTestPackets.WriteFile(fileSystem, "hello world\n", "a.txt");
        SftpTestPackets.WriteFile(fileSystem, "bee\n", "dir", "b.txt");
        SftpTestPackets.WriteFile(fileSystem, "hidden\n", ".hidden.txt");

        return fileSystem;
    }
}
