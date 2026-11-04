using Surl.Content;

namespace Surl.Protocol.Ldap;

/// <summary>
/// A file system with a file at every path that cannot be read: opening one throws
/// <see cref="Failure"/>, as a disk that fails or a file the server may not read does.
/// </summary>
/// <param name="failure">What opening a file throws.</param>
internal sealed class UnitTestUnreadableContentFileSystem(Exception failure) : IContentFileSystem
{
    public Exception Failure { get; } = failure;

    public ContentEntryKind GetEntryKind(string path) => ContentEntryKind.File;

    public Stream OpenFileForAsyncRead(string path) => throw Failure;

    public string ResolveFinalPath(string path) => throw new NotSupportedException();

    public long GetFileLength(string path) => throw new NotSupportedException();

    public DateTimeOffset GetLastWriteTimeUtc(string path) => throw new NotSupportedException();

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => throw new NotSupportedException();
}
