namespace Surl.Control;

/// <summary>One live Redis key as the control endpoint lists it: name, type and size, never the value.</summary>
/// <param name="Key">The key's bytes.</param>
/// <param name="Type">The Redis type name: <c>string</c>, <c>list</c>, <c>hash</c> or <c>set</c>.</param>
/// <param name="ByteCount">The bytes the value holds.</param>
/// <param name="ExpiresAt">When the key expires, or <see langword="null"/> when it never does.</param>
public sealed record ControlKeyInfo(ReadOnlyMemory<byte> Key, string Type, long ByteCount, DateTimeOffset? ExpiresAt);
