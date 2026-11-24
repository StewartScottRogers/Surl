using Surl.Content;

namespace Surl.Protocol.Dict;

/// <summary>
/// Lists <c>movedFile</c> as a file inside the served root, then resolves it to
/// <c>outsideRootPath</c> once the listing has been read, as when it is replaced by a link out
/// of the root between the listing and the lookup.
/// </summary>
internal sealed class UnitTestFileMovedOutsideRootOnceListedContentFileSystem(IContentFileSystem inner, string movedFile, string outsideRootPath) : IContentFileSystem
{
    private bool listed;

    public ContentEntryKind GetEntryKind(string path) => inner.GetEntryKind(path);

    public string ResolveFinalPath(string path) =>
        listed && path == movedFile ? outsideRootPath : inner.ResolveFinalPath(path);

    public long GetFileLength(string path) => inner.GetFileLength(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => inner.GetLastWriteTimeUtc(path);

    public Stream OpenFileForAsyncRead(string path) => inner.OpenFileForAsyncRead(path);

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path)
    {
        foreach (string name in inner.EnumerateDirectoryEntryNames(path).ToList())
        {
            yield return name;
        }

        listed = true;
    }
}
