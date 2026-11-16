namespace Surl.Extended.Protocol.Redis;

/// <summary>One live key read back with its string value (ADR-0164 decision 9).</summary>
/// <param name="Info">The key's name, type, size and expiry.</param>
/// <param name="Value">A copy of the string's bytes, or <see langword="null"/> when the key holds another type, which is listed and not read.</param>
public sealed record RedisKeyContents(RedisKeyInfo Info, ReadOnlyMemory<byte>? Value);
