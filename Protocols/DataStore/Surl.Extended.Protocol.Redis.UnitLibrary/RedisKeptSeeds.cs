namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The keys a keyspace reset writes back (ADR-0158 decision 4): one per database and key, in the
/// order each was first kept, held apart from the databases so no command changes them. They are
/// held to the keyspace's own bounds (ADR-0158 amendment 4). Callers hold
/// <see cref="RedisKeyspace.SyncRoot"/>.
/// </summary>
internal sealed class RedisKeptSeeds
{
    private readonly OrderedDictionary<(int Database, string Key), RedisKeptSeed> seeds = [];
    private long byteCount;

    /// <summary>The kept keys, in the order each was first kept.</summary>
    public IEnumerable<RedisKeptSeed> InOrderFirstKept => seeds.Values;

    /// <summary>Whether the kept keys have reached a bound, so no further key can be kept.</summary>
    /// <param name="maxKeyCount">The most keys.</param>
    /// <param name="maxByteCount">The most key and value bytes.</param>
    /// <returns>Whether either bound is reached.</returns>
    public bool HaveReached(long maxKeyCount, long maxByteCount) => seeds.Count >= maxKeyCount || byteCount >= maxByteCount;

    /// <summary>Keeps <paramref name="seed"/>, replacing an earlier one of the same database and key.</summary>
    /// <param name="seed">The key to write back.</param>
    public void Keep(RedisKeptSeed seed)
    {
        var identity = (seed.Database, seed.Key);
        if (seeds.TryGetValue(identity, out var earlier))
        {
            byteCount -= ByteCountOf(earlier);
        }

        seeds[identity] = seed;
        byteCount += ByteCountOf(seed);
    }

    private static long ByteCountOf(RedisKeptSeed seed) => seed.Key.Length + seed.Value.ByteCount;
}
