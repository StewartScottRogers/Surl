using Surl.Content;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// An empty file system whose every write fails with an <see cref="IOException"/>, as a full or
/// read-only disk does: a message bus loads from it and then cannot save a change.
/// </summary>
internal sealed class UnitTestWriteFailingContentFileSystem : IContentFileSystem
{
    public ContentEntryKind GetEntryKind(string path) => ContentEntryKind.None;

    public string ResolveFinalPath(string path) => path;

    public long GetFileLength(string path) => throw new FileNotFoundException(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => throw new FileNotFoundException(path);

    public Stream OpenFileForAsyncRead(string path) => throw new FileNotFoundException(path);

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => [];

    public void CreateDirectory(string path) => throw new IOException("The disk failed.");

    public Stream CreateFileForAsyncWrite(string path) => throw new IOException("The disk failed.");

    public Stream OpenFileForAsyncReadWrite(string path) => throw new IOException("The disk failed.");
}
