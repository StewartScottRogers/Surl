using Surl.Content;

namespace Surl.Protocol.Http;

/// <summary>
/// A hand-written <see cref="IContentFileSystem"/> whose root and every file in it exist, but
/// whose file length cannot be read: <see cref="GetFileLength"/> throws the given exception,
/// as an unreadable file on a real disk would.
/// </summary>
internal sealed class ThrowingContentFileSystem(string root, Exception failure) : IContentFileSystem
{
    public ContentEntryKind GetEntryKind(string path) => path == root ? ContentEntryKind.Directory : ContentEntryKind.File;

    public string ResolveFinalPath(string path) => path;

    public long GetFileLength(string path) => throw failure;

    public DateTimeOffset GetLastWriteTimeUtc(string path) => throw failure;

    public Stream OpenFileForAsyncRead(string path) => throw failure;

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => throw failure;
}
