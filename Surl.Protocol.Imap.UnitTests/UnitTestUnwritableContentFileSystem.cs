using Surl.Content;
using Surl.MailStore;

namespace Surl.Protocol.Imap;

/// <summary>
/// A file system that keeps files in the mail store's messages folder in memory, as a disk with
/// room for them does, and throws the exception it was given on every other write - the mail
/// store's index among them - as a disk that fills once the messages are in does.
/// </summary>
internal sealed class UnitTestUnwritableContentFileSystem(Exception writeFailure) : IContentFileSystem
{
    private readonly Dictionary<string, MemoryStream> files = new(StringComparer.Ordinal);

    public ContentEntryKind GetEntryKind(string path) => files.ContainsKey(path) ? ContentEntryKind.File : ContentEntryKind.None;

    public string ResolveFinalPath(string path) => path;

    public long GetFileLength(string path) => files[path].ToArray().LongLength;

    public DateTimeOffset GetLastWriteTimeUtc(string path) => throw new FileNotFoundException(path);

    public Stream OpenFileForAsyncRead(string path) => new MemoryStream(files[path].ToArray(), writable: false);

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => [];

    public Stream CreateFileForAsyncWrite(string path)
    {
        if (Path.GetFileName(Path.GetDirectoryName(path)) != MailStoreFiles.MessagesFolderName)
        {
            throw writeFailure;
        }

        var file = new MemoryStream();
        files[path] = file;
        return file;
    }

    public void MoveFileReplacing(string source, string destination)
    {
        files[destination] = files[source];
        files.Remove(source);
    }

    public void DeleteFile(string path) => files.Remove(path);

    public void CreateDirectory(string path)
    {
    }
}
