namespace Surl.Control;

/// <summary>One Redis key read back with its string value.</summary>
/// <param name="Info">The key's name, type, size and expiry.</param>
/// <param name="Value">The string's bytes, or <see langword="null"/> when the key holds another type, which is listed and not read.</param>
public sealed record ControlKeyContents(ControlKeyInfo Info, ReadOnlyMemory<byte>? Value);
