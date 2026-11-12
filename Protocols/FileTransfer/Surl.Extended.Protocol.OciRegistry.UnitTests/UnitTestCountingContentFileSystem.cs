using System.Collections.Concurrent;
using Surl.Content;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// An <see cref="IContentFileSystem"/> that passes every call to another one and counts how often
/// each file is opened for reading, so a test sees how many times a blob file was read.
/// </summary>
internal sealed class UnitTestCountingContentFileSystem(IContentFileSystem inner) : IContentFileSystem
{
    private readonly ConcurrentDictionary<string, int> reads = new(StringComparer.Ordinal);

    /// <summary>
    /// How many times the file at <paramref name="path"/> was opened for reading.
    /// </summary>
    public int ReadsOf(string path) => reads.GetValueOrDefault(path);

    public ContentEntryKind GetEntryKind(string path) => inner.GetEntryKind(path);

    public string ResolveFinalPath(string path) => inner.ResolveFinalPath(path);

    public long GetFileLength(string path) => inner.GetFileLength(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => inner.GetLastWriteTimeUtc(path);

    public Stream OpenFileForAsyncRead(string path)
    {
        reads.AddOrUpdate(path, 1, (_, count) => count + 1);

        return inner.OpenFileForAsyncRead(path);
    }

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => inner.EnumerateDirectoryEntryNames(path);

    public Stream CreateFileForAsyncWrite(string path) => inner.CreateFileForAsyncWrite(path);

    public Stream OpenFileForAsyncReadWrite(string path) => inner.OpenFileForAsyncReadWrite(path);

    public void DeleteFile(string path) => inner.DeleteFile(path);

    public void MoveFileReplacing(string source, string destination) => inner.MoveFileReplacing(source, destination);

    public void MoveFileWithoutReplacing(string source, string destination) => inner.MoveFileWithoutReplacing(source, destination);

    public void SetLastWriteTimeUtc(string path, DateTimeOffset lastWriteTimeUtc) => inner.SetLastWriteTimeUtc(path, lastWriteTimeUtc);

    public void MoveDirectory(string source, string destination) => inner.MoveDirectory(source, destination);

    public void CreateDirectory(string path) => inner.CreateDirectory(path);

    public void RemoveEmptyDirectory(string path) => inner.RemoveEmptyDirectory(path);
}
