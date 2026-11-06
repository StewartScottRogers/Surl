namespace Surl.Extended.Protocol.Redis;

/// <summary>One key in a <see cref="RedisDatabase"/>: its value, expiry and <c>SCAN</c> sequence number.</summary>
/// <param name="key">The key, one character per byte (Latin-1).</param>
/// <param name="sequenceNumber">The number <c>SCAN</c>'s cursor counts by, from 1 in each database.</param>
/// <param name="value">The value.</param>
internal sealed class RedisKeyEntry(string key, long sequenceNumber, RedisStoredValue value)
{
    /// <summary>The key, one character per byte (Latin-1).</summary>
    public string Key { get; } = key;

    /// <summary>The number <c>SCAN</c>'s cursor counts by, given when the key was created.</summary>
    public long SequenceNumber { get; } = sequenceNumber;

    /// <summary>The value; only <see cref="RedisDatabase.Put"/> replaces it, so the byte count stays exact.</summary>
    public RedisStoredValue Value { get; internal set; } = value;

    /// <summary>When the key expires, in Unix milliseconds, or <see langword="null"/> when it does not.</summary>
    public long? ExpiresAtMilliseconds { get; set; }

    /// <summary>Whether the key is absent at <paramref name="nowMilliseconds"/>: its expiry has come.</summary>
    /// <param name="nowMilliseconds">The time, in Unix milliseconds.</param>
    /// <returns>Whether the key has expired.</returns>
    public bool HasExpiredAt(long nowMilliseconds) => ExpiresAtMilliseconds <= nowMilliseconds;
}
