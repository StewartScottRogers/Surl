namespace Surl.Api;

/// <summary>One page of Redis keys in ordinal order of their bytes.</summary>
/// <param name="Keys">The keys on the page, without their values.</param>
/// <param name="HasMore">Whether matching keys follow.</param>
public sealed record SurlKeyPage(IReadOnlyList<SurlKeyInfo> Keys, bool HasMore);
