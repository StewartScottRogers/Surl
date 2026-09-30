using Surl.Content;

namespace Surl.Protocol.Smtp;

/// <summary>
/// A file system holding nothing, on which every write throws the exception it was given, as a
/// disk that fails does.
/// </summary>
internal sealed class UnitTestUnwritableContentFileSystem(Exception writeFailure) : IContentFileSystem
{
    public ContentEntryKind GetEntryKind(string path) => ContentEntryKind.None;

    public string ResolveFinalPath(string path) => path;

    public long GetFileLength(string path) => throw new FileNotFoundException(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => throw new FileNotFoundException(path);

    public Stream OpenFileForAsyncRead(string path) => throw new FileNotFoundException(path);

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => [];

    public Stream CreateFileForAsyncWrite(string path) => throw writeFailure;

    public void CreateDirectory(string path) => throw writeFailure;
}
