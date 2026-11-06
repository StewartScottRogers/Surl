namespace Surl.HttpServing;

/// <summary>
/// A request as an <see cref="IHttpRequestHandler"/> answers it, whichever HTTP version carried
/// it: its method, target, authority, fields and body (ADR-0081, decision 8; ADR-0084, decision 1).
/// </summary>
/// <param name="Method">The method, such as <c>GET</c>.</param>
/// <param name="Target">The request target as the client sent it, which names the path and query.</param>
/// <param name="Authority">The host the request names, or <see langword="null"/> when it names none.</param>
/// <param name="Fields">The request's fields, in the order received.</param>
/// <param name="Body">The request's body, which the handler may read or leave unread.</param>
/// <param name="Http1Version">
/// The version on an HTTP/1.x request line, or <see langword="null"/> for a request another HTTP
/// version carried. An answerer never reads it; only a handler deciding an upgrade does, because
/// RFC 9110 section 7.8 has an HTTP/1.0 request's <c>Upgrade</c> ignored (ADR-0091).
/// </param>
public sealed record HttpRequest(
    string Method,
    string Target,
    string? Authority,
    IReadOnlyList<KeyValuePair<string, string>> Fields,
    IHttpRequestBody Body,
    Version? Http1Version = null);
