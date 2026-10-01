using Surl.Content;

namespace Surl.Protocol.Pop3;

/// <summary>
/// A file system holding nothing that accepts writes and forgets them until
/// <see cref="FailsWrites"/> is set, and from then on throws the exception it was given for every
/// write, as a disk that fails does.
/// </summary>
internal sealed class UnitTestWriteFailingContentFileSystem(Exception writeFailure) : IContentFileSystem
{
    /// <summary>
    /// Whether every write throws the exception the file system was given.
    /// </summary>
    public bool FailsWrites { get; set; }

    public ContentEntryKind GetEntryKind(string path) => ContentEntryKind.None;

    public string ResolveFinalPath(string path) => path;

    public long GetFileLength(string path) => throw new FileNotFoundException(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => throw new FileNotFoundException(path);

    public Stream OpenFileForAsyncRead(string path) => throw new FileNotFoundException(path);

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => [];

    public Stream CreateFileForAsyncWrite(string path) => FailsWrites ? throw writeFailure : new MemoryStream();

    public void CreateDirectory(string path) => ThrowIfFailingWrites();

    public void MoveFileReplacing(string source, string destination) => ThrowIfFailingWrites();

    public void DeleteFile(string path) => ThrowIfFailingWrites();

    private void ThrowIfFailingWrites()
    {
        if (FailsWrites)
        {
            throw writeFailure;
        }
    }
}
