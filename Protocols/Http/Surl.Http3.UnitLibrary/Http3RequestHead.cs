namespace Surl.Http3;

/// <summary>
/// A well-formed HTTP/3 request's header section, mapped as RFC 9114 section 4.3 says: what a
/// request handler needs of it, before its body.
/// </summary>
/// <param name="Method">The <c>:method</c>.</param>
/// <param name="Target">The <c>:path</c>; for <c>CONNECT</c>, the <c>:authority</c>.</param>
/// <param name="Authority">The <c>:authority</c>, else the one <c>host</c> field, else <see langword="null"/>.</param>
/// <param name="Fields">The regular fields, lowercase names, in the order received.</param>
/// <param name="ContentLength">The <c>content-length</c>, or <see langword="null"/> when none was sent.</param>
/// <param name="ExpectsContinue">Whether an <c>expect</c> field asks for <c>100-continue</c>.</param>
public sealed record Http3RequestHead(
    string Method,
    string Target,
    string? Authority,
    IReadOnlyList<KeyValuePair<string, string>> Fields,
    long? ContentLength,
    bool ExpectsContinue);
