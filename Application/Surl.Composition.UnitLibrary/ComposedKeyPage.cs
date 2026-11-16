using Surl.Extended.Protocol.Redis;

namespace Surl.Composition;

/// <summary>A page of Redis keys in ordinal order of their bytes, listed without their values (ADR-0164 decision 9).</summary>
/// <param name="Keys">At most the limit asked for, each after the cursor.</param>
/// <param name="HasMore">Whether matching keys beyond these remain.</param>
public sealed record ComposedKeyPage(IReadOnlyList<RedisKeyInfo> Keys, bool HasMore);
