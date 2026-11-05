using System.Security.Cryptography;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Content;

/// <summary>
/// The object metadata and ETags the object-storage servers (S3, Azure Storage, Cloud Storage,
/// the OCI registry) keep beside the shared content store (ADR-0100, decision 2.3): what a file
/// system cannot hold, in one bounded index keyed by the file's path relative to the served root.
/// </summary>
/// <remarks>
/// <para>
/// <b>Stamped, and stale is absent.</b> Each entry is stamped with the file's length and last
/// write time when it was written. An entry whose stamp no longer matches its file - because FTP,
/// SFTP, HTTP or SMB overwrote it, or a rename or delete through the <see cref="ContentStore"/>
/// moved it away - is ignored: the file answers with the defaults, and the entry is dropped from
/// memory when it is next looked at and from the index file on its next write. Renames through
/// other protocols therefore lose metadata (ADR-0100's stated cost).
/// </para>
/// <para>
/// <b>Defaults.</b> A file with no current entry answers with
/// <see cref="DefaultContentType"/>, no other header field, no user metadata, the quoted
/// lower-case hexadecimal MD5 of its bytes as its ETag - computed on first read and cached in the
/// index with the file's stamp - and its last write time in microseconds since the Unix epoch as
/// its generation.
/// </para>
/// <para>
/// <b>Bounds</b> (ADR-0006): at most <see cref="MaxEntries"/> entries and
/// <see cref="MaxMetadataBytes"/> bytes of metadata per object; past either, nothing is stored and
/// <see cref="StoreAsync"/> answers <see cref="ObjectMetadataStoreOutcome.MetadataIndexFull"/> or
/// <see cref="ObjectMetadataStoreOutcome.MetadataTooLarge"/>. Stale entries are dropped before the
/// index is called full. A computed ETag that finds the index full is answered but not cached.
/// </para>
/// <para>
/// <b>Persistence.</b> With a data directory the index is
/// <c>&lt;served root&gt;/.surl/objects/index</c> in <see cref="ObjectMetadataIndexFormat"/>'s
/// bytes, rewritten whole through <c>.index-&lt;guid&gt;</c> beside it and
/// <see cref="IContentFileSystem.MoveFileReplacing"/> after every change, and loaded once at start
/// by <see cref="LoadAsync"/>; the <see cref="ContentStore"/> never serves it, as it never serves
/// anything under <c>.surl</c> (ADR-0031 decision 5). A write that fails is answered as a
/// <c>StorageFailure</c> for the server to note, never ends serving, and leaves the change in
/// memory. Without a data directory nothing is written.
/// </para>
/// <para>
/// Safe for concurrent use: every operation runs alone.
/// </para>
/// </remarks>
public sealed class ObjectMetadataIndex
{
    /// <summary>
    /// The default for <see cref="MaxEntries"/>: 100000.
    /// </summary>
    public const int DefaultMaxEntries = 100_000;

    /// <summary>
    /// The default for <see cref="MaxMetadataBytes"/>: 8192, Azure's limit.
    /// </summary>
    public const int DefaultMaxMetadataBytes = 8192;

    /// <summary>
    /// The longest ETag the index keeps, in bytes of UTF-8: 1024.
    /// </summary>
    public const int MaxETagBytes = 1024;

    /// <summary>
    /// The <c>Content-Type</c> of a file stored with none: <c>application/octet-stream</c>, as the
    /// HTTP server names every file until the content store knows media types.
    /// </summary>
    public const string DefaultContentType = "application/octet-stream";

    /// <summary>
    /// The name of the folder under <c>.surl</c> the index is kept in: <c>objects</c>.
    /// </summary>
    public const string ObjectsFolderName = "objects";

    /// <summary>
    /// The index's name in its folder: <c>index</c>.
    /// </summary>
    public const string IndexFileName = "index";

    /// <summary>
    /// The reason an index that does not parse is refused with: <c>not an object metadata index</c>.
    /// </summary>
    public const string MalformedIndexReason = "not an object metadata index";

    private const string TemporaryIndexPrefix = ".index-";

    private readonly IContentFileSystem fileSystem;
    private readonly ISurlEventPublisher events;
    private readonly string servedRoot;
    private readonly string? stateFolderPath;
    private readonly Dictionary<string, ObjectMetadataEntry> entries = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>
    /// Creates an empty index for the files under <paramref name="servedRoot"/>.
    /// </summary>
    /// <param name="fileSystem">The seam every look at a file and every write of the index goes
    /// through: the content store's own.</param>
    /// <param name="servedRoot">The full path of the directory being served, as the
    /// <see cref="ContentStore"/> is given it.</param>
    /// <param name="persists">Whether the index is kept in <c>&lt;served root&gt;/.surl/objects/index</c>,
    /// as it is with a data directory; without one it is held in memory only.</param>
    /// <param name="maxEntries">The most entries held; at least 0.</param>
    /// <param name="maxMetadataBytes">The most bytes of metadata per object; 0 to 65535.</param>
    /// <param name="events">Where each stored entry is published as a
    /// <see cref="StoreChange.MetadataStored"/> change named <c>/</c> and its key (ADR-0126);
    /// <see langword="null"/> publishes nothing.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="servedRoot"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A bound is out of its range.</exception>
    public ObjectMetadataIndex(
        IContentFileSystem fileSystem,
        string servedRoot,
        bool persists,
        int maxEntries = DefaultMaxEntries,
        int maxMetadataBytes = DefaultMaxMetadataBytes,
        ISurlEventPublisher? events = null)
    {
        this.events = events ?? DiscardingSurlEventPublisher.Instance;
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentException.ThrowIfNullOrEmpty(servedRoot);
        ArgumentOutOfRangeException.ThrowIfNegative(maxEntries);
        ArgumentOutOfRangeException.ThrowIfNegative(maxMetadataBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxMetadataBytes, ushort.MaxValue);

        this.fileSystem = fileSystem;
        this.servedRoot = servedRoot;
        MaxEntries = maxEntries;
        MaxMetadataBytes = maxMetadataBytes;
        if (persists)
        {
            stateFolderPath = Path.Join(servedRoot, ContentStore.ServiceStateFolderName, ObjectsFolderName);
            IndexPath = Path.Join(stateFolderPath, IndexFileName);
        }
    }

    /// <summary>
    /// The most entries the index holds.
    /// </summary>
    public int MaxEntries { get; }

    /// <summary>
    /// The most bytes of metadata one object may carry: the UTF-8 bytes of its five header
    /// fields and of its user metadata's names and values.
    /// </summary>
    public int MaxMetadataBytes { get; }

    /// <summary>
    /// The full path of the index file; <see langword="null"/> when the index is held in memory only.
    /// </summary>
    public string? IndexPath { get; }

    /// <summary>
    /// How many entries the index holds in memory, stale ones not yet dropped included.
    /// </summary>
    public int Count => entries.Count;

    /// <summary>
    /// Whether <paramref name="key"/> names a file the index can keep metadata for: the path
    /// relative to the served root, segments joined by <c>/</c>, each a segment the content store
    /// accepts, and the first not <c>.surl</c>.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns><see langword="true"/> when the key is valid.</returns>
    public static bool IsValidKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var segments = key.Split('/');
        return Encoding.UTF8.GetByteCount(key) <= ushort.MaxValue
            && !segments[0].Equals(ContentStore.ServiceStateFolderName, StringComparison.OrdinalIgnoreCase)
            && segments.All(segment => segment.Length > 0 && RequestPathSegments.CheckSegment(segment) == ContentPathRefusal.None);
    }

    /// <summary>
    /// Whether <paramref name="eTag"/> is an ETag the index can keep: not empty and at most
    /// <see cref="MaxETagBytes"/> bytes of UTF-8.
    /// </summary>
    /// <param name="eTag">The ETag to check.</param>
    /// <returns><see langword="true"/> when the ETag is valid.</returns>
    public static bool IsValidETag(string eTag)
    {
        ArgumentNullException.ThrowIfNull(eTag);
        return eTag.Length > 0 && Encoding.UTF8.GetByteCount(eTag) <= MaxETagBytes;
    }

    /// <summary>
    /// Loads the index file, replacing what is held in memory; does nothing when the index is held
    /// in memory only or no index file exists yet.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="ObjectMetadataIndexLoadException">The index file cannot be read (its
    /// message is the exception's), or does not parse or passes a bound (its message is
    /// <see cref="MalformedIndexReason"/>).</exception>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (IndexPath is null)
        {
            return;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var bytes = await ReadIndexBytesAsync(IndexPath, cancellationToken);
            var loaded = bytes is null ? [] : DecodeIndex(IndexPath, bytes);
            entries.Clear();
            foreach (var (key, entry) in loaded)
            {
                entries.Add(key, entry);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Stores the metadata an upload set for the file at <paramref name="key"/>, which the server
    /// has just written through the content store, replacing any it had.
    /// </summary>
    /// <param name="key">The file's path relative to the served root; see <see cref="IsValidKey"/>.</param>
    /// <param name="metadata">The metadata set at upload.</param>
    /// <param name="cancellationToken">Cancels computing the ETag and writing the index.</param>
    /// <returns>Whether it is stored, what the object now answers with, and any failure to
    /// rewrite the index file.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is not valid, or
    /// <paramref name="metadata"/>'s ETag is given and not valid.</exception>
    public async Task<ObjectMetadataStoreResult> StoreAsync(string key, ObjectMetadata metadata, CancellationToken cancellationToken)
    {
        RequireValidKey(key);
        ArgumentNullException.ThrowIfNull(metadata);
        if (metadata.ETag is not null && !IsValidETag(metadata.ETag))
        {
            throw new ArgumentException("The ETag is empty or longer than the index keeps.", nameof(metadata));
        }

        string?[] fields = [.. new[] { metadata.ContentType, metadata.ContentEncoding, metadata.ContentDisposition, metadata.CacheControl, metadata.ContentLanguage }
            .Select(field => string.IsNullOrEmpty(field) ? null : field)];
        List<KeyValuePair<string, string>> userMetadata = [.. metadata.UserMetadata.OrderBy(pair => pair.Key, StringComparer.Ordinal)];
        if (ObjectMetadataEntry.CountMetadataBytes(fields, userMetadata) > MaxMetadataBytes)
        {
            return new ObjectMetadataStoreResult(ObjectMetadataStoreOutcome.MetadataTooLarge, null, null);
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var path = FullPathOf(key);
            if (!TryReadStamp(path, out var length, out var lastWriteTicks))
            {
                return new ObjectMetadataStoreResult(ObjectMetadataStoreOutcome.NoSuchFile, null, null);
            }

            if (!entries.ContainsKey(key) && !HasRoomAfterDroppingStaleEntries())
            {
                return new ObjectMetadataStoreResult(ObjectMetadataStoreOutcome.MetadataIndexFull, null, null);
            }

            var eTag = metadata.ETag ?? await ComputeETagAsync(path, cancellationToken);
            var entry = new ObjectMetadataEntry(
                length, lastWriteTicks, metadata.Generation ?? GenerationOf(lastWriteTicks), eTag,
                fields[0], fields[1], fields[2], fields[3], fields[4], userMetadata);
            entries[key] = entry;
            var failure = await WriteIndexAsync(cancellationToken);
            events.Publish(new StoreChangedEvent(StoreKind.Content, StoreChange.MetadataStored, "/" + key, null, null));
            return new ObjectMetadataStoreResult(ObjectMetadataStoreOutcome.Stored, PropertiesOf(entry), failure);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Reads what the object at <paramref name="key"/> answers with: the metadata stored with its
    /// upload while its stamp still matches the file, else the defaults, caching the computed ETag.
    /// </summary>
    /// <param name="key">The file's path relative to the served root; see <see cref="IsValidKey"/>.</param>
    /// <param name="cancellationToken">Cancels computing the ETag and writing the index.</param>
    /// <returns>The object's properties, <see langword="null"/> when no file is at the key, and
    /// any failure to rewrite the index file.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is not valid.</exception>
    public async Task<ObjectMetadataReading> ReadAsync(string key, CancellationToken cancellationToken)
    {
        RequireValidKey(key);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var path = FullPathOf(key);
            if (!TryReadStamp(path, out var length, out var lastWriteTicks))
            {
                entries.Remove(key);
                return new ObjectMetadataReading(null, null);
            }

            if (entries.TryGetValue(key, out var current) && current.Describes(length, lastWriteTicks))
            {
                return new ObjectMetadataReading(PropertiesOf(current), null);
            }

            entries.Remove(key);
            var entry = new ObjectMetadataEntry(
                length, lastWriteTicks, GenerationOf(lastWriteTicks), await ComputeETagAsync(path, cancellationToken),
                null, null, null, null, null, []);
            string? failure = null;
            if (entries.Count < MaxEntries)
            {
                entries.Add(key, entry);
                failure = await WriteIndexAsync(cancellationToken);
            }

            return new ObjectMetadataReading(PropertiesOf(entry), failure);
        }
        finally
        {
            gate.Release();
        }
    }

    private static void RequireValidKey(string key)
    {
        if (!IsValidKey(key))
        {
            throw new ArgumentException("The key is not a path the content store can hold.", nameof(key));
        }
    }

    private static long GenerationOf(long lastWriteTicks) => (lastWriteTicks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerMicrosecond;

    private static ObjectProperties PropertiesOf(ObjectMetadataEntry entry) => new(
        entry.ContentType ?? DefaultContentType,
        entry.ContentEncoding,
        entry.ContentDisposition,
        entry.CacheControl,
        entry.ContentLanguage,
        entry.UserMetadata,
        entry.ETag,
        entry.Generation,
        entry.Length,
        new DateTimeOffset(entry.LastWriteTicks, TimeSpan.Zero));

    private Dictionary<string, ObjectMetadataEntry> DecodeIndex(string indexPath, byte[] bytes)
    {
        try
        {
            return ObjectMetadataIndexFormat.Decode(bytes, MaxEntries, MaxMetadataBytes);
        }
        catch (InvalidDataException exception)
        {
            throw new ObjectMetadataIndexLoadException(indexPath, exception.Message, exception);
        }
    }

    private static bool IsStorageFailure(Exception exception) => exception is IOException or UnauthorizedAccessException;

    private async Task<byte[]?> ReadIndexBytesAsync(string indexPath, CancellationToken cancellationToken)
    {
        try
        {
            if (fileSystem.GetEntryKind(indexPath) != ContentEntryKind.File)
            {
                return null;
            }

            using var stream = fileSystem.OpenFileForAsyncRead(indexPath);
            using var bytes = new MemoryStream();
            await stream.CopyToAsync(bytes, cancellationToken);
            return bytes.ToArray();
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            throw new ObjectMetadataIndexLoadException(indexPath, exception.Message, exception);
        }
    }

    private string FullPathOf(string key) => Path.Join([servedRoot, .. key.Split('/')]);

    private bool TryReadStamp(string path, out long length, out long lastWriteTicks)
    {
        if (fileSystem.GetEntryKind(path) != ContentEntryKind.File)
        {
            length = 0;
            lastWriteTicks = 0;
            return false;
        }

        length = fileSystem.GetFileLength(path);
        lastWriteTicks = fileSystem.GetLastWriteTimeUtc(path).UtcTicks;
        return true;
    }

    private bool HasRoomAfterDroppingStaleEntries()
    {
        if (entries.Count < MaxEntries)
        {
            return true;
        }

        foreach (var (key, entry) in entries.ToList())
        {
            if (!TryReadStamp(FullPathOf(key), out var length, out var lastWriteTicks) || !entry.Describes(length, lastWriteTicks))
            {
                entries.Remove(key);
            }
        }

        return entries.Count < MaxEntries;
    }

    private async Task<string> ComputeETagAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = fileSystem.OpenFileForAsyncRead(path);
        var digest = await MD5.HashDataAsync(stream, cancellationToken);
        return "\"" + Convert.ToHexStringLower(digest) + "\"";
    }

    private async Task<string?> WriteIndexAsync(CancellationToken cancellationToken)
    {
        if (stateFolderPath is null)
        {
            return null;
        }

        var bytes = ObjectMetadataIndexFormat.Encode(entries);
        var temporaryPath = Path.Join(stateFolderPath, TemporaryIndexPrefix + Guid.NewGuid().ToString("N"));
        try
        {
            fileSystem.CreateDirectory(stateFolderPath);
            await using (var stream = fileSystem.CreateFileForAsyncWrite(temporaryPath))
            {
                await stream.WriteAsync(bytes, cancellationToken);
            }

            fileSystem.MoveFileReplacing(temporaryPath, IndexPath!);
            return null;
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            DeleteTemporaryFile(temporaryPath);
            return exception.Message;
        }
    }

    private void DeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            fileSystem.DeleteFile(temporaryPath);
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            // A temporary file that cannot be deleted is left behind and ignored at the next load.
        }
    }
}
