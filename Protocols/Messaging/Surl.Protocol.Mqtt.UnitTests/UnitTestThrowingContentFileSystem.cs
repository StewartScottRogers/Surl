using Surl.Content;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// A file system that reports <paramref name="entryKind"/> at every path and throws
/// <paramref name="failure"/> from every other member, as a disk that cannot be read or
/// written does.
/// </summary>
/// <param name="entryKind">What <see cref="GetEntryKind"/> reports.</param>
/// <param name="failure">What every other member throws.</param>
internal sealed class UnitTestThrowingContentFileSystem(ContentEntryKind entryKind, Exception failure) : IContentFileSystem
{
    public ContentEntryKind GetEntryKind(string path) => entryKind;

    public string ResolveFinalPath(string path) => throw failure;

    public long GetFileLength(string path) => throw failure;

    public DateTimeOffset GetLastWriteTimeUtc(string path) => throw failure;

    public Stream OpenFileForAsyncRead(string path) => throw failure;

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => throw failure;

    public void CreateDirectory(string path) => throw failure;
}
