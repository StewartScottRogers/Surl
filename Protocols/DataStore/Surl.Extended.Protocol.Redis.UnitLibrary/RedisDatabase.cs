namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// One of the keyspace's 16 databases (ADR-0106 decision 3): its keys in creation order. A key
/// whose expiry has come is absent from that instant and removed when next touched, so nothing
/// sweeps in the background (ADR-0106 decision 1). Callers hold <see cref="RedisKeyspace.SyncRoot"/>.
/// </summary>
/// <param name="keyspace">The keyspace, which counts every key and byte against its bounds.</param>
internal sealed class RedisDatabase(RedisKeyspace keyspace)
{
    private readonly Dictionary<string, RedisKeyEntry> entries = new(StringComparer.Ordinal);
    private readonly SortedDictionary<long, RedisKeyEntry> entriesBySequenceNumber = [];
    private long lastSequenceNumber;

    /// <summary>The live key named <paramref name="key"/>, or <see langword="null"/>; an expired one is removed.</summary>
    /// <param name="key">The key.</param>
    /// <param name="nowMilliseconds">The time, in Unix milliseconds.</param>
    /// <returns>The key's entry, or <see langword="null"/> when absent.</returns>
    public RedisKeyEntry? Find(string key, long nowMilliseconds)
    {
        if (!entries.TryGetValue(key, out var entry))
        {
            return null;
        }

        if (!entry.HasExpiredAt(nowMilliseconds))
        {
            return entry;
        }

        Remove(entry);
        return null;
    }

    /// <summary>
    /// Stores <paramref name="value"/> at <paramref name="key"/>: a live key keeps its expiry and
    /// sequence number, an absent one is created without expiry and numbered next.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <param name="nowMilliseconds">The time, in Unix milliseconds.</param>
    /// <returns>The key's entry.</returns>
    public RedisKeyEntry Put(string key, RedisStoredValue value, long nowMilliseconds)
    {
        var entry = Find(key, nowMilliseconds);
        if (entry is not null)
        {
            keyspace.Count(0, value.ByteCount - entry.Value.ByteCount);
            entry.Value = value;
            return entry;
        }

        entry = new RedisKeyEntry(key, ++lastSequenceNumber, value);
        entries.Add(key, entry);
        entriesBySequenceNumber.Add(entry.SequenceNumber, entry);
        keyspace.Count(1, ByteCountOf(entry));
        return entry;
    }

    /// <summary>Deletes the live key named <paramref name="key"/>.</summary>
    /// <param name="key">The key.</param>
    /// <param name="nowMilliseconds">The time, in Unix milliseconds.</param>
    /// <returns>Whether a live key was deleted.</returns>
    public bool Delete(string key, long nowMilliseconds)
    {
        var entry = Find(key, nowMilliseconds);
        if (entry is null)
        {
            return false;
        }

        Remove(entry);
        return true;
    }

    /// <summary>Every live key, in creation order (ADR-0106 decision 1).</summary>
    /// <param name="nowMilliseconds">The time, in Unix milliseconds.</param>
    /// <returns>The entries.</returns>
    public IReadOnlyList<RedisKeyEntry> LiveEntries(long nowMilliseconds)
    {
        RemoveExpired(nowMilliseconds);
        return [.. entriesBySequenceNumber.Values];
    }

    /// <summary>
    /// One <c>SCAN</c> step (ADR-0106 decision 1): up to <paramref name="count"/> live keys whose
    /// sequence number is at or above <paramref name="cursor"/>, in number order, and the next
    /// cursor - the last number returned plus one, or 0 once no key is left.
    /// </summary>
    /// <param name="cursor">The cursor the client sent.</param>
    /// <param name="count">How many keys at most, at least 1.</param>
    /// <param name="nowMilliseconds">The time, in Unix milliseconds.</param>
    /// <returns>The keys and the next cursor.</returns>
    public (IReadOnlyList<RedisKeyEntry> Entries, ulong NextCursor) Scan(ulong cursor, long count, long nowMilliseconds)
    {
        RemoveExpired(nowMilliseconds);
        var taken = entriesBySequenceNumber.Values
            .SkipWhile(entry => (ulong)entry.SequenceNumber < cursor)
            .Take((int)Math.Min(count, int.MaxValue - 1) + 1)
            .ToList();
        return taken.Count <= count
            ? (taken, 0)
            : (taken[..^1], (ulong)taken[^2].SequenceNumber + 1);
    }

    /// <summary>How many live keys the database holds (<c>DBSIZE</c>).</summary>
    /// <param name="nowMilliseconds">The time, in Unix milliseconds.</param>
    /// <returns>The count.</returns>
    public int CountKeys(long nowMilliseconds)
    {
        RemoveExpired(nowMilliseconds);
        return entries.Count;
    }

    /// <summary>Deletes every key (<c>FLUSHDB</c>).</summary>
    public void Clear()
    {
        foreach (var entry in entries.Values.ToList())
        {
            Remove(entry);
        }
    }

    private static long ByteCountOf(RedisKeyEntry entry) => entry.Key.Length + entry.Value.ByteCount;

    /// <summary>Removes every key whose expiry has come.</summary>
    /// <param name="nowMilliseconds">The time, in Unix milliseconds.</param>
    public void RemoveExpired(long nowMilliseconds)
    {
        foreach (var entry in entries.Values.Where(entry => entry.HasExpiredAt(nowMilliseconds)).ToList())
        {
            Remove(entry);
        }
    }

    private void Remove(RedisKeyEntry entry)
    {
        entries.Remove(entry.Key);
        entriesBySequenceNumber.Remove(entry.SequenceNumber);
        keyspace.Count(-1, -ByteCountOf(entry));
    }
}
