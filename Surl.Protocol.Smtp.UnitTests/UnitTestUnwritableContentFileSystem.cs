using Surl.Content;
using Surl.MailStore;

namespace Surl.Protocol.Smtp;

/// <summary>
/// A file system on which every write throws the exception it was given, as a disk that fails
/// does - or, with <paramref name="keepsMessageFiles"/>, one that keeps the files in the mail
/// store's messages folder in memory and throws on every other write, the index among them, as
/// a disk that fills once the messages are in does.
/// </summary>
internal sealed class UnitTestUnwritableContentFileSystem(Exception writeFailure, bool keepsMessageFiles = false) : IContentFileSystem
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
        if (!keepsMessageFiles || Path.GetFileName(Path.GetDirectoryName(path)) != MailStoreFiles.MessagesFolderName)
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
        if (!keepsMessageFiles)
        {
            throw writeFailure;
        }
    }
}
