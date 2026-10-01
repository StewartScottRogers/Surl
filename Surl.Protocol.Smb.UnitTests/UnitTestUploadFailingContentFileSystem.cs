using Surl.Content;

namespace Surl.Protocol.Smb;

/// <summary>
/// The standard file system, whose uploads go wrong at the close: with
/// <see cref="DirectoryAppearingWhenAnUploadOpens"/> set, a directory appears there as soon as an
/// upload's temporary file is opened, so the commit finds the target can no longer take a file;
/// with <see cref="RenameFails"/>, renaming the temporary file over the target throws as a
/// failing disk would.
/// </summary>
internal sealed class UnitTestUploadFailingContentFileSystem : IContentFileSystem
{
    public InMemoryContentFileSystem Inner { get; } = SmbTestExchange.StandardFileSystem();

    public string? DirectoryAppearingWhenAnUploadOpens { get; init; }

    public bool RenameFails { get; init; }

    public ContentStore ContentStore() =>
        new(InMemoryContentFileSystem.RootPath, this, new ContentExposureOptions { AllowUploads = true });

    public ContentEntryKind GetEntryKind(string path) => Inner.GetEntryKind(path);

    public string ResolveFinalPath(string path) => Inner.ResolveFinalPath(path);

    public long GetFileLength(string path) => Inner.GetFileLength(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => Inner.GetLastWriteTimeUtc(path);

    public Stream OpenFileForAsyncRead(string path) => Inner.OpenFileForAsyncRead(path);

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => Inner.EnumerateDirectoryEntryNames(path);

    public Stream OpenFileForAsyncReadWrite(string path)
    {
        var stream = Inner.OpenFileForAsyncReadWrite(path);
        if (DirectoryAppearingWhenAnUploadOpens is { } directory)
        {
            Inner.CreateDirectory(directory);
        }

        return stream;
    }

    public void DeleteFile(string path) => Inner.DeleteFile(path);

    public void MoveFileReplacing(string source, string destination)
    {
        if (RenameFails)
        {
            throw new IOException("The disk failed.");
        }

        Inner.MoveFileReplacing(source, destination);
    }
}
