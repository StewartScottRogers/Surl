namespace Surl.Http2;

/// <summary>
/// A well-formed HTTP/2 request's header section, mapped as RFC 9113 section 8.3 says: what an
/// request handler needs of it, before its body.
/// </summary>
/// <param name="Method">The <c>:method</c>.</param>
/// <param name="Target">The <c>:path</c>; for <c>CONNECT</c>, the <c>:authority</c>.</param>
/// <param name="Authority">The <c>:authority</c>, else the one <c>host</c> field, else <see langword="null"/>.</param>
/// <param name="Fields">The regular fields, lowercase names, in the order received.</param>
/// <param name="ContentLength">The <c>content-length</c>, or <see langword="null"/> when none was sent.</param>
/// <param name="ExpectsContinue">Whether an <c>expect</c> field asks for <c>100-continue</c>.</param>
public sealed record Http2RequestHead(
    string Method,
    string Target,
    string? Authority,
    IReadOnlyList<KeyValuePair<string, string>> Fields,
    long? ContentLength,
    bool ExpectsContinue)
{
    /// <summary>
    /// Gets the <c>:scheme</c> a proxy forwards the request by (RFC 9113 section 8.3.1), or
    /// <see langword="null"/> for a <c>CONNECT</c>, which carries none (section 8.5).
    /// </summary>
    public string? Scheme { get; init; }
}
