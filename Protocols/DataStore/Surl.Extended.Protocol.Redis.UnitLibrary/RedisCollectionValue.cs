namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// A hash, list or set value (ADR-0106 decision 1). Unlike a string, a collection is changed in
/// place, keeping its own byte count; the command that changes it then calls
/// <see cref="RedisDatabase.Recount"/>, which recounts the keyspace and removes the key once the
/// collection is empty, as each command page says.
/// </summary>
internal abstract class RedisCollectionValue : RedisStoredValue
{
    private long byteCount;

    /// <summary>How many fields, elements or members it holds.</summary>
    public abstract int Count { get; }

    /// <inheritdoc/>
    public sealed override long ByteCount => byteCount;

    /// <summary>Adds <paramref name="bytes"/>, which may be negative, to <see cref="ByteCount"/>.</summary>
    /// <param name="bytes">The change in bytes.</param>
    protected void CountBytes(long bytes) => byteCount += bytes;
}
