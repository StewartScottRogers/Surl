using Surl.Content;

namespace Surl.Protocol.Imap;

/// <summary>
/// A file system with no files and no room: every file it is asked to create throws
/// <see cref="IOException"/> "no room", as a full disk does, so a message's pending file cannot
/// be created.
/// </summary>
internal sealed class UnitTestNoRoomContentFileSystem : IContentFileSystem
{
    public ContentEntryKind GetEntryKind(string path) => ContentEntryKind.None;

    public string ResolveFinalPath(string path) => path;

    public long GetFileLength(string path) => throw new FileNotFoundException(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => throw new FileNotFoundException(path);

    public Stream OpenFileForAsyncRead(string path) => throw new FileNotFoundException(path);

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => [];

    public Stream CreateFileForAsyncWrite(string path) => throw new IOException("no room");

    public void MoveFileReplacing(string source, string destination) => throw new FileNotFoundException(source);

    public void DeleteFile(string path)
    {
    }

    public void CreateDirectory(string path)
    {
    }
}
