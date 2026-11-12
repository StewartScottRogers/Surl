using Surl.Content;

namespace Surl.Composition;

/// <summary>
/// A content file system whose reads throw <see cref="OperationCanceledException"/>: what a
/// read cancelled while the service state loads looks like, with no disk. It lists an empty
/// directory and resolves a path to itself, so the store's start-up sweep passes.
/// </summary>
internal sealed class UnitTestCancellingContentFileSystem : IContentFileSystem
{
    public ContentEntryKind GetEntryKind(string path) => throw new OperationCanceledException();

    public string ResolveFinalPath(string path) => path;

    public long GetFileLength(string path) => throw new OperationCanceledException();

    public DateTimeOffset GetLastWriteTimeUtc(string path) => throw new OperationCanceledException();

    public Stream OpenFileForAsyncRead(string path) => throw new OperationCanceledException();

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => [];
}
