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
/// <param name="Cipher">The <c>cipher</c>, or <see langword="null"/> when it was left out.</param>
internal sealed record DigestMd5Response(
    string UserName,
    string Realm,
    string Nonce,
    string ClientNonce,
    string NonceCount,
    string Qop,
    string DigestUri,
    string Response,
    string? AuthorizationId,
    string? Cipher = null)
{
    /// <summary>
    /// The realm Surl offers (ADR-0049, section 5).
    /// </summary>
    public const string OfferedRealm = "surl";

    /// <summary>
    /// The quality of protection with no security layer.
    /// </summary>
    public const string AuthenticationOnly = "auth";

    /// <summary>
    /// The quality of protection with integrity: every message after the login carries a MAC.
    /// </summary>
    public const string Integrity = "auth-int";

    /// <summary>
    /// The quality of protection with confidentiality: every message after the login is also encrypted.
    /// </summary>
    public const string Confidentiality = "auth-conf";

    /// <summary>
    /// Two-key triple DES in CBC mode (RFC 2831, section 2.4).
    /// </summary>
    public const string TripleDesCipher = "3des";

    /// <summary>
    /// RC4 with a 128-bit key (RFC 2831, section 2.4).
    /// </summary>
    public const string Rc4Cipher = "rc4";

    private static readonly string[] RequiredDirectives =
        ["username", "nonce", "cnonce", "nc", "qop", "digest-uri", "response"];

    /// <summary>
    /// Whether <see cref="Qop"/> asks for a security layer: <c>auth-int</c> or <c>auth-conf</c>.
    /// </summary>
    public bool HasSecurityLayer => Qop is Integrity or Confidentiality;

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
            directives.GetValueOrDefault("authzid"),
            directives.GetValueOrDefault("cipher"));
    }

    /// <summary>
    /// Whether this answers <paramref name="issuedNonce"/> as ADR-0049 section 5 requires: that
    /// nonce, <c>nc</c> <c>00000001</c>, a <c>qop</c> the challenge offered, a realm empty or
    /// <c>surl</c>, and an <c>authzid</c>, if any, equal to the <c>username</c>.
    /// </summary>
    /// <param name="issuedNonce">The nonce this exchange's challenge carried.</param>
    /// <param name="offersSecurityLayers">
    /// Whether the challenge offered <c>auth-int</c>, and <c>auth-conf</c> with <c>3des</c> and
    /// <c>rc4</c> (ADR-0072, decision 4), as well as <c>auth</c>.
    /// </param>
    /// <returns><see langword="true"/> when every rule holds.</returns>
    public bool Answers(string issuedNonce, bool offersSecurityLayers) =>
        string.Equals(Nonce, issuedNonce, StringComparison.Ordinal)
        & string.Equals(NonceCount, "00000001", StringComparison.Ordinal)
        & ChoosesOfferedQop(offersSecurityLayers)
        & (Realm.Length == 0 | string.Equals(Realm, OfferedRealm, StringComparison.Ordinal))
        & string.Equals(AuthorizationId ?? UserName, UserName, StringComparison.Ordinal);

    // auth always; auth-int, and auth-conf with an offered cipher, only where layers were offered.
    private bool ChoosesOfferedQop(bool offersSecurityLayers) => Qop switch
    {
        AuthenticationOnly => true,
        Integrity => offersSecurityLayers,
        Confidentiality => offersSecurityLayers & Cipher is TripleDesCipher or Rc4Cipher,
        _ => false,
    };
}
