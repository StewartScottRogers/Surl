namespace Surl.Protocol.Http;

/// <summary>
/// A well-formed HTTP/2 request's header section, mapped as RFC 9113 section 8.3 says: what an
/// <see cref="Surl.HttpServing.HttpRequest"/> needs of it, before its body.
/// </summary>
/// <param name="Method">The <c>:method</c>.</param>
/// <param name="Target">The <c>:path</c>; for <c>CONNECT</c>, the <c>:authority</c>.</param>
/// <param name="Authority">The <c>:authority</c>, else the one <c>host</c> field, else <see langword="null"/>.</param>
/// <param name="Fields">The regular fields, lowercase names, in the order received.</param>
/// <param name="ContentLength">The <c>content-length</c>, or <see langword="null"/> when none was sent.</param>
/// <param name="ExpectsContinue">Whether an <c>expect</c> field asks for <c>100-continue</c>.</param>
internal sealed record Http2RequestHead(
    string Method,
    string Target,
    string? Authority,
    IReadOnlyList<KeyValuePair<string, string>> Fields,
    long? ContentLength,
    bool ExpectsContinue);
