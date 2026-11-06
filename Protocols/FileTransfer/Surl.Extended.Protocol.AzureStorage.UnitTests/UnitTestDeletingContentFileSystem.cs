using Surl.Content;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// A read-only view of an in-memory content file system that deletes one file the moment one
/// folder is listed: as another protocol deleting a blob while a List Blobs walk is under way.
/// </summary>
/// <param name="inner">The file system read through.</param>
/// <param name="listedFolder">The folder whose listing triggers the deletion.</param>
/// <param name="deletedFile">The file deleted.</param>
internal sealed class UnitTestDeletingContentFileSystem(InMemoryContentFileSystem inner, string listedFolder, string deletedFile) : IContentFileSystem
{
    public ContentEntryKind GetEntryKind(string path) => inner.GetEntryKind(path);

    public string ResolveFinalPath(string path) => inner.ResolveFinalPath(path);

    public long GetFileLength(string path) => inner.GetFileLength(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => inner.GetLastWriteTimeUtc(path);

    public Stream OpenFileForAsyncRead(string path) => inner.OpenFileForAsyncRead(path);

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path)
    {
        if (path == listedFolder && inner.GetEntryKind(deletedFile) == ContentEntryKind.File)
        {
            inner.DeleteFile(deletedFile);
        }

        return inner.EnumerateDirectoryEntryNames(path);
    }
}
