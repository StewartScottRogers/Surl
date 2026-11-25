namespace Surl.Cookies;

/// <summary>One <c>name=value</c> pair a client sent in a <c>Cookie</c> header.</summary>
/// <param name="Name">The name, never empty.</param>
/// <param name="Value">The value, possibly empty.</param>
public sealed record CookiePair(string Name, string Value);
