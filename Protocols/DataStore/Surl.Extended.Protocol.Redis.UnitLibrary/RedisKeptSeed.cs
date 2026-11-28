namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// One string key a keyspace reset writes back (ADR-0158 decision 4). It holds how long the key
/// lives, not when it expires, so the deadline is measured again from each reset.
/// </summary>
/// <param name="Database">The database, 0 to 15.</param>
/// <param name="Key">The key's name.</param>
/// <param name="Value">The value as seeded; a string value is never changed in place.</param>
/// <param name="Lifetime">How long the key lives from a reset, or <see langword="null"/> for a key that never expires.</param>
internal sealed record RedisKeptSeed(int Database, string Key, RedisStringValue Value, TimeSpan? Lifetime);
