namespace Surl.Control;

/// <summary>A page of Redis keys in ordinal order of their bytes.</summary>
/// <param name="Keys">The keys, without values.</param>
/// <param name="HasMore">Whether more matching keys follow the last one.</param>
public sealed record ControlKeyPage(IReadOnlyList<ControlKeyInfo> Keys, bool HasMore);
