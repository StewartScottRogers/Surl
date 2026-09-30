namespace Surl.Content;

/// <summary>
/// <see cref="IContentFileSystem"/> held in memory: files and directories keyed by full path,
/// so surl can serve, and take uploads, without touching the disk (ADR-0031 decision 4).
/// </summary>
/// <remarks>
/// <para>
/// An instance starts holding one empty directory, <see cref="RootPath"/>, and lasts as long
/// as it is referenced; nothing is static, so two instances share nothing. Every member is
/// safe to call from many threads at once: every protocol server and connection shares one
/// instance.
/// </para>
/// <para>
/// The bytes of every file held, files still being written included, never pass
/// <see cref="MaxTotalBytes"/>: the write that would pass it throws <see cref="IOException"/>,
/// the in-memory equivalent of a full disk, and keeps none of its bytes.
/// </para>
/// <para>
/// There are no symbolic links, so <see cref="ResolveFinalPath(string)"/> only normalises.
/// Paths are compared ordinally after <see cref="Path.GetFullPath(string)"/> and with any
/// trailing separator removed.
/// </para>
/// </remarks>
public sealed class InMemoryContentFileSystem : IContentFileSystem
{
    /// <summary>
    /// The default bound on the bytes held, 256 MiB: room for two uploads of ADR-0006's
    /// 100 MiB default at once, one of them replacing a file.
    /// </summary>
    public const long DefaultMaxTotalBytes = 268_435_456;

    /// <summary>
    /// The served root every instance starts with: <c>C:\surl</c> on Windows and
    /// <c>/surl</c> on Linux and macOS. It is a name only; nothing is created there on disk.
    /// </summary>
    public static readonly string RootPath = RootPathFor(OperatingSystem.IsWindows());

    private readonly Lock gate = new();
    private readonly Dictionary<string, StoredEntry> entries = new(StringComparer.Ordinal);
    private readonly TimeProvider timeProvider;
    private long totalBytes;

    /// <summary>
    /// Creates an in-memory file system holding only the empty directory <see cref="RootPath"/>.
    /// </summary>
    /// <param name="timeProvider">The clock last-write times are read from.</param>
    /// <param name="maxTotalBytes">The bound on the bytes of every file held, at least 0.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxTotalBytes"/> is
    /// negative.</exception>
    public InMemoryContentFileSystem(TimeProvider timeProvider, long maxTotalBytes = DefaultMaxTotalBytes)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfNegative(maxTotalBytes);
        this.timeProvider = timeProvider;
        MaxTotalBytes = maxTotalBytes;
        entries.Add(RootPath, StoredEntry.NewDirectory(timeProvider.GetUtcNow()));
    }

    /// <summary>
    /// The bound on the bytes of every file held, temporary upload files included.
    /// </summary>
    public long MaxTotalBytes { get; }

    /// <summary>
    /// The bytes of every file held now, files still being written included.
    /// </summary>
    public long TotalBytes
    {
        get
        {
            lock (gate)
            {
                return totalBytes;
            }
        }
    }

    internal static string RootPathFor(bool isWindows) => isWindows ? @"C:\surl" : "/surl";

    /// <inheritdoc/>
    public ContentEntryKind GetEntryKind(string path)
    {
        string key = Normalise(path);
        lock (gate)
        {
            return entries.TryGetValue(key, out StoredEntry? entry) ? entry.Kind : ContentEntryKind.None;
        }
    }

    /// <inheritdoc/>
    /// <remarks>No symbolic link exists in memory, so this is the path normalised.</remarks>
    public string ResolveFinalPath(string path) => Normalise(path);

    /// <inheritdoc/>
    /// <exception cref="FileNotFoundException">No file is at <paramref name="path"/>.</exception>
    public long GetFileLength(string path)
    {
        string key = Normalise(path);
        lock (gate)
        {
            return RequireFile(key).Contents.LongLength;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A file's is when its write stream was disposed, kept by
    /// <see cref="MoveFileReplacing(string, string)"/>; a directory's is when it was created.
    /// </remarks>
    /// <exception cref="FileNotFoundException">Nothing is at <paramref name="path"/>.</exception>
    public DateTimeOffset GetLastWriteTimeUtc(string path)
    {
        string key = Normalise(path);
        lock (gate)
        {
            return entries.TryGetValue(key, out StoredEntry? entry)
                ? entry.LastWriteTimeUtc
                : throw new FileNotFoundException("Nothing is at this path in memory.", key);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The stream reads the bytes the file held when it was opened, so a later write, replace
    /// or delete never changes what it reads.
    /// </remarks>
    /// <exception cref="FileNotFoundException">No file is at <paramref name="path"/>.</exception>
    public Stream OpenFileForAsyncRead(string path)
    {
        string key = Normalise(path);
        lock (gate)
        {
            return new MemoryStream(RequireFile(key).Contents, writable: false);
        }
    }

    /// <inheritdoc/>
    /// <remarks>The names are taken all at once, when this is called.</remarks>
    /// <exception cref="DirectoryNotFoundException">No directory is at
    /// <paramref name="path"/>.</exception>
    public IEnumerable<string> EnumerateDirectoryEntryNames(string path)
    {
        string directory = Normalise(path);
        lock (gate)
        {
            RequireDirectory(directory);
            return entries.Keys
                .Where(key => string.Equals(Path.GetDirectoryName(key), directory, StringComparison.Ordinal))
                .Select(key => Path.GetFileName(key))
                .ToList();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The file is there, empty, as soon as this returns; its bytes appear when the stream is
    /// disposed. A write that would pass <see cref="MaxTotalBytes"/>, or that is made after the
    /// file was deleted or replaced, throws <see cref="IOException"/> and keeps nothing.
    /// </remarks>
    /// <exception cref="DirectoryNotFoundException">No directory is above
    /// <paramref name="path"/>.</exception>
    /// <exception cref="UnauthorizedAccessException">A directory is at
    /// <paramref name="path"/>.</exception>
    public Stream CreateFileForAsyncWrite(string path)
    {
        string key = Normalise(path);
        lock (gate)
        {
            RequireParentDirectory(key);
            RemoveFileMaking(key);
            var file = StoredEntry.NewFile(timeProvider.GetUtcNow());
            entries.Add(key, file);
            return new InMemoryFileWriteStream(this, file);
        }
    }

    /// <inheritdoc/>
    /// <remarks>A directory at <paramref name="path"/> is left alone, as nothing is a file there.</remarks>
    public void DeleteFile(string path)
    {
        string key = Normalise(path);
        lock (gate)
        {
            if (entries.TryGetValue(key, out StoredEntry? entry) && entry.Kind == ContentEntryKind.File)
            {
                Remove(key, entry);
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>The moved file keeps its bytes and its last-write time, as a rename on disk does.</remarks>
    /// <exception cref="FileNotFoundException">No file is at <paramref name="source"/>.</exception>
    /// <exception cref="DirectoryNotFoundException">No directory is above
    /// <paramref name="destination"/>.</exception>
    /// <exception cref="UnauthorizedAccessException">A directory is at
    /// <paramref name="destination"/>.</exception>
    public void MoveFileReplacing(string source, string destination)
    {
        string from = Normalise(source);
        string to = Normalise(destination);
        lock (gate)
        {
            StoredEntry moved = RequireFile(from);
            if (from == to)
            {
                return;
            }

            RequireParentDirectory(to);
            RemoveFileMaking(to);
            entries.Remove(from);
            entries.Add(to, moved);
        }
    }

    /// <inheritdoc/>
    /// <remarks>Each directory created has the current time as its last-write time.</remarks>
    /// <exception cref="IOException">A file is at <paramref name="path"/> or above it.</exception>
    public void CreateDirectory(string path)
    {
        string? current = Normalise(path);
        lock (gate)
        {
            List<string> missing = [];
            while (current is not null && !entries.ContainsKey(current))
            {
                missing.Add(current);
                current = Path.GetDirectoryName(current);
            }

            if (current is not null && entries[current].Kind == ContentEntryKind.File)
            {
                throw new IOException($"A file is in the way of the directory: {current}");
            }

            DateTimeOffset now = timeProvider.GetUtcNow();
            foreach (string directory in missing)
            {
                entries.Add(directory, StoredEntry.NewDirectory(now));
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Every entry inside the directory moves with it, keeping its bytes and last-write time; a
    /// file still being written moves too, and its bytes land at the new path.
    /// </remarks>
    /// <exception cref="DirectoryNotFoundException">No directory is at <paramref name="source"/>,
    /// or none is above <paramref name="destination"/>.</exception>
    /// <exception cref="IOException">Something is at <paramref name="destination"/>, or it is
    /// inside <paramref name="source"/>.</exception>
    public void MoveDirectory(string source, string destination)
    {
        string from = Normalise(source);
        string to = Normalise(destination);
        string prefix = from + Path.DirectorySeparatorChar;
        lock (gate)
        {
            RequireDirectory(from);
            RequireParentDirectory(to);
            if (entries.ContainsKey(to) || to.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new IOException($"The directory cannot be moved to this path in memory: {to}");
            }

            List<string> moving = [.. entries.Keys.Where(key => key == from || key.StartsWith(prefix, StringComparison.Ordinal))];
            foreach (string key in moving)
            {
                StoredEntry entry = entries[key];
                entries.Remove(key);
                entries.Add(to + key[from.Length..], entry);
            }
        }
    }

    /// <inheritdoc/>
    /// <exception cref="DirectoryNotFoundException">No directory is at
    /// <paramref name="path"/>.</exception>
    /// <exception cref="IOException">The directory holds an entry.</exception>
    public void RemoveEmptyDirectory(string path)
    {
        string key = Normalise(path);
        string prefix = key + Path.DirectorySeparatorChar;
        lock (gate)
        {
            RequireDirectory(key);
            if (entries.Keys.Any(other => other.StartsWith(prefix, StringComparison.Ordinal)))
            {
                throw new IOException($"The directory in memory is not empty: {key}");
            }

            entries.Remove(key);
        }
    }

    private static string Normalise(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private StoredEntry RequireFile(string key) =>
        entries.TryGetValue(key, out StoredEntry? entry) && entry.Kind == ContentEntryKind.File
            ? entry
            : throw new FileNotFoundException("No file is at this path in memory.", key);

    private void RequireDirectory(string key)
    {
        if (!entries.TryGetValue(key, out StoredEntry? entry) || entry.Kind != ContentEntryKind.Directory)
        {
            throw new DirectoryNotFoundException($"No directory is at this path in memory: {key}");
        }
    }

    private void RequireParentDirectory(string key) =>
        RequireDirectory(Path.GetDirectoryName(key) ?? string.Empty);

    // Makes room for a file at key: removes the file there, refuses a directory.
    private void RemoveFileMaking(string key)
    {
        if (!entries.TryGetValue(key, out StoredEntry? existing))
        {
            return;
        }

        if (existing.Kind == ContentEntryKind.Directory)
        {
            throw new UnauthorizedAccessException($"A directory is at this path in memory: {key}");
        }

        Remove(key, existing);
    }

    private void Remove(string key, StoredEntry file)
    {
        entries.Remove(key);
        totalBytes -= file.ChargedBytes;
        file.IsRemoved = true;
    }

    private void Charge(StoredEntry file, int count)
    {
        lock (gate)
        {
            if (file.IsRemoved)
            {
                throw new IOException("The in-memory file was deleted or replaced while it was being written.");
            }

            if (count > MaxTotalBytes - totalBytes)
            {
                throw new IOException("The in-memory file system is full.");
            }

            file.ChargedBytes += count;
            totalBytes += count;
        }
    }

    private void Commit(StoredEntry file, byte[] contents)
    {
        lock (gate)
        {
            file.Contents = contents;
            file.LastWriteTimeUtc = timeProvider.GetUtcNow();
        }
    }

    private sealed class StoredEntry
    {
        private StoredEntry(ContentEntryKind kind, DateTimeOffset lastWriteTimeUtc)
        {
            Kind = kind;
            LastWriteTimeUtc = lastWriteTimeUtc;
        }

        public ContentEntryKind Kind { get; }

        public byte[] Contents { get; set; } = [];

        public DateTimeOffset LastWriteTimeUtc { get; set; }

        // The bytes this file counts against the bound: its contents once written, and every
        // byte accepted so far while it is being written.
        public long ChargedBytes { get; set; }

        public bool IsRemoved { get; set; }

        public static StoredEntry NewDirectory(DateTimeOffset now) => new(ContentEntryKind.Directory, now);

        public static StoredEntry NewFile(DateTimeOffset now) => new(ContentEntryKind.File, now);
    }

    // Collects a file's bytes, charging each write against the bound before keeping it, and
    // hands them to the file when disposed.
    private sealed class InMemoryFileWriteStream(InMemoryContentFileSystem owner, StoredEntry file) : Stream
    {
        private readonly MemoryStream written = new();
        private bool disposed;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => !disposed;

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

        public override void Write(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            Write(buffer.AsSpan(offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            owner.Charge(file, buffer.Length);
            written.Write(buffer);
        }

        public override void WriteByte(byte value) => Write([value]);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                disposed = true;
                owner.Commit(file, written.ToArray());
                written.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
