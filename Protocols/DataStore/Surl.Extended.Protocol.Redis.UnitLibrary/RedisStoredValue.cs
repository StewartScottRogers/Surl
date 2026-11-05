namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The value a key holds, of one Redis type (ADR-0106 decision 1). A string is never changed in
/// place: a command that changes one stores a new one. A hash, list or set is a
/// <see cref="RedisCollectionValue"/>, changed in place and then recounted. Either way the
/// keyspace's byte count stays exact.
/// </summary>
internal abstract class RedisStoredValue
{
    /// <summary>The type's name, as <c>TYPE</c> answers it and <c>SCAN … TYPE</c> filters by.</summary>
    public abstract string TypeName { get; }

    /// <summary>The value's bytes, counted against the keyspace's 256 MiB (ADR-0106 decision 6).</summary>
    public abstract long ByteCount { get; }
}
