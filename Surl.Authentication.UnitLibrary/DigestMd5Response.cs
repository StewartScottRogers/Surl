namespace Surl.Authentication;

/// <summary>
/// A SASL <c>DIGEST-MD5</c> response's directives (RFC 2831, section 2.1.2), each as received,
/// one character per byte (ADR-0049, section 5).
/// </summary>
/// <param name="UserName">The <c>username</c>.</param>
/// <param name="Realm">The <c>realm</c>; empty when it was left out, as RFC 2831 hashes it.</param>
/// <param name="Nonce">The <c>nonce</c>.</param>
/// <param name="ClientNonce">The <c>cnonce</c>.</param>
/// <param name="NonceCount">The <c>nc</c>.</param>
/// <param name="Qop">The <c>qop</c>.</param>
/// <param name="DigestUri">The <c>digest-uri</c>.</param>
/// <param name="Response">The <c>response</c>, 32 hex digits when well formed.</param>
/// <param name="AuthorizationId">The <c>authzid</c>, or <see langword="null"/> when it was left out.</param>
internal sealed record DigestMd5Response(
    string UserName,
    string Realm,
    string Nonce,
    string ClientNonce,
    string NonceCount,
    string Qop,
    string DigestUri,
    string Response,
    string? AuthorizationId)
{
    /// <summary>
    /// The realm Surl offers (ADR-0049, section 5).
    /// </summary>
    public const string OfferedRealm = "surl";

    private static readonly string[] RequiredDirectives =
        ["username", "nonce", "cnonce", "nc", "qop", "digest-uri", "response"];

    /// <summary>
    /// Reads the directives of <paramref name="text"/>, names matched case-insensitively.
    /// </summary>
    /// <param name="text">The response, one character per byte.</param>
    /// <returns>The directives, or <see langword="null"/> when they cannot be read, one comes twice or a required one is missing.</returns>
    public static DigestMd5Response? Read(string text)
    {
        var directives = DigestParameterParser.Parse(text);
        if (directives is null || !RequiredDirectives.All(directives.ContainsKey))
        {
            return null;
        }

        return new DigestMd5Response(
            directives["username"],
            directives.GetValueOrDefault("realm", string.Empty),
            directives["nonce"],
            directives["cnonce"],
            directives["nc"],
            directives["qop"],
            directives["digest-uri"],
            directives["response"],
            directives.GetValueOrDefault("authzid"));
    }

    /// <summary>
    /// Whether this answers <paramref name="issuedNonce"/> as ADR-0049 section 5 requires: that
    /// nonce, <c>nc</c> <c>00000001</c>, <c>qop</c> <c>auth</c>, a realm empty or <c>surl</c>, and an
    /// <c>authzid</c>, if any, equal to the <c>username</c>.
    /// </summary>
    /// <param name="issuedNonce">The nonce this exchange's challenge carried.</param>
    /// <returns><see langword="true"/> when every rule holds.</returns>
    public bool Answers(string issuedNonce) =>
        string.Equals(Nonce, issuedNonce, StringComparison.Ordinal)
        & string.Equals(NonceCount, "00000001", StringComparison.Ordinal)
        & string.Equals(Qop, "auth", StringComparison.Ordinal)
        & (Realm.Length == 0 | string.Equals(Realm, OfferedRealm, StringComparison.Ordinal))
        & string.Equals(AuthorizationId ?? UserName, UserName, StringComparison.Ordinal);
}
