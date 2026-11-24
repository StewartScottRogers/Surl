namespace Surl.Cookies;

/// <summary>
/// One cookie the HTTP server sets: its name and value, the attributes upstream curl parses, and the two scoping keys that
/// pick which replies carry it (ADR-0189 decisions 1 to 3; https://curl.se/docs/http-cookies.html, curl 8.21.0).
/// </summary>
/// <param name="Name">The cookie name; never empty, no control character, space, <c>;</c>, <c>,</c> or <c>=</c>.</param>
/// <param name="Value">The cookie value as the test wrote it; no control character.</param>
/// <param name="Domain">The <c>Domain</c> attribute, or <see langword="null"/> to write none.</param>
/// <param name="Path">The <c>Path</c> attribute, or <see langword="null"/> to write none.</param>
/// <param name="Expires">The <c>Expires</c> attribute, an IMF-fixdate written as given, or <see langword="null"/>.</param>
/// <param name="MaxAge">The <c>Max-Age</c> attribute in seconds (never negative), or <see langword="null"/>.</param>
/// <param name="Secure">Whether to write <c>Secure</c>.</param>
/// <param name="HttpOnly">Whether to write <c>HttpOnly</c>.</param>
/// <param name="SameSite"><c>Strict</c>, <c>Lax</c> or <c>None</c>, or <see langword="null"/> to write none.</param>
/// <param name="OnPath">The request-path prefix the cookie is limited to, or <see langword="null"/> for every path.</param>
/// <param name="OnListener">The listen URL the cookie is limited to, or <see langword="null"/> for every listener.</param>
public sealed record CookieDefinition(
    string Name,
    string Value,
    string? Domain = null,
    string? Path = null,
    string? Expires = null,
    long? MaxAge = null,
    bool Secure = false,
    bool HttpOnly = false,
    string? SameSite = null,
    string? OnPath = null,
    string? OnListener = null);
