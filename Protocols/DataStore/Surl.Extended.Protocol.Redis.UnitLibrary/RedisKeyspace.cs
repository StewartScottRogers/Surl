namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The server's 16 in-memory databases (ADR-0106 decision 3), shared by every connection and by
/// <c>redis</c> and <c>rediss</c>, and never written to disk: a new server starts empty. It counts
/// keys and key and value bytes across all 16 against ADR-0106 decision 6's bounds.
/// </summary>
internal sealed class RedisKeyspace
{
    /// <summary>The most keys across the 16 databases (ADR-0106 decision 6).</summary>
    public const long DefaultMaxKeyCount = 1_000_000;

    /// <summary>The most key and value bytes across the 16 databases: 256 MiB (ADR-0106 decision 6).</summary>
    public const long DefaultMaxByteCount = 256L * 1024 * 1024;

    private readonly RedisDatabase[] databases;
    private readonly long maxKeyCount;
    private readonly long maxByteCount;

    /// <summary>Creates an empty keyspace with ADR-0106 decision 6's bounds.</summary>
    public RedisKeyspace()
        : this(DefaultMaxKeyCount, DefaultMaxByteCount)
    {
    }

    /// <summary>Creates an empty keyspace with the given bounds.</summary>
    /// <param name="maxKeyCount">The most keys across the databases.</param>
    /// <param name="maxByteCount">The most key and value bytes across the databases.</param>
    public RedisKeyspace(long maxKeyCount, long maxByteCount)
    {
        this.maxKeyCount = maxKeyCount;
        this.maxByteCount = maxByteCount;
        databases = [.. Enumerable.Range(0, RedisSession.DatabaseCount).Select(_ => new RedisDatabase(this))];
    }

    /// <summary>Held while a command reads or changes the keyspace, so each command is atomic.</summary>
    public Lock SyncRoot { get; } = new();

    /// <summary>How many keys the databases hold, expired ones not yet removed included.</summary>
    public long KeyCount { get; private set; }

    /// <summary>How many key and value bytes the databases hold.</summary>
    public long ByteCount { get; private set; }

    /// <summary>
    /// Whether a bound is reached, so a command that would grow the keyspace is
    /// <c>-OOM command not allowed when used memory &gt; 'maxmemory'.</c> (ADR-0106 decision 6).
    /// </summary>
    public bool IsFull => KeyCount >= maxKeyCount || ByteCount >= maxByteCount;

    /// <summary>
    /// Whether a bound is reached by live keys: when <see cref="IsFull"/>, expired keys not yet
    /// removed are removed from every database first, as they are already absent.
    /// </summary>
    /// <param name="nowMilliseconds">The time, in Unix milliseconds.</param>
    /// <returns>Whether a command that would grow the keyspace is refused.</returns>
    public bool IsFullOfLiveKeys(long nowMilliseconds)
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

    /// <summary>The database numbered <paramref name="index"/>, 0 to 15.</summary>
    /// <param name="index">The database number.</param>
    public RedisDatabase this[int index] => databases[index];

    /// <summary>Deletes every key in every database (<c>FLUSHALL</c>).</summary>
    public void Clear()
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
}
