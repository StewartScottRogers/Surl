using Surl.Content;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// A read-only view of an in-memory content file system that records the path of every file opened
/// for reading, so a test can show which files an answer read.
/// </summary>
/// <param name="inner">The file system read through.</param>
internal sealed class UnitTestReadRecordingContentFileSystem(InMemoryContentFileSystem inner) : IContentFileSystem
{
    /// <summary>
    /// Every path opened for reading, in order.
    /// </summary>
    public List<string> OpenedForRead { get; } = [];

    public ContentEntryKind GetEntryKind(string path) => inner.GetEntryKind(path);

    public string ResolveFinalPath(string path) => inner.ResolveFinalPath(path);

    public long GetFileLength(string path) => inner.GetFileLength(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => inner.GetLastWriteTimeUtc(path);

    public Stream OpenFileForAsyncRead(string path)
    {
        OpenedForRead.Add(path);

        return inner.OpenFileForAsyncRead(path);
    }

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => inner.EnumerateDirectoryEntryNames(path);
}
