using Surl.Content;

namespace Surl.MailStore;

/// <summary>
/// An in-memory file system that throws a set exception from the reads, writes, renames or
/// deletes of any path holding a set fragment, as a disk that fails there does; everything else
/// goes through to <see cref="Files"/>.
/// </summary>
internal sealed class UnitTestFaultingContentFileSystem : IContentFileSystem
{
    public InMemoryContentFileSystem Files { get; } = new(new SettableTimeProvider());

    /// <summary>
    /// A path holding this fragment cannot be opened for reading, nor its length read;
    /// <see langword="null"/> for none.
    /// </summary>
    public string? FailReadsOf { get; set; }

    /// <summary>
    /// A path holding this fragment cannot be created for writing; <see langword="null"/> for none.
    /// </summary>
    public string? FailCreatesOf { get; set; }

    /// <summary>
    /// A path holding this fragment is created, but every write to it, and closing it, throws;
    /// <see langword="null"/> for none.
    /// </summary>
    public string? FailWritesOf { get; set; }

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

    public long GetFileLength(string path) =>
        Fails(FailReadsOf, path) ? throw Failure : Files.GetFileLength(path);

    public DateTimeOffset GetLastWriteTimeUtc(string path) => Files.GetLastWriteTimeUtc(path);

    public Stream OpenFileForAsyncRead(string path) =>
        Fails(FailReadsOf, path) ? throw Failure : Files.OpenFileForAsyncRead(path);

    public IEnumerable<string> EnumerateDirectoryEntryNames(string path) => Files.EnumerateDirectoryEntryNames(path);

    public Stream CreateFileForAsyncWrite(string path)
    {
        if (Fails(FailCreatesOf, path))
        {
            throw Failure;
        }

        var file = Files.CreateFileForAsyncWrite(path);
        return Fails(FailWritesOf, path) ? new FailingWriteStream(file, Failure) : file;
    }

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

    /// <summary>
    /// A file whose every write, and whose closing, throws <paramref name="failure"/>; the file
    /// itself is closed first, empty.
    /// </summary>
    private sealed class FailingWriteStream(Stream file, Exception failure) : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw failure;

        public override void Write(ReadOnlySpan<byte> buffer) => throw failure;

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException(failure);

        protected override void Dispose(bool disposing)
        {
            file.Dispose();
            base.Dispose(disposing);
            throw failure;
        }
    }
}
