using Surl.Content;

namespace Surl.MessageBus;

/// <summary>
/// An in-memory file system that throws a set exception from the reads, writes, appends,
/// renames, deletes or listings of any path holding a set fragment, as a disk that fails there
/// does; everything else goes through to <see cref="Files"/>.
/// </summary>
internal sealed class UnitTestFaultingContentFileSystem : IContentFileSystem
{
    public InMemoryContentFileSystem Files { get; } = new(new SettableTimeProvider());

    /// <summary>A path holding this fragment cannot be opened for reading; <see langword="null"/> for none.</summary>
    public string? FailReadsOf { get; set; }

    /// <summary>A folder holding this fragment cannot be listed; <see langword="null"/> for none.</summary>
    public string? FailListingsOf { get; set; }

    /// <summary>A path holding this fragment cannot be created for writing; <see langword="null"/> for none.</summary>
    public string? FailCreatesOf { get; set; }

    /// <summary>A path holding this fragment cannot be opened to append to; <see langword="null"/> for none.</summary>
    public string? FailAppendsTo { get; set; }

    /// <summary>A path holding this fragment cannot be renamed into place; <see langword="null"/> for none.</summary>
    public string? FailMovesTo { get; set; }

    /// <summary>A path holding this fragment cannot be deleted; <see langword="null"/> for none.</summary>
    public string? FailDeletesOf { get; set; }

    public Exception Failure { get; set; } = new IOException("The disk failed.");

    public ContentEntryKind GetEntryKind(string path) => Files.GetEntryKind(path);

    public string ResolveFinalPath(string path) => Files.ResolveFinalPath(path);

    public long GetFileLength(string path) => Files.GetFileLength(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => Files.GetLastWriteTimeUtc(path);

    public Stream OpenFileForAsyncRead(string path) =>
        Fails(FailReadsOf, path) ? throw Failure : Files.OpenFileForAsyncRead(path);

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) =>
        Fails(FailListingsOf, path) ? throw Failure : Files.EnumerateDirectoryEntryNames(path);

    public Stream CreateFileForAsyncWrite(string path) =>
        Fails(FailCreatesOf, path) ? throw Failure : Files.CreateFileForAsyncWrite(path);

    public Stream OpenFileForAsyncReadWrite(string path) =>
        Fails(FailAppendsTo, path) ? throw Failure : Files.OpenFileForAsyncReadWrite(path);

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
