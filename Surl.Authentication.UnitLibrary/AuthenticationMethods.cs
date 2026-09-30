using System.Collections.Frozen;

namespace Surl.Authentication;

/// <summary>
/// What ADR-0032 section 3 says of each <see cref="AuthenticationMethod"/>: which are accepted
/// by default, which send a plain-text secret, and which <c>Authorization</c> scheme names each.
/// </summary>
public static class AuthenticationMethods
{
    /// <summary>
    /// The methods accepted without <c>--auth</c>:
    /// <c>digest,cram-md5,basic,plain,login,bearer,oauthbearer,xoauth2,aws-sigv4</c>. NTLM,
    /// Negotiate, <c>DIGEST-MD5</c> and <c>APOP</c> are left out (ADR-0032, section 3; ADR-0049,
    /// section 3).
    /// </summary>
    public static IReadOnlySet<AuthenticationMethod> DefaultAccepted { get; } = new[]
    {
        AuthenticationMethod.Digest,
        AuthenticationMethod.CramMd5,
        AuthenticationMethod.Basic,
        AuthenticationMethod.Plain,
        AuthenticationMethod.Login,
        AuthenticationMethod.Bearer,
        AuthenticationMethod.OAuthBearer,
        AuthenticationMethod.XOAuth2,
        AuthenticationMethod.AwsSigV4,
    }.ToFrozenSet();

    /// <summary>
    /// Whether <paramref name="method"/> sends the secret itself, which a passive listener reads
    /// straight off the wire: Basic, Bearer and the SASL <c>PLAIN</c>, <c>LOGIN</c>,
    /// <c>OAUTHBEARER</c> and <c>XOAUTH2</c> (ADR-0032, section 3; ADR-0049, section 1).
    /// </summary>
    /// <param name="method">The method.</param>
    /// <returns><see langword="true"/> for the six methods that send a password or token.</returns>
    public static bool SendsPlaintextSecret(AuthenticationMethod method) =>
        method is AuthenticationMethod.Basic or AuthenticationMethod.Bearer or AuthenticationMethod.Plain
            or AuthenticationMethod.Login or AuthenticationMethod.OAuthBearer or AuthenticationMethod.XOAuth2;

    /// <summary>
    /// Whether <paramref name="method"/> logs in the connection rather than the request, so an
    /// accepted login serves the connection's later requests that carry no <c>Authorization</c>:
    /// NTLM and Negotiate, whose later requests upstream curl 8.21.0 sends without one
    /// (ADR-0041, ADR-0044).
    /// </summary>
    /// <param name="method">The method.</param>
    /// <returns><see langword="true"/> for NTLM and Negotiate.</returns>
    public static bool AuthenticatesConnection(AuthenticationMethod method) =>
        method is AuthenticationMethod.Ntlm or AuthenticationMethod.Negotiate;

    /// <summary>
    /// The method an <c>Authorization</c> field's scheme names, matched case-insensitively
    /// (RFC 9110 section 11.1): <c>Negotiate</c>, <c>NTLM</c>, <c>Digest</c>, <c>Basic</c>,
    /// <c>Bearer</c> and <c>AWS4-HMAC-SHA256</c>, the scheme upstream curl's <c>--aws-sigv4</c>
    /// sends for the <c>aws</c> provider, or the same with any other provider's letters and
    /// digits before <c>4-HMAC-SHA256</c> (<c>OSC4-HMAC-SHA256</c>, ADR-0043).
    /// </summary>
    /// <param name="scheme">The scheme token as received.</param>
    /// <param name="method">The method, when the scheme names one.</param>
    /// <returns><see langword="true"/> when the scheme names one of the six methods.</returns>
    public static bool TryFromAuthorizationScheme(string scheme, out AuthenticationMethod method)
    {
        if (MethodsByAuthorizationScheme.TryGetValue(scheme, out method))
        {
            return true;
        }

        var isAwsSigV4 = AwsSigV4Provider.IsScheme(scheme);
        method = isAwsSigV4 ? AuthenticationMethod.AwsSigV4 : default;

        return isAwsSigV4;
    }

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
