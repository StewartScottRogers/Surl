using Surl.Content;

namespace Surl.Protocol.Tftp;

/// <summary>
/// An <see cref="IContentFileSystem"/> over another one in which every entry vanishes after
/// its first look, so a test can make a file or directory disappear between the content
/// store mapping a file name and reading what is there.
/// </summary>
internal sealed class UnitTestVanishingContentFileSystem(IContentFileSystem inner) : IContentFileSystem
{
    private readonly HashSet<string> looked = new(StringComparer.Ordinal);

    public ContentEntryKind GetEntryKind(string path) =>
        looked.Add(path) ? inner.GetEntryKind(path) : ContentEntryKind.None;

    public string ResolveFinalPath(string path) => inner.ResolveFinalPath(path);

    public long GetFileLength(string path) => inner.GetFileLength(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => inner.GetLastWriteTimeUtc(path);

    public Stream OpenFileForAsyncRead(string path) => inner.OpenFileForAsyncRead(path);

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => inner.EnumerateDirectoryEntryNames(path);
}
