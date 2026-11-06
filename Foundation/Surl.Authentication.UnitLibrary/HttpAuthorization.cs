namespace Surl.Authentication;

/// <summary>
/// A request's first <c>Authorization</c> field whose scheme names one of ADR-0032 section 3's
/// methods, split into the method and the credentials after it (RFC 9110 section 11.4:
/// <c>auth-scheme [ 1*SP credentials ]</c>).
/// </summary>
/// <param name="Method">The method the scheme names.</param>
/// <param name="Credentials">What follows the scheme and its spaces, as received.</param>
internal sealed record HttpAuthorization(AuthenticationMethod Method, string Credentials)
{
    /// <summary>
    /// Finds the first <c>Authorization</c> field (name matched case-insensitively) and reads it.
    /// A field whose scheme names no known method is treated as no credentials (ADR-0032,
    /// section 4, step 4).
    /// </summary>
    /// <param name="fields">Every header field, in the order received.</param>
    /// <returns>The field's method and credentials, or <see langword="null"/>.</returns>
    public static HttpAuthorization? Find(IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        var value = fields
            .FirstOrDefault(field => string.Equals(field.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
            .Value;
        if (value is null)
        {
            return null;
        }

        var space = value.IndexOf(' ', StringComparison.Ordinal);
        var scheme = space < 0 ? value : value[..space];
        var credentials = space < 0 ? string.Empty : value[space..].TrimStart(' ');

        return AuthenticationMethods.TryFromAuthorizationScheme(scheme, out var method)
            ? new HttpAuthorization(method, credentials)
            : null;
    }
}
