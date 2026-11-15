namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// An opaque, in-memory deep copy of one <see cref="RedisKeyspace"/>'s 16 databases (ADR-0158
/// decision 5): every live key with its value and its expiry as an absolute deadline on the
/// keyspace's clock. Made by <see cref="RedisKeyspace.TakeSnapshot(long)"/> and given back by
/// <see cref="RedisKeyspace.Restore(RedisKeyspaceSnapshot)"/> to the keyspace that made it, and to
/// no other.
/// </summary>
public sealed class RedisKeyspaceSnapshot
{
    internal RedisKeyspaceSnapshot(RedisKeyspace keyspace, long byteCount, IReadOnlyList<RedisDatabaseSnapshot> databases)
    {
        Keyspace = keyspace;
        ByteCount = byteCount;
        Databases = databases;
    }

    /// <summary>The key and value bytes the snapshot holds, counted as the keyspace counts them.</summary>
    public long ByteCount { get; }

    /// <summary>The keyspace that made it, the only one that restores it.</summary>
    internal RedisKeyspace Keyspace { get; }

    internal IReadOnlyList<RedisDatabaseSnapshot> Databases { get; }
}
