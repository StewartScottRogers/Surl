using Surl.Content;

namespace Surl.MailStore;

/// <summary>
/// An in-memory file system that throws a set exception from the reads, writes or deletes of
/// any path holding a set fragment, as a disk that fails there does; everything else goes
/// through to <see cref="Files"/>.
/// </summary>
internal sealed class UnitTestFaultingContentFileSystem : IContentFileSystem
{
    public InMemoryContentFileSystem Files { get; } = new(new SettableTimeProvider());

    /// <summary>
    /// A path holding this fragment cannot be opened for reading; <see langword="null"/> for none.
    /// </summary>
    public string? FailReadsOf { get; set; }

    /// <summary>
    /// A path holding this fragment cannot be renamed into place; <see langword="null"/> for none.
    /// </summary>
    public string? FailMovesTo { get; set; }

    /// <summary>
    /// A path holding this fragment cannot be deleted; <see langword="null"/> for none.
    /// </summary>
    public string? FailDeletesOf { get; set; }

    /// <summary>
    /// Runs with the destination before every rename, as another session acting mid-write does;
    /// <see langword="null"/> for nothing.
    /// </summary>
    public Action<string>? BeforeMoveTo { get; set; }

    public Exception Failure { get; set; } = new IOException("The disk failed.");

    public ContentEntryKind GetEntryKind(string path) => Files.GetEntryKind(path);

    public string ResolveFinalPath(string path) => Files.ResolveFinalPath(path);

    public long GetFileLength(string path) => Files.GetFileLength(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => Files.GetLastWriteTimeUtc(path);

    public Stream OpenFileForAsyncRead(string path) =>
        Fails(FailReadsOf, path) ? throw Failure : Files.OpenFileForAsyncRead(path);

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => Files.EnumerateDirectoryEntryNames(path);

    public Stream CreateFileForAsyncWrite(string path) => Files.CreateFileForAsyncWrite(path);

    public void DeleteFile(string path)
    {
        if (Fails(FailDeletesOf, path))
        {
            throw Failure;
        }

        Files.DeleteFile(path);
    }

    public void MoveFileReplacing(string source, string destination)
    {
        BeforeMoveTo?.Invoke(destination);
        if (Fails(FailMovesTo, destination))
        {
            throw Failure;
        }

        Files.MoveFileReplacing(source, destination);
    }

    public void CreateDirectory(string path) => Files.CreateDirectory(path);

    private static bool Fails(string? fragment, string path) =>
        fragment is not null && path.Contains(fragment, StringComparison.Ordinal);
}
