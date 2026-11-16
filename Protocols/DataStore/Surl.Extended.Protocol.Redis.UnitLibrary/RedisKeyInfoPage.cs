namespace Surl.Extended.Protocol.Redis;

/// <summary>A page of live keys in ordinal order of their bytes (ADR-0164 decision 9).</summary>
/// <param name="Keys">At most the limit asked for, each after the cursor.</param>
/// <param name="HasMore">Whether matching keys beyond these remain.</param>
public sealed record RedisKeyInfoPage(IReadOnlyList<RedisKeyInfo> Keys, bool HasMore);
