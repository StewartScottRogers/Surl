using Surl.Content;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// A read-only view of an in-memory content file system in which one folder resolves outside the
/// served root, as a symbolic link out of the root does on disk.
/// </summary>
/// <param name="inner">The file system read through.</param>
/// <param name="escapingFolder">The folder that resolves outside the root.</param>
internal sealed class UnitTestEscapingContentFileSystem(InMemoryContentFileSystem inner, string escapingFolder) : IContentFileSystem
{
    public ContentEntryKind GetEntryKind(string path) => inner.GetEntryKind(path);

    public string ResolveFinalPath(string path) =>
        inner.ResolveFinalPath(path) == inner.ResolveFinalPath(escapingFolder) ? inner.ResolveFinalPath("/elsewhere") : inner.ResolveFinalPath(path);

    public long GetFileLength(string path) => inner.GetFileLength(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => inner.GetLastWriteTimeUtc(path);

    public Stream OpenFileForAsyncRead(string path) => inner.OpenFileForAsyncRead(path);

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => inner.EnumerateDirectoryEntryNames(path);
}
