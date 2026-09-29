namespace Surl.Protocol.Abstractions;

/// <summary>
/// What an <see cref="IHttpAuthenticationSession"/> judges of one HTTP request, after its head
/// has been read and found well-formed (ADR-0032, sections 4 and 6). The body is not included:
/// no offered method needs it.
/// </summary>
/// <param name="Method">The request method.</param>
/// <param name="Target">The request target as received.</param>
/// <param name="IsWrite"><see langword="true"/> for every method but <c>GET</c> and <c>HEAD</c>.</param>
/// <param name="Fields">Every header field, name and value, in the order received.</param>
public sealed record HttpAuthenticationRequest(
    string Method,
    string Target,
    bool IsWrite,
    IReadOnlyList<KeyValuePair<string, string>> Fields);
