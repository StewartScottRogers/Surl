using Surl.Content;

namespace Surl.Composition;

/// <summary>
/// A content file system whose every call throws <see cref="OperationCanceledException"/>: what a
/// read cancelled while the service state loads looks like, with no disk.
/// </summary>
internal sealed class UnitTestCancellingContentFileSystem : IContentFileSystem
{
    public ContentEntryKind GetEntryKind(string path) => throw new OperationCanceledException();

    public string ResolveFinalPath(string path) => throw new OperationCanceledException();

    public long GetFileLength(string path) => throw new OperationCanceledException();

    public DateTimeOffset GetLastWriteTimeUtc(string path) => throw new OperationCanceledException();

    public Stream OpenFileForAsyncRead(string path) => throw new OperationCanceledException();

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => throw new OperationCanceledException();
}
