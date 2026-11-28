using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The server's 16 in-memory databases (ADR-0106 decision 3), shared by every connection and by
/// <c>redis</c> and <c>rediss</c>, and never written to disk: a new server starts empty. It counts
/// keys and key and value bytes across all 16 against ADR-0106 decision 6's bounds.
/// </summary>
/// <remarks>
/// A composition that owns the keyspace builds it, hands it to a <see cref="RedisServer"/> and
/// prepares it between tests (ADR-0158 decisions 3 to 5): <see cref="SeedKey"/> writes a string key
/// the way <c>SET</c> does but never consults the fault plan, <see cref="Reset"/> returns to the
/// configured state, and <see cref="TakeSnapshot(long)"/> and <see cref="Restore(RedisKeyspaceSnapshot)"/> copy
/// the databases out and back. Seed, reset and restore publish their change on the event publisher it
/// was given.
/// <para>
/// The configured state a reset returns to is exactly the keys seeded with <c>keepOnReset</c>, with
/// their seeded values and their expiries measured again from the reset; with no such seed, all 16
/// databases are empty. No client command, unkept seed, snapshot or restore changes it.
/// </para>
/// </remarks>
public sealed class RedisKeyspace
{
    /// <summary>The most keys across the 16 databases (ADR-0106 decision 6).</summary>
    public const long DefaultMaxKeyCount = 1_000_000;

    /// <summary>The most key and value bytes across the 16 databases: 256 MiB (ADR-0106 decision 6).</summary>
    public const long DefaultMaxByteCount = 256L * 1024 * 1024;

    /// <summary>The most bytes a seeded key and its value may hold together: 16 MiB (ADR-0158 decision 3).</summary>
    public const long MaxSeedByteCount = 16L * 1024 * 1024;

    /// <summary>The default most bytes <see cref="TakeSnapshot(long)"/> copies: 256 MiB.</summary>
    public const long DefaultMaxSnapshotBytes = 268_435_456;

    /// <summary>The longest expiry a seed may give: 365 days (ADR-0158 decision 3).</summary>
    public static readonly TimeSpan MaxSeedExpiry = TimeSpan.FromDays(365);

    private readonly RedisDatabase[] databases;
    private readonly long maxKeyCount;
    private readonly long maxByteCount;
    private readonly TimeProvider timeProvider;
    private readonly ISurlEventPublisher events;
    private readonly RedisKeptSeeds keptSeeds = new();

    /// <summary>Creates an empty keyspace with ADR-0106 decision 6's bounds, for tests of the server.</summary>
    internal RedisKeyspace()
        : this(DefaultMaxKeyCount, DefaultMaxByteCount)
    {
    }

    /// <summary>Creates an empty keyspace with the given bounds, for tests of them.</summary>
    /// <param name="maxKeyCount">The most keys across the databases.</param>
    /// <param name="maxByteCount">The most key and value bytes across the databases.</param>
    /// <param name="timeProvider">The clock a seeded key's expiry is measured on; the system clock when omitted.</param>
    /// <param name="events">Where changes are published; nowhere when omitted.</param>
    internal RedisKeyspace(long maxKeyCount, long maxByteCount, TimeProvider? timeProvider = null, ISurlEventPublisher? events = null)
        : this(timeProvider ?? TimeProvider.System, events ?? DiscardingSurlEventPublisher.Instance, maxKeyCount, maxByteCount)
    {
    }

    /// <summary>Creates an empty keyspace with ADR-0106 decision 6's bounds.</summary>
    /// <param name="timeProvider">The clock a seeded key's expiry is measured on.</param>
    /// <param name="events">Where a seed, reset and restore publish their <see cref="StoreChangedEvent"/>.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public RedisKeyspace(TimeProvider timeProvider, ISurlEventPublisher events)
        : this(timeProvider, events, DefaultMaxKeyCount, DefaultMaxByteCount)
    {
    }

    private RedisKeyspace(TimeProvider timeProvider, ISurlEventPublisher events, long maxKeyCount, long maxByteCount)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(events);
        this.timeProvider = timeProvider;
        this.events = events;
        this.maxKeyCount = maxKeyCount;
        this.maxByteCount = maxByteCount;
        databases = [.. Enumerable.Range(0, RedisSession.DatabaseCount).Select(index => new RedisDatabase(this, index))];
    }

    /// <summary>How many keys the databases hold, expired ones not yet removed included.</summary>
    public long KeyCount { get; private set; }

    /// <summary>How many key and value bytes the databases hold.</summary>
    public long ByteCount { get; private set; }

    /// <summary>Held while a command reads or changes the keyspace, so each command is atomic.</summary>
    internal Lock SyncRoot { get; } = new();

    /// <summary>The changes the command now running has made, published when it has answered (ADR-0158 decision 2).</summary>
    internal RedisKeyspaceJournal Journal { get; } = new();

    /// <summary>
    /// Whether a bound is reached, so a command that would grow the keyspace is
    /// <c>-OOM command not allowed when used memory &gt; 'maxmemory'.</c> (ADR-0106 decision 6).
    /// </summary>
    internal bool IsFull => KeyCount >= maxKeyCount || ByteCount >= maxByteCount;

    /// <summary>The database numbered <paramref name="index"/>, 0 to 15.</summary>
    /// <param name="index">The database number.</param>
    internal RedisDatabase this[int index] => databases[index];

    /// <summary>
    /// Seeds one string key as <c>SET</c> writes it - replacing a key that exists and clearing its
    /// expiry - but never consulting the fault plan, then publishes <see cref="StoreChange.Seeded"/>
    /// naming <c>&lt;database&gt;/&lt;key&gt;</c>. A key found already expired is removed first and
    /// published as <see cref="StoreChange.Deleted"/>, also when the seed is then refused. A seed
    /// is lost by <see cref="Reset"/> unless <paramref name="keepOnReset"/> is set (ADR-0158
    /// decision 4).
    /// </summary>
    /// <param name="database">The database, 0 to 15.</param>
    /// <param name="key">The key, at least 1 byte.</param>
    /// <param name="value">The value.</param>
    /// <param name="expiry">How long the key lives, measured on the keyspace's clock from now, or
    /// <see langword="null"/> for a key that never expires; 1 millisecond to <see cref="MaxSeedExpiry"/>.</param>
    /// <param name="keepOnReset">Whether <see cref="Reset"/> writes this key back too, with this value and
    /// <paramref name="expiry"/> measured from the reset. It replaces an earlier kept seed of the same
    /// database and key.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="database"/> or <paramref name="expiry"/> is out of range.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The key and value together hold more than
    /// <see cref="MaxSeedByteCount"/> bytes, the keyspace is full, or <paramref name="keepOnReset"/> is set
    /// and the kept keys have reached the keyspace's bounds; nothing is written or kept.</exception>
    public void SeedKey(int database, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value, TimeSpan? expiry = null, bool keepOnReset = false)
    {
        ThrowIfSeedOutOfRange(database, key, value, expiry);
        var seed = new RedisKeptSeed(database, RedisKeys.Name(key.ToArray()), new RedisStringValue(value.ToArray()), expiry);
        var nowMilliseconds = timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        lock (SyncRoot)
        {
            ThrowIfNoRoomToSeed(nowMilliseconds, keepOnReset);
            // Finding the key removes it if it has expired, so its removal is published before the write.
            databases[database].Find(seed.Key, nowMilliseconds);
            PublishDeletionsOfExpiredKeys();
            Write(seed, nowMilliseconds);
            Journal.Drain();
            if (keepOnReset)
            {
                keptSeeds.Keep(seed);
            }

            Publish(StoreChange.Seeded, RedisKeyspaceChange.NameOf(database, seed.Key), value.Length);
        }
    }

    /// <summary>
    /// Reads one live key back as a test does (ADR-0164 decision 9): publishes nothing, asks no fault rule, removes
    /// no expired key and journals nothing.
    /// </summary>
    /// <param name="database">The database, 0 to 15.</param>
    /// <param name="key">The key's bytes.</param>
    /// <returns>The key with its string value (none for another type), or <see langword="null"/> when absent or expired.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="database"/> is outside 0 to 15.</exception>
    public RedisKeyContents? ReadKey(int database, ReadOnlyMemory<byte> key)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(database);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(database, RedisSession.DatabaseCount);
        var nowMilliseconds = timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        lock (SyncRoot)
        {
            var entry = databases[database].Peek(RedisKeys.Name(key.ToArray()), nowMilliseconds);
            return entry is null
                ? null
                : new RedisKeyContents(InfoOf(entry), entry.Value is RedisStringValue text ? new ReadOnlyMemory<byte>((byte[])text.Bytes.Clone()) : (ReadOnlyMemory<byte>?)null);
        }
    }

    /// <summary>
    /// Lists live keys matching a <c>KEYS</c> glob in ordinal order of their bytes, one page at a time
    /// (ADR-0164 decision 9): publishes nothing, asks no fault rule and reads no value.
    /// </summary>
    /// <param name="database">The database, 0 to 15.</param>
    /// <param name="pattern">The glob, one character per byte (Latin-1); <c>*</c> lists every key.</param>
    /// <param name="after">The last key seen; keys ordered after it are listed. Empty starts at the first.</param>
    /// <param name="limit">The most keys to return, at least 1.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="database"/> is outside 0 to 15 or <paramref name="limit"/> is below 1.</exception>
    public RedisKeyInfoPage ListKeys(int database, string pattern, ReadOnlyMemory<byte> after, int limit)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentOutOfRangeException.ThrowIfNegative(database);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(database, RedisSession.DatabaseCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        var nowMilliseconds = timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        var afterName = RedisKeys.Name(after.ToArray());
        lock (SyncRoot)
        {
            var matching = databases[database].PeekLive(nowMilliseconds)
                .Where(entry => string.CompareOrdinal(entry.Key, afterName) > 0 && RedisGlobPattern.IsMatch(pattern, entry.Key))
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Take(limit + 1)
                .ToList();
            return new RedisKeyInfoPage([.. matching.Take(limit).Select(InfoOf)], matching.Count > limit);
        }
    }

    private RedisKeyInfo InfoOf(RedisKeyEntry entry) => new(
        RedisKeys.Bytes(entry.Key),
        entry.Value.TypeName,
        entry.Value.ByteCount,
        entry.ExpiresAtMilliseconds is { } deadline ? DateTimeOffset.FromUnixTimeMilliseconds(deadline) : null);

    /// <summary>
    /// Returns every database to the configured state - empty but for the keys
    /// <see cref="SeedKey"/> was told to keep, each with its seeded value and its expiry measured
    /// again from now - and publishes one <see cref="StoreChange.Reset"/> with an empty name and
    /// 0 bytes, and nothing for the keys written back. Connections stay and keep their selected
    /// database.
    /// </summary>
    public void Reset()
    {
        var nowMilliseconds = timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        lock (SyncRoot)
        {
            ReplaceAll(Enumerable.Repeat(RedisDatabaseSnapshot.Empty, databases.Length));
            foreach (var seed in keptSeeds.InOrderFirstKept)
            {
                // The reset is the one change published, so what each write journals is dropped at once,
                // which also keeps the journal's search of its entries from growing with every key.
                Write(seed, nowMilliseconds);
                Journal.Drain();
            }

            Publish(StoreChange.Reset, string.Empty, 0);
        }
    }

    /// <summary>
    /// Copies every live key of every database - its value and its expiry as an absolute deadline - into
    /// a <see cref="RedisKeyspaceSnapshot"/>. Changes nothing and publishes nothing.
    /// </summary>
    /// <param name="maxBytes">The most key and value bytes the snapshot may hold; at least 0.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxBytes"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">The keyspace holds more than <paramref name="maxBytes"/>;
    /// nothing is copied, never a truncated snapshot.</exception>
    public RedisKeyspaceSnapshot TakeSnapshot(long maxBytes = DefaultMaxSnapshotBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);
        var nowMilliseconds = timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        lock (SyncRoot)
        {
            var copies = databases.Select(database => database.Export(nowMilliseconds)).ToList();
            var liveBytes = copies.Sum(copy => copy.Entries.Sum(entry => entry.Key.Length + entry.Value.ByteCount));
            if (liveBytes > maxBytes)
            {
                throw new InvalidOperationException($"The Redis keyspace holds {liveBytes} bytes, more than the {maxBytes} a snapshot may hold.");
            }
            return new RedisKeyspaceSnapshot(this, liveBytes, copies);
        }
    }

    /// <summary>
    /// Replaces every database with <paramref name="snapshot"/>'s keys, values and expiry deadlines and
    /// publishes one <see cref="StoreChange.Restored"/> with an empty name and the restored bytes. A key
    /// restored after its deadline has passed expires when next touched.
    /// </summary>
    /// <param name="snapshot">A snapshot this keyspace took.</param>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="snapshot"/> was taken from another keyspace.</exception>
    public void Restore(RedisKeyspaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Keyspace != this)
        {
            throw new ArgumentException("The snapshot was taken from another Redis keyspace.", nameof(snapshot));
        }

        lock (SyncRoot)
        {
            ReplaceAll(snapshot.Databases);
            Publish(StoreChange.Restored, string.Empty, snapshot.ByteCount);
        }
    }

    /// <summary>
    /// Whether a bound is reached by live keys: when <see cref="IsFull"/>, expired keys not yet
    /// removed are removed from every database first, as they are already absent.
    /// </summary>
    /// <param name="nowMilliseconds">The time, in Unix milliseconds.</param>
    /// <returns>Whether a command that would grow the keyspace is refused.</returns>
    internal bool IsFullOfLiveKeys(long nowMilliseconds)
    {
        if (!IsFull)
        {
            return false;
        }

        foreach (var database in databases)
        {
            database.RemoveExpired(nowMilliseconds);
        }

        return IsFull;
    }

    /// <summary>Deletes every key in every database (<c>FLUSHALL</c>).</summary>
    internal void Clear()
    {
        foreach (var database in databases)
        {
            database.Clear();
        }
    }

    /// <summary>Adds <paramref name="keys"/> and <paramref name="bytes"/>, either negative, to the counts.</summary>
    /// <param name="keys">The change in keys.</param>
    /// <param name="bytes">The change in bytes.</param>
    internal void Count(long keys, long bytes)
    {
        KeyCount += keys;
        ByteCount += bytes;
    }

    private static void ThrowIfSeedOutOfRange(int database, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value, TimeSpan? expiry)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(database);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(database, RedisSession.DatabaseCount);
        if (key.IsEmpty)
        {
            throw new ArgumentException("A key has at least 1 byte.", nameof(key));
        }

        if (expiry is { } lifetime)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(lifetime, TimeSpan.FromMilliseconds(1), nameof(expiry));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(lifetime, MaxSeedExpiry, nameof(expiry));
        }

        if ((long)key.Length + value.Length > MaxSeedByteCount)
        {
            throw new InvalidOperationException($"A seeded key and value hold at most {MaxSeedByteCount} bytes together.");
        }
    }

    private void ThrowIfNoRoomToSeed(long nowMilliseconds, bool keepOnReset)
    {
        if (IsFullOfLiveKeys(nowMilliseconds))
        {
            PublishDeletionsOfExpiredKeys();
            throw new InvalidOperationException("The Redis keyspace is full, so the key was not seeded.");
        }

        if (keepOnReset && keptSeeds.HaveReached(maxKeyCount, maxByteCount))
        {
            PublishDeletionsOfExpiredKeys();
            throw new InvalidOperationException("The keys a Redis keyspace reset writes back are at the keyspace's bounds, so the key was not seeded.");
        }
    }

    /// <summary>Writes <paramref name="seed"/> to its database, its lifetime measured from <paramref name="nowMilliseconds"/>.</summary>
    private void Write(RedisKeptSeed seed, long nowMilliseconds)
    {
        var entry = databases[seed.Database].Put(seed.Key, seed.Value, nowMilliseconds);
        entry.ExpiresAtMilliseconds = seed.Lifetime is { } lifetime ? nowMilliseconds + (long)lifetime.TotalMilliseconds : null;
    }

    /// <summary>Publishes the journalled removals of expired keys. Called while the journal holds nothing else.</summary>
    private void PublishDeletionsOfExpiredKeys()
    {
        foreach (var change in Journal.Drain())
        {
            Publish(change.Change, change.Name, null);
        }
    }

    private void ReplaceAll(IEnumerable<RedisDatabaseSnapshot> contents)
    {
        foreach (var (database, content) in databases.Zip(contents))
        {
            database.Replace(content);
        }
    }

    private void Publish(StoreChange change, string name, long? byteCount) =>
        events.Publish(new StoreChangedEvent(StoreKind.Keyspace, change, name, byteCount, null));
}
