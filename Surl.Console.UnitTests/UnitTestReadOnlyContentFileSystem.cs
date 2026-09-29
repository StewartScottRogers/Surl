using Surl.Content;

namespace Surl.Console;

/// <summary>
/// A read-only content file system over <see cref="Files"/>, with no disk: a path is a file
/// when <see cref="Files"/> holds it, and nothing otherwise. Opening a file throws
/// <see cref="OpenFailure"/> when one is set. Every path asked about is recorded.
/// </summary>
internal sealed class UnitTestReadOnlyContentFileSystem : IContentFileSystem
{
    /// <summary>The files, by full path.</summary>
    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

    /// <summary>Thrown by every open instead of opening, when set.</summary>
    public Exception? OpenFailure { get; init; }

    /// <summary>Every path asked about, in order.</summary>
    public List<string> AccessedPaths { get; } = [];

    public ContentEntryKind GetEntryKind(string path)
    {
        AccessedPaths.Add(path);
        return Files.ContainsKey(path) ? ContentEntryKind.File : ContentEntryKind.None;
    }

    public long GetFileLength(string path)
    {
        AccessedPaths.Add(path);
        return Files[path].Length;
    }

    public Stream OpenFileForAsyncRead(string path)
    {
        AccessedPaths.Add(path);
        return OpenFailure is null ? new MemoryStream(Files[path], writable: false) : throw OpenFailure;
    }

    public string ResolveFinalPath(string path) => throw new NotSupportedException();

    public DateTimeOffset GetLastWriteTimeUtc(string path) => throw new NotSupportedException();

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => throw new NotSupportedException();
}
