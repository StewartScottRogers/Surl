using Surl.Content;

namespace Surl.Composition;

/// <summary>
/// A read-only content file system over <see cref="Files"/>, with no disk: a path is a file when
/// <see cref="Files"/> holds it, and nothing otherwise; a write fails as a read-only disk's does.
/// </summary>
internal sealed class UnitTestReadOnlyContentFileSystem : IContentFileSystem
{
    /// <summary>The files, by full path.</summary>
    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

    public ContentEntryKind GetEntryKind(string path) => Files.ContainsKey(path) ? ContentEntryKind.File : ContentEntryKind.None;

    public long GetFileLength(string path) => Files[path].Length;

    public Stream OpenFileForAsyncRead(string path) => new MemoryStream(Files[path], writable: false);

    public string ResolveFinalPath(string path) => throw new NotSupportedException();

    public DateTimeOffset GetLastWriteTimeUtc(string path) => throw new NotSupportedException();

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => throw new NotSupportedException();

    /// <summary>Refuses as a read-only disk does, with the storage failure a writer copes with.</summary>
    public void CreateDirectory(string path) => throw new IOException("The disk is read-only.");
}
