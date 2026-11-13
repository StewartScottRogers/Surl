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
/// the <c>Content-Type</c> <see cref="ContentMediaTypes.Of"/> names from its key's extension
/// (<see cref="DefaultContentType"/> when the extension names none), no other header field, no user metadata, the quoted
/// lower-case hexadecimal MD5 of its bytes as its ETag - computed on first read, with its CRC-32C
/// (<see cref="Crc32cChecksum"/>) from the same read, and cached in the
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
/// <b>Folder objects.</b> A folder a client put as an object (S3's zero-byte <c>x/</c>) is kept
/// under the folder's key and a final <c>/</c> by <see cref="StoreFolderObjectAsync"/>, and lasts
/// while the folder exists (ADR-0108 Amendment 3).
/// </para>
/// <para>
/// <b>Verified digests.</b> A server that checks a file against a content digest - the OCI
/// registry, whose blob files are named by the digest of their bytes - records the digest with
/// <see cref="RecordVerifiedDigestAsync"/> against the stamp the file had when it was hashed, and
/// asks <see cref="IsDigestVerifiedAsync"/> before hashing it again (ADR-0115, decision 2.1). A
/// record whose stamp no longer matches is stale and dropped. Verified digests are held in memory
/// only, at most <see cref="MaxEntries"/> of them, are never written to the index file, and are
/// forgotten on a reset or restore.
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
    /// The <c>Content-Type</c> of a file stored with none whose extension names no media type:
    /// <see cref="ContentMediaTypes.Unknown"/>, <c>application/octet-stream</c>.
    /// </summary>
    public const string DefaultContentType = ContentMediaTypes.Unknown;

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

    private const int DigestBufferBytes = 81920;

    // The quoted MD5 of no bytes: a folder object's default ETag.
    private const string EmptyETag = "\"d41d8cd98f00b204e9800998ecf8427e\"";

    private readonly IContentFileSystem fileSystem;
    private readonly ISurlEventPublisher events;
    private readonly string servedRoot;
    private readonly string? stateFolderPath;
    private readonly Dictionary<string, ObjectMetadataEntry> entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Digest, long Length, long LastWriteTicks)> verifiedDigests = new(StringComparer.Ordinal);
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
    /// <see cref="StoreChange.MetadataStored"/> change named <c>/</c> and its key (ADR-0129);
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
    /// The most bytes of metadata one object may carry: the UTF-8 bytes of its six header
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
        return await StoreEntryAsync(key, metadata, cancellationToken);
    }

    /// <summary>
    /// Records that a client put the folder at <paramref name="key"/> as an object - a zero-byte
    /// object whose key ends in <c>/</c> (ADR-0108, decision 6) - with the metadata it set,
    /// replacing any it had. The folder object lasts while the folder exists; its length is 0, its
    /// default ETag the quoted MD5 of no bytes, and its last write time the folder's when stored.
    /// </summary>
    /// <param name="key">The folder's path relative to the served root and a final <c>/</c>; see
    /// <see cref="IsValidFolderObjectKey"/>.</param>
    /// <param name="metadata">The metadata set at upload.</param>
    /// <param name="cancellationToken">Cancels writing the index.</param>
    /// <returns>Whether it is stored (<see cref="ObjectMetadataStoreOutcome.NoSuchFile"/> when no
    /// folder is there), what the object now answers with, and any failure to rewrite the index file.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is not valid, or
    /// <paramref name="metadata"/>'s ETag is given and not valid.</exception>
    public async Task<ObjectMetadataStoreResult> StoreFolderObjectAsync(string key, ObjectMetadata metadata, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!IsValidFolderObjectKey(key))
        {
            throw new ArgumentException("The key is not a folder the content store can hold and a final '/'.", nameof(key));
        }

        return await StoreEntryAsync(key, metadata, cancellationToken);
    }

    /// <summary>
    /// Drops the folder object at <paramref name="key"/> (<see cref="StoreFolderObjectAsync"/>)
    /// from the index, whether or not its folder still holds keys, as a client's DeleteObject of
    /// <c>x/</c> forgets the object <c>x/</c> and leaves the keys under it. A key with no entry
    /// changes nothing.
    /// </summary>
    /// <param name="key">The folder's path relative to the served root and a final <c>/</c>; see
    /// <see cref="IsValidFolderObjectKey"/>.</param>
    /// <param name="cancellationToken">Cancels writing the index.</param>
    /// <returns>The failure to rewrite the index file, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is not valid.</exception>
    public async Task<string?> ForgetFolderObjectAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!IsValidFolderObjectKey(key))
        {
            throw new ArgumentException("The key is not a folder the content store can hold and a final '/'.", nameof(key));
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!entries.Remove(key))
            {
                return null;
            }

            var failure = await WriteIndexAsync(cancellationToken);
            events.Publish(new StoreChangedEvent(StoreKind.Content, StoreChange.Deleted, "/" + key, null, null));
            return failure;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Every folder object (<see cref="StoreFolderObjectAsync"/>) whose folder still exists, by
    /// its key; one whose folder is gone is dropped.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait for the index's lock.</param>
    /// <returns>Each folder object's properties, keyed by its key with the final <c>/</c>.</returns>
    public async Task<IReadOnlyDictionary<string, ObjectProperties>> ReadFolderObjectsAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var found = new Dictionary<string, ObjectProperties>(StringComparer.Ordinal);
            foreach (var (key, entry) in entries.Where(pair => IsFolderObjectKey(pair.Key)).ToList())
            {
                if (StampOf(key) is null)
                {
                    entries.Remove(key);
                }
                else
                {
                    found.Add(key, PropertiesOf(key, entry));
                }
            }

            return found;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Whether <paramref name="key"/> names a folder object the index can keep: a key
    /// <see cref="IsValidKey"/> accepts, then a final <c>/</c>.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns><see langword="true"/> when the key is valid.</returns>
    public static bool IsValidFolderObjectKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return IsFolderObjectKey(key) && IsValidKey(key[..^1]);
    }

    private static void RequireValidMetadata(ObjectMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (metadata.ETag is not null && !IsValidETag(metadata.ETag))
        {
            throw new ArgumentException("The ETag is empty or longer than the index keeps.", nameof(metadata));
        }

        if (metadata.Metageneration < 1)
        {
            throw new ArgumentException("The metageneration is less than 1.", nameof(metadata));
        }
    }

    private async Task<ObjectMetadataStoreResult> StoreEntryAsync(string key, ObjectMetadata metadata, CancellationToken cancellationToken)
    {
        RequireValidMetadata(metadata);

        string?[] fields = [.. new[] { metadata.ContentType, metadata.ContentEncoding, metadata.ContentDisposition, metadata.CacheControl, metadata.ContentLanguage, metadata.ContentMd5 }
            .Select(field => string.IsNullOrEmpty(field) ? null : field)];
        List<KeyValuePair<string, string>> userMetadata = [.. metadata.UserMetadata.OrderBy(pair => pair.Key, StringComparer.Ordinal)];
        if (ObjectMetadataEntry.CountMetadataBytes(fields, userMetadata) > MaxMetadataBytes)
        {
            return new ObjectMetadataStoreResult(ObjectMetadataStoreOutcome.MetadataTooLarge, null, null);
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (StampOf(key) is not var (length, lastWriteTicks))
            {
                return new ObjectMetadataStoreResult(ObjectMetadataStoreOutcome.NoSuchFile, null, null);
            }

            if (!entries.ContainsKey(key) && !HasRoomAfterDroppingStaleEntries())
            {
                return new ObjectMetadataStoreResult(ObjectMetadataStoreOutcome.MetadataIndexFull, null, null);
            }

            var (eTag, crc32c) = metadata.ETag is null ? await DefaultDigestsAsync(key, cancellationToken) : (metadata.ETag, metadata.Crc32c);
            var entry = new ObjectMetadataEntry(
                length, lastWriteTicks, metadata.Generation ?? GenerationOf(lastWriteTicks), metadata.Metageneration ?? 1, eTag,
                fields[0], fields[1], fields[2], fields[3], fields[4], fields[5], userMetadata, crc32c);
            entries[key] = entry;
            var failure = await WriteIndexAsync(cancellationToken);
            events.Publish(new StoreChangedEvent(StoreKind.Content, StoreChange.MetadataStored, "/" + key, null, null));
            return new ObjectMetadataStoreResult(ObjectMetadataStoreOutcome.Stored, PropertiesOf(key, entry), failure);
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
            if (StampOf(key) is not var (length, lastWriteTicks))
            {
                entries.Remove(key);
                return new ObjectMetadataReading(null, null);
            }

            if (entries.TryGetValue(key, out var current) && current.Describes(length, lastWriteTicks))
            {
                return new ObjectMetadataReading(PropertiesOf(key, current), null);
            }

            entries.Remove(key);
            var (eTag, crc32c) = await ComputeDigestsAsync(FullPathOf(key), cancellationToken);
            var entry = new ObjectMetadataEntry(
                length, lastWriteTicks, GenerationOf(lastWriteTicks), 1, eTag,
                null, null, null, null, null, null, [], crc32c);
            string? failure = null;
            if (entries.Count < MaxEntries)
            {
                entries.Add(key, entry);
                failure = await WriteIndexAsync(cancellationToken);
            }

            return new ObjectMetadataReading(PropertiesOf(key, entry), failure);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Takes a copy of every entry, for a content store snapshot (ADR-0124 decision 3).
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait for the index's lock.</param>
    /// <returns>The copy.</returns>
    internal async Task<ObjectMetadataIndexContents> CaptureEntriesAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return new ObjectMetadataIndexContents(new Dictionary<string, ObjectMetadataEntry>(entries, StringComparer.Ordinal));
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Whether the file at <paramref name="key"/>, as it is now, was hashed to
    /// <paramref name="digest"/> by <see cref="RecordVerifiedDigestAsync"/>. A record whose stamp
    /// no longer matches the file is dropped.
    /// </summary>
    /// <param name="key">The file's path relative to the served root; see <see cref="IsValidKey"/>.</param>
    /// <param name="digest">The digest, compared ordinally, e.g. <c>sha256:&lt;hex&gt;</c>.</param>
    /// <param name="cancellationToken">Cancels the wait for the index's lock.</param>
    /// <returns><see langword="true"/> when the digest is recorded for the file's current stamp.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is not valid.</exception>
    public async Task<bool> IsDigestVerifiedAsync(string key, string digest, CancellationToken cancellationToken)
    {
        RequireValidKey(key);
        ArgumentNullException.ThrowIfNull(digest);
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!verifiedDigests.TryGetValue(key, out var verified))
            {
                return false;
            }

            if (!HasStamp(key, verified.Length, verified.LastWriteTicks))
            {
                verifiedDigests.Remove(key);
                return false;
            }

            return verified.Digest == digest;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Records that the file at <paramref name="key"/> hashed to <paramref name="digest"/> while
    /// its status was <paramref name="hashedStatus"/>. Nothing is recorded when the file has
    /// changed since, or when <see cref="MaxEntries"/> current records are already held.
    /// </summary>
    /// <param name="key">The file's path relative to the served root; see <see cref="IsValidKey"/>.</param>
    /// <param name="digest">The digest its bytes hashed to.</param>
    /// <param name="hashedStatus">The file's length and last write time read before it was hashed.</param>
    /// <param name="cancellationToken">Cancels the wait for the index's lock.</param>
    /// <returns><see langword="true"/> when the digest is recorded.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is not valid.</exception>
    public async Task<bool> RecordVerifiedDigestAsync(string key, string digest, ContentFileStatus hashedStatus, CancellationToken cancellationToken)
    {
        RequireValidKey(key);
        ArgumentNullException.ThrowIfNull(digest);
        ArgumentNullException.ThrowIfNull(hashedStatus);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var lastWriteTicks = hashedStatus.LastModifiedUtc.UtcTicks;
            if (!HasStamp(key, hashedStatus.Length, lastWriteTicks) || !HasRoomForVerifiedDigest(key))
            {
                return false;
            }

            verifiedDigests[key] = (digest, hashedStatus.Length, lastWriteTicks);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Replaces every entry with <paramref name="contents"/>: none, for a reset, or a snapshot's
    /// copy, for a restore (ADR-0124 decision 3). Never written to a file: only an index held in
    /// memory is reset or restored.
    /// </summary>
    /// <param name="contents">The entries to hold.</param>
    /// <param name="cancellationToken">Cancels the wait for the index's lock.</param>
    /// <returns>A task that completes once the entries are replaced.</returns>
    internal async Task ReplaceEntriesAsync(ObjectMetadataIndexContents contents, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            verifiedDigests.Clear();
            entries.Clear();
            foreach (var (key, entry) in contents.Entries)
            {
                entries.Add(key, entry);
            }
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

    private static ObjectProperties PropertiesOf(string key, ObjectMetadataEntry entry) => new(
        entry.ContentType ?? ContentMediaTypes.Of(key),
        entry.ContentEncoding,
        entry.ContentDisposition,
        entry.CacheControl,
        entry.ContentLanguage,
        entry.UserMetadata,
        entry.ETag,
        entry.Generation,
        entry.Length,
        new DateTimeOffset(entry.LastWriteTicks, TimeSpan.Zero))
    {
        ContentMd5 = entry.ContentMd5,
        Metageneration = entry.Metageneration,
        Crc32c = entry.Crc32c,
    };

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

    private static bool IsFolderObjectKey(string key) => key.EndsWith('/');

    // A file's length and last write time; a folder object's 0 and its folder's last write time.
    // Null when no entry of the key's kind is there.
    private (long Length, long LastWriteTicks)? StampOf(string key)
    {
        var isFolderObject = IsFolderObjectKey(key);
        var path = FullPathOf(isFolderObject ? key[..^1] : key);
        if (fileSystem.GetEntryKind(path) != (isFolderObject ? ContentEntryKind.Directory : ContentEntryKind.File))
        {
            return null;
        }

        return (isFolderObject ? 0 : fileSystem.GetFileLength(path), fileSystem.GetLastWriteTimeUtc(path).UtcTicks);
    }

    // A folder object lasts while its folder exists, whatever is written inside it; a file's
    // entry while its stamp matches.
    private bool IsCurrent(string key, ObjectMetadataEntry entry) =>
        StampOf(key) is var (length, lastWriteTicks) && (IsFolderObjectKey(key) || entry.Describes(length, lastWriteTicks));

    // A folder object's are those of no bytes: the empty-body MD5 and a CRC-32C of 0.
    private Task<(string ETag, uint? Crc32c)> DefaultDigestsAsync(string key, CancellationToken cancellationToken) =>
        IsFolderObjectKey(key) ? Task.FromResult<(string, uint?)>((EmptyETag, 0u)) : ComputeDigestsAsync(FullPathOf(key), cancellationToken);

    private bool HasRoomAfterDroppingStaleEntries()
    {
        if (entries.Count < MaxEntries)
        {
            return true;
        }

        foreach (var (key, entry) in entries.ToList())
        {
            if (!IsCurrent(key, entry))
            {
                entries.Remove(key);
            }
        }

        return entries.Count < MaxEntries;
    }

    // Whether a file is at the key with this length and last write time.
    private bool HasStamp(string key, long length, long lastWriteTicks) =>
        StampOf(key) is var (currentLength, currentLastWriteTicks) && currentLength == length && currentLastWriteTicks == lastWriteTicks;

    private bool HasRoomForVerifiedDigest(string key)
    {
        if (verifiedDigests.Count < MaxEntries || verifiedDigests.ContainsKey(key))
        {
            return true;
        }

        foreach (var (staleKey, _) in verifiedDigests.Where(pair => !HasStamp(pair.Key, pair.Value.Length, pair.Value.LastWriteTicks)).ToList())
        {
            verifiedDigests.Remove(staleKey);
        }

        return verifiedDigests.Count < MaxEntries;
    }

    // The quoted lower-case hexadecimal MD5 and the CRC-32C of the file's bytes, in one read.
    private async Task<(string ETag, uint? Crc32c)> ComputeDigestsAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = fileSystem.OpenFileForAsyncRead(path);
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        var buffer = new byte[DigestBufferBytes];
        var crc32c = 0u;
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            md5.AppendData(buffer, 0, read);
            crc32c = Crc32cChecksum.Append(crc32c, buffer.AsSpan(0, read));
        }

        return ("\"" + Convert.ToHexStringLower(md5.GetHashAndReset()) + "\"", crc32c);
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
