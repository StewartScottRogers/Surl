using Surl.Content;

namespace Surl.Protocol.Dict;

/// <summary>
/// Lists <c>goneFile</c> as a file, then reports it as holding nothing once the listing has
/// been read, as when it is deleted between the listing and the lookup.
/// </summary>
internal sealed class UnitTestFileGoneOnceListedContentFileSystem(IContentFileSystem inner, string goneFile) : IContentFileSystem
{
    private bool listed;

    public ContentEntryKind GetEntryKind(string path) =>
        listed && path == goneFile ? ContentEntryKind.None : inner.GetEntryKind(path);

    public string ResolveFinalPath(string path) => inner.ResolveFinalPath(path);

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
