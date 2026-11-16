namespace Surl.Extended.Protocol.Redis;

/// <summary>One live key as a test lists it back (ADR-0164 decision 9): its name, type and size, never its value.</summary>
/// <param name="Key">The key's bytes, as a client or a seed wrote them.</param>
/// <param name="Type">The Redis type name: <c>string</c>, <c>list</c>, <c>hash</c> or <c>set</c>.</param>
/// <param name="ByteCount">The bytes the value holds.</param>
/// <param name="ExpiresAt">When the key expires, or <see langword="null"/> when it never does.</param>
public sealed record RedisKeyInfo(ReadOnlyMemory<byte> Key, string Type, long ByteCount, DateTimeOffset? ExpiresAt);
