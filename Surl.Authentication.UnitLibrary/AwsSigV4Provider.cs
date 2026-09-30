namespace Surl.Authentication;

/// <summary>
/// The provider upstream curl's <c>--aws-sigv4 &lt;provider1&gt;[:...]</c> names, which spells the
/// <c>Authorization</c> scheme (<c>AWS4-HMAC-SHA256</c> for <c>aws</c>), the scope terminator
/// (<c>aws4_request</c>) and the prefix of the secret the signing key starts from (<c>AWS4</c>)
/// (ADR-0043).
/// </summary>
internal static class AwsSigV4Provider
{
    private const string SchemeSuffix = "4-HMAC-SHA256";

    /// <summary>
    /// Whether <paramref name="scheme"/> is a Signature Version 4 scheme: one or more ASCII
    /// letters or digits followed by <c>4-HMAC-SHA256</c>, matched case-insensitively.
    /// </summary>
    /// <param name="scheme">The <c>Authorization</c> scheme as received.</param>
    /// <returns><see langword="true"/> for <c>AWS4-HMAC-SHA256</c>, <c>OSC4-HMAC-SHA256</c> and the like.</returns>
    public static bool IsScheme(string scheme) =>
        scheme.Length > SchemeSuffix.Length
        && scheme.EndsWith(SchemeSuffix, StringComparison.OrdinalIgnoreCase)
        && scheme[..^SchemeSuffix.Length].All(char.IsAsciiLetterOrDigit);

    /// <summary>
    /// Whether <paramref name="provider"/> is a provider as a scope terminator spells it: one or
    /// more lower-case ASCII letters or digits.
    /// </summary>
    /// <param name="provider">The terminator before <c>4_request</c>.</param>
    /// <returns><see langword="true"/> for <c>aws</c>, <c>osc</c> and the like.</returns>
    public static bool IsName(string provider) =>
        provider.Length > 0 && provider.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character));

    /// <summary>
    /// The algorithm the string to sign names: <c>AWS4-HMAC-SHA256</c> for <c>aws</c>.
    /// </summary>
    /// <param name="provider">The provider, lower case.</param>
    /// <returns>The provider in upper case, then <c>4-HMAC-SHA256</c>.</returns>
    public static string AlgorithmOf(string provider) => provider.ToUpperInvariant() + SchemeSuffix;

    /// <summary>
    /// What the secret is prefixed with to make the first HMAC key: <c>AWS4</c> for <c>aws</c>.
    /// </summary>
    /// <param name="provider">The provider, lower case.</param>
    /// <returns>The provider in upper case, then <c>4</c>.</returns>
    public static string SecretPrefixOf(string provider) => provider.ToUpperInvariant() + "4";
}
