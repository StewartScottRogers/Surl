using System.Collections.Frozen;

namespace Surl.Authentication;

/// <summary>
/// What ADR-0032 section 3 says of each <see cref="AuthenticationMethod"/>: which are accepted
/// by default, which send a plain-text secret, and which <c>Authorization</c> scheme names each.
/// </summary>
public static class AuthenticationMethods
{
    /// <summary>
    /// The methods accepted without <c>--auth</c>: <c>basic,bearer,digest,aws-sigv4</c>. NTLM
    /// and Negotiate are left out (ADR-0032, section 3).
    /// </summary>
    public static IReadOnlySet<AuthenticationMethod> DefaultAccepted { get; } = new[]
    {
        AuthenticationMethod.Digest,
        AuthenticationMethod.Basic,
        AuthenticationMethod.Bearer,
        AuthenticationMethod.AwsSigV4,
    }.ToFrozenSet();

    /// <summary>
    /// Whether <paramref name="method"/> sends the secret itself, which a passive listener reads
    /// straight off the wire: Basic and Bearer (ADR-0032, section 3).
    /// </summary>
    /// <param name="method">The method.</param>
    /// <returns><see langword="true"/> for Basic and Bearer.</returns>
    public static bool SendsPlaintextSecret(AuthenticationMethod method) =>
        method is AuthenticationMethod.Basic or AuthenticationMethod.Bearer;

    /// <summary>
    /// The method an <c>Authorization</c> field's scheme names, matched case-insensitively
    /// (RFC 9110 section 11.1): <c>Negotiate</c>, <c>NTLM</c>, <c>Digest</c>, <c>Basic</c>,
    /// <c>Bearer</c> and <c>AWS4-HMAC-SHA256</c>, the scheme upstream curl's <c>--aws-sigv4</c>
    /// sends for the <c>aws</c> provider.
    /// </summary>
    /// <param name="scheme">The scheme token as received.</param>
    /// <param name="method">The method, when the scheme names one.</param>
    /// <returns><see langword="true"/> when the scheme names one of the six methods.</returns>
    public static bool TryFromAuthorizationScheme(string scheme, out AuthenticationMethod method) =>
        MethodsByAuthorizationScheme.TryGetValue(scheme, out method);

    /// <summary>
    /// The <c>Authorization</c> scheme that names <paramref name="method"/>, spelt as
    /// <see cref="TryFromAuthorizationScheme"/> lists it: the method's name in the verbose log's
    /// login note (ADR-0032, section 8).
    /// </summary>
    /// <param name="method">The method.</param>
    /// <returns><c>Negotiate</c>, <c>NTLM</c>, <c>Digest</c>, <c>Basic</c>, <c>Bearer</c> or <c>AWS4-HMAC-SHA256</c>.</returns>
    public static string AuthorizationSchemeOf(AuthenticationMethod method) =>
        MethodsByAuthorizationScheme.First(pair => pair.Value == method).Key;

    private static readonly Dictionary<string, AuthenticationMethod> MethodsByAuthorizationScheme =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Negotiate"] = AuthenticationMethod.Negotiate,
            ["NTLM"] = AuthenticationMethod.Ntlm,
            ["Digest"] = AuthenticationMethod.Digest,
            ["Basic"] = AuthenticationMethod.Basic,
            ["Bearer"] = AuthenticationMethod.Bearer,
            ["AWS4-HMAC-SHA256"] = AuthenticationMethod.AwsSigV4,
        };
}
